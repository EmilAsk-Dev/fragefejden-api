using Azure;
using FrageFejden_api.Entities.Tables;



namespace FrageFejden.Entities
{
    public class Attempt
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid QuizId { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? Score { get; set; }
        public int XpEarned { get; set; }


        public AppUser User { get; set; } = null!;
        public Quiz Quiz { get; set; } = null!;
        public ICollection<AttemptAnswer> Answers { get; set; } = new List<AttemptAnswer>();
    }
}
