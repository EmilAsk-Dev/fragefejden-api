using FrageFejden.DTOs.Quiz;
using FrageFejden.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using FrageFejden.Entities;
using Swashbuckle.AspNetCore.Annotations;

namespace FrageFejden.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Quizzes")]
    public class QuizzesController : ControllerBase
    {
        private readonly IQuizService _quizService;
        private readonly UserManager<AppUser> _userManager;

        public QuizzesController(IQuizService quizService, UserManager<AppUser> userManager)
        {
            _quizService = quizService;
            _userManager = userManager;
        }

        [SwaggerOperation(
            summary: "Hämtar alla quiz baserat på filter.",
            description: "Denna metod hämtar alla quiz från databasen baserat på de angivna filtreringskriterierna och returnerar en lista med quizöversikter."
        )]
        [Authorize(Roles = "admin,teacher")]
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<QuizSummaryDto>), 200)]
        public async Task<ActionResult<IEnumerable<QuizSummaryDto>>> GetQuizzes([FromQuery] QuizFilterDto filter)
        {
            var quizzes = await _quizService.GetQuizzesAsync(filter);
            return Ok(quizzes);
        }

        [SwaggerOperation(
            summary: "Hämtar alla publicerade quiz.",
            description: "Denna metod hämtar alla publicerade quiz från databasen och returnerar en lista med quizöversikter. Valfria filterparametrar kan användas för att begränsa resultaten baserat på ämne och nivå."
        )]
        [Authorize(Roles = "admin,teacher,student")]
        [HttpGet("published")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<QuizSummaryDto>), 200)]
        public async Task<ActionResult<IEnumerable<QuizSummaryDto>>> GetPublishedQuizzes(
            [FromQuery] Guid? subjectId = null,
            [FromQuery] Guid? levelId = null)
        {
            var quizzes = await _quizService.GetPublishedQuizzesAsync(subjectId, levelId);
            return Ok(quizzes);
        }

        [SwaggerOperation(
            summary: "Hämtar en quiz baserat på dess ID.",
            description: "Denna metod hämtar en quiz från databasen baserat på dess unika ID och returnerar quizdetaljer."
        )]
        [Authorize(Roles = "admin,teacher,student")]
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(QuizDto), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<QuizDto>> GetQuiz(Guid id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            
            var canAccess = await _quizService.CanUserAccessQuizAsync(id, userId.Value);
            if (!canAccess)
                return Forbid("You don't have access to this quiz");

            var quiz = await _quizService.GetQuizByIdAsync(id);
            if (quiz == null)
                return NotFound($"Quiz with ID {id} not found");

            return Ok(quiz);
        }

        [SwaggerOperation(
            summary: "Hämtar en quiz med frågor baserat på dess ID.",
            description: "Denna metod hämtar en quiz från databasen baserat på dess unika ID och inkluderar dess frågor. En valfri parameter kan användas för att inkludera svaren på frågorna."
        )]
        [Authorize(Roles = "admin,teacher,student")]
        [HttpGet("{id:guid}/questions")]
        [ProducesResponseType(typeof(QuizWithQuestionsDto), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<QuizWithQuestionsDto>> GetQuizWithQuestions(
            Guid id,
            [FromQuery] bool includeAnswers = false)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            
            var canAccess = await _quizService.CanUserAccessQuizAsync(id, userId.Value);
            if (!canAccess)
                return Forbid("You don't have access to this quiz");

            var quiz = await _quizService.GetQuizWithQuestionsAsync(id, includeAnswers);
            if (quiz == null)
                return NotFound($"Quiz with ID {id} not found");

            return Ok(quiz);
        }

        [SwaggerOperation(
            summary: "Skapar en ny quiz.",
            description: "Denna metod skapar en ny quiz i databasen baserat på den angivna informationen."
        )]
        [Authorize(Roles = "admin,teacher")]
        [HttpPost]
        [ProducesResponseType(typeof(QuizDto), 201)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<QuizDto>> CreateQuiz([FromBody] CreateQuizDto createDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var quiz = await _quizService.CreateQuizAsync(createDto, userId.Value);

            return CreatedAtAction(
                nameof(GetQuiz),
                new { id = quiz.Id },
                quiz);
        }

        [SwaggerOperation(
            summary: "Uppdaterar en befintlig quiz.",
            description: "Denna metod uppdaterar en befintlig quiz i databasen baserat på dess unika ID och den angivna informationen."
        )]
        [Authorize(Roles = "admin,teacher")]
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(QuizDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<QuizDto>> UpdateQuiz(Guid id, [FromBody] UpdateQuizDto updateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var quiz = await _quizService.UpdateQuizAsync(id, updateDto, userId.Value);

            if (quiz == null)
            {
                var exists = await _quizService.QuizExistsAsync(id);
                return exists ? Forbid() : NotFound($"Quiz with ID {id} not found");
            }

            return Ok(quiz);
        }

        [SwaggerOperation(
            summary: "Tar bort en quiz baserat på dess ID.",
            description: "Denna metod tar bort en quiz från databasen baserat på dess unika ID, förutsatt att användaren har rätt behörighet och att quizzen inte har några försök."
        )]
        [Authorize(Roles = "admin,teacher")]
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteQuiz(Guid id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var deleted = await _quizService.DeleteQuizAsync(id, userId.Value);

            if (!deleted)
            {
                var exists = await _quizService.QuizExistsAsync(id);
                return exists ?
                    Forbid("Cannot delete quiz: insufficient permissions or quiz has attempts") :
                    NotFound($"Quiz with ID {id} not found");
            }

            return NoContent();
        }

        [SwaggerOperation(
            summary: "Publicerar eller avpublicerar en quiz.",
            description: "Denna metod ändrar publiceringsstatusen för en quiz baserat på dess unika ID och den angivna informationen."
        )]
        [Authorize(Roles = "admin,teacher")]
        [HttpPatch("{id:guid}/publish")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> PublishQuiz(Guid id, [FromBody] PublishQuizDto publishDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var success = await _quizService.PublishQuizAsync(id, publishDto.IsPublished, userId.Value);

            if (!success)
            {
                var exists = await _quizService.QuizExistsAsync(id);
                return exists ? Forbid() : NotFound($"Quiz with ID {id} not found");
            }

            return Ok(new { message = $"Quiz {(publishDto.IsPublished ? "published" : "unpublished")} successfully" });
        }

        [SwaggerOperation(
            summary: "Uppdaterar ordningen på frågor i en quiz.",
            description: "Denna metod uppdaterar ordningen på frågor i en quiz baserat på dess unika ID och den angivna informationen."
        )]
        [Authorize(Roles = "admin,teacher")]
        [HttpPut("{id:guid}/questions")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateQuizQuestions(Guid id, [FromBody] UpdateQuizQuestionsDto updateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var success = await _quizService.UpdateQuizQuestionsAsync(id, updateDto, userId.Value);

            if (!success)
            {
                var exists = await _quizService.QuizExistsAsync(id);
                return exists ? Forbid() : NotFound($"Quiz with ID {id} not found");
            }

            return Ok(new { message = "Quiz questions updated successfully" });
        }

        [SwaggerOperation(
            summary: "Hämtar statistik för en specifik quiz.",
            description: "Denna metod hämtar statistik för en specifik quiz baserat på dess unika ID, inklusive total antal försök, genomsnittlig poäng, högsta och lägsta poäng, senaste försöket och en sammanfattning av de senaste försöken."
        )]
        [Authorize(Roles = "admin,teacher")]
        [HttpGet("{id:guid}/stats")]
        [ProducesResponseType(typeof(QuizStatsDto), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<QuizStatsDto>> GetQuizStats(Guid id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var stats = await _quizService.GetQuizStatsAsync(id, userId.Value);

            if (stats == null)
            {
                var exists = await _quizService.QuizExistsAsync(id);
                return exists ? Forbid() : NotFound($"Quiz with ID {id} not found");
            }

            return Ok(stats);
        }

        [SwaggerOperation(
            summary: "Hämtar alla quiz skapade av den inloggade användaren.",
            description: "Denna metod hämtar alla quiz från databasen som skapats av den inloggade användaren och returnerar en lista med quizöversikter."
        )]
        [Authorize(Roles = "admin,teacher")]
        [HttpGet("my-quizzes")]
        [ProducesResponseType(typeof(IEnumerable<QuizSummaryDto>), 200)]
        [ProducesResponseType(401)]
        public async Task<ActionResult<IEnumerable<QuizSummaryDto>>> GetMyQuizzes()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var quizzes = await _quizService.GetQuizzesByUserAsync(userId.Value);
            return Ok(quizzes);
        }

        [SwaggerOperation(
            summary: "Kontrollerar om en quiz finns baserat på dess ID.",
            description: "Denna metod kontrollerar om en quiz finns i databasen baserat på dess unika ID och returnerar en lämplig HTTP-statuskod."
        )]
        [Authorize(Roles = "admin,student")]
        [HttpHead("{id:guid}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> CheckQuizExists(Guid id)
        {
            var exists = await _quizService.QuizExistsAsync(id);
            return exists ? Ok() : NotFound();
        }

        [SwaggerOperation(
            summary: "Kontrollerar om den inloggade användaren har åtkomst till en specifik quiz.",
            description: "Denna metod kontrollerar om den inloggade användaren har åtkomst till en specifik quiz baserat på dess unika ID och returnerar en lämplig HTTP-statuskod."
        )]
        [Authorize(Roles = "admin,teacher,student")]
        [HttpGet("{id:guid}/access")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> CheckQuizAccess(Guid id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null)
                return Unauthorized();

            var exists = await _quizService.QuizExistsAsync(id);
            if (!exists)
                return NotFound($"Quiz with ID {id} not found");

            var canAccess = await _quizService.CanUserAccessQuizAsync(id, userId.Value);
            return canAccess ? Ok() : Forbid();
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