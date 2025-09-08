using System.ComponentModel.DataAnnotations;

namespace FrageFejden_api.Entities.Tables
{

    public class UserDailyQuestion
    {
        [Key]
        public Guid id { get; set; }
        public string UserId { get; set; } = default!;
        public DateOnly Date { get; set; }
        public int QuestionId { get; set; }
        public string? AnswerGiven { get; set; }
        public bool Answered { get; set; }
        public bool? Correct { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? AnsweredAt { get; set; }
    }

}
