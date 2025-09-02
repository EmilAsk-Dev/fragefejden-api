using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FrageFejden.DTOs.Subject
{
    public class SubjectDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public Guid CreatedById { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TopicsCount { get; set; }
        public int LevelsCount { get; set; }
        public int QuizzesCount { get; set; }
        public int QuestionsCount { get; set; }
    }

    public class CreateSubjectDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        [StringLength(500)]
        public string? Description { get; set; }
    }

    public class UpdateSubjectDto
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        [StringLength(500)]
        public string? Description { get; set; }
    }

    public class SubjectSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int TopicsCount { get; set; }
        public int LevelsCount { get; set; }
        public int QuizzesCount { get; set; }
        public int QuestionsCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SubjectWithDetailsDto : SubjectDto
    {
        public List<TopicSummaryDto> Topics { get; set; } = new List<TopicSummaryDto>();
        public List<LevelSummaryDto> Levels { get; set; } = new List<LevelSummaryDto>();
    }

    public class TopicSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int QuestionsCount { get; set; }
    }

    public class LevelSummaryDto
    {
        public Guid Id { get; set; }
        public int LevelNumber { get; set; }
        public string? Title { get; set; }
        public int MinXpUnlock { get; set; }
        public int QuizzesCount { get; set; }
    }
}