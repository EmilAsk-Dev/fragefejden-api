using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FrageFejden.Entities;
using Microsoft.EntityFrameworkCore;
using FrageFejden_api.Api;

namespace FrageFejden.Services
{


    // ===== Contract =====
    public interface ISubjectService
    {
        // Class -> Subjects
        Task<IReadOnlyList<Subject>> GetSubjectsForClassAsync(Guid classId);
        Task<Subject?> GetSubjectInClassAsync(Guid classId, Guid subjectId);
        Task<Subject> AddSubjectToClassAsync(Guid classId, string name, string? description, Guid createdById);
        Task<bool> UpdateSubjectInClassAsync(Guid classId, Guid subjectId, string? name, string? description);
        Task<bool> RemoveSubjectFromClassAsync(Guid classId, Guid subjectId);
        Task<bool> ExistsByNameInClassAsync(Guid classId, string subjectName);
        Task<int> CountSubjectsInClassAsync(Guid classId);

        // Subject -> Topics
        Task<IReadOnlyList<TopicSummaryDto>> GetTopicsForSubjectAsync(Guid subjectId);
        Task<Topic?> GetTopicAsync(Guid topicId);
        Task<Topic> CreateTopicAsync(Guid subjectId, string name, string? description, int? sortOrder = null);
        Task<bool> UpdateTopicAsync(Guid topicId, string? name, string? description, int? sortOrder = null);
        Task<bool> DeleteTopicAsync(Guid topicId);

        // Progress (levels under topic)
        Task<TopicProgressDto?> GetTopicProgressAsync(Guid topicId, Guid userId);

        // Subject-general quizzes (TopicId == null && LevelId == null)
        Task<IReadOnlyList<Quiz>> GetGeneralQuizzesForSubjectAsync(Guid subjectId, bool onlyPublished = true);

        // Teacher -> Subjects
        // alla ämnen som den inloggade läraren "äger" eller har skapat quiz i
        Task<IReadOnlyList<Subject>> GetSubjectsForTeacherAsync(Guid teacherId);

        // alla topics (kurser) i ett ämne där läraren har minst ett quiz
        Task<IReadOnlyList<TopicSummaryDto>> GetTeacherTopicsInSubjectAsync(Guid teacherId, Guid subjectId);
    }

    // ===== Implementation =====
    public class SubjectService : ISubjectService
    {
        private readonly AppDbContext _context;
        public SubjectService(AppDbContext context) => _context = context;

        // ---- Class -> Subjects ----
        public async Task<IReadOnlyList<Subject>> GetSubjectsForClassAsync(Guid classId)
        {
            return await _context.Subjects
                .AsNoTracking()
                .Where(s => s.ClassId == classId)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<Subject?> GetSubjectInClassAsync(Guid classId, Guid subjectId)
        {
            return await _context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == subjectId && s.ClassId == classId);
        }

