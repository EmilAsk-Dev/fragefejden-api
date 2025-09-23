using FrageFejden.DTOs.Quiz;
using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
        Task<IEnumerable<QuizSummaryDto>> GetPublishedQuizzesAsync(Guid? subjectId = null, Guid? topicId = null, Guid? levelId = null);
        Task<bool> QuizExistsAsync(Guid id);
        Task<bool> CanUserAccessQuizAsync(Guid quizId, Guid userId);
        Task<Guid> CreateAsync(Guid topicId, Guid levelId, string stem, string? explanation, List<(string Text, bool IsCorrect)> options);

    }

    public class QuizService : IQuizService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public QuizService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<IEnumerable<QuizSummaryDto>> GetQuizzesAsync(QuizFilterDto? filter = null)
        {
            var query = _context.Quizzes
                .Include(q => q.Topic).ThenInclude(t => t.Subject)
                .Include(q => q.Level)
                .Include(q => q.Class)
                .Include(q => q.QuizQuestions)
                .Include(q => q.Attempts)
                .AsQueryable();

            if (filter != null)
            {
                if (filter.SubjectId.HasValue)
                    query = query.Where(q => q.Topic.SubjectId == filter.SubjectId.Value);
                if (filter.LevelId.HasValue)
                    query = query.Where(q => q.LevelId == filter.LevelId.Value);
                if (filter.ClassId.HasValue)
                    query = query.Where(q => q.ClassId == filter.ClassId.Value);
                if (filter.IsPublished.HasValue)
                    query = query.Where(q => q.IsPublished == filter.IsPublished.Value);
                if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
                {
                    var s = filter.SearchTerm.Trim().ToLower();
                    query = query.Where(q => q.Title.ToLower().Contains(s) || (q.Description ?? "").ToLower().Contains(s));
                }
                var skip = Math.Max(0, (filter.PageNumber - 1) * filter.PageSize);
                var take = Math.Max(1, filter.PageSize);
                query = query.Skip(skip).Take(take);
            }

            return await query
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new QuizSummaryDto
                {
                    Id = q.Id,
                    Title = q.Title,
                    Description = q.Description,
                    SubjectName = q.Topic.Subject.Name,
                    LevelTitle = q.Level != null ? q.Level.Title : null,
                    LevelNumber = q.Level != null ? (int?)q.Level.LevelNumber : null,
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
                .Include(q => q.Topic).ThenInclude(t => t.Subject)
                .Include(q => q.Level)
                .Include(q => q.Class)
                .Include(q => q.CreatedBy)
                .Include(q => q.QuizQuestions)
                .Include(q => q.Attempts)
                .Where(q => q.Id == id)
                .Select(q => new QuizDto
                {
                    Id = q.Id,
                    SubjectId = q.Topic.SubjectId,
                    SubjectName = q.Topic.Subject.Name,
                    LevelId = q.LevelId,
                    LevelTitle = q.Level != null ? q.Level.Title : null,
                    LevelNumber = q.Level != null ? (int?)q.Level.LevelNumber : null,
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
                    AverageScore = q.Attempts.Any(a => a.Score.HasValue)
                        ? q.Attempts.Where(a => a.Score.HasValue).Average(a => (double)a.Score!.Value)
                        : (double?)null
                })
                .FirstOrDefaultAsync();
        }

        public async Task<QuizWithQuestionsDto?> GetQuizWithQuestionsAsync(Guid id, bool includeCorrectAnswers = false)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Topic).ThenInclude(t => t.Subject)
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
                SubjectId = quiz.Topic != null ? quiz.Topic.SubjectId : quiz.SubjectId,
                SubjectName = quiz.Topic?.Subject?.Name ?? quiz.Subject?.Name ?? "(okänt ämne)",
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
                AverageScore = quiz.Attempts.Where(a => a.Score.HasValue).Select(a => (double)a.Score!.Value).DefaultIfEmpty().Average(),
                Questions = quiz.QuizQuestions.Select(qq => new QuizQuestionDto
                {
                    Id = qq.Id,
                    QuestionId = qq.QuestionId,
                    SortOrder = qq.SortOrder,
                    QuestionStem = qq.Question.Stem,
                    QuestionType = qq.Question.Type.ToString(),
                    Difficulty = qq.Question.Difficulty.ToString(),
                    Options = qq.Question.Options
                    .OrderBy(o => o.SortOrder)
                    .Select(o => new QuestionOptionDto
                    {
                        Id = o.Id,
                        OptionText = o.OptionText,
                        SortOrder = o.SortOrder
                    })
                    .ToList(),                                
                                CorrectOptionId = includeCorrectAnswers
                    ? qq.Question.Options.Where(o => o.IsCorrect).Select(o => (Guid?)o.Id).FirstOrDefault()
                    : null
                }).ToList()

            };
        }

        public async Task<QuizDto> CreateQuizAsync(CreateQuizDto createDto, Guid createdById)
        {
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var topic = await _context.Topics.Include(t => t.Subject).FirstOrDefaultAsync(t => t.Id == createDto.TopicId);
                if (topic == null) throw new ArgumentException("Topic not found.");

                if (createDto.SubjectId != Guid.Empty && createDto.SubjectId != topic.SubjectId)
                    throw new ArgumentException("SubjectId does not match the Topic's Subject.");

                var quiz = new Quiz
                {
                    Id = Guid.NewGuid(),
                    TopicId = topic.Id,
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

                if (createDto.QuestionIds != null && createDto.QuestionIds.Any())
                {
                    var qq = createDto.QuestionIds.Select((questionId, index) => new QuizQuestion
                    {
                        Id = Guid.NewGuid(),
                        QuizId = quiz.Id,
                        QuestionId = questionId,
                        SortOrder = index + 1
                    }).ToList();

                    _context.QuizQuestions.AddRange(qq);
                    await _context.SaveChangesAsync();
                }

                await tx.CommitAsync();
                var dto = await GetQuizByIdAsync(quiz.Id);
                return dto ?? throw new InvalidOperationException("Failed to retrieve created quiz");
            }
            catch
            {
                await tx.RollbackAsync();
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

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.QuizQuestions.RemoveRange(quiz.QuizQuestions);

                var newSet = updateDto.Questions.Select(q => new QuizQuestion
                {
                    Id = Guid.NewGuid(),
                    QuizId = id,
                    QuestionId = q.QuestionId,
                    SortOrder = q.SortOrder
                }).ToList();

                _context.QuizQuestions.AddRange(newSet);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();
                return true;
            }
            catch
            {
                await tx.RollbackAsync();
                return false;
            }
        }

        public async Task<QuizStatsDto?> GetQuizStatsAsync(Guid id, Guid userId)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Attempts.Where(a => a.CompletedAt != null))
                    .ThenInclude(a => a.User)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quiz == null) return null;

            var user = await _context.Users.FindAsync(userId);
            if (user == null || (quiz.CreatedById != userId && user.Role != Role.admin))
                return null;

            var completedAttempts = quiz.Attempts.Where(a => a.Score.HasValue).ToList();
            var hasAny = completedAttempts.Any();

            var avg = hasAny ? completedAttempts.Average(a => (double)a.Score!.Value) : 0d;
            double? hi = hasAny ? completedAttempts.Max(a => (double?)a.Score) : null;
            double? lo = hasAny ? completedAttempts.Min(a => (double?)a.Score) : null;

            var passedAttempts = completedAttempts.Count(a => a.Score >= 70);
            var passRate = hasAny ? (double)passedAttempts / completedAttempts.Count * 100 : 0;

            return new QuizStatsDto
            {
                Id = quiz.Id,
                Title = quiz.Title,
                TotalAttempts = completedAttempts.Count,
                AverageScore = avg,
                HighestScore = hi,
                LowestScore = lo,
                PassRate = passRate,
                LastAttempt = completedAttempts.Any() ? completedAttempts.Max(a => a.StartedAt) : (DateTime?)null,
                RecentAttempts = completedAttempts
                    .OrderByDescending(a => a.StartedAt)
                    .Take(10)
                    .Select(a => new QuizAttemptSummaryDto
                    {
                        Id = a.Id,
                        UserName = a.User.FullName,
                        StartedAt = a.StartedAt,
                        CompletedAt = a.CompletedAt,
                        Score = a.Score,
                        XpEarned = a.XpEarned
                    })
                    .ToList()
            };
        }

        public async Task<IEnumerable<QuizSummaryDto>> GetQuizzesByUserAsync(Guid userId)
        {
            return await _context.Quizzes
                .Include(q => q.Topic).ThenInclude(t => t.Subject)
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
                    SubjectName = q.Topic.Subject.Name,
                    LevelTitle = q.Level != null ? q.Level.Title : null,
                    LevelNumber = q.Level != null ? (int?)q.Level.LevelNumber : null,
                    ClassName = q.Class != null ? q.Class.Name : null,
                    IsPublished = q.IsPublished,
                    QuestionCount = q.QuizQuestions.Count,
                    AttemptCount = q.Attempts.Count,
                    CreatedAt = q.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<QuizSummaryDto>> GetPublishedQuizzesAsync(Guid? subjectId = null, Guid? topicId = null, Guid? levelId = null)
        {
            var query = _context.Quizzes
                .AsNoTracking()
                .Include(q => q.Topic).ThenInclude(t => t.Subject)
                .Include(q => q.Level)
                .Include(q => q.Class)
                .Include(q => q.QuizQuestions)
                .Include(q => q.Attempts)
                .Where(q => q.IsPublished);

            if (subjectId.HasValue) query = query.Where(q => q.Topic.SubjectId == subjectId.Value);
            if (topicId.HasValue) query = query.Where(q => q.TopicId == topicId.Value);
            if (levelId.HasValue) query = query.Where(q => q.LevelId == levelId.Value);

            return await query
                .OrderByDescending(q => q.CreatedAt)
                .Select(q => new QuizSummaryDto
                {
                    Id = q.Id,
                    Title = q.Title,
                    Description = q.Description,
                    SubjectName = q.Topic.Subject.Name,
                    LevelTitle = q.Level != null ? q.Level.Title : null,
                    LevelNumber = q.Level != null ? (int?)q.Level.LevelNumber : null,
                    ClassName = q.Class != null ? q.Class.Name : null,
                    IsPublished = q.IsPublished,
                    QuestionCount = q.QuizQuestions.Count,
                    AttemptCount = q.Attempts.Count,
                    CreatedAt = q.CreatedAt
                })
                .ToListAsync();
        }

        public Task<bool> QuizExistsAsync(Guid id) => _context.Quizzes.AnyAsync(q => q.Id == id);

        public async Task<bool> CanUserAccessQuizAsync(Guid quizId, Guid userId)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Level)
                .FirstOrDefaultAsync(q => q.Id == quizId);

            if (quiz == null) return false;

            
            if (quiz.IsPublished) return true;

            
            if (quiz.CreatedById == userId) return true;

            
            if (quiz.ClassId.HasValue)
            {
                var isMember = await _context.ClassMemberships
                    .AnyAsync(cm => cm.ClassId == quiz.ClassId.Value && cm.UserId == userId);
                if (isMember) return true;
            }

            
            if (quiz.LevelId.HasValue)
            {
                
                var progress = await _context.UserProgresses
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.LevelId == quiz.LevelId);

                
                if (progress == null)
                {
                    var level = await _context.Levels.FirstOrDefaultAsync(l => l.Id == quiz.LevelId.Value);
                    if (level?.LevelNumber == 1) return true;

                    
                    var previousLevelsCompleted = await _context.UserProgresses
                        .Where(p => p.UserId == userId &&
                                   p.TopicId == level!.TopicId &&
                                   p.Level!.LevelNumber < level.LevelNumber &&
                                   p.IsLevelCompleted)
                        .CountAsync();

                    var totalPreviousLevels = await _context.Levels
                        .Where(l => l.TopicId == level!.TopicId && l.LevelNumber < level.LevelNumber)
                        .CountAsync();

                    return previousLevelsCompleted >= totalPreviousLevels;
                }

                
                if (progress.IsLevelCompleted && !progress.CanRetry)
                    return false;

                return true;
            }

            return false;
        }

        public async Task<Guid> CreateAsync(
    Guid topicId,
    Guid levelId,
    string stem,
    string? explanation,
    List<(string Text, bool IsCorrect)> options)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(stem))
                throw new ArgumentException("Question stem is required.", nameof(stem));
            if (options is null || options.Count < 2)
                throw new ArgumentException("A question needs at least two options.", nameof(options));
            if (options.Any(o => string.IsNullOrWhiteSpace(o.Text)))
                throw new ArgumentException("All options must have non-empty text.", nameof(options));
            var correctCount = options.Count(o => o.IsCorrect);
            if (correctCount != 1)
                throw new ArgumentException("Exactly one option must be marked as correct.", nameof(options));

            // Get SubjectId from Topic (lightweight query - no tracking needed)
            var topicInfo = await _context.Topics
                .AsNoTracking() // Safe since we're only reading
                .Where(t => t.Id == topicId)
                .Select(t => new { t.SubjectId, t.Id })
                .FirstOrDefaultAsync();

            if (topicInfo == null)
                throw new ArgumentException("Topic not found.", nameof(topicId));

            // Validate level exists and belongs to topic
            var levelExists = await _context.Levels
                .AsNoTracking()
                .AnyAsync(l => l.Id == levelId && l.TopicId == topicId);

            if (!levelExists)
                throw new ArgumentException("Level not found or doesn't belong to the specified topic.");

            // Get current user
            var userIdStr = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? throw new InvalidOperationException("No signed-in user.");

            if (!Guid.TryParse(userIdStr, out var userIdGuid))
                throw new InvalidOperationException("User id claim is not a valid GUID.");

            var userExists = await _context.Users.AsNoTracking().AnyAsync(u => u.Id == userIdGuid);
            if (!userExists)
                throw new InvalidOperationException("Current user not found in AspNetUsers.");

            // Generate new IDs to ensure they're truly new
            var questionId = Guid.NewGuid();
            var optionIds = options.Select(_ => Guid.NewGuid()).ToList();

            // Create question entity with fresh ID
            var question = new Question
            {
                Id = questionId, // Use the generated ID
                SubjectId = topicInfo.SubjectId,
                TopicId = topicId,
                Type = QuestionType.multi_select,
                Difficulty = Difficulty.medium,
                Stem = stem.Trim(),
                Explanation = string.IsNullOrWhiteSpace(explanation) ? null : explanation.Trim(),
                SourceAi = false,
                CreatedById = userIdGuid,
                CreatedAt = DateTime.UtcNow // Use UtcNow consistently
            };

            // Create option entities with fresh IDs
            var questionOptions = options
                .Select((opt, idx) => new QuestionOption
                {
                    Id = optionIds[idx], // Use the generated IDs
                    QuestionId = questionId,
                    OptionText = opt.Text.Trim(),
                    IsCorrect = opt.IsCorrect,
                    SortOrder = idx + 1
                })
                .ToList();

            try
            {
                // Add entities to context (EF will treat them as new since they have new IDs)
                _context.Questions.Add(question);
                _context.QuestionOptions.AddRange(questionOptions);

                await _context.SaveChangesAsync();
                return questionId;
            }
            catch (DbUpdateException ex)
            {
                // Handle specific database errors
                if (ex.InnerException is SqlException sqlEx && sqlEx.Number == 547)
                {
                    if (sqlEx.Message.Contains("FK_Questions_Subjects_SubjectId"))
                        throw new InvalidOperationException($"The subject with ID {topicInfo.SubjectId} does not exist in the database.");
                    if (sqlEx.Message.Contains("FK_Questions_Topics_TopicId"))
                        throw new InvalidOperationException($"The topic with ID {topicId} does not exist in the database.");
                    if (sqlEx.Message.Contains("FK_Questions_CreatedBy"))
                        throw new InvalidOperationException($"The user with ID {userIdGuid} does not exist in the database.");
                }

                throw new InvalidOperationException($"Database error occurred: {ex.Message}", ex);
            }
        }
    }
}


