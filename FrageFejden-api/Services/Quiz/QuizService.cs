using FrageFejden.DTOs.Quiz;
using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FrageFejden.Services
{
    public interface IQuizService
    {
        Task<IEnumerable<QuizSummaryDto>> GetQuizzesAsync(QuizFilterDto? filter = null);
        Task<QuizDto?> GetQuizByIdAsync(Guid id);
        Task<QuizWithQuestionsDto?> GetQuizWithQuestionsAsync(Guid id, bool includeCorrectAnswers = false);
        Task<QuizDto> CreateQuizAsync(CreateQuizDto createDto, Guid createdById);
        Task<QuizDto?> UpdateQuizAsync(Guid id, UpdateQuizDto updateDto, Guid userId);
        Task<bool> DeleteQuizAsync(Guid id, Guid userId);
        Task<bool> PublishQuizAsync(Guid id, bool isPublished, Guid userId);
        Task<bool> UpdateQuizQuestionsAsync(Guid id, UpdateQuizQuestionsDto updateDto, Guid userId);
        Task<QuizStatsDto?> GetQuizStatsAsync(Guid id, Guid userId);
        Task<IEnumerable<QuizSummaryDto>> GetQuizzesByUserAsync(Guid userId);
        Task<IEnumerable<QuizSummaryDto>> GetPublishedQuizzesAsync(Guid? subjectId = null, Guid? levelId = null);
        Task<bool> QuizExistsAsync(Guid id);
        Task<bool> CanUserAccessQuizAsync(Guid quizId, Guid userId);
    }

    public class QuizService : IQuizService
    {
        private readonly AppDbContext _context;

        public QuizService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<QuizSummaryDto>> GetQuizzesAsync(QuizFilterDto? filter = null)
        {
            var query = _context.Quizzes
                .Include(q => q.Subject)
                .Include(q => q.Level)
                .Include(q => q.Class)
                .Include(q => q.QuizQuestions)
                .Include(q => q.Attempts)
                .AsQueryable();

            if (filter != null)
            {
                if (filter.SubjectId.HasValue)
                    query = query.Where(q => q.SubjectId == filter.SubjectId.Value);

                if (filter.LevelId.HasValue)
                    query = query.Where(q => q.LevelId == filter.LevelId.Value);

                if (filter.ClassId.HasValue)
                    query = query.Where(q => q.ClassId == filter.ClassId.Value);

                if (filter.IsPublished.HasValue)
                    query = query.Where(q => q.IsPublished == filter.IsPublished.Value);

                if (!string.IsNullOrEmpty(filter.SearchTerm))
                {
                    var searchTerm = filter.SearchTerm.ToLower();
                    query = query.Where(q => q.Title.ToLower().Contains(searchTerm) ||
                                           (q.Description != null && q.Description.ToLower().Contains(searchTerm)));
                }

                query = query.Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize);
            }

            return await query
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new QuizSummaryDto
                {
                    Id = q.Id,
                    Title = q.Title,
                    Description = q.Description,
                    SubjectName = q.Subject.Name,
                    LevelTitle = q.Level != null ? q.Level.Title : null,
                    LevelNumber = q.Level != null ? q.Level.LevelNumber : null,
                    ClassName = q.Class != null ? q.Class.Name : null,
                    IsPublished = q.IsPublished,
                    QuestionCount = q.QuizQuestions.Count,
                    AttemptCount = q.Attempts.Count,
                    CreatedAt = q.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<QuizDto?> GetQuizByIdAsync(Guid id)
        {
            return await _context.Quizzes
                .Include(q => q.Subject)
                .Include(q => q.Level)
                .Include(q => q.Class)
                .Include(q => q.CreatedBy)
                .Include(q => q.QuizQuestions)
                .Include(q => q.Attempts)
                .Where(q => q.Id == id)
                .Select(q => new QuizDto
                {
                    Id = q.Id,
                    SubjectId = q.SubjectId,
                    SubjectName = q.Subject.Name,
                    LevelId = q.LevelId,
                    LevelTitle = q.Level != null ? q.Level.Title : null,
                    LevelNumber = q.Level != null ? q.Level.LevelNumber : null,
                    ClassId = q.ClassId,
                    ClassName = q.Class != null ? q.Class.Name : null,
                    Title = q.Title,
                    Description = q.Description,
                    IsPublished = q.IsPublished,
                    CreatedById = q.CreatedById,
                    CreatedByName = q.CreatedBy != null ? q.CreatedBy.FullName : null,
                    CreatedAt = q.CreatedAt,
                    QuestionCount = q.QuizQuestions.Count,
                    AttemptCount = q.Attempts.Count,
                    AverageScore = q.Attempts.Any(a => a.Score.HasValue) ?
                        q.Attempts.Where(a => a.Score.HasValue).Average(a => a.Score!.Value) : null
                })
                .FirstOrDefaultAsync();
        }

        public async Task<QuizWithQuestionsDto?> GetQuizWithQuestionsAsync(Guid id, bool includeCorrectAnswers = false)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Subject)
                .Include(q => q.Level)
                .Include(q => q.Class)
                .Include(q => q.CreatedBy)
                .Include(q => q.QuizQuestions.OrderBy(qq => qq.SortOrder))
                    .ThenInclude(qq => qq.Question)
                        .ThenInclude(question => question.Options.OrderBy(o => o.SortOrder))
                .Include(q => q.Attempts)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null) return null;

            return new QuizWithQuestionsDto
            {
                Id = quiz.Id,
                SubjectId = quiz.SubjectId,
                SubjectName = quiz.Subject.Name,
                LevelId = quiz.LevelId,
                LevelTitle = quiz.Level?.Title,
                LevelNumber = quiz.Level?.LevelNumber,
                ClassId = quiz.ClassId,
                ClassName = quiz.Class?.Name,
                Title = quiz.Title,
                Description = quiz.Description,
                IsPublished = quiz.IsPublished,
                CreatedById = quiz.CreatedById,
                CreatedByName = quiz.CreatedBy?.FullName,
                CreatedAt = quiz.CreatedAt,
                QuestionCount = quiz.QuizQuestions.Count,
                AttemptCount = quiz.Attempts.Count,
                AverageScore = quiz.Attempts.Any(a => a.Score.HasValue) ?
                    quiz.Attempts.Where(a => a.Score.HasValue).Average(a => a.Score!.Value) : null,
                Questions = quiz.QuizQuestions.Select(qq => new QuizQuestionDto
                {
                    Id = qq.Id,
                    QuestionId = qq.QuestionId,
                    SortOrder = qq.SortOrder,
                    QuestionStem = qq.Question.Stem,
                    QuestionType = qq.Question.Type.ToString(),
                    Difficulty = qq.Question.Difficulty.ToString(),
                    Options = qq.Question.Options.Select(o => new QuestionOptionDto
                    {
                        Id = o.Id,
                        OptionText = o.OptionText,
                        SortOrder = o.SortOrder
                        // IsCorrect är bortaget om inte includeCorrectAnswers är true
                    }).ToList()
                }).ToList()
            };
        }

        public async Task<QuizDto> CreateQuizAsync(CreateQuizDto createDto, Guid createdById)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var quiz = new Quiz
                {
                    Id = Guid.NewGuid(),
                    SubjectId = createDto.SubjectId,
                    LevelId = createDto.LevelId,
                    ClassId = createDto.ClassId,
                    Title = createDto.Title,
                    Description = createDto.Description,
                    IsPublished = createDto.IsPublished,
                    CreatedById = createdById,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Quizzes.Add(quiz);
                await _context.SaveChangesAsync();


                if (createDto.QuestionIds.Any())
                {
                    var quizQuestions = createDto.QuestionIds.Select((questionId, index) => new QuizQuestion
                    {
                        Id = Guid.NewGuid(),
                        QuizId = quiz.Id,
                        QuestionId = questionId,
                        SortOrder = index + 1
                    }).ToList();

                    _context.QuizQuestions.AddRange(quizQuestions);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();


                return await GetQuizByIdAsync(quiz.Id) ?? throw new InvalidOperationException("Failed to retrieve created quiz");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<QuizDto?> UpdateQuizAsync(Guid id, UpdateQuizDto updateDto, Guid userId)
        {
            var quiz = await _context.Quizzes.FindAsync(id);
            if (quiz == null) return null;


            var user = await _context.Users.FindAsync(userId);
            if (user == null || (quiz.CreatedById != userId && user.Role != Role.admin))
                return null;

            quiz.Title = updateDto.Title;
            quiz.Description = updateDto.Description;
            quiz.IsPublished = updateDto.IsPublished;
            quiz.LevelId = updateDto.LevelId;
            quiz.ClassId = updateDto.ClassId;

            await _context.SaveChangesAsync();
            return await GetQuizByIdAsync(id);
        }

        public async Task<bool> DeleteQuizAsync(Guid id, Guid userId)
        {
            var quiz = await _context.Quizzes.FindAsync(id);
            if (quiz == null) return false;


            var user = await _context.Users.FindAsync(userId);
            if (user == null || (quiz.CreatedById != userId && user.Role != Role.admin))
                return false;


            var hasAttempts = await _context.Attempts.AnyAsync(a => a.QuizId == id);
            if (hasAttempts) return false;

            _context.Quizzes.Remove(quiz);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> PublishQuizAsync(Guid id, bool isPublished, Guid userId)
        {
            var quiz = await _context.Quizzes.FindAsync(id);
            if (quiz == null) return false;


            var user = await _context.Users.FindAsync(userId);
            if (user == null || (quiz.CreatedById != userId && user.Role != Role.admin))
                return false;

            quiz.IsPublished = isPublished;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateQuizQuestionsAsync(Guid id, UpdateQuizQuestionsDto updateDto, Guid userId)
        {
            var quiz = await _context.Quizzes.Include(q => q.QuizQuestions).FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return false;


            var user = await _context.Users.FindAsync(userId);
            if (user == null || (quiz.CreatedById != userId && user.Role != Role.admin))
                return false;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {

                _context.QuizQuestions.RemoveRange(quiz.QuizQuestions);


                var newQuizQuestions = updateDto.Questions.Select(q => new QuizQuestion
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    QuestionId = q.QuestionId,
                    SortOrder = q.SortOrder
                }).ToList();

                _context.QuizQuestions.AddRange(newQuizQuestions);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<QuizStatsDto?> GetQuizStatsAsync(Guid id, Guid userId)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Attempts.OrderByDescending(a => a.StartedAt).Take(10))
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null) return null;


            var user = await _context.Users.FindAsync(userId);
            if (user == null || (quiz.CreatedById != userId && user.Role != Role.admin))
                return null;

            var attempts = quiz.Attempts.Where(a => a.Score.HasValue).ToList();

            return new QuizStatsDto
            {
                Id = quiz.Id,
                Title = quiz.Title,
                TotalAttempts = quiz.Attempts.Count,
                AverageScore = attempts.Any() ? attempts.Average(a => a.Score!.Value) : 0,
                HighestScore = attempts.Any() ? attempts.Max(a => a.Score!.Value) : null,
                LowestScore = attempts.Any() ? attempts.Min(a => a.Score!.Value) : null,
                LastAttempt = quiz.Attempts.Any() ? quiz.Attempts.Max(a => a.StartedAt) : null,
                RecentAttempts = quiz.Attempts.Take(10).Select(a => new QuizAttemptSummaryDto
                {
                    Id = a.Id,
                    UserName = a.User.FullName,
                    StartedAt = a.StartedAt,
                    CompletedAt = a.CompletedAt,
                    Score = a.Score,
                    XpEarned = a.XpEarned
                }).ToList()
            };
        }

        public async Task<IEnumerable<QuizSummaryDto>> GetQuizzesByUserAsync(Guid userId)
        {
            return await _context.Quizzes
                .Include(q => q.Subject)
                .Include(q => q.Level)
                .Include(q => q.Class)
                .Include(q => q.QuizQuestions)
                .Include(q => q.Attempts)
                .Where(q => q.CreatedById == userId)
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new QuizSummaryDto
                {
                    Id = q.Id,
                    Title = q.Title,
                    Description = q.Description,
                    SubjectName = q.Subject.Name,
                    LevelTitle = q.Level != null ? q.Level.Title : null,
                    LevelNumber = q.Level != null ? q.Level.LevelNumber : null,
                    ClassName = q.Class != null ? q.Class.Name : null,
                    IsPublished = q.IsPublished,
                    QuestionCount = q.QuizQuestions.Count,
                    AttemptCount = q.Attempts.Count,
                    CreatedAt = q.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<QuizSummaryDto>> GetPublishedQuizzesAsync(Guid? subjectId = null, Guid? levelId = null)
        {
            var query = _context.Quizzes
                .Include(q => q.Subject)
                .Include(q => q.Level)
                .Include(q => q.Class)
                .Include(q => q.QuizQuestions)
                .Include(q => q.Attempts)
                .Where(q => q.IsPublished);

            if (subjectId.HasValue)
                query = query.Where(q => q.SubjectId == subjectId.Value);

            if (levelId.HasValue)
                query = query.Where(q => q.LevelId == levelId.Value);

            return await query
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new QuizSummaryDto
                {
                    Id = q.Id,
                    Title = q.Title,
                    Description = q.Description,
                    SubjectName = q.Subject.Name,
                    LevelTitle = q.Level != null ? q.Level.Title : null,
                    LevelNumber = q.Level != null ? q.Level.LevelNumber : null,
                    ClassName = q.Class != null ? q.Class.Name : null,
                    IsPublished = q.IsPublished,
                    QuestionCount = q.QuizQuestions.Count,
                    AttemptCount = q.Attempts.Count,
                    CreatedAt = q.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<bool> QuizExistsAsync(Guid id)
        {
            return await _context.Quizzes.AnyAsync(q => q.Id == id);
        }

        public async Task<bool> CanUserAccessQuizAsync(Guid quizId, Guid userId)
        {
            var quiz = await _context.Quizzes.FindAsync(quizId);
            if (quiz == null) return false;


            if (quiz.IsPublished) return true;


            if (quiz.CreatedById == userId) return true;


            if (quiz.ClassId.HasValue)
            {
                var isMember = await _context.ClassMemberships
                    .AnyAsync(cm => cm.ClassId == quiz.ClassId.Value && cm.UserId == userId);
                return isMember;
            }

            return false;
        }
    }
}