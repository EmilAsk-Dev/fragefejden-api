using System;
using System.Collections.Generic;

namespace FrageFejden.Entities
{
    public class Quiz
    {
        public Guid Id { get; set; }

        public Guid SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        public Guid? TopicId { get; set; }
        public Topic? Topic { get; set; }

        public Guid? LevelId { get; set; }
        public Level? Level { get; set; }

        public Guid? ClassId { get; set; }
        public Class? Class { get; set; }

        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsPublished { get; set; } = false;

        public Guid? CreatedById { get; set; }
        public AppUser? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
        public ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();
    }
}