        public async Task<Subject> AddSubjectToClassAsync(Guid classId, string name, string? description, Guid createdById)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));

            var clsExists = await _context.Classes.AnyAsync(c => c.Id == classId);
            if (!clsExists) throw new ArgumentException("Class not found.", nameof(classId));

            var trimmed = name.Trim();
            var dupe = await _context.Subjects.AnyAsync(s => s.ClassId == classId && s.Name.ToLower() == trimmed.ToLower());
            if (dupe) throw new InvalidOperationException($"Subject '{trimmed}' already exists in this class.");

            var subject = new Subject
            {
                Id = Guid.NewGuid(),
                Name = trimmed,
                Description = string.IsNullOrWhiteSpace(description) ? null : description!.Trim(),
                CreatedById = createdById,
                CreatedAt = DateTime.UtcNow,
                ClassId = classId
            };

            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();
            return subject;
        }

        public async Task<bool> UpdateSubjectInClassAsync(Guid classId, Guid subjectId, string? name, string? description)
        {
            var subj = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.ClassId == classId);
            if (subj is null) return false;

            if (!string.IsNullOrWhiteSpace(name))
            {
                var newName = name.Trim();
                var nameTaken = await _context.Subjects
                    .AnyAsync(s => s.ClassId == classId && s.Id != subjectId && s.Name.ToLower() == newName.ToLower());
                if (nameTaken) throw new InvalidOperationException($"Subject '{newName}' already exists in this class.");
                subj.Name = newName;
            }

            if (description != null)
                subj.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveSubjectFromClassAsync(Guid classId, Guid subjectId)
        {
            var subj = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId && s.ClassId == classId);
            if (subj is null) return false;

            _context.Subjects.Remove(subj);
            await _context.SaveChangesAsync();
            return true;
        }

        public Task<bool> ExistsByNameInClassAsync(Guid classId, string subjectName)
        {
            var lowered = subjectName.Trim().ToLower();
            return _context.Subjects.AnyAsync(s => s.ClassId == classId && s.Name.ToLower() == lowered);
        }

        public Task<int> CountSubjectsInClassAsync(Guid classId)
            => _context.Subjects.CountAsync(s => s.ClassId == classId);

        // ---- Subject -> Topics ----
        public async Task<IReadOnlyList<TopicSummaryDto>> GetTopicsForSubjectAsync(Guid subjectId)
        {
            return await _context.Topics
                .AsNoTracking()
                .Where(t => t.SubjectId == subjectId)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
                .Select(t => new TopicSummaryDto
                {
                    TopicId = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    SortOrder = t.SortOrder,
                    LevelCount = t.Levels.Count
                })
                .ToListAsync();
        }

        public Task<Topic?> GetTopicAsync(Guid topicId)
        {
            return _context.Topics
                .AsNoTracking()
                .Include(t => t.Subject)
                .FirstOrDefaultAsync(t => t.Id == topicId);
        }

        public async Task<Topic> CreateTopicAsync(Guid subjectId, string name, string? description, int? sortOrder = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Name is required.", nameof(name));

            var subjExists = await _context.Subjects.AnyAsync(s => s.Id == subjectId);
            if (!subjExists) throw new ArgumentException("Subject not found.", nameof(subjectId));

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
                Description = string.IsNullOrWhiteSpace(description) ? null : description!.Trim(),
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

            _context.Topics.Remove(topic);
            await _context.SaveChangesAsync();
            return true;
        }

        // ---- Progress (topic-based) ----
        public async Task<TopicProgressDto?> GetTopicProgressAsync(Guid topicId, Guid userId)
        {
            var topic = await _context.Topics
                .AsNoTracking()
                .Include(t => t.Subject)
                .Include(t => t.Levels)
                .FirstOrDefaultAsync(t => t.Id == topicId);

            if (topic is null) return null;

            // XP is per Subject
            var xp = await _context.UserProgresses
                .Where(up => up.UserId == userId && up.SubjectId == topic.SubjectId)
                .Select(up => (int?)up.Xp)
                .FirstOrDefaultAsync() ?? 0;

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

        public async Task<IReadOnlyList<Subject>> GetSubjectsForTeacherAsync(Guid teacherId)
        {
            // 1) Ämnen läraren själv skapat
            var ownedSubjectIds = _context.Subjects
                .Where(s => s.CreatedById == teacherId)
                .Select(s => s.Id);

            // 2) Ämnen där läraren har skapat minst ett quiz
            //    (antingen ämne direkt på quiz, eller via quiz.Topic.SubjectId)
            var subjectIdsFromQuizzes = _context.Quizzes
                .Where(q => q.CreatedById == teacherId)
                .Select(q => q.TopicId != null ? q.Topic!.SubjectId : q.SubjectId);

            var ids = await ownedSubjectIds
                .Concat(subjectIdsFromQuizzes)
                .Distinct()
                .ToListAsync();

            return await _context.Subjects
                .AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<TopicSummaryDto>> GetTeacherTopicsInSubjectAsync(Guid teacherId, Guid subjectId)
        {
            // Topic-IDs i aktuellt ämne där läraren har minst ett quiz
            var topicIds = await _context.Quizzes
                .Where(q => q.CreatedById == teacherId && q.TopicId != null && q.Topic!.SubjectId == subjectId)
                .Select(q => q.TopicId!.Value)
                .Distinct()
                .ToListAsync();

            return await _context.Topics
                .AsNoTracking()
                .Where(t => t.SubjectId == subjectId && topicIds.Contains(t.Id))
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


        // ---- Subject-general quizzes (no topic/level) ----
        public async Task<IReadOnlyList<Quiz>> GetGeneralQuizzesForSubjectAsync(Guid subjectId, bool onlyPublished = true)
        {
            var q = _context.Quizzes
                .AsNoTracking()
                .Where(z => z.SubjectId == subjectId && z.TopicId == null && z.LevelId == null);

            if (onlyPublished) q = q.Where(z => z.IsPublished);

            return await q.OrderByDescending(z => z.CreatedAt).ToListAsync();
        }
    }
}
