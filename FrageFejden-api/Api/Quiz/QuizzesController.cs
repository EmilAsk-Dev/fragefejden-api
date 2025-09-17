using FrageFejden.DTOs.Quiz;
using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using FrageFejden.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FrageFejden.Controllers.Quiz
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Tags("Quizzes")]
    public class QuizzesController : ControllerBase
    {
        private readonly IQuizService _quizService;
        private readonly IProgressService _progressService;
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;

        public QuizzesController(
            IQuizService quizService,
            IProgressService progressService,
            UserManager<AppUser> userManager,
            AppDbContext context)
        {
            _quizService = quizService;
            _progressService = progressService;
            _userManager = userManager;
            _context = context;
        }

        [SwaggerOperation(summary: "Hämtar alla quiz baserat på filter.")]
        [Authorize(Roles = "admin,teacher")]
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<QuizSummaryDto>), 200)]
        public async Task<ActionResult<IEnumerable<QuizSummaryDto>>> GetQuizzes([FromQuery] QuizFilterDto filter)
        {
            var quizzes = await _quizService.GetQuizzesAsync(filter);
            return Ok(quizzes);
        }

        [SwaggerOperation(summary: "Hämtar publicerade quiz. Filtrera valfritt på subjectId, topicId och/eller levelId.")]
        [AllowAnonymous]
        [HttpGet("published")]
        [ProducesResponseType(typeof(IEnumerable<QuizSummaryDto>), 200)]
        public async Task<ActionResult<IEnumerable<QuizSummaryDto>>> GetPublishedQuizzes(
            [FromQuery] Guid? subjectId = null,
            [FromQuery] Guid? topicId = null,
            [FromQuery] Guid? levelId = null)
        {
            var quizzes = await _quizService.GetPublishedQuizzesAsync(subjectId, topicId, levelId);
            return Ok(quizzes);
        }

        [SwaggerOperation(summary: "Hämtar en quiz baserat på dess ID.")]
        [Authorize(Roles = "admin,teacher,student")]
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(QuizDto), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<QuizDto>> GetQuiz(Guid id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var canAccess = await _quizService.CanUserAccessQuizAsync(id, userId.Value);
            if (!canAccess) return Forbid("You don't have access to this quiz");

            var quiz = await _quizService.GetQuizByIdAsync(id);
            if (quiz == null) return NotFound($"Quiz with ID {id} not found");

            return Ok(quiz);
        }

        [SwaggerOperation(summary: "Hämtar en quiz med frågor baserat på dess ID.")]
        [Authorize(Roles = "admin,teacher,student")]
        [HttpGet("{id:guid}/questions")]
        [ProducesResponseType(typeof(QuizWithQuestionsDto), 200)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<QuizWithQuestionsDto>> GetQuizWithQuestions(Guid id, [FromQuery] bool includeAnswers = false)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var canAccess = await _quizService.CanUserAccessQuizAsync(id, userId.Value);
            if (!canAccess) return Forbid("You don't have access to this quiz");

            var quiz = await _quizService.GetQuizWithQuestionsAsync(id, includeAnswers);
            if (quiz == null) return NotFound($"Quiz with ID {id} not found");

            return Ok(quiz);
        }

        [SwaggerOperation(summary: "Skapar ett nytt quiz.")]
        [Authorize(Roles = "admin,teacher")]
        [HttpPost]
        [ProducesResponseType(typeof(QuizDto), 201)]
        public async Task<ActionResult<QuizDto>> CreateQuiz([FromBody] CreateQuizDto createDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var quiz = await _quizService.CreateQuizAsync(createDto, userId.Value);
            return CreatedAtAction(nameof(GetQuiz), new { id = quiz.Id }, quiz);
        }

        [SwaggerOperation(summary: "Uppdaterar ett quiz.")]
        [Authorize(Roles = "admin,teacher")]
        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(QuizDto), 200)]
        public async Task<ActionResult<QuizDto>> UpdateQuiz(Guid id, [FromBody] UpdateQuizDto updateDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var quiz = await _quizService.UpdateQuizAsync(id, updateDto, userId.Value);
            if (quiz == null)
            {
                var exists = await _quizService.QuizExistsAsync(id);
                return exists ? Forbid() : NotFound($"Quiz with ID {id} not found");
            }

            return Ok(quiz);
        }

        [SwaggerOperation(summary: "Tar bort ett quiz.")]
        [Authorize(Roles = "admin,teacher")]
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(204)]
        public async Task<IActionResult> DeleteQuiz(Guid id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var deleted = await _quizService.DeleteQuizAsync(id, userId.Value);
            if (!deleted)
            {
                var exists = await _quizService.QuizExistsAsync(id);
                return exists ? Forbid("Cannot delete quiz: insufficient permissions or quiz has attempts")
                              : NotFound($"Quiz with ID {id} not found");
            }

            return NoContent();
        }

        [SwaggerOperation(summary: "Publicerar/avpublicerar ett quiz.")]
        [Authorize(Roles = "admin,teacher")]
        [HttpPatch("{id:guid}/publish")]
        public async Task<IActionResult> PublishQuiz(Guid id, [FromBody] PublishQuizDto publishDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var success = await _quizService.PublishQuizAsync(id, publishDto.IsPublished, userId.Value);
            if (!success)
            {
                var exists = await _quizService.QuizExistsAsync(id);
                return exists ? Forbid() : NotFound($"Quiz with ID {id} not found");
            }

            return Ok(new { message = $"Quiz {(publishDto.IsPublished ? "published" : "unpublished")} successfully" });
        }

        [SwaggerOperation(summary: "Uppdaterar frågeordning i ett quiz.")]
        [Authorize(Roles = "admin,teacher")]
        [HttpPut("{id:guid}/questions")]
        public async Task<IActionResult> UpdateQuizQuestions(Guid id, [FromBody] UpdateQuizQuestionsDto updateDto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var success = await _quizService.UpdateQuizQuestionsAsync(id, updateDto, userId.Value);
            if (!success)
            {
                var exists = await _quizService.QuizExistsAsync(id);
                return exists ? Forbid() : NotFound($"Quiz with ID {id} not found");
            }

            return Ok(new { message = "Quiz questions updated successfully" });
        }

        [SwaggerOperation(summary: "Hämtar statistik för ett quiz.")]
        [Authorize(Roles = "admin,teacher")]
        [HttpGet("{id:guid}/stats")]
        [ProducesResponseType(typeof(QuizStatsDto), 200)]
        public async Task<ActionResult<QuizStatsDto>> GetQuizStats(Guid id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var stats = await _quizService.GetQuizStatsAsync(id, userId.Value);
            if (stats == null)
            {
                var exists = await _quizService.QuizExistsAsync(id);
                return exists ? Forbid() : NotFound($"Quiz with ID {id} not found");
            }

            return Ok(stats);
        }

        [SwaggerOperation(summary: "Kontrollerar om användaren har åtkomst till ett quiz.")]
        [Authorize(Roles = "admin,teacher,student")]
        [HttpGet("{id:guid}/access")]
        public async Task<IActionResult> CheckQuizAccess(Guid id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var exists = await _quizService.QuizExistsAsync(id);
            if (!exists) return NotFound($"Quiz with ID {id} not found");

            var canAccess = await _quizService.CanUserAccessQuizAsync(id, userId.Value);
            return canAccess ? Ok() : Forbid();
        }


        [SwaggerOperation(summary: "Hämtar användarens status för ett specifikt quiz.")]
        [Authorize(Roles = "admin,teacher,student")]
        [HttpGet("{id:guid}/my-status")]
        public async Task<ActionResult<UserQuizStatusDto>> GetMyQuizStatus(Guid id)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var quiz = await _context.Quizzes
                .Include(q => q.Level)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null) return NotFound("Quiz not found");

            var progress = quiz.LevelId.HasValue ?
                await _progressService.GetUserProgressForLevelAsync(userId.Value, quiz.LevelId.Value) : null;

            var lastAttempt = await _context.Attempts
                .Include(a => a.Answers)
                .Where(a => a.QuizId == id && a.UserId == userId.Value && a.CompletedAt != null)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync();

            var canAccess = quiz.LevelId.HasValue ?
                await _progressService.CanUserAccessLevelAsync(userId.Value, quiz.LevelId.Value) : true;

            return Ok(new UserQuizStatusDto
            {
                QuizId = id,
                CanAccess = canAccess,
                HasCompleted = lastAttempt != null,
                BestScore = progress?.BestScore ?? lastAttempt?.Score,
                IsLevelCompleted = progress?.IsLevelCompleted ?? false,
                CanRetry = progress?.CanRetry ?? false,
                RetryCount = progress?.RetryCount ?? 0,
                LastAttemptAt = lastAttempt?.CompletedAt,
                XpEarned = progress?.Xp ?? 0
            });
        }

        [SwaggerOperation(summary: "Ger användaren tillåtelse att försöka igen på en nivå.")]
        [Authorize(Roles = "admin,teacher")]
        [HttpPost("{id:guid}/allow-retry")]
        public async Task<IActionResult> AllowRetry(Guid id, [FromBody] AllowRetryRequest request)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var quiz = await _context.Quizzes.FirstOrDefaultAsync(q => q.Id == id);
            if (quiz?.LevelId == null) return NotFound("Quiz or level not found");

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || (currentUser.Role != Role.admin && quiz.CreatedById != userId))
                return Forbid("Insufficient permissions");

            await _progressService.SetRetryPermissionAsync(request.UserId, quiz.LevelId.Value, request.CanRetry);

            return Ok(new { message = $"Retry permission {(request.CanRetry ? "granted" : "revoked")} successfully" });
        }

        private async Task<Guid?> GetCurrentUserIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.Id;
        }
    }

    public class UserQuizStatusDto
    {
        public Guid QuizId { get; set; }
        public bool CanAccess { get; set; }
        public bool HasCompleted { get; set; }
        public int? BestScore { get; set; }
        public bool IsLevelCompleted { get; set; }
        public bool CanRetry { get; set; }
        public int RetryCount { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public int XpEarned { get; set; }
    }

    public class AllowRetryRequest
    {
        public Guid UserId { get; set; }
        public bool CanRetry { get; set; }
    }
}