using FrageFejden.DTOs.Subject;
using FrageFejden.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using FrageFejden.Entities;
using System.Security.Claims;
using Swashbuckle.AspNetCore.Annotations;
using FrageFejden.Entities.Enums;

namespace FrageFejden.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Subjects")]
    public class SubjectsController : ControllerBase
    {
        private readonly ISubjectService _subjectService;
        private readonly UserManager<AppUser> _userManager;

        public SubjectsController(ISubjectService subjectService, UserManager<AppUser> userManager)
        {
            _subjectService = subjectService;
            _userManager = userManager;
        }

        [SwaggerOperation(
            Summary = "Hämtar alla ämnen",
            Description = "Hämtar en lista med alla ämnen i systemet."
        )]
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SubjectSummaryDto>), 200)]
        public async Task<ActionResult<IEnumerable<SubjectSummaryDto>>> GetAllSubjects()
        {
            var subjects = await _subjectService.GetAllSubjectsAsync();
            return Ok(subjects);
        }

        [SwaggerOperation(
            Summary = "Hämtar ett ämne efter ID",
            Description = "Hämtar detaljer för ett specifikt ämne med dess ID."
        )]
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(SubjectDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<SubjectDto>> GetSubject(Guid id)
        {
            var subject = await _subjectService.GetSubjectByIdAsync(id);

            if (subject == null)
                return NotFound($"Subject with ID {id} not found");

            return Ok(subject);
        }

        [SwaggerOperation(
            Summary = "Hämtar ett ämne med detaljer",
            Description = "Hämtar ett ämne inklusive dess tillhörande nivåer och ämnesområden."
        )]
        [HttpGet("{id:guid}/details")]
        [ProducesResponseType(typeof(SubjectWithDetailsDto), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<SubjectWithDetailsDto>> GetSubjectWithDetails(Guid id)
        {
            var subject = await _subjectService.GetSubjectWithDetailsAsync(id);

            if (subject == null)
                return NotFound($"Subject with ID {id} not found");

            return Ok(subject);
        }

        [SwaggerOperation(
            Summary = "Skapar ett nytt ämne",
            Description = "Skapar ett nytt ämne i systemet. Kräver autentisering."
        )]
        [HttpPost]
        [ProducesResponseType(typeof(SubjectDto), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [Authorize(Roles = "admin,teacher")]
        public async Task<ActionResult<SubjectDto>> CreateSubject([FromBody] CreateSubjectDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var subject = await _subjectService.CreateSubjectAsync(createDto, userId.Value);

            return CreatedAtAction(
                nameof(GetSubject),
                new { id = subject.Id },
                subject);
        }

        [SwaggerOperation(
            Summary = "Uppdaterar ett befintligt ämne",
            Description = "Uppdaterar detaljer för ett befintligt ämne. Kräver autentisering och ägarskap."
        )]
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(SubjectDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        [Authorize(Roles = "admin,teacher")]
        public async Task<ActionResult<SubjectDto>> UpdateSubject(Guid id, [FromBody] UpdateSubjectDto updateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var subject = await _subjectService.UpdateSubjectAsync(id, updateDto, userId.Value);

            if (subject == null)
            {
                
                var exists = await _subjectService.SubjectExistsAsync(id);
                return exists ? Forbid() : NotFound($"Subject with ID {id} not found");
            }

            return Ok(subject);
        }

        [SwaggerOperation(
            Summary = "Tar bort ett ämne",
            Description = "Tar bort ett ämne från systemet. Kräver autentisering och ägarskap."
        )]
        [Authorize(Roles = "admin,teacher")]
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteSubject(Guid id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var deleted = await _subjectService.DeleteSubjectAsync(id, userId.Value);

            if (!deleted)
            {
                
                var exists = await _subjectService.SubjectExistsAsync(id);
                return exists ?
                    Forbid("Cannot delete subject: insufficient permissions or subject has dependent entities") :
                    NotFound($"Subject with ID {id} not found");
            }

            return NoContent();
        }

        [SwaggerOperation(
            Summary = "Hämtar ämnen skapade av den inloggade användaren",
            Description = "Hämtar en lista med ämnen som skapats av den autentiserade användaren."
        )]
        [HttpGet("my-subjects")]
        [ProducesResponseType(typeof(IEnumerable<SubjectSummaryDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<IEnumerable<SubjectSummaryDto>>> GetMySubjects()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var subjects = await _subjectService.GetSubjectsByUserAsync(userId.Value);
            return Ok(subjects);
        }

        [SwaggerOperation(
            Summary = "Kontrollerar om ett ämne finns",
            Description = "Kontrollerar om ett ämne med det angivna ID:t finns i systemet."
        )]
        [HttpHead("{id:guid}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> CheckSubjectExists(Guid id)
        {
            var exists = await _subjectService.SubjectExistsAsync(id);
            return exists ? Ok() : NotFound();
        }

        private async Task<Guid?> GetCurrentUserIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.Id;
        }

        private async Task<AppUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }
    }
}