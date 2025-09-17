using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FrageFejden.DTOs.Quiz
{
    public class QuizDto
    {
        public Guid Id { get; set; }


        public Guid TopicId { get; set; }
        public string TopicName { get; set; } = null!;

        
        public Guid SubjectId { get; set; }
        public string SubjectName { get; set; } = null!;

        public Guid? LevelId { get; set; }
        public string? LevelTitle { get; set; }
        public int? LevelNumber { get; set; }

        public Guid? ClassId { get; set; }
        public string? ClassName { get; set; }

        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsPublished { get; set; }

        public Guid? CreatedById { get; set; }
        public string? CreatedByName { get; set; }

        public DateTime CreatedAt { get; set; }
        public int QuestionCount { get; set; }
        public int AttemptCount { get; set; }

        // average as double
        public double? AverageScore { get; set; }
    }

    public class CreateQuizDto
    {
        
        [Required]
        public Guid TopicId { get; set; }

        
        public Guid SubjectId { get; set; }

        public Guid? LevelId { get; set; }
        public Guid? ClassId { get; set; }

        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Title { get; set; } = null!;

        [StringLength(1000)]
        public string? Description { get; set; }

        public bool IsPublished { get; set; } = false;

       
        public List<Guid> QuestionIds { get; set; } = new();
    }

    public class UpdateQuizDto
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        public string Title { get; set; } = null!;

        [StringLength(1000)]
        public string? Description { get; set; }

        public bool IsPublished { get; set; }

        
        public Guid? TopicId { get; set; }

        public Guid? LevelId { get; set; }
        public Guid? ClassId { get; set; }
    }

    public class QuizSummaryDto
    {
        public Guid Id { get; set; }

        
        public Guid TopicId { get; set; }
        public string TopicName { get; set; } = null!;

        
        public string SubjectName { get; set; } = null!;

        public string Title { get; set; } = null!;
        public string? Description { get; set; }

        public string? LevelTitle { get; set; }
        public int? LevelNumber { get; set; }
        public string? ClassName { get; set; }

        public bool IsPublished { get; set; }
        public int QuestionCount { get; set; }
        public int AttemptCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class QuizWithQuestionsDto : QuizDto
    {
        public List<QuizQuestionDto> Questions { get; set; } = new();
    }

    public class QuizQuestionDto
    {
        public Guid Id { get; set; }
        public Guid QuestionId { get; set; }
        public int SortOrder { get; set; }
        public string QuestionStem { get; set; } = null!;
        public string QuestionType { get; set; } = null!;
        public string Difficulty { get; set; } = null!;
        public List<QuestionOptionDto> Options { get; set; } = new();
        public Guid? CorrectOptionId { get; set; } 
    }

    public class QuestionOptionDto
    {
        public Guid Id { get; set; }
        public string OptionText { get; set; } = null!;
        public int SortOrder { get; set; }
    }

    public class UpdateQuizQuestionsDto
    {
        [Required]
        public List<QuizQuestionOrderDto> Questions { get; set; } = new();
    }

    public class QuizQuestionOrderDto
    {
        [Required]
        public Guid QuestionId { get; set; }

        [Range(1, int.MaxValue)]
        public int SortOrder { get; set; }
    }

    public class QuizStatsDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public int TotalAttempts { get; set; }
        public double AverageScore { get; set; }
        public double? HighestScore { get; set; }
        public double? LowestScore { get; set; }
        public double PassRate { get; set; }
        public DateTime? LastAttempt { get; set; }
        public List<QuizAttemptSummaryDto> RecentAttempts { get; set; } = new();
    }

    public class QuizAttemptSummaryDto
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = "";
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? Score { get; set; }
        public int XpEarned { get; set; }
    }

    public class PublishQuizDto
    {
        [Required]
        public bool IsPublished { get; set; }
    }

    public class QuizFilterDto
    {
        public Guid? SubjectId { get; set; }
        
        public Guid? TopicId { get; set; }

        public Guid? LevelId { get; set; }
        public Guid? ClassId { get; set; }
        public bool? IsPublished { get; set; }
        public string? SearchTerm { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
