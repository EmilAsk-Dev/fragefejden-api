using FrageFejden.Entities;

namespace FrageFejden_api.Entities.Tables
{
    public class AttemptAnswer
    {
        public Guid Id { get; set; }
        public Guid AttemptId { get; set; }
        public Guid QuestionId { get; set; }
        public Guid SelectedOptionId { get; set; }
        public bool IsCorrect { get; set; }
        public int TimeMs { get; set; }
        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

      
        public Attempt Attempt { get; set; } = null!;
        public Question Question { get; set; } = null!;
        public QuestionOption SelectedOption { get; set; } = null!;
    }
}
