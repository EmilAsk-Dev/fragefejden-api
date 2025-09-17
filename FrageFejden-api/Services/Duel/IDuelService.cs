using FrageFejden.Entities;
using FrageFejden.Entities.Enums;

namespace FrageFejden.Services.Interfaces
{
    public interface IDuelService
    {
        Task<Duel> CreateDuelAsync(Guid initiatorId, Guid subjectId, Guid? levelId = null, int bestOf = 5);
        Task<DuelParticipant> InviteToQuelAsync(Guid duelId, Guid inviterId, Guid inviteeId);
        Task<bool> AcceptDuelInvitationAsync(Guid duelId, Guid userId);
        Task<bool> DeclineDuelInvitationAsync(Guid duelId, Guid userId);
        Task<bool> StartDuelAsync(Guid duelId);
        Task<DuelRound> CreateDuelRoundAsync(Guid duelId, int roundNumber);
        Task<bool> SubmitRoundAnswerAsync(Guid duelId, Guid userId, Guid questionId, Guid? selectedOptionId, int timeMs);
        Task<bool> CompleteDuelAsync(Guid duelId);
        Task<Duel?> GetDuelByIdAsync(Guid duelId);
        Task<List<Duel>> GetUserDuelsAsync(Guid userId, DuelStatus? status = null);
        Task<List<Duel>> GetPendingInvitationsAsync(Guid userId);
        Task<List<DuelParticipant>> GetClassmatesForDuelAsync(Guid userId, Guid subjectId);
        Task<bool> CanUserCreateDuelAsync(Guid userId, Guid subjectId);
        Task<DuelStats> GetUserDuelStatsAsync(Guid userId, Guid? subjectId = null);
    }

    public class DuelStats
    {
        public int TotalDuels { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public double WinRate { get; set; }
        public int CurrentStreak { get; set; }
        public int BestStreak { get; set; }
    }
}
