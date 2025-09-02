using FrageFejden.DTOs.Subject;
using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FrageFejden.Services
{
    public interface ISubjectService
    {
        Task<IEnumerable<SubjectSummaryDto>> GetAllSubjectsAsync();
        Task<SubjectDto?> GetSubjectByIdAsync(Guid id);
        Task<SubjectWithDetailsDto?> GetSubjectWithDetailsAsync(Guid id);
        Task<SubjectDto> CreateSubjectAsync(CreateSubjectDto createDto, Guid createdById);
        Task<SubjectDto?> UpdateSubjectAsync(Guid id, UpdateSubjectDto updateDto, Guid userId);
        Task<bool> DeleteSubjectAsync(Guid id, Guid userId);
        Task<IEnumerable<SubjectSummaryDto>> GetSubjectsByUserAsync(Guid userId);
        Task<bool> SubjectExistsAsync(Guid id);
    }

    public class SubjectService : ISubjectService
    {
        private readonly AppDbContext _context;

        public SubjectService(AppDbContext context)
        {
            _context = context;
        }

        [SwaggerOperation(
            summary: "Hämtar alla ämnen.",
            description: "Denna metod hämtar alla ämnen från databasen och returnerar en lista med ämnesöversikter."
        )]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IEnumerable<SubjectSummaryDto>> GetAllSubjectsAsync()
        {
            return await _context.Subjects
                .Include(s => s.Topics)
                .Include(s => s.Levels)
                .Include(s => s.Quizzes)
                .Include(s => s.Questions)
                .OrderBy(s => s.Name)
                .Select(s => new SubjectSummaryDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    TopicsCount = s.Topics.Count,
                    LevelsCount = s.Levels.Count,
                    QuizzesCount = s.Quizzes.Count,
                    QuestionsCount = s.Questions.Count,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();
        }

        [SwaggerOperation(
            summary: "Hämtar ett ämne baserat på dess ID.",
            description: "Denna metod hämtar ett ämne från databasen baserat på dess unika ID och returnerar ämnesdetaljer."
        )]
        [Authorize]
        public async Task<SubjectDto?> GetSubjectByIdAsync(Guid id)
        {
            return await _context.Subjects
                .Include(s => s.CreatedBy)
                .Include(s => s.Topics)
                .Include(s => s.Levels)
                .Include(s => s.Quizzes)
                .Include(s => s.Questions)
                .Where(s => s.Id == id)
                .Select(s => new SubjectDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    CreatedById = s.CreatedById,
                    CreatedByName = s.CreatedBy != null ? s.CreatedBy.FullName : null,
                    CreatedAt = s.CreatedAt,
                    TopicsCount = s.Topics.Count,
                    LevelsCount = s.Levels.Count,
                    QuizzesCount = s.Quizzes.Count,
                    QuestionsCount = s.Questions.Count
                })
                .FirstOrDefaultAsync();
        }
        [SwaggerOperation(
            summary: "Hämtar ett ämne med detaljer baserat på dess ID.",
            description: "Denna metod hämtar ett ämne från databasen baserat på dess unika ID och inkluderar detaljer som tillhörande ämnen och nivåer."
        )]
        [Authorize]
        public async Task<SubjectWithDetailsDto?> GetSubjectWithDetailsAsync(Guid id)
        {
            var subject = await _context.Subjects
                .Include(s => s.CreatedBy)
                .Include(s => s.Topics)
                .Include(s => s.Levels)
                .Include(s => s.Quizzes)
                .Include(s => s.Questions)
                .Where(s => s.Id == id)
                .FirstOrDefaultAsync();

            if (subject == null) return null;

            return new SubjectWithDetailsDto
            {
                Id = subject.Id,
                Name = subject.Name,
                Description = subject.Description,
                CreatedById = subject.CreatedById,
                CreatedByName = subject.CreatedBy?.FullName,
                CreatedAt = subject.CreatedAt,
                TopicsCount = subject.Topics.Count,
                LevelsCount = subject.Levels.Count,
                QuizzesCount = subject.Quizzes.Count,
                QuestionsCount = subject.Questions.Count,
                Topics = subject.Topics.Select(t => new TopicSummaryDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    QuestionsCount = t.Questions.Count
                }).ToList(),
                Levels = subject.Levels.OrderBy(l => l.LevelNumber).Select(l => new LevelSummaryDto
                {
                    Id = l.Id,
                    LevelNumber = l.LevelNumber,
                    Title = l.Title,
                    MinXpUnlock = l.MinXpUnlock,
                    QuizzesCount = l.Quizzes.Count
                }).ToList()
            };
        }
        [SwaggerOperation(
            summary: "Skapar ett nytt ämne.",
            description: "Denna metod skapar ett nytt ämne i databasen baserat på den angivna informationen."
        )]
        [Authorize(Roles = "admin,teacher")]
        public async Task<SubjectDto> CreateSubjectAsync(CreateSubjectDto createDto, Guid createdById)
        {
            var subject = new Subject
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Description = createDto.Description,
                CreatedById = createdById,
                CreatedAt = DateTime.UtcNow
            };

            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            return new SubjectDto
            {
                Id = subject.Id,
                Name = subject.Name,
                Description = subject.Description,
                CreatedById = subject.CreatedById,
                CreatedAt = subject.CreatedAt,
                TopicsCount = 0,
                LevelsCount = 0,
                QuizzesCount = 0,
                QuestionsCount = 0
            };
        }
        [SwaggerOperation(
            summary: "Uppdaterar ett befintligt ämne.",
            description: "Denna metod uppdaterar ett befintligt ämne i databasen baserat på dess unika ID och den angivna informationen."
        )]
        [Authorize(Roles = "admin,teacher")]
        public async Task<SubjectDto?> UpdateSubjectAsync(Guid id, UpdateSubjectDto updateDto, Guid userId)
        {
            var subject = await _context.Subjects
                .Include(s => s.Topics)
                .Include(s => s.Levels)
                .Include(s => s.Quizzes)
                .Include(s => s.Questions)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (subject == null) return null;


            var user = await _context.Users.FindAsync(userId);
            if (user == null || (subject.CreatedById != userId && user.Role != Role.admin))
                return null;

            subject.Name = updateDto.Name;
            subject.Description = updateDto.Description;

            await _context.SaveChangesAsync();

            return new SubjectDto
            {
                Id = subject.Id,
                Name = subject.Name,
                Description = subject.Description,
                CreatedById = subject.CreatedById,
                CreatedAt = subject.CreatedAt,
                TopicsCount = subject.Topics.Count,
                LevelsCount = subject.Levels.Count,
                QuizzesCount = subject.Quizzes.Count,
                QuestionsCount = subject.Questions.Count
            };
        }
        [SwaggerOperation(
            summary: "Tar bort ett ämne baserat på dess ID.",
            description: "Denna metod tar bort ett ämne från databasen baserat på dess unika ID, förutsatt att användaren har rätt behörighet och att ämnet inte har några beroende entiteter."
        )]
        [Authorize(Roles = "admin,teacher")]
        public async Task<bool> DeleteSubjectAsync(Guid id, Guid userId)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null) return false;


            var user = await _context.Users.FindAsync(userId);
            if (user == null || (subject.CreatedById != userId && user.Role != Role.admin))
                return false;


            var hasQuizzes = await _context.Quizzes.AnyAsync(q => q.SubjectId == id);
            var hasQuestions = await _context.Questions.AnyAsync(q => q.SubjectId == id);
            if (hasQuizzes || hasQuestions) return false;

            _context.Subjects.Remove(subject);
            await _context.SaveChangesAsync();
            return true;
        }
        [SwaggerOperation(
            summary: "Hämtar alla ämnen skapade av en specifik användare.",
            description: "Denna metod hämtar alla ämnen från databasen som skapats av en specifik användare baserat på deras unika ID."
        )]
        [Authorize]
        public async Task<IEnumerable<SubjectSummaryDto>> GetSubjectsByUserAsync(Guid userId)
        {
            return await _context.Subjects
                .Include(s => s.Topics)
                .Include(s => s.Levels)
                .Include(s => s.Quizzes)
                .Include(s => s.Questions)
                .Where(s => s.CreatedById == userId)
                .OrderBy(s => s.Name)
                .Select(s => new SubjectSummaryDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Description = s.Description,
                    TopicsCount = s.Topics.Count,
                    LevelsCount = s.Levels.Count,
                    QuizzesCount = s.Quizzes.Count,
                    QuestionsCount = s.Questions.Count,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<bool> SubjectExistsAsync(Guid id)
        {
            return await _context.Subjects.AnyAsync(s => s.Id == id);
        }
    }
}