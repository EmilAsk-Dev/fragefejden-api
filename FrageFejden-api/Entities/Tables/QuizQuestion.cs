using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class QuizQuestion
    {
        public Guid Id { get; set; }
        public Guid QuizId { get; set; }
        public Guid QuestionId { get; set; }
        public int SortOrder { get; set; }

        public Quiz Quiz { get; set; } = null!;
        public Question Question { get; set; } = null!;
    }
}
