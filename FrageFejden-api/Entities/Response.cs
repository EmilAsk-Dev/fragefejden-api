using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class Response
    {
        public long Id { get; set; }
        public long AttemptId { get; set; }
        public long QuestionId { get; set; }
        public long? SelectedOptionId { get; set; }
        public string? FreeText { get; set; }
        public bool IsCorrect { get; set; }
        public int? TimeMs { get; set; }

        public Attempt Attempt { get; set; } = null!;
        public Question Question { get; set; } = null!;
        public QuestionOption? SelectedOption { get; set; }
    }
}
