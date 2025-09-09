using FrageFejden.DTOs.Subject;
using FrageFejden.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrageFejden.Entities;
using System.Security.Claims;
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

        
        [HttpGet("classes/{classId:guid}")]
        [SwaggerOperation(Summary = "List subjects in a class")]
        [ProducesResponseType(typeof(IEnumerable<SubjectDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetForClass(Guid classId)
        {
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
            if (body is null) return BadRequest("Request body required.");

            var userId = GetUserId();
            if (userId is null) return Forbid();

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
                // e.g., duplicate name in class
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
                // e.g., name collision
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
            var ok = await _subjects.RemoveSubjectFromClassAsync(classId, subjectId);
            return ok ? NoContent() : NotFound();
        }

        

        private static SubjectDto ToDto(Subject s) => new SubjectDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            CreatedById = s.CreatedById,
            CreatedAt = s.CreatedAt
            // If your SubjectDto includes counts/relations, populate them here
        };

        private Guid? GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }
}
