using FrageFejden.Entities;
using FrageFejden_api.Api;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static FrageFejden.Controllers.TopicsController;

namespace FrageFejden.Services
{
    // ===== DTOs =====
   

    public class LevelDto
    {
        public Guid LevelId { get; set; }
        public Guid TopicId { get; set; }
        public int LevelNumber { get; set; }
        public string? Title { get; set; }
        public int MinXpUnlock { get; set; }
    }

    

    // ===== Contract =====
    public interface ITopicService
    {
        // Topics
        Task<Topic?> GetTopicAsync(Guid topicId);
        Task<IReadOnlyList<TopicSummaryDto>> GetTopicsForSubjectAsync(Guid subjectId);
        Task<Topic> CreateTopicAsync(Guid subjectId, string name, string? description, int? sortOrder = null);
        Task<bool> UpdateTopicAsync(Guid topicId, string? name, string? description, int? sortOrder = null);
        Task<bool> DeleteTopicAsync(Guid topicId);

        // Levels under a Topic
        Task<IReadOnlyList<LevelDto>> GetLevelsForTopicAsync(Guid topicId);
        Task<Level> CreateLevelAsync(Guid topicId, int levelNumber, string? title, int minXpUnlock);
        Task<bool> UpdateLevelAsync(Guid levelId, int? levelNumber = null, string? title = null, int? minXpUnlock = null);
        Task<bool> DeleteLevelAsync(Guid levelId);

        // Progress
        Task<TopicProgressDto?> GetTopicProgressAsync(Guid topicId, Guid userId);

        // Quizzes scoped to Topic
        Task<IReadOnlyList<Quiz>> GetTopicQuizzesAsync(Guid topicId, bool onlyPublished = true);

        // Helpers / study text
        Task<Level?> GetLevelAsync(Guid levelId);
        Task<LevelStudyDto?> GetLevelStudyAsync(Guid levelId);
        Task<bool> UpdateLevelStudyTextAsync(Guid levelId, string? studyText);

        // Per-user study-read state
        Task<LevelStudyReadStatusDto> GetLevelStudyReadStatusAsync(Guid levelId, Guid userId);
        Task MarkLevelStudyReadAsync(Guid levelId, Guid userId);

        Task<TopicProgressDto?> CompleteLevelAsync(Guid topicId, Guid levelId, Guid userId);


    }

    // ===== Implementation =====
    public class TopicService : ITopicService
    {
        private readonly AppDbContext _context;
        public TopicService(AppDbContext context) => _context = context;

        // ---- Topics ----
        public Task<Topic?> GetTopicAsync(Guid topicId)
        {
            return _context.Topics
                .AsNoTracking()
                .Include(t => t.Subject)
                .FirstOrDefaultAsync(t => t.Id == topicId);
        }

        public async Task<IReadOnlyList<TopicSummaryDto>> GetTopicsForSubjectAsync(Guid subjectId)
        {
            return await _context.Topics
                .AsNoTracking()
                .Where(t => t.SubjectId == subjectId)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
                .Select(t => new TopicSummaryDto
                {
                    TopicId = t.Id,
                    SubjectId = t.SubjectId,
                    Name = t.Name,
                    Description = t.Description,
                    SortOrder = t.SortOrder,
                    LevelCount = t.Levels.Count
                })
                .ToListAsync();
        }

        public async Task<Topic> CreateTopicAsync(Guid subjectId, string name, string? description, int? sortOrder = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));

            var subjectExists = await _context.Subjects.AnyAsync(s => s.Id == subjectId);
            if (!subjectExists) throw new ArgumentException("Subject not found.", nameof(subjectId));

            var trimmed = name.Trim();
            var dup = await _context.Topics.AnyAsync(t => t.SubjectId == subjectId && t.Name.ToLower() == trimmed.ToLower());
            if (dup) throw new InvalidOperationException($"Topic '{trimmed}' already exists in this subject.");

            int nextOrder = sortOrder
                ?? (await _context.Topics.Where(t => t.SubjectId == subjectId)
                        .Select(t => (int?)t.SortOrder)
                        .MaxAsync() ?? -1) + 1;

            var topic = new Topic
            {
                Id = Guid.NewGuid(),
                SubjectId = subjectId,
                Name = trimmed,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                SortOrder = nextOrder
            };

