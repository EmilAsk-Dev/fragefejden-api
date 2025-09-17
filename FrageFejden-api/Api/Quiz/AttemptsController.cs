using FrageFejden.DTOs.Quiz;
using FrageFejden.Entities;
using FrageFejden.Services;
using FrageFejden_api.Entities.Tables;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FrageFejden.Api.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize(Roles = "admin,teacher,student")]
    [Tags("Attempts")]
    public class AttemptsController : ControllerBase
    {
        private const int PASS_PERCENTAGE = 70;
        private const int XP_PER_CORRECT_ANSWER = 10;

        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IProgressService _progressService;

        public AttemptsController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IProgressService progressService)
        {
            _context = context;
            _userManager = userManager;
            _progressService = progressService;
        }

        
        [HttpPost("quizzes/{quizId:guid}/attempts")]
        public async Task<ActionResult<StartAttemptResponse>> StartAttempt(Guid quizId, [FromBody] StartAttemptRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            
            var quiz = await _context.Quizzes
                .Include(q => q.Level)
                .Include(q => q.Topic)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null) return NotFound("Quiz not found");

            // Check if user can access this quiz
            var canAccess = await CanUserAccessQuizAsync(quizId, user.Id);
            if (!canAccess.CanAccess)
            {
                return BadRequest(new
                {
                    message = canAccess.Reason,
                    requiresUnlock = canAccess.RequiresUnlock,
                    canRetry = canAccess.CanRetry
                });
            }

            
            var activeAttempt = await _context.Attempts
                .FirstOrDefaultAsync(a => a.QuizId == quizId &&
                                        a.UserId == user.Id &&
                                        a.CompletedAt == null);

            if (activeAttempt != null)
            {
                return Ok(new StartAttemptResponse { AttemptId = activeAttempt.Id });
            }

            var attempt = new Attempt
            {
                Id = Guid.NewGuid(),
                QuizId = quizId,
                UserId = user.Id,
                StartedAt = DateTime.UtcNow
            };

            _context.Attempts.Add(attempt);
            await _context.SaveChangesAsync();

            return Ok(new StartAttemptResponse { AttemptId = attempt.Id });
        }

        
        [HttpPost("attempts/{attemptId:guid}/answers")]
        public async Task<ActionResult<SubmitAnswerResponse>> SubmitAnswer(
            Guid attemptId,
            [FromBody] SubmitAnswerRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var attempt = await _context.Attempts
                .Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == user.Id);

            if (attempt == null) return NotFound("Attempt not found");
            if (attempt.CompletedAt != null) return BadRequest("Attempt already finished");

           
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == request.QuestionId);

            if (question == null) return BadRequest("Question not found");

            var selectedOption = question.Options.FirstOrDefault(o => o.Id == request.SelectedOptionId);
            if (selectedOption == null) return BadRequest("Option not found");

            var correctOption = question.Options.FirstOrDefault(o => o.IsCorrect);
            var isCorrect = correctOption?.Id == selectedOption.Id;

           
            var existingAnswer = attempt.Answers.FirstOrDefault(a => a.QuestionId == request.QuestionId);

            if (existingAnswer != null)
            {
                existingAnswer.SelectedOptionId = request.SelectedOptionId;
                existingAnswer.IsCorrect = isCorrect;
                existingAnswer.TimeMs = Math.Max(0, request.TimeMs);
                existingAnswer.AnsweredAt = DateTime.UtcNow;
            }
            else
            {
                var answer = new AttemptAnswer
                {
                    Id = Guid.NewGuid(),
                    AttemptId = attemptId,
                    QuestionId = request.QuestionId,
                    SelectedOptionId = request.SelectedOptionId,
                    IsCorrect = isCorrect,
                    TimeMs = Math.Max(0, request.TimeMs)
                };

                _context.AttemptAnswers.Add(answer);
            }

            await _context.SaveChangesAsync();

            return Ok(new SubmitAnswerResponse
            {
                IsCorrect = isCorrect,
                CorrectOptionId = correctOption?.Id
            });
        }

        [HttpPost("attempts/{attemptId:guid}/finish")]
        public async Task<ActionResult<FinishAttemptResponse>> FinishAttempt(Guid attemptId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var attempt = await _context.Attempts
                .Include(a => a.Quiz)
                    .ThenInclude(q => q.Level)
                .Include(a => a.Quiz.Topic)
                .Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == user.Id);

            if (attempt == null) return NotFound("Attempt not found");

            if (attempt.CompletedAt != null)
            {
                
                return await GetCompletedAttemptResultAsync(attempt);
            }

            
            var totalQuestions = await _context.QuizQuestions.CountAsync(qq => qq.QuizId == attempt.QuizId);
            var correctAnswers = attempt.Answers.Count(a => a.IsCorrect);
            var score = totalQuestions > 0 ? (int)Math.Round(100.0 * correctAnswers / totalQuestions) : 0;
            var xpEarned = correctAnswers * XP_PER_CORRECT_ANSWER;
            var passed = score >= PASS_PERCENTAGE;

            
            attempt.Score = score;
            attempt.XpEarned = xpEarned;
            attempt.CompletedAt = DateTime.UtcNow;

            
            await _progressService.UpdateProgressAsync(user.Id, attempt.Quiz, score, xpEarned, passed);

            await _context.SaveChangesAsync();

            
            var nextLevelId = passed ? await GetNextLevelIdAsync(attempt.Quiz.LevelId) : null;

            return Ok(new FinishAttemptResponse
            {
                Score = score,
                CorrectCount = correctAnswers,
                TotalQuestions = totalQuestions,
                DurationMs = attempt.Answers.Sum(a => a.TimeMs),
                XpEarned = xpEarned,
                Passed = passed,
                NextLevelId = nextLevelId
            });
        }

        
        [HttpGet("quizzes/{quizId:guid}/status")]
        public async Task<ActionResult<QuizStatusResponse>> GetQuizStatus(Guid quizId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var quiz = await _context.Quizzes
                .Include(q => q.Level)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null) return NotFound("Quiz not found");

            var accessResult = await CanUserAccessQuizAsync(quizId, user.Id);

            var lastAttempt = await _context.Attempts
                .Include(a => a.Answers)
                .Where(a => a.QuizId == quizId && a.UserId == user.Id && a.CompletedAt != null)
                .OrderByDescending(a => a.CompletedAt)
                .FirstOrDefaultAsync();

            var progress = quiz.LevelId.HasValue ?
                await _context.UserProgresses.FirstOrDefaultAsync(
                    p => p.UserId == user.Id && p.LevelId == quiz.LevelId) : null;

            return Ok(new QuizStatusResponse
            {
                CanAccess = accessResult.CanAccess,
                Reason = accessResult.Reason,
                RequiresUnlock = accessResult.RequiresUnlock,
                CanRetry = accessResult.CanRetry,
                HasCompleted = lastAttempt != null,
                BestScore = lastAttempt?.Score,
                IsLevelCompleted = progress?.IsLevelCompleted ?? false,
                LastAttemptAt = lastAttempt?.CompletedAt
            });
        }

        private async Task<(bool CanAccess, string Reason, bool RequiresUnlock, bool CanRetry)> CanUserAccessQuizAsync(Guid quizId, Guid userId)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Level)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz?.LevelId == null)
                return (true, "", false, false);

            var progress = await _context.UserProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.LevelId == quiz.LevelId);

            if (progress == null)
            {
                var isFirstLevel = await IsFirstAvailableLevelAsync(userId, quiz.LevelId.Value);
                if (!isFirstLevel)
                    return (false, "Previous levels must be completed first", true, false);

                return (true, "", false, false);
            }

            if (progress.IsLevelCompleted && !progress.CanRetry)
                return (false, "Level already completed", false, false);

            if (progress.IsLevelCompleted && progress.CanRetry)
                return (true, "Retry attempt", false, true);

            return (true, "", false, false);
        }

        private async Task<bool> IsFirstAvailableLevelAsync(Guid userId, Guid levelId)
        {
            var level = await _context.Levels
                .Include(l => l.Topic)
                .FirstOrDefaultAsync(l => l.Id == levelId);

            if (level == null) return false;

            var previousLevels = await _context.Levels
                .Where(l => l.TopicId == level.TopicId && l.LevelNumber < level.LevelNumber)
                .ToListAsync();

            if (!previousLevels.Any()) return true;

            var completedPreviousLevels = await _context.UserProgresses
                .Where(p => p.UserId == userId &&
                           previousLevels.Select(pl => pl.Id).Contains(p.LevelId!.Value) &&
                           p.IsLevelCompleted)
                .CountAsync();

            return completedPreviousLevels == previousLevels.Count;
        }

        private async Task<Guid?> GetNextLevelIdAsync(Guid? currentLevelId)
        {
            if (!currentLevelId.HasValue) return null;

            var unlockRule = await _context.UnlockRules
                .FirstOrDefaultAsync(r => r.FromLevelId == currentLevelId);

            if (unlockRule != null)
                return unlockRule.ToLevelId;

            var currentLevel = await _context.Levels
                .FirstOrDefaultAsync(l => l.Id == currentLevelId);

            if (currentLevel == null) return null;

            var nextLevel = await _context.Levels
                .Where(l => l.TopicId == currentLevel.TopicId &&
                           l.LevelNumber > currentLevel.LevelNumber)
                .OrderBy(l => l.LevelNumber)
                .FirstOrDefaultAsync();

            return nextLevel?.Id;
        }

        private async Task<ActionResult<FinishAttemptResponse>> GetCompletedAttemptResultAsync(Attempt attempt)
        {
            var totalQuestions = await _context.QuizQuestions.CountAsync(qq => qq.QuizId == attempt.QuizId);
            var correctAnswers = attempt.Answers.Count(a => a.IsCorrect);
            var passed = attempt.Score >= PASS_PERCENTAGE;
            var nextLevelId = passed ? await GetNextLevelIdAsync(attempt.Quiz.LevelId) : null;

            return Ok(new FinishAttemptResponse
            {
                Score = attempt.Score ?? 0,
                CorrectCount = correctAnswers,
                TotalQuestions = totalQuestions,
                DurationMs = attempt.Answers.Sum(a => a.TimeMs),
                XpEarned = attempt.XpEarned,
                Passed = passed,
                NextLevelId = nextLevelId
            });
        }
    }

    public class StartAttemptRequest
    {
        public bool BypassLock { get; set; } = false;
    }

    public class StartAttemptResponse
    {
        public Guid AttemptId { get; set; }
    }

    public class SubmitAnswerRequest
    {
        public Guid QuestionId { get; set; }
        public Guid SelectedOptionId { get; set; }
        public int TimeMs { get; set; }
    }

    public class SubmitAnswerResponse
    {
        public bool IsCorrect { get; set; }
        public Guid? CorrectOptionId { get; set; }
    }

    public class FinishAttemptResponse
    {
        public int Score { get; set; }
        public int CorrectCount { get; set; }
        public int TotalQuestions { get; set; }
        public int DurationMs { get; set; }
        public int XpEarned { get; set; }
        public bool Passed { get; set; }
        public Guid? NextLevelId { get; set; }
    }

    public class QuizStatusResponse
    {
        public bool CanAccess { get; set; }
        public string Reason { get; set; } = "";
        public bool RequiresUnlock { get; set; }
        public bool CanRetry { get; set; }
        public bool HasCompleted { get; set; }
        public int? BestScore { get; set; }
        public bool IsLevelCompleted { get; set; }
        public DateTime? LastAttemptAt { get; set; }
    }
}