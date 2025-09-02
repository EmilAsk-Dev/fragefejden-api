using FrageFejden.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public DateTime CreatedAt { get; set; }

        public Subject Subject { get; set; } = null!;
        public Topic? Topic { get; set; }
        public AppUser? CreatedBy { get; set; }
        public AppUser? ApprovedBy { get; set; }
        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
        public ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
    }
}
