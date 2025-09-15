using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FrageFejden.Entities;
using FrageFejden.Entities.Enums;
using System.Security.Claims;

namespace FrageFejden_api.Api.Statistics
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StatisticsController : ControllerBase
    {
        private readonly AppDbContext _context; 

        public StatisticsController(AppDbContext context)
        {
            _context = context;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.Parse(userIdClaim ?? throw new UnauthorizedAccessException());
        }

        // 1. Hämta alla elever i alla klasser som inloggade läraren har
        [HttpGet("students")]
        public async Task<ActionResult<List<StudentDto>>> GetAllStudentsInTeacherClasses()
        {
            var teacherId = GetCurrentUserId();

            var students = await _context.Set<ClassMembership>()
                .Where(cm => cm.Class.CreatedById == teacherId && cm.RoleInClass == Role.student)
                .Select(cm => new StudentDto
                {
                    Id = cm.UserId,
                    FullName = cm.User.FullName,
                    ClassName = cm.Class.Name,
                    ClassId = cm.ClassId,
                    ExperiencePoints = cm.User.experiencePoints,
                    EnrolledAt = cm.EnrolledAt
                })
                .OrderBy(s => s.ClassName)
                .ThenBy(s => s.FullName)
                .ToListAsync();

            return Ok(students);
        }

        // 2. Hämta alla quiz som den inloggade läraren skapat
        [HttpGet("quizzes")]
        public async Task<ActionResult<List<TeacherQuizDto>>> GetTeacherQuizzes()
        {
            var teacherId = GetCurrentUserId();

            var quizzes = await _context.Set<Quiz>()
                .Where(q => q.CreatedById == teacherId)
                .Select(q => new TeacherQuizDto
                {
                    Id = q.Id,
                    Title = q.Title,
                    Description = q.Description,
                    SubjectName = q.Subject.Name,
                    ClassName = q.Class != null ? q.Class.Name : null,
                    TopicName = q.Topic != null ? q.Topic.Name : null,
                    LevelTitle = q.Level != null ? q.Level.Title : null,
                    IsPublished = q.IsPublished,
                    CreatedAt = q.CreatedAt,
                    AttemptCount = q.Attempts.Count,
                    QuestionCount = q.QuizQuestions.Count
                })
                .OrderByDescending(q => q.CreatedAt)
                .ToListAsync();

            return Ok(quizzes);
        }

        // 3. Hämta elevers svar för att kunna räkna genomsnittligt svar för alla eleverna
        [HttpGet("student-responses")]
        public async Task<ActionResult<List<StudentResponseDto>>> GetStudentResponses()
        {
            var teacherId = GetCurrentUserId();

            var responses = await _context.Set<Response>()
                .Where(r => r.Attempt.Quiz.CreatedById == teacherId)
                .Select(r => new StudentResponseDto
                {
                    StudentId = r.Attempt.UserId,
                    StudentName = r.Attempt.User.FullName,
                    QuizId = r.Attempt.QuizId,
                    QuizTitle = r.Attempt.Quiz.Title,
                    QuestionId = r.QuestionId,
                    IsCorrect = r.IsCorrect,
                    TimeMs = r.TimeMs,
                    AttemptId = r.AttemptId,
                    CompletedAt = r.Attempt.CompletedAt
                })
                .ToListAsync();

            return Ok(responses);
        }
        [Authorize(Roles = "teacher")]
        // 4. Hämta genomsnittligt resultat för alla elever
        [HttpGet("average-scores")]
        public async Task<ActionResult<AverageScoreDto>> GetAverageScores()
        {
            var teacherId = GetCurrentUserId();

            var attempts = await _context.Set<Attempt>()
                .Where(a => a.Quiz.CreatedById == teacherId && a.CompletedAt != null)
                .ToListAsync();

            if (!attempts.Any())
            {
                return Ok(new AverageScoreDto { AverageScore = 0, TotalAttempts = 0 });
            }

            var avgScore = attempts.Where(a => a.Score.HasValue).Average(a => a.Score!.Value);
            var avgXp = attempts.Average(a => a.XpEarned);

            return Ok(new AverageScoreDto
            {
                AverageScore = Math.Round(avgScore, 2),
                AverageXpEarned = Math.Round(avgXp, 2),
                TotalAttempts = attempts.Count
            });
        }

        // 5. Hämta bästa resultatet på den bästa eleven
        [HttpGet("top-performer")]
        public async Task<ActionResult<TopPerformerDto>> GetTopPerformer()
        {
            var teacherId = GetCurrentUserId();

            var topPerformer = await _context.Set<Attempt>()
                .Where(a => a.Quiz.CreatedById == teacherId && a.CompletedAt != null && a.Score != null)
                .GroupBy(a => a.UserId)
                .Select(g => new TopPerformerDto
                {
                    StudentId = g.Key,
                    StudentName = g.First().User.FullName,
                    BestScore = g.Max(a => a.Score!.Value),
                    AverageScore = Math.Round(g.Average(a => a.Score!.Value), 2),
                    TotalXpEarned = g.Sum(a => a.XpEarned),
                    TotalAttempts = g.Count(),
                    ExperiencePoints = g.First().User.experiencePoints
                })
                .OrderByDescending(tp => tp.BestScore)
                .ThenByDescending(tp => tp.AverageScore)
                .FirstOrDefaultAsync();

            return Ok(topPerformer);
        }

        // 6. Hämta genomförda quiz samt när de genomfördes
        [HttpGet("completed-quizzes")]
        public async Task<ActionResult<List<CompletedQuizDto>>> GetCompletedQuizzes()
        {
            var teacherId = GetCurrentUserId();

            var completedQuizzes = await _context.Set<Attempt>()
                .Where(a => a.Quiz.CreatedById == teacherId && a.CompletedAt != null)
                .Select(a => new CompletedQuizDto
                {
                    AttemptId = a.Id,
                    QuizId = a.QuizId,
                    QuizTitle = a.Quiz.Title,
                    StudentId = a.UserId,
                    StudentName = a.User.FullName,
                    Score = a.Score,
                    XpEarned = a.XpEarned,
                    StartedAt = a.StartedAt,
                    CompletedAt = a.CompletedAt!.Value,
                    Duration = a.CompletedAt!.Value - a.StartedAt
                })
                .OrderByDescending(cq => cq.CompletedAt)
                .ToListAsync();

            return Ok(completedQuizzes);
        }

        // 7. Hämta tidsgenomsnitt för genomförda quiz
        [HttpGet("time-averages")]
        public async Task<ActionResult<TimeAveragesDto>> GetTimeAverages()
        {
            var teacherId = GetCurrentUserId();

            var completedAttempts = await _context.Set<Attempt>()
                .Where(a => a.Quiz.CreatedById == teacherId && a.CompletedAt != null)
                .ToListAsync();

            if (!completedAttempts.Any())
            {
                return Ok(new TimeAveragesDto());
            }

            var durations = completedAttempts
                .Select(a => (a.CompletedAt!.Value - a.StartedAt).TotalMinutes)
                .ToList();

            var responses = await _context.Set<Response>()
                .Where(r => r.Attempt.Quiz.CreatedById == teacherId && r.TimeMs != null)
                .Select(r => r.TimeMs!.Value)
                .ToListAsync();

            return Ok(new TimeAveragesDto
            {
                AverageQuizDurationMinutes = Math.Round(durations.Average(), 2),
                AverageResponseTimeSeconds = responses.Any() ? Math.Round(responses.Average() / 1000.0, 2) : 0,
                TotalCompletedQuizzes = completedAttempts.Count,
                TotalResponses = responses.Count
            });
        }

        // 8. Hämta leaderboard för alla elever
        [HttpGet("leaderboard")]
        public async Task<ActionResult<List<LeaderboardEntryDto>>> GetLeaderboard()
        {
            var teacherId = GetCurrentUserId();

            // Get all students in teacher's classes
            var studentIds = await _context.Set<ClassMembership>()
                .Where(cm => cm.Class.CreatedById == teacherId && cm.RoleInClass == Role.student)
                .Select(cm => cm.UserId)
                .ToListAsync();

            var leaderboard = await _context.Set<AppUser>()
                .Where(u => studentIds.Contains(u.Id))
                .Select(u => new LeaderboardEntryDto
                {
                    StudentId = u.Id,
                    FullName = u.FullName,
                    ExperiencePoints = u.experiencePoints,
                    TotalQuizzesCompleted = u.ClassMemberships
                        .SelectMany(cm => cm.Class.Quizzes)
                        .Where(q => q.CreatedById == teacherId)
                        .SelectMany(q => q.Attempts)
                        .Where(a => a.UserId == u.Id && a.CompletedAt != null)
                        .Count(),
                    AverageScore = u.ClassMemberships
                        .SelectMany(cm => cm.Class.Quizzes)
                        .Where(q => q.CreatedById == teacherId)
                        .SelectMany(q => q.Attempts)
                        .Where(a => a.UserId == u.Id && a.CompletedAt != null && a.Score != null)
                        .Average(a => (double?)a.Score) ?? 0,
                    TotalXpEarned = u.ClassMemberships
                        .SelectMany(cm => cm.Class.Quizzes)
                        .Where(q => q.CreatedById == teacherId)
                        .SelectMany(q => q.Attempts)
                        .Where(a => a.UserId == u.Id && a.CompletedAt != null)
                        .Sum(a => a.XpEarned)
                })
                .OrderByDescending(l => l.ExperiencePoints)
                .ThenByDescending(l => l.AverageScore)
                .ToListAsync();

            // Add ranking
            for (int i = 0; i < leaderboard.Count; i++)
            {
                leaderboard[i].Rank = i + 1;
            }

            return Ok(leaderboard);
        }

        // Bonus: Quiz-specific statistics
        [HttpGet("quiz/{quizId}/statistics")]
        public async Task<ActionResult<QuizStatisticsDto>> GetQuizStatistics(Guid quizId)
        {
            var teacherId = GetCurrentUserId();

            var quiz = await _context.Set<Quiz>()
                .Where(q => q.Id == quizId && q.CreatedById == teacherId)
                .FirstOrDefaultAsync();

            if (quiz == null)
            {
                return NotFound("Quiz not found or you don't have permission to view it.");
            }

            var attempts = await _context.Set<Attempt>()
                .Where(a => a.QuizId == quizId && a.CompletedAt != null)
                .Include(a => a.User)
                .ToListAsync();

            var responses = await _context.Set<Response>()
                .Where(r => r.Attempt.QuizId == quizId && r.Attempt.CompletedAt != null)
                .ToListAsync();

            return Ok(new QuizStatisticsDto
            {
                QuizId = quizId,
                QuizTitle = quiz.Title,
                TotalAttempts = attempts.Count,
                AverageScore = attempts.Where(a => a.Score != null).Average(a => (double?)a.Score) ?? 0,
                HighestScore = attempts.Where(a => a.Score != null).Max(a => (int?)a.Score) ?? 0,
                LowestScore = attempts.Where(a => a.Score != null).Min(a => (int?)a.Score) ?? 0,
                AverageCompletionTimeMinutes = attempts.Average(a => (a.CompletedAt!.Value - a.StartedAt).TotalMinutes),
                CorrectAnswerPercentage = responses.Any() ? (responses.Count(r => r.IsCorrect) * 100.0 / responses.Count) : 0
            });
        }
    }

    // DTOs
    public class StudentDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string ClassName { get; set; } = null!;
        public Guid ClassId { get; set; }
        public double ExperiencePoints { get; set; }
        public DateTime EnrolledAt { get; set; }
    }

    public class TeacherQuizDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string SubjectName { get; set; } = null!;
        public string? ClassName { get; set; }
        public string? TopicName { get; set; }
        public string? LevelTitle { get; set; }
        public bool IsPublished { get; set; }
        public DateTime CreatedAt { get; set; }
        public int AttemptCount { get; set; }
        public int QuestionCount { get; set; }
    }

    public class StudentResponseDto
    {
        public Guid StudentId { get; set; }
        public string StudentName { get; set; } = null!;
        public Guid QuizId { get; set; }
        public string QuizTitle { get; set; } = null!;
        public Guid QuestionId { get; set; }
        public bool IsCorrect { get; set; }
        public int? TimeMs { get; set; }
        public Guid AttemptId { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class AverageScoreDto
    {
        public double AverageScore { get; set; }
        public double AverageXpEarned { get; set; }
        public int TotalAttempts { get; set; }
    }

    public class TopPerformerDto
    {
        public Guid StudentId { get; set; }
        public string StudentName { get; set; } = null!;
        public int BestScore { get; set; }
        public double AverageScore { get; set; }
        public int TotalXpEarned { get; set; }
        public int TotalAttempts { get; set; }
        public double ExperiencePoints { get; set; }
    }

    public class CompletedQuizDto
    {
        public Guid AttemptId { get; set; }
        public Guid QuizId { get; set; }
        public string QuizTitle { get; set; } = null!;
        public Guid StudentId { get; set; }
        public string StudentName { get; set; } = null!;
        public int? Score { get; set; }
        public int XpEarned { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime CompletedAt { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class TimeAveragesDto
    {
        public double AverageQuizDurationMinutes { get; set; }
        public double AverageResponseTimeSeconds { get; set; }
        public int TotalCompletedQuizzes { get; set; }
        public int TotalResponses { get; set; }
    }

    public class LeaderboardEntryDto
    {
        public int Rank { get; set; }
        public Guid StudentId { get; set; }
        public string FullName { get; set; } = null!;
        public double ExperiencePoints { get; set; }
        public int TotalQuizzesCompleted { get; set; }
        public double AverageScore { get; set; }
        public int TotalXpEarned { get; set; }
    }

    public class QuizStatisticsDto
    {
        public Guid QuizId { get; set; }
        public string QuizTitle { get; set; } = null!;
        public int TotalAttempts { get; set; }
        public double AverageScore { get; set; }
        public int HighestScore { get; set; }
        public int LowestScore { get; set; }
        public double AverageCompletionTimeMinutes { get; set; }
        public double CorrectAnswerPercentage { get; set; }
    }
}