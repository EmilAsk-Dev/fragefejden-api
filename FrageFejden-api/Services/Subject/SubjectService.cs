using FrageFejden.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FrageFejden.Services
{
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
    }

    
    public class SubjectService : ISubjectService
    {
        private readonly AppDbContext _context;

        public SubjectService(AppDbContext context)
        {
            _context = context;
        }

        
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
                // Optional: keep unique per class
                if (!newName.Equals(subj.Name, StringComparison.OrdinalIgnoreCase) &&
                    cls.Subjects.Any(s => s.Name.ToLower() == newName.ToLower()))
                {
                    throw new InvalidOperationException($"Subject '{newName}' already exists in this class.");
                }
                subj.Name = newName;
            }

            if (description != null)
            {
                subj.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveSubjectFromClassAsync(Guid classId, Guid subjectId)
        {
            var cls = await _context.Classes
                .Include(c => c.Subjects)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (cls is null) return false;

            var subj = cls.Subjects.FirstOrDefault(s => s.Id == subjectId);
            if (subj is null) return false;

            
            cls.Subjects.Remove(subj);
         

            await _context.SaveChangesAsync();
            return true;
        }

        
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
    }
}
