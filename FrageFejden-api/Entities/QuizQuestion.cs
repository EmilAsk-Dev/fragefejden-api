using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class QuizQuestion
    {
        public long Id { get; set; }
        public long QuizId { get; set; }
        public long QuestionId { get; set; }
        public int SortOrder { get; set; }

        public Quiz Quiz { get; set; } = null!;
        public Question Question { get; set; } = null!;
    }
}
