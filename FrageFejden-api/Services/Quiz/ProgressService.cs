using FrageFejden.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FrageFejden.Services
{
    public interface IProgressService
    {
        Task UpdateProgressAsync(Guid userId, Quiz quiz, int score, int xpEarned, bool passed);
        Task<bool> CanUserAccessLevelAsync(Guid userId, Guid levelId);
        Task<UserProgress?> GetUserProgressForLevelAsync(Guid userId, Guid levelId);
        Task SetRetryPermissionAsync(Guid userId, Guid levelId, bool canRetry);
    }

    public class ProgressService : IProgressService
    {
        private readonly AppDbContext _context;
        private const int PASS_PERCENTAGE = 70;

        public ProgressService(AppDbContext context)
        {
            _context = context;
        }

        public async Task UpdateProgressAsync(Guid userId, Quiz quiz, int score, int xpEarned, bool passed)
        {
            if (!quiz.LevelId.HasValue) return;

            var progress = await _context.UserProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.LevelId == quiz.LevelId);

            if (progress == null)
            {
                progress = new UserProgress
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SubjectId = quiz.Topic?.SubjectId ?? quiz.SubjectId,
                    TopicId = quiz.TopicId,
                    LevelId = quiz.LevelId.Value,
                    Xp = 0,
                    LastActivity = DateTime.UtcNow
                };
                _context.UserProgresses.Add(progress);
            }

            progress.Xp += xpEarned;
            progress.LastActivity = DateTime.UtcNow;

           
            if (!progress.BestScore.HasValue || score > progress.BestScore.Value)
            {
                progress.BestScore = score;
            }

            if (passed && !progress.IsLevelCompleted)
            {
                progress.IsLevelCompleted = true;
                progress.CompletedAt = DateTime.UtcNow;
                progress.CanRetry = false; 
            }

         
            if (progress.IsLevelCompleted && passed)
            {
                progress.RetryCount++;
                progress.LastRetryAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<bool> CanUserAccessLevelAsync(Guid userId, Guid levelId)
        {
            var level = await _context.Levels
                .Include(l => l.Topic)
                .FirstOrDefaultAsync(l => l.Id == levelId);

            if (level == null) return false;

            
            var progress = await _context.UserProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.LevelId == levelId);

            
            if (progress == null && level.LevelNumber == 1)
                return true;

           
            if (progress == null)
            {
                var previousLevelsCompleted = await _context.UserProgresses
                    .Where(p => p.UserId == userId &&
                               p.TopicId == level.TopicId &&
                               p.Level!.LevelNumber < level.LevelNumber &&
                               p.IsLevelCompleted)
                    .CountAsync();

                var totalPreviousLevels = await _context.Levels
                    .Where(l => l.TopicId == level.TopicId && l.LevelNumber < level.LevelNumber)
                    .CountAsync();

                return previousLevelsCompleted >= totalPreviousLevels;
            }

            if (progress.IsLevelCompleted)
                return progress.CanRetry;

            return true;
        }

        public async Task<UserProgress?> GetUserProgressForLevelAsync(Guid userId, Guid levelId)
        {
            return await _context.UserProgresses
                .Include(p => p.Level)
                .Include(p => p.Topic)
                .Include(p => p.Subject)
                .FirstOrDefaultAsync(p => p.UserId == userId && p.LevelId == levelId);
        }

        public async Task SetRetryPermissionAsync(Guid userId, Guid levelId, bool canRetry)
        {
            var progress = await _context.UserProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.LevelId == levelId);

            if (progress != null)
            {
                progress.CanRetry = canRetry;
                await _context.SaveChangesAsync();
            }
        }
    }
}