using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FrageFejden.DTOs.Subject;
using FrageFejden.Entities;
using FrageFejden.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace FrageFejden.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] 
    [Tags("Subjects")]
    public class SubjectsController : ControllerBase
    {
        private readonly ISubjectService _subjects;

        public SubjectsController(ISubjectService subjects)
        {
            _subjects = subjects;
        }

        // DTOs local to this controller for class endpoints
        public class SubjectDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = null!;
            public string? Description { get; set; }
            public Guid CreatedById { get; set; }
            public DateTime CreatedAt { get; set; }
            public int TopicCount { get; set; }
            public int LevelCount { get; set; }
            public int QuizCount { get; set; }
            public int QuestionCount { get; set; }
        }

        public class SubjectCreateDto
        {
            public string Name { get; set; } = null!;
            public string? Description { get; set; }
        }

        public class SubjectUpdateDto
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
        }

        // ---------- Progress endpoint for the React page ----------
        // GET: /api/subjects/{subjectId}/levels/progress?classId=...
        [HttpGet("{subjectId:guid}/levels/progress")]
        [SwaggerOperation(Summary = "Get level progress for a subject for the current user")]
        [ProducesResponseType(typeof(SubjectProgressDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSubjectProgress(Guid subjectId, [FromQuery] Guid? classId = null)
        {
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            // allow admin, teacher, or student
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();

            var result = await _subjects.GetSubjectProgressAsync(subjectId, userId.Value, classId);
            if (result is null) return NotFound();
            return Ok(result);
        }

        // ---------- Class-scoped subject endpoints ----------
        // GET api/subjects/classes/{classId}
        [HttpGet("classes/{classId:guid}")]
        [SwaggerOperation(Summary = "List subjects in a class")]
        [ProducesResponseType(typeof(IEnumerable<SubjectDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetForClass(Guid classId)
        {
            // allow teacher + student + admin
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();

            var items = await _subjects.GetSubjectsForClassAsync(classId);
            return Ok(items.Select(ToDto));
        }

        // GET api/subjects/classes/{classId}/{subjectId}
        [HttpGet("classes/{classId:guid}/{subjectId:guid}")]
        [SwaggerOperation(Summary = "Get a subject in a class by id")]
        [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetInClass(Guid classId, Guid subjectId)
        {
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();

            var subj = await _subjects.GetSubjectInClassAsync(classId, subjectId);
            return subj is null ? NotFound() : Ok(ToDto(subj));
        }

        // POST api/subjects/classes/{classId}
        [HttpPost("classes/{classId:guid}")]
        [SwaggerOperation(Summary = "Create/add a new subject to a class")]
        [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateForClass(Guid classId, [FromBody] SubjectCreateDto body)
        {
            // limit creation to admin/teacher
            if (!UserIsIn("admin", "teacher")) return Forbid();

            if (body is null) return BadRequest("Request body required.");

            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            try
            {
                var created = await _subjects.AddSubjectToClassAsync(
                    classId,
                    body.Name,
                    body.Description,
                    userId.Value);

                var dto = ToDto(created);
                return CreatedAtAction(nameof(GetInClass),
                    new { classId, subjectId = created.Id },
                    dto);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // PUT api/subjects/classes/{classId}/{subjectId}
        [HttpPut("classes/{classId:guid}/{subjectId:guid}")]
        [SwaggerOperation(Summary = "Update a subject in a class")]
        [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateInClass(Guid classId, Guid subjectId, [FromBody] SubjectUpdateDto body)
        {
            // limit update to admin/teacher
            if (!UserIsIn("admin", "teacher")) return Forbid();

            if (body is null) return BadRequest("Request body required.");

            try
            {
                var ok = await _subjects.UpdateSubjectInClassAsync(
                    classId,
                    subjectId,
                    body.Name,
                    body.Description);

                if (!ok) return NotFound();
                var updated = await _subjects.GetSubjectInClassAsync(classId, subjectId);
                return Ok(ToDto(updated!));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        // DELETE api/subjects/classes/{classId}/{subjectId}
        [HttpDelete("classes/{classId:guid}/{subjectId:guid}")]
        [SwaggerOperation(Summary = "Remove a subject from a class")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteInClass(Guid classId, Guid subjectId)
        {
            // limit delete to admin/teacher
            if (!UserIsIn("admin", "teacher")) return Forbid();

            var ok = await _subjects.RemoveSubjectFromClassAsync(classId, subjectId);
            return ok ? NoContent() : NotFound();
        }

        // ---------- helpers ----------
        private static SubjectDto ToDto(Subject s) => new SubjectDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            CreatedById = s.CreatedById,
            CreatedAt = s.CreatedAt
        };

        private Guid? GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        private bool UserIsIn(params string[] rolesAllowed)
        {
            var userRoles = GetRoleValues(User);
            return userRoles.Intersect(rolesAllowed.Select(r => r.ToLowerInvariant())).Any();
        }

        private static IEnumerable<string> GetRoleValues(ClaimsPrincipal user)
        {
            // Collect potential role claims: standard + common custom names
            var claims = new List<string>();
            claims.AddRange(user.FindAll(ClaimTypes.Role).Select(c => c.Value));
            claims.AddRange(user.FindAll("role").Select(c => c.Value));
            claims.AddRange(user.FindAll("roles").Select(c => c.Value));

            // Handle comma- or space-separated values in a single claim if any
            var split = claims
                .SelectMany(v => v.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(v => v.Trim().ToLowerInvariant());

            return split.Distinct();
        }
    }
}
