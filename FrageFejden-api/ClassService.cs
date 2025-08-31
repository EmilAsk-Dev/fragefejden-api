using FrageFejden.Entities;
using FrageFejden.Entities.Enums; // for Role enum (student/teacher)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;

namespace FrageFejden_api
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public sealed class ClassController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _db;

        public ClassController(UserManager<AppUser> userManager, AppDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        // ========= DTOs =========

        public sealed record CreateClassRequest(string Name, string? GradeLabel, string? Description = null, bool MakeMeTeacher = true);
        public sealed record UpdateClassRequest(string Name, string? GradeLabel, string? Description);
        public sealed record AddMemberRequest(Guid UserId, Role RoleInClass);
        public sealed record JoinRequest(string JoinCode);

        // ========= Helpers =========

        private Guid GetCurrentUserId()
        {
            var id = _userManager.GetUserId(User);
            if (id is null || !Guid.TryParse(id, out var guid))
                throw new UnauthorizedAccessException("Not authenticated");
            return guid;
        }

        private async Task<bool> IsTeacherOrOwnerAsync(long classId, Guid userId, CancellationToken ct)
        {
            // Owner?
            var cls = await _db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId, ct);
            if (cls is null) return false;
            if (cls.CreatedById == userId) return true;

            // Teacher membership?
            var mem = await _db.ClassMemberships.AsNoTracking()
                .FirstOrDefaultAsync(m => m.ClassId == classId && m.UserId == userId, ct);

            return mem is not null && mem.RoleInClass == Role.teacher;
        }

        private static string GenerateJoinCode(int len = 8)
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // avoid ambiguous chars
            var rng = RandomNumberGenerator.Create();
            var bytes = new byte[len];
            rng.GetBytes(bytes);
            var sb = new System.Text.StringBuilder(len);
            foreach (var b in bytes)
                sb.Append(alphabet[b % alphabet.Length]);
            return sb.ToString();
        }

        // ========= Endpoints =========

        /// <summary>List all classes (paged)</summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _db.Classes.AsNoTracking().OrderBy(c => c.Name);
            var total = await query.CountAsync(ct);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            return Ok(new { total, page, pageSize, items });
        }

        /// <summary>Get a class by id</summary>
        [HttpGet("{id:long}")]
        [Authorize]
        public async Task<IActionResult> GetById(long id, CancellationToken ct)
        {
            var cls = await _db.Classes
                .AsNoTracking()
                .Include(c => c.Memberships)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(c => c.Id == id, ct);

            return cls is null ? NotFound() : Ok(cls);
        }

        /// <summary>Get current user's classes</summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMyClasses(CancellationToken ct)
        {
            var uid = GetCurrentUserId();

            var data = await _db.ClassMemberships.AsNoTracking()
                .Where(m => m.UserId == uid)
                .Select(m => new
                {
                    m.ClassId,
                    m.RoleInClass,
                    m.EnrolledAt,
                    Class = new { m.Class.Id, m.Class.Name, m.Class.GradeLabel, m.Class.JoinCode, m.Class.CreatedById, m.Class.CreatedAt }
                })
                .OrderBy(x => x.Class.Name)
                .ToListAsync(ct);

            return Ok(data);
        }

        /// <summary>Create a class</summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateClassRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest("Name is required.");

            var uid = GetCurrentUserId();

            var cls = new Class
            {
                Name = req.Name.Trim(),
                GradeLabel = string.IsNullOrWhiteSpace(req.GradeLabel) ? null : req.GradeLabel.Trim(),
                CreatedAt = DateTime.UtcNow,
                CreatedById = uid,
                JoinCode = GenerateJoinCode()
            };

            _db.Classes.Add(cls);
            await _db.SaveChangesAsync(ct);

            if (req.MakeMeTeacher)
            {
                _db.ClassMemberships.Add(new ClassMembership
                {
                    ClassId = cls.Id,
                    UserId = uid,
                    RoleInClass = Role.teacher,
                    EnrolledAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(ct);
            }

            return CreatedAtAction(nameof(GetById), new { id = cls.Id }, cls);
        }

        /// <summary>Update a class (owner/teacher only)</summary>
        [HttpPut("{id:long}")]
        [Authorize]
        public async Task<IActionResult> Update(long id, [FromBody] UpdateClassRequest req, CancellationToken ct)
        {
            var uid = GetCurrentUserId();
            if (!await IsTeacherOrOwnerAsync(id, uid, ct)) return Forbid();

            var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cls is null) return NotFound();

            if (!string.IsNullOrWhiteSpace(req.Name)) cls.Name = req.Name.Trim();
            cls.GradeLabel = string.IsNullOrWhiteSpace(req.GradeLabel) ? null : req.GradeLabel.Trim();

            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        /// <summary>Delete a class (owner/teacher only)</summary>
        [HttpDelete("{id:long}")]
        [Authorize]
        public async Task<IActionResult> Delete(long id, CancellationToken ct)
        {
            var uid = GetCurrentUserId();
            if (!await IsTeacherOrOwnerAsync(id, uid, ct)) return Forbid();

            var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cls is null) return NotFound();

            _db.Classes.Remove(cls);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        /// <summary>Join a class via JoinCode</summary>
        [HttpPost("join")]
        [Authorize]
        public async Task<IActionResult> Join([FromBody] JoinRequest req, CancellationToken ct)
        {
            var uid = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(req.JoinCode)) return BadRequest("JoinCode is required.");

            var cls = await _db.Classes.FirstOrDefaultAsync(c => c.JoinCode == req.JoinCode.Trim(), ct);
            if (cls is null) return NotFound("Invalid join code.");

            var exists = await _db.ClassMemberships.AnyAsync(m => m.ClassId == cls.Id && m.UserId == uid, ct);
            if (exists) return Conflict("Already a member of this class.");

            _db.ClassMemberships.Add(new ClassMembership
            {
                ClassId = cls.Id,
                UserId = uid,
                RoleInClass = Role.student,
                EnrolledAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);

            return Ok(new { cls.Id, cls.Name, cls.GradeLabel });
        }

        /// <summary>Leave a class</summary>
        [HttpDelete("{id:long}/leave")]
        [Authorize]
        public async Task<IActionResult> Leave(long id, CancellationToken ct)
        {
            var uid = GetCurrentUserId();

            var mem = await _db.ClassMemberships.FirstOrDefaultAsync(m => m.ClassId == id && m.UserId == uid, ct);
            if (mem is null) return NotFound();

            _db.ClassMemberships.Remove(mem);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        /// <summary>List members of a class</summary>
        [HttpGet("{id:long}/members")]
        [Authorize]
        public async Task<IActionResult> Members(long id, CancellationToken ct)
        {
            var uid = GetCurrentUserId();
            if (!await IsTeacherOrOwnerAsync(id, uid, ct)) return Forbid();

            var members = await _db.ClassMemberships.AsNoTracking()
                .Where(m => m.ClassId == id)
                .Select(m => new
                {
                    m.UserId,
                    m.RoleInClass,
                    m.EnrolledAt,
                    User = new { m.User.Id, m.User.FullName, m.User.Email, m.User.AvatarUrl }
                })
                .OrderBy(x => x.User.FullName)
                .ToListAsync(ct);

            return Ok(members);
        }

        /// <summary>Add a member (owner/teacher only)</summary>
        [HttpPost("{id:long}/members")]
        [Authorize]
        public async Task<IActionResult> AddMember(long id, [FromBody] AddMemberRequest req, CancellationToken ct)
        {
            var uid = GetCurrentUserId();
            if (!await IsTeacherOrOwnerAsync(id, uid, ct)) return Forbid();

            var exists = await _db.ClassMemberships.AnyAsync(m => m.ClassId == id && m.UserId == req.UserId, ct);
            if (exists) return Conflict("User already in class.");

            _db.ClassMemberships.Add(new ClassMembership
            {
                ClassId = id,
                UserId = req.UserId,
                RoleInClass = req.RoleInClass,
                EnrolledAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);

            return Ok();
        }

        /// <summary>Remove a member (owner/teacher only)</summary>
        [HttpDelete("{id:long}/members/{userId:guid}")]
        [Authorize]
        public async Task<IActionResult> RemoveMember(long id, Guid userId, CancellationToken ct)
        {
            var uid = GetCurrentUserId();
            if (!await IsTeacherOrOwnerAsync(id, uid, ct)) return Forbid();

            var mem = await _db.ClassMemberships.FirstOrDefaultAsync(m => m.ClassId == id && m.UserId == userId, ct);
            if (mem is null) return NotFound();

            _db.ClassMemberships.Remove(mem);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        /// <summary>Regenerate JoinCode (owner/teacher only)</summary>
        [HttpPost("{id:long}/regen-joincode")]
        [Authorize]
        public async Task<IActionResult> RegenerateJoinCode(long id, CancellationToken ct)
        {
            var uid = GetCurrentUserId();
            if (!await IsTeacherOrOwnerAsync(id, uid, ct)) return Forbid();

            var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (cls is null) return NotFound();

            cls.JoinCode = GenerateJoinCode();
            await _db.SaveChangesAsync(ct);

            return Ok(new { cls.Id, cls.JoinCode });
        }
    }
}
