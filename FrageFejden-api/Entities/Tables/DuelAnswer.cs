using FrageFejden.Entities;

namespace FrageFejden_api.Entities.Tables
{
    public class DuelAnswer
    {
        public Guid Id { get; set; }
        public Guid DuelRoundId { get; set; }
        public Guid UserId { get; set; }

        public int SelectedIndex { get; set; }   
        public bool IsCorrect { get; set; }
        public int TimeMs { get; set; }
        public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

        public DuelRound Round { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }

}
