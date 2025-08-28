using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class Quiz
    {
        public long Id { get; set; }
        public long SubjectId { get; set; }
        public long? LevelId { get; set; }      // null => general quiz
        public long? ClassId { get; set; }      // optional
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsPublished { get; set; } = false;
        public Guid? CreatedById { get; set; }  // Changed from long to Guid
        public DateTime CreatedAt { get; set; }

        public Subject Subject { get; set; } = null!;
        public Level? Level { get; set; }
        public Class? Class { get; set; }
        public AppUser? CreatedBy { get; set; }
        public ICollection<QuizQuestion> QuizQuestions { get; set; } = new List<QuizQuestion>();
        public ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();
    }
}
