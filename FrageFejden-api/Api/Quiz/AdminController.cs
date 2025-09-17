using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FrageFejden.Entities;
using FrageFejden.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FrageFejden.Controllers.Quiz
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")]
    [Tags("Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IProgressService _progressService;

        public AdminController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IProgressService progressService)
        {
            _context = context;
            _userManager = userManager;
            _progressService = progressService;
        }

        [HttpGet("users/{userId:guid}/progress")]
        public async Task<ActionResult<List<AdminUserProgressDto>>> GetUserProgress(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found");

            var progress = await _context.UserProgresses
                .Include(p => p.Subject)
                .Include(p => p.Topic)
                .Include(p => p.Level)
                .Where(p => p.UserId == userId)
                .OrderBy(p => p.Subject.Name)
                .ThenBy(p => p.Topic!.Name)
                .ThenBy(p => p.Level!.LevelNumber)
                .ToListAsync();

            var result = progress.Select(p => new AdminUserProgressDto
            {
                ProgressId = p.Id,
                UserId = p.UserId,
                UserName = user.FullName,
                SubjectName = p.Subject.Name,
                TopicName = p.Topic?.Name,
                LevelTitle = p.Level?.Title,
                LevelNumber = p.Level?.LevelNumber ?? 0,
                Xp = p.Xp,
                IsCompleted = p.IsLevelCompleted,
                CompletedAt = p.CompletedAt,
                CanRetry = p.CanRetry,
                RetryCount = p.RetryCount,
                BestScore = p.BestScore,
                LastActivity = p.LastActivity
            }).ToList();

            return Ok(result);
        }

        [HttpPost("users/{userId:guid}/progress/{levelId:guid}/reset")]
        public async Task<IActionResult> ResetUserProgress(Guid userId, Guid levelId)
        {
            var progress = await _context.UserProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.LevelId == levelId);

            if (progress == null) return NotFound("Progress not found");

            // Reset progress but keep XP
            progress.IsLevelCompleted = false;
            progress.CompletedAt = null;
            progress.CanRetry = false;
            progress.RetryCount = 0;
            progress.BestScore = null;
            progress.LastRetryAt = null;
            progress.LastActivity = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Progress reset successfully" });
        }

        [HttpPost("users/{userId:guid}/progress/{levelId:guid}/allow-retry")]
        public async Task<IActionResult> AllowUserRetry(Guid userId, Guid levelId, [FromBody] SetRetryRequest request)
        {
            await _progressService.SetRetryPermissionAsync(userId, levelId, request.CanRetry);
            return Ok(new { message = $"Retry permission {(request.CanRetry ? "granted" : "revoked")}" });
        }

        [HttpGet("progress/stats")]
        public async Task<ActionResult<AdminProgressStatsDto>> GetProgressStats()
        {
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.UserProgresses
                .Where(p => p.LastActivity >= DateTime.UtcNow.AddDays(-30))
                .Select(p => p.UserId)
                .Distinct()
                .CountAsync();

            var completedLevels = await _context.UserProgresses
                .CountAsync(p => p.IsLevelCompleted);

            var totalLevels = await _context.Levels.CountAsync();

            var topPerformers = await _context.UserProgresses
                .Include(p => p.User)
                .GroupBy(p => p.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    UserName = g.First().User.FullName,
                    TotalXp = g.Sum(p => p.Xp),
                    CompletedLevels = g.Count(p => p.IsLevelCompleted)
                })
                .OrderByDescending(x => x.TotalXp)
                .Take(10)
                .ToListAsync();

            return Ok(new AdminProgressStatsDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                CompletedLevels = completedLevels,
                TotalLevels = totalLevels,
                CompletionRate = totalLevels > 0 ? (double)completedLevels / totalLevels * 100 : 0,
                TopPerformers = topPerformers.Select(tp => new TopPerformerDto
                {
                    UserId = tp.UserId,
                    UserName = tp.UserName,
                    TotalXp = tp.TotalXp,
                    CompletedLevels = tp.CompletedLevels
                }).ToList()
            });
        }

        [HttpDelete("users/{userId:guid}/attempts/{attemptId:guid}")]
        public async Task<IActionResult> DeleteAttempt(Guid userId, Guid attemptId)
        {
            var attempt = await _context.Attempts
                .Include(a => a.Answers)
                .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId);

            if (attempt == null) return NotFound("Attempt not found");

            _context.AttemptAnswers.RemoveRange(attempt.Answers);
            _context.Attempts.Remove(attempt);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Attempt deleted successfully" });
        }
    }

    // Admin DTOs
    public class AdminUserProgressDto
    {
        public Guid ProgressId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = "";
        public string SubjectName { get; set; } = "";
        public string? TopicName { get; set; }
        public string? LevelTitle { get; set; }
        public int LevelNumber { get; set; }
        public int Xp { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool CanRetry { get; set; }
        public int RetryCount { get; set; }
        public int? BestScore { get; set; }
        public DateTime? LastActivity { get; set; }
    }

    public class AdminProgressStatsDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int CompletedLevels { get; set; }
        public int TotalLevels { get; set; }
        public double CompletionRate { get; set; }
        public List<TopPerformerDto> TopPerformers { get; set; } = new();
    }

    public class TopPerformerDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = "";
        public int TotalXp { get; set; }
        public int CompletedLevels { get; set; }
    }

    public class SetRetryRequest
    {
        public bool CanRetry { get; set; }
    }
}