            _context.Topics.Add(topic);
            await _context.SaveChangesAsync();
            return topic;
        }

        public async Task<bool> UpdateTopicAsync(Guid topicId, string? name, string? description, int? sortOrder = null)
        {
            var topic = await _context.Topics.FirstOrDefaultAsync(t => t.Id == topicId);
            if (topic is null) return false;

            if (!string.IsNullOrWhiteSpace(name))
            {
                var newName = name.Trim();
                var dup = await _context.Topics.AnyAsync(t =>
                    t.SubjectId == topic.SubjectId && t.Id != topicId && t.Name.ToLower() == newName.ToLower());
                if (dup) throw new InvalidOperationException($"Topic '{newName}' already exists in this subject.");
                topic.Name = newName;
            }

            if (description != null)
                topic.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            if (sortOrder.HasValue)
                topic.SortOrder = sortOrder.Value;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteTopicAsync(Guid topicId)
        {
            var topic = await _context.Topics.FirstOrDefaultAsync(t => t.Id == topicId);
            if (topic is null) return false;

            _context.Topics.Remove(topic); // Levels cascade
            await _context.SaveChangesAsync();
            return true;
        }

        // ---- Levels ----
        public async Task<IReadOnlyList<LevelDto>> GetLevelsForTopicAsync(Guid topicId)
        {
            var topicExists = await _context.Topics.AnyAsync(t => t.Id == topicId);
            if (!topicExists) return Array.Empty<LevelDto>();

            return await _context.Levels
                .AsNoTracking()
                .Where(l => l.TopicId == topicId)
                .OrderBy(l => l.LevelNumber)
                .Select(l => new LevelDto
                {
                    LevelId = l.Id,
                    TopicId = l.TopicId,
                    LevelNumber = l.LevelNumber,
                    Title = l.Title,
                    MinXpUnlock = l.MinXpUnlock
                })
                .ToListAsync();
        }

        public async Task<Level> CreateLevelAsync(Guid topicId, int levelNumber, string? title, int minXpUnlock)
        {
            var topic = await _context.Topics.FirstOrDefaultAsync(t => t.Id == topicId);
            if (topic is null) throw new ArgumentException("Topic not found.", nameof(topicId));

            // Enforce unique LevelNumber per Topic
            var exists = await _context.Levels.AnyAsync(l => l.TopicId == topicId && l.LevelNumber == levelNumber);
            if (exists) throw new InvalidOperationException($"Level {levelNumber} already exists in this topic.");

            var level = new Level
            {
                Id = Guid.NewGuid(),
                TopicId = topicId,
                LevelNumber = levelNumber,
                Title = string.IsNullOrWhiteSpace(title) ? null : title!.Trim(),
                MinXpUnlock = minXpUnlock
            };

            _context.Levels.Add(level);
            await _context.SaveChangesAsync();
            return level;
        }

        public async Task<bool> UpdateLevelAsync(Guid levelId, int? levelNumber = null, string? title = null, int? minXpUnlock = null)
        {
            var level = await _context.Levels.FirstOrDefaultAsync(l => l.Id == levelId);
            if (level is null) return false;

            if (levelNumber.HasValue && levelNumber.Value != level.LevelNumber)
            {
                var dup = await _context.Levels.AnyAsync(l => l.TopicId == level.TopicId && l.LevelNumber == levelNumber.Value && l.Id != levelId);
                if (dup) throw new InvalidOperationException($"Level {levelNumber.Value} already exists in this topic.");
                level.LevelNumber = levelNumber.Value;
            }

            if (title != null)
                level.Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();

            if (minXpUnlock.HasValue)
                level.MinXpUnlock = minXpUnlock.Value;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteLevelAsync(Guid levelId)
        {
            var level = await _context.Levels.FirstOrDefaultAsync(l => l.Id == levelId);
            if (level is null) return false;

            _context.Levels.Remove(level);
            await _context.SaveChangesAsync();
            return true;
        }

        // ---- Progress ----
        public async Task<TopicProgressDto?> GetTopicProgressAsync(Guid topicId, Guid userId)
        {
            var topic = await _context.Topics
                .AsNoTracking()
                .Include(t => t.Subject)
                .Include(t => t.Levels)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic is null) return null;

            // ⚠️ Topic-scoped XP (take MAX in case you have multiple rows per topic/level)
            var xp = await _context.UserProgresses
                .Where(up => up.UserId == userId && up.TopicId == topicId)
                .Select(up => (int?)up.Xp)
                .MaxAsync() ?? 0;

            var levels = topic.Levels.OrderBy(l => l.LevelNumber).ToList();

            var dto = new TopicProgressDto
            {
                SubjectId = topic.SubjectId,
                SubjectName = topic.Subject!.Name,
                TopicId = topic.Id,
                TopicName = topic.Name,
                TotalLevels = levels.Count,
                UserXp = xp
            };

            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                var next = (i + 1 < levels.Count) ? levels[i + 1] : null;

                var isUnlocked = xp >= l.MinXpUnlock;
                var isCompleted = next != null && xp >= next.MinXpUnlock;

                dto.Levels.Add(new TopicLevelStatusDto
                {
                    LevelId = l.Id,
                    LevelNumber = l.LevelNumber,
                    Title = l.Title,
                    MinXpUnlock = l.MinXpUnlock,
                    IsUnlocked = isUnlocked,
                    IsCompleted = isCompleted
                });
            }

            dto.CompletedLevels = dto.Levels.Count(x => x.IsCompleted);
            return dto;
        }


        // ---- Quizzes for Topic ----
        public async Task<IReadOnlyList<Quiz>> GetTopicQuizzesAsync(Guid topicId, bool onlyPublished = true)
        {
            var q = _context.Quizzes
                .AsNoTracking()
                .Where(z => z.TopicId == topicId); // topic-scoped (not general)

            if (onlyPublished) q = q.Where(z => z.IsPublished);

            return await q.OrderByDescending(z => z.CreatedAt).ToListAsync();
        }

        public Task<Level?> GetLevelAsync(Guid levelId)
        {
            return _context.Levels
                .Include(l => l.Topic)
                .FirstOrDefaultAsync(l => l.Id == levelId);
        }

        public async Task<LevelStudyDto?> GetLevelStudyAsync(Guid levelId)
        {
            var l = await _context.Levels.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == levelId);
            if (l is null) return null;

            return new LevelStudyDto
            {
                LevelId = l.Id,
                TopicId = l.TopicId,
                LevelNumber = l.LevelNumber,
                Title = l.Title,
                MinXpUnlock = l.MinXpUnlock,
                StudyText = l.StudyText
            };
        }

        public async Task<bool> UpdateLevelStudyTextAsync(Guid levelId, string? studyText)
        {
            var level = await _context.Levels.FirstOrDefaultAsync(l => l.Id == levelId);
            if (level is null) return false;

            level.StudyText = string.IsNullOrWhiteSpace(studyText) ? null : studyText;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<LevelStudyReadStatusDto> GetLevelStudyReadStatusAsync(Guid levelId, Guid userId)
        {
            var up = await _context.UserProgresses.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId && p.LevelId == levelId);

            return new LevelStudyReadStatusDto
            {
                HasReadStudyText = up?.HasReadStudyText ?? false,
                ReadAt = up?.ReadAt
            };
        }

        public async Task MarkLevelStudyReadAsync(Guid levelId, Guid userId)
        {
            // Need SubjectId/TopicId to keep your UserProgress normalized
            var level = await _context.Levels
                .Include(l => l.Topic)
                .FirstOrDefaultAsync(l => l.Id == levelId);
            if (level is null) throw new ArgumentException("Level not found.", nameof(levelId));

            var subjectId = level.Topic.SubjectId;
            var topicId = level.TopicId;

            var up = await _context.UserProgresses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.LevelId == levelId);

            if (up is null)
            {
                up = new UserProgress
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SubjectId = subjectId,
                    TopicId = topicId,
                    LevelId = levelId,
                    Xp = 0,
                    LastActivity = DateTime.UtcNow,
                    HasReadStudyText = true,
                    ReadAt = DateTime.UtcNow
                };
                _context.UserProgresses.Add(up);
            }
            else
            {
                if (!up.HasReadStudyText)
                {
                    up.HasReadStudyText = true;
                    up.ReadAt = DateTime.UtcNow;
                }
                up.LastActivity = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<TopicProgressDto?> CompleteLevelAsync(Guid topicId, Guid levelId, Guid userId)
        {
            var topic = await _context.Topics
                .Include(t => t.Subject)
                .Include(t => t.Levels)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic is null) return null;

            var levels = topic.Levels.OrderBy(l => l.LevelNumber).ToList();
            var current = levels.FirstOrDefault(l => l.Id == levelId);
            if (current is null) return null;

            var next = levels.SkipWhile(l => l.Id != levelId).Skip(1).FirstOrDefault();

            // ⚠️ Topic-scoped XP row (LevelId == null means “aggregate for topic”)
            var up = await _context.UserProgresses.FirstOrDefaultAsync(x =>
                x.UserId == userId &&
                x.SubjectId == topic.SubjectId &&
                x.TopicId == topicId &&
                x.LevelId == null
            );

            if (up is null)
            {
                up = new UserProgress
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SubjectId = topic.SubjectId,
                    TopicId = topicId,
                    LevelId = null,         // topic aggregate row
                    Xp = 0,
                    LastActivity = DateTime.UtcNow
                };
                _context.UserProgresses.Add(up);
            }

            if (next != null && up.Xp < next.MinXpUnlock)
            {
                up.Xp = next.MinXpUnlock;
            }

            up.LastActivity = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return await GetTopicProgressAsync(topicId, userId);
        }

    }
}
