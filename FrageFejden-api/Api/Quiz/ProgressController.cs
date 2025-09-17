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

namespace FrageFejden.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin,teacher,student")]
    [Tags("Progress")]
    public class ProgressController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IProgressService _progressService;

        public ProgressController(
            AppDbContext context,
            UserManager<AppUser> userManager,
            IProgressService progressService)
        {
            _context = context;
            _userManager = userManager;
            _progressService = progressService;
        }

        [HttpGet("overview")]
        public async Task<ActionResult<UserProgressOverviewDto>> GetProgressOverview()
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var progresses = await _context.UserProgresses
                .Include(p => p.Subject)
                .Include(p => p.Topic)
                .Include(p => p.Level)
                .Where(p => p.UserId == userId.Value)
                .ToListAsync();

            var subjects = progresses.GroupBy(p => p.SubjectId)
                .Select(g => new SubjectProgressDto
                {
                    SubjectId = g.Key,
                    SubjectName = g.First().Subject.Name,
                    TotalXp = g.Sum(p => p.Xp),
                    CompletedLevels = g.Count(p => p.IsLevelCompleted),
                    TotalLevels = g.Count(),
                    Topics = g.Where(p => p.TopicId.HasValue)
                             .GroupBy(p => p.TopicId!.Value)
                             .Select(tg => new TopicProgressDto
                             {
                                 TopicId = tg.Key,
                                 TopicName = tg.First().Topic!.Name,
                                 CompletedLevels = tg.Count(p => p.IsLevelCompleted),
                                 TotalLevels = tg.Count()
                             }).ToList()
                }).ToList();

            return Ok(new UserProgressOverviewDto
            {
                UserId = userId.Value,
                TotalXp = progresses.Sum(p => p.Xp),
                CompletedLevels = progresses.Count(p => p.IsLevelCompleted),
                TotalLevels = progresses.Count,
                Subjects = subjects
            });
        }

        [HttpGet("level/{levelId:guid}")]
        public async Task<ActionResult<LevelProgressDto>> GetLevelProgress(Guid levelId)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var progress = await _progressService.GetUserProgressForLevelAsync(userId.Value, levelId);

            if (progress == null)
            {
                var level = await _context.Levels
                    .Include(l => l.Topic)
                        .ThenInclude(t => t!.Subject)
                    .FirstOrDefaultAsync(l => l.Id == levelId);

                if (level == null) return NotFound("Level not found");

                return Ok(new LevelProgressDto
                {
                    LevelId = levelId,
                    LevelTitle = level.Title,
                    LevelNumber = level.LevelNumber,
                    TopicName = level.Topic?.Name,
                    SubjectName = level.Topic?.Subject?.Name,
                    Xp = 0,
                    IsCompleted = false,
                    CanRetry = false,
                    BestScore = null,
                    CanAccess = await _progressService.CanUserAccessLevelAsync(userId.Value, levelId)
                });
            }

            return Ok(new LevelProgressDto
            {
                LevelId = levelId,
                LevelTitle = progress.Level?.Title,
                LevelNumber = progress.Level?.LevelNumber ?? 0,
                TopicName = progress.Topic?.Name,
                SubjectName = progress.Subject?.Name,
                Xp = progress.Xp,
                IsCompleted = progress.IsLevelCompleted,
                CompletedAt = progress.CompletedAt,
                CanRetry = progress.CanRetry,
                RetryCount = progress.RetryCount,
                BestScore = progress.BestScore,
                CanAccess = await _progressService.CanUserAccessLevelAsync(userId.Value, levelId)
            });
        }

        [HttpGet("topic/{topicId:guid}/path")]
        public async Task<ActionResult<LearningPathDto>> GetLearningPath(Guid topicId)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var topic = await _context.Topics
                .Include(t => t.Subject)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic == null) return NotFound("Topic not found");

            var levels = await _context.Levels
                .Where(l => l.TopicId == topicId)
                .OrderBy(l => l.LevelNumber)
                .ToListAsync();

            var progresses = await _context.UserProgresses
                .Where(p => p.UserId == userId.Value && p.TopicId == topicId)
                .ToDictionaryAsync(p => p.LevelId!.Value);

            var pathLevels = new List<PathLevelDto>();

            foreach (var level in levels)
            {
                var hasProgress = progresses.TryGetValue(level.Id, out var progress);
                var canAccess = await _progressService.CanUserAccessLevelAsync(userId.Value, level.Id);

                pathLevels.Add(new PathLevelDto
                {
                    LevelId = level.Id,
                    LevelNumber = level.LevelNumber,
                    Title = level.Title,
                    Description = level.Description,
                    CanAccess = canAccess,
                    IsCompleted = hasProgress && progress!.IsLevelCompleted,
                    BestScore = progress?.BestScore,
                    Xp = progress?.Xp ?? 0,
                    Status = GetLevelStatus(canAccess, hasProgress && progress!.IsLevelCompleted),

                    StudyText = level.StudyText,
                });
            }

            return Ok(new LearningPathDto
            {
                TopicId = topicId,
                TopicName = topic.Name,
                SubjectName = topic.Subject?.Name,
                TotalLevels = levels.Count,
                CompletedLevels = pathLevels.Count(l => l.IsCompleted),
                Levels = pathLevels
            });
        }

        [HttpPost("mark-study-text/{levelId:guid}")]
        public async Task<IActionResult> MarkStudyTextAsRead(Guid levelId)
        {
            var userId = await GetCurrentUserIdAsync();
            if (userId == null) return Unauthorized();

            var level = await _context.Levels
                .Include(l => l.Topic)
                .FirstOrDefaultAsync(l => l.Id == levelId);

            if (level == null) return NotFound("Level not found");

            var progress = await _context.UserProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId.Value && p.LevelId == levelId);

            if (progress == null)
            {
                progress = new UserProgress
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    SubjectId = level.Topic?.SubjectId ?? Guid.Empty,
                    TopicId = level.TopicId,
                    LevelId = levelId,
                    Xp = 0,
                    LastActivity = DateTime.UtcNow
                };
                _context.UserProgresses.Add(progress);
            }

            progress.HasReadStudyText = true;
            progress.ReadAt = DateTime.UtcNow;
            progress.LastActivity = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Study text marked as read" });
        }

        private async Task<Guid?> GetCurrentUserIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user?.Id;
        }

        private static string GetLevelStatus(bool canAccess, bool isCompleted)
        {
            if (isCompleted) return "completed";
            if (canAccess) return "available";
            return "locked";
        }
    }


    public class UserProgressOverviewDto
    {
        public Guid UserId { get; set; }
        public int TotalXp { get; set; }
        public int CompletedLevels { get; set; }
        public int TotalLevels { get; set; }
        public List<SubjectProgressDto> Subjects { get; set; } = new();
    }

    public class SubjectProgressDto
    {
        public Guid SubjectId { get; set; }
        public string SubjectName { get; set; } = "";
        public int TotalXp { get; set; }
        public int CompletedLevels { get; set; }
        public int TotalLevels { get; set; }
        public List<TopicProgressDto> Topics { get; set; } = new();
    }

    public class TopicProgressDto
    {
        public Guid TopicId { get; set; }
        public string TopicName { get; set; } = "";
        public int CompletedLevels { get; set; }
        public int TotalLevels { get; set; }
    }

    public class LevelProgressDto
    {
        public Guid LevelId { get; set; }
        public string? LevelTitle { get; set; }
        public int LevelNumber { get; set; }
        public string? TopicName { get; set; }
        public string? SubjectName { get; set; }
        public int Xp { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool CanRetry { get; set; }
        public int RetryCount { get; set; }
        public int? BestScore { get; set; }
        public bool CanAccess { get; set; }
    }

    public class LearningPathDto
    {
        public Guid TopicId { get; set; }
        public string TopicName { get; set; } = "";
        public string? SubjectName { get; set; }
        public int TotalLevels { get; set; }
        public int CompletedLevels { get; set; }
        public List<PathLevelDto> Levels { get; set; } = new();
    }

    public class PathLevelDto
    {
        public Guid LevelId { get; set; }
        public int LevelNumber { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public bool CanAccess { get; set; }
        public bool IsCompleted { get; set; }
        public int? BestScore { get; set; }
        public int Xp { get; set; }
        public string Status { get; set; } = "";

        public string? StudyText { get; set; }  

    }
}