using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
//using FrageFejden.Data;
namespace FrageFejden_api.Services;

public sealed class ClassService : IClassService
{
    private readonly AppDbContext _db;
    public ClassService(AppDbContext db) => _db = db;

    public async Task<(IReadOnlyList<Class> Items, int Total)> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        var q = _db.Classes.AsNoTracking().OrderBy(c => c.Name);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public Task<Class?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _db.Classes.AsNoTracking()
            .Include(c => c.Memberships).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<MyClassDto>> GetMyClassesAsync(Guid userId, CancellationToken ct)
    {
        return await _db.ClassMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Class.Name) 
            .Select(m => new MyClassDto(
                m.ClassId,
                m.RoleInClass,
                m.EnrolledAt,
                m.Class.Id,
                m.Class.Name,
                m.Class.GradeLabel,
                m.Class.JoinCode,
                m.Class.CreatedById,
                m.Class.CreatedAt))
            .ToListAsync(ct);
    }


    public async Task<IReadOnlyList<MemberDto>> GetMembersAsync(Guid classId, Guid requesterId, CancellationToken ct)
    {
        if (!await IsTeacherOrOwnerAsync(classId, requesterId, ct)) throw new UnauthorizedAccessException();
        var members = await _db.ClassMemberships.AsNoTracking()
            .Where(m => m.ClassId == classId)
            .Select(m => new MemberDto(
                m.UserId, m.RoleInClass, m.EnrolledAt,
                m.User.Id, m.User.FullName, m.User.Email, m.User.AvatarUrl))
            .OrderBy(x => x.FullName)
            .ToListAsync(ct);
        return members;
    }

    public async Task<Class> CreateAsync(Guid ownerId, CreateClassRequest req, CancellationToken ct)
    {
        var cls = new Class
        {
            // Id is Guid (use DB default or set here: Id = Guid.NewGuid())
            Name = req.Name.Trim(),
            GradeLabel = string.IsNullOrWhiteSpace(req.GradeLabel) ? null : req.GradeLabel.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedById = ownerId,
            JoinCode = GenerateJoinCode()
        };

        _db.Classes.Add(cls);
        await _db.SaveChangesAsync(ct);

        if (req.MakeMeTeacher)
        {
            _db.ClassMemberships.Add(new ClassMembership
            {
                ClassId = cls.Id,
                UserId = ownerId,
                RoleInClass = Role.teacher,
                EnrolledAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }

        return cls;
    }

    public async Task<bool> UpdateAsync(Guid id, Guid requesterId, UpdateClassRequest req, CancellationToken ct)
    {
        if (!await IsTeacherOrOwnerAsync(id, requesterId, ct)) throw new UnauthorizedAccessException();

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cls is null) return false;

        if (!string.IsNullOrWhiteSpace(req.Name)) cls.Name = req.Name.Trim();
        cls.GradeLabel = string.IsNullOrWhiteSpace(req.GradeLabel) ? null : req.GradeLabel!.Trim();

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid requesterId, CancellationToken ct)
    {
        if (!await IsTeacherOrOwnerAsync(id, requesterId, ct)) throw new UnauthorizedAccessException();

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cls is null) return false;

        _db.Classes.Remove(cls);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<JoinedClassDto?> JoinByCodeAsync(Guid userId, string joinCode, CancellationToken ct)
    {
        var code = joinCode.Trim();
        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.JoinCode == code, ct);
        if (cls is null) return null;

        var exists = await _db.ClassMemberships.AnyAsync(m => m.ClassId == cls.Id && m.UserId == userId, ct);
        if (exists) throw new InvalidOperationException("Already a member of this class.");

        _db.ClassMemberships.Add(new ClassMembership
        {
            ClassId = cls.Id,
            UserId = userId,
            RoleInClass = Role.student,
            EnrolledAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return new JoinedClassDto(cls.Id, cls.Name, cls.GradeLabel);
    }

    public async Task<bool> LeaveAsync(Guid userId, Guid classId, CancellationToken ct)
    {
        var mem = await _db.ClassMemberships.FirstOrDefaultAsync(m => m.ClassId == classId && m.UserId == userId, ct);
        if (mem is null) return false;

        _db.ClassMemberships.Remove(mem);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AddMemberAsync(Guid classId, Guid requesterId, Guid userId, Role role, CancellationToken ct)
    {
        if (!await IsTeacherOrOwnerAsync(classId, requesterId, ct)) throw new UnauthorizedAccessException();

        var exists = await _db.ClassMemberships.AnyAsync(m => m.ClassId == classId && m.UserId == userId, ct);
        if (exists) return false;

        _db.ClassMemberships.Add(new ClassMembership
        {
            ClassId = classId,
            UserId = userId,
            RoleInClass = role,
            EnrolledAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveMemberAsync(Guid classId, Guid requesterId, Guid userId, CancellationToken ct)
    {
        if (!await IsTeacherOrOwnerAsync(classId, requesterId, ct)) throw new UnauthorizedAccessException();

        var mem = await _db.ClassMemberships.FirstOrDefaultAsync(m => m.ClassId == classId && m.UserId == userId, ct);
        if (mem is null) return false;

        _db.ClassMemberships.Remove(mem);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(Guid Id, string JoinCode)?> RegenerateJoinCodeAsync(Guid classId, Guid requesterId, CancellationToken ct)
    {
        if (!await IsTeacherOrOwnerAsync(classId, requesterId, ct)) throw new UnauthorizedAccessException();

        var cls = await _db.Classes.FirstOrDefaultAsync(c => c.Id == classId, ct);
        if (cls is null) return null;

        cls.JoinCode = GenerateJoinCode();
        await _db.SaveChangesAsync(ct);

        return (cls.Id, cls.JoinCode);
    }

    public async Task<bool> IsTeacherOrOwnerAsync(Guid classId, Guid userId, CancellationToken ct)
    {
        var cls = await _db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId, ct);
        if (cls is null) return false;
        if (cls.CreatedById == userId) return true;

        var mem = await _db.ClassMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ClassId == classId && m.UserId == userId, ct);

        return mem is not null && mem.RoleInClass == Role.teacher;
    }

    private static string GenerateJoinCode(int len = 8)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        using var rng = RandomNumberGenerator.Create();
        Span<byte> bytes = stackalloc byte[len];
        rng.GetBytes(bytes);
        var chars = new char[len];
        for (var i = 0; i < len; i++) chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }


    public async Task<double?> GetPointsForUser(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(ct);

        return user?.experiencePoints;
    }

    public async Task<List<ScoreDto>> GetScoresForClassAsync(Guid userId, Guid classId, CancellationToken ct)
    {
        // Ensure the requesting user is a member of the class
        var isMember = await _db.ClassMemberships
            .AsNoTracking()
            .AnyAsync(m => m.UserId == userId && m.ClassId == classId, ct);

        if (!isMember) return new List<ScoreDto>(); // or throw/Forbid at controller

        // Join memberships to users and project to ScoreDto
        var result = await _db.ClassMemberships
            .AsNoTracking()
            .Where(m => m.ClassId == classId)
            .Join(_db.Users,
                m => m.UserId,
                u => u.Id,
                (m, u) => new ScoreDto
                {
                    UserId = u.Id,
                    UserName = u.FullName,
                    Score = u.experiencePoints
                })
            .ToListAsync(ct);

        return result;
    }

    public async Task<(Guid Id, string Name)?> FindClassByJoinCodeAsync(string joinCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(joinCode)) return null;

        var klass = await _db.Classes
            .AsNoTracking()
            .Where(c => c.JoinCode == joinCode)
            .Select(c => new { c.Id, c.Name })
            .FirstOrDefaultAsync(ct);

        return klass is null ? null : (klass.Id, klass.Name);
    }
}
