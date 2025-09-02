using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class Quiz
    {
        public Guid Id { get; set; }

        public Guid SubjectId { get; set; }
        public Guid? LevelId { get; set; }
        public Guid? ClassId { get; set; }

        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsPublished { get; set; } = false;

        public Guid? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; }

        public Subject Subject { get; set; } = null!;
        public Level? Level { get; set; }
        public Class? Class { get; set; }

        public AppUser? CreatedBy { get; set; }  // Made nullable to match CreatedById
        public ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
        public ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();
    }
}