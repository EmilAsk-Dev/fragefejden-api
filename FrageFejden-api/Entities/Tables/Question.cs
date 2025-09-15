using System;
using System.Collections.Generic;
using FrageFejden.Entities.Enums;

namespace FrageFejden.Entities
{
    public class Question
    {
        public Guid Id { get; set; }
        public Guid SubjectId { get; set; }
        public Guid? TopicId { get; set; }   

        public QuestionType Type { get; set; }
        public Difficulty Difficulty { get; set; }

        public string Stem { get; set; } = null!;
        public string? Explanation { get; set; }

        public bool SourceAi { get; set; } = false;
        public Guid CreatedById { get; set; }
        public Guid? ApprovedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now.AddHours(2);

        public Subject Subject { get; set; } = null!;
        public Topic? Topic { get; set; }
        public AppUser? CreatedBy { get; set; }
        public AppUser? ApprovedBy { get; set; }

        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
        public ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
    }
}
