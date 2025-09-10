using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FrageFejden.Entities;
using FrageFejden.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using FrageFejden_api.Api;

namespace FrageFejden.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Subjects")]
    public class SubjectsController : ControllerBase
    {
        private readonly ISubjectService _subjects;
        public SubjectsController(ISubjectService subjects) => _subjects = subjects;

        
        public class SubjectDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = null!;
            public string? Description { get; set; }
            public string IconUrl { get; set; } = null!; 
            public Guid? ClassId { get; set; }
            public Guid CreatedById { get; set; }
            public DateTime CreatedAt { get; set; }
            public int TopicCount { get; set; }
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

        
        public class SubjectTopicDto
        {
            public Guid Id { get; set; }
            public Guid SubjectId { get; set; }
            public string Name { get; set; } = null!;
            public string? Description { get; set; }
            public int SortOrder { get; set; }
            public int LevelCount { get; set; }
            public int QuizCount { get; set; }
            public int QuestionCount { get; set; }
        }

        public class SubjectTopicCreateDto
        {
            public string Name { get; set; } = null!;
            public string? Description { get; set; }
            public int? SortOrder { get; set; }
        }

        public class SubjectTopicUpdateDto
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public int? SortOrder { get; set; }
        }

       

        [HttpGet("classes/{classId:guid}")]
        [SwaggerOperation(Summary = "List subjects in a class")]
        [ProducesResponseType(typeof(IEnumerable<SubjectDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetForClass(Guid classId)
        {
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();
            var items = await _subjects.GetSubjectsForClassAsync(classId);
            return Ok(items.Select(ToSubjectDto));
        }

        [HttpGet("classes/{classId:guid}/{subjectId:guid}")]
        [SwaggerOperation(Summary = "Get a subject in a class by id")]
        [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetInClass(Guid classId, Guid subjectId)
        {
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();
            var subj = await _subjects.GetSubjectInClassAsync(classId, subjectId);
            return subj is null ? NotFound() : Ok(ToSubjectDto(subj));
        }

        [HttpPost("classes/{classId:guid}")]
        [SwaggerOperation(Summary = "Create/add a new subject to a class")]
        [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateForClass(Guid classId, [FromBody] SubjectCreateDto body)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();
            if (body is null) return BadRequest("Request body required.");
            var userId = GetUserId();
            if (userId is null) return Unauthorized();

            try
            {
                var created = await _subjects.AddSubjectToClassAsync(classId, body.Name, body.Description, userId.Value);
                return CreatedAtAction(nameof(GetInClass), new { classId, subjectId = created.Id }, ToSubjectDto(created));
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
            catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        }

        [HttpPut("classes/{classId:guid}/{subjectId:guid}")]
        [SwaggerOperation(Summary = "Update a subject in a class")]
        [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateInClass(Guid classId, Guid subjectId, [FromBody] SubjectUpdateDto body)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();
            if (body is null) return BadRequest("Request body required.");
            try
            {
                var ok = await _subjects.UpdateSubjectInClassAsync(classId, subjectId, body.Name, body.Description);
                if (!ok) return NotFound();
                var updated = await _subjects.GetSubjectInClassAsync(classId, subjectId);
                return Ok(ToSubjectDto(updated!));
            }
            catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        }

        [HttpDelete("classes/{classId:guid}/{subjectId:guid}")]
        [SwaggerOperation(Summary = "Remove a subject from a class")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteInClass(Guid classId, Guid subjectId)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();
            var ok = await _subjects.RemoveSubjectFromClassAsync(classId, subjectId);
            return ok ? NoContent() : NotFound();
        }

        

        [HttpGet("{subjectId:guid}/topics")]
        [SwaggerOperation(Summary = "List topics for a subject")]
        [ProducesResponseType(typeof(IEnumerable<TopicSummaryDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTopics(Guid subjectId)
        {
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();
            var topics = await _subjects.GetTopicsForSubjectAsync(subjectId);
            return Ok(topics);
        }

        [HttpGet("{subjectId:guid}/topics/{topicId:guid}")]
        [SwaggerOperation(Summary = "Get a topic by id for a subject")]
        [ProducesResponseType(typeof(SubjectTopicDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTopic(Guid subjectId, Guid topicId)
        {
            if (!UserIsIn("admin", "teacher", "student")) return Forbid();
            var topic = await _subjects.GetTopicAsync(topicId);
            if (topic is null || topic.SubjectId != subjectId) return NotFound();
            return Ok(ToSubjectTopicDto(topic));
        }

        [HttpPost("{subjectId:guid}/topics")]
        [SwaggerOperation(Summary = "Create a topic under a subject")]
        [ProducesResponseType(typeof(SubjectTopicDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateTopic(Guid subjectId, [FromBody] SubjectTopicCreateDto body)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();
            if (body is null) return BadRequest("Request body required.");

            var created = await _subjects.CreateTopicAsync(subjectId, body.Name, body.Description, body.SortOrder);
            return CreatedAtAction(nameof(GetTopic), new { subjectId, topicId = created.Id }, ToSubjectTopicDto(created));
        }

        [HttpPut("{subjectId:guid}/topics/{topicId:guid}")]
        [SwaggerOperation(Summary = "Update a topic under a subject")]
        [ProducesResponseType(typeof(SubjectTopicDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTopic(Guid subjectId, Guid topicId, [FromBody] SubjectTopicUpdateDto body)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();
            if (body is null) return BadRequest("Request body required.");

            var existing = await _subjects.GetTopicAsync(topicId);
            if (existing is null || existing.SubjectId != subjectId) return NotFound();

            var ok = await _subjects.UpdateTopicAsync(topicId, body.Name, body.Description, body.SortOrder);
            if (!ok) return NotFound();

            var updated = await _subjects.GetTopicAsync(topicId);
            return Ok(ToSubjectTopicDto(updated!));
        }

        [HttpDelete("{subjectId:guid}/topics/{topicId:guid}")]
        [SwaggerOperation(Summary = "Delete a topic from a subject")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTopic(Guid subjectId, Guid topicId)
        {
            if (!UserIsIn("admin", "teacher")) return Forbid();
            var existing = await _subjects.GetTopicAsync(topicId);
            if (existing is null || existing.SubjectId != subjectId) return NotFound();

            var ok = await _subjects.DeleteTopicAsync(topicId);
            return ok ? NoContent() : NotFound();
        }

        // ===== mappers =====
        private static SubjectDto ToSubjectDto(Subject s) => new SubjectDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            ClassId = s.ClassId,
            CreatedById = s.CreatedById,
            CreatedAt = s.CreatedAt,
            IconUrl = s.IconUrl,
            TopicCount = s.Topics?.Count ?? 0,
            QuizCount = s.Quizzes?.Count ?? 0,
            QuestionCount = s.Questions?.Count ?? 0
        };

        private static SubjectTopicDto ToSubjectTopicDto(Topic t) => new SubjectTopicDto
        {
            Id = t.Id,
            SubjectId = t.SubjectId,
            Name = t.Name,
            Description = t.Description,
            SortOrder = t.SortOrder,
            LevelCount = t.Levels?.Count ?? 0,
            QuizCount = t.Quizzes?.Count ?? 0,
            QuestionCount = t.Questions?.Count ?? 0
        };

        // ===== auth helpers =====
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
            var claims = new List<string>();
            claims.AddRange(user.FindAll(ClaimTypes.Role).Select(c => c.Value));
            claims.AddRange(user.FindAll("role").Select(c => c.Value));
            claims.AddRange(user.FindAll("roles").Select(c => c.Value));

            return claims
                .SelectMany(v => v.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(v => v.Trim().ToLowerInvariant())
                .Distinct();
        }
    }
}
