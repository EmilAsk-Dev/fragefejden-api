using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FrageFejden.DTOs.Subject;
using FrageFejden.Entities;
using Microsoft.EntityFrameworkCore;

namespace FrageFejden.Services
{
    public class SubjectLevelStatusDto
    {
        public Guid LevelId { get; set; }
        public int LevelNumber { get; set; }
        public string? Title { get; set; }
        public int MinXpUnlock { get; set; }
        public bool IsUnlocked { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class SubjectProgressDto
    {
        public Guid SubjectId { get; set; }
        public string SubjectName { get; set; } = null!;
        public int TotalLevels { get; set; }
        public int CompletedLevels { get; set; }
        public int UserXp { get; set; }
        public List<SubjectLevelStatusDto> Levels { get; set; } = new();
    }

    public interface ISubjectService
    {
        Task<IReadOnlyList<Subject>> GetSubjectsForClassAsync(Guid classId);
        Task<Subject?> GetSubjectInClassAsync(Guid classId, Guid subjectId);

        Task<Subject> AddSubjectToClassAsync(
            Guid classId,
            string name,
            string? description,
            Guid createdById);

        Task<bool> UpdateSubjectInClassAsync(
            Guid classId,
            Guid subjectId,
            string? name,
            string? description);

        Task<bool> RemoveSubjectFromClassAsync(Guid classId, Guid subjectId);

        // Optional helpers
        Task<bool> ExistsByNameInClassAsync(Guid classId, string subjectName);
        Task<int> CountSubjectsInClassAsync(Guid classId);

        // Progress (used by the React page)
        Task<SubjectProgressDto?> GetSubjectProgressAsync(Guid subjectId, Guid userId, Guid? classId = null);


    }

    public class SubjectService : ISubjectService
    {
        private readonly AppDbContext _context;

        public SubjectService(AppDbContext context)
        {
            _context = context;
        }



        // -------- READ --------
        public async Task<IReadOnlyList<Subject>> GetSubjectsForClassAsync(Guid classId)
        {
            var cls = await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subjects)
                .FirstOrDefaultAsync(c => c.Id == classId);

            return cls?.Subjects?.ToList() ?? new List<Subject>();
        }

        public async Task<Subject?> GetSubjectInClassAsync(Guid classId, Guid subjectId)
        {
            var cls = await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subjects)
                .FirstOrDefaultAsync(c => c.Id == classId);

            return cls?.Subjects.FirstOrDefault(s => s.Id == subjectId);
        }

        // -------- CREATE --------
        public async Task<Subject> AddSubjectToClassAsync(
            Guid classId,
            string name,
            string? description,
            Guid createdById)
        {
            var cls = await _context.Classes
                .Include(c => c.Subjects)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (cls is null)
                throw new ArgumentException("Class not found.", nameof(classId));

            var trimmedName = name?.Trim() ?? throw new ArgumentNullException(nameof(name));
            if (trimmedName.Length == 0)
                throw new ArgumentException("Name is required.", nameof(name));

            // Unique per class (case-insensitive)
            if (cls.Subjects.Any(s => s.Name.ToLower() == trimmedName.ToLower()))
                throw new InvalidOperationException($"Subject '{trimmedName}' already exists in this class.");

            var subject = new Subject
            {
                Id = Guid.NewGuid(),
                Name = trimmedName,
                Description = string.IsNullOrWhiteSpace(description) ? null : description!.Trim(),
                CreatedById = createdById,
                CreatedAt = DateTime.UtcNow
            };

            cls.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            return subject;
        }

        // -------- UPDATE --------
        public async Task<bool> UpdateSubjectInClassAsync(
            Guid classId,
            Guid subjectId,
            string? name,
            string? description)
        {
            var cls = await _context.Classes
                .Include(c => c.Subjects)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (cls is null) return false;

            var subj = cls.Subjects.FirstOrDefault(s => s.Id == subjectId);
            if (subj is null) return false;

            if (!string.IsNullOrWhiteSpace(name))
            {
                var newName = name.Trim();

                if (!newName.Equals(subj.Name, StringComparison.OrdinalIgnoreCase) &&
                    cls.Subjects.Any(s => s.Name.ToLower() == newName.ToLower()))
                {
                    throw new InvalidOperationException($"Subject '{newName}' already exists in this class.");
                }
                subj.Name = newName;
            }

            if (description != null) // allow explicit null to clear
            {
                subj.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // -------- DELETE --------
        public async Task<bool> RemoveSubjectFromClassAsync(Guid classId, Guid subjectId)
        {
            var cls = await _context.Classes
                .Include(c => c.Subjects)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (cls is null) return false;

            var subj = cls.Subjects.FirstOrDefault(s => s.Id == subjectId);
            if (subj is null) return false;

            // Many-to-many: this detaches. One-to-many: consider _context.Remove(subj)
            cls.Subjects.Remove(subj);

            await _context.SaveChangesAsync();
            return true;
        }

        // -------- HELPERS --------
        public async Task<bool> ExistsByNameInClassAsync(Guid classId, string subjectName)
        {
            var lowered = subjectName.Trim().ToLower();
            var cls = await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subjects)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (cls is null) return false;
            return cls.Subjects.Any(s => s.Name.ToLower() == lowered);
        }

        public async Task<int> CountSubjectsInClassAsync(Guid classId)
        {
            var cls = await _context.Classes
                .AsNoTracking()
                .Include(c => c.Subjects)
                .FirstOrDefaultAsync(c => c.Id == classId);

            return cls?.Subjects.Count ?? 0;
        }

        // -------- PROGRESS (XP-based unlock/completion) --------
        public async Task<SubjectProgressDto?> GetSubjectProgressAsync(Guid subjectId, Guid userId, Guid? classId = null)
        {
            var subject = await _context.Set<Subject>()
                .Include(s => s.Levels.OrderBy(l => l.LevelNumber))
                .FirstOrDefaultAsync(s => s.Id == subjectId);
            if (subject is null) return null;

            var xp = await _context.Set<UserProgress>()
                .Where(up => up.SubjectId == subjectId && up.UserId == userId)
                .Select(up => (int?)up.Xp)
                .FirstOrDefaultAsync() ?? 0;

            var levels = subject.Levels
                .OrderBy(l => l.LevelNumber)
                .ToList();

            var dto = new SubjectProgressDto
            {
                SubjectId = subject.Id,
                SubjectName = subject.Name,
                TotalLevels = levels.Count,
                UserXp = xp
            };

            for (int i = 0; i < levels.Count; i++)
            {
                var l = levels[i];
                var next = (i + 1 < levels.Count) ? levels[i + 1] : null;

                var isUnlocked = xp >= l.MinXpUnlock;
                var isCompleted = next != null && xp >= next.MinXpUnlock;

                dto.Levels.Add(new SubjectLevelStatusDto
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
    }
}
