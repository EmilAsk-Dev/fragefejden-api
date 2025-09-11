using FrageFejden.Entities.Enums;
using System.ComponentModel.DataAnnotations;

namespace FrageFejden.DTOs.Duel
{
    public class CreateDuelRequest
    {
        [Required]
        public Guid SubjectId { get; set; }

        public Guid? LevelId { get; set; }

        [Range(3, 21)]
        public int BestOf { get; set; } = 5;
    }

    public class InviteToDuelRequest
    {
        [Required]
        public Guid DuelId { get; set; }

        [Required]
        public Guid InviteeId { get; set; }
    }

    public class DuelActionRequest
    {
        [Required]
        public Guid DuelId { get; set; }
    }

    public class SubmitDuelAnswerRequest
    {
        [Required]
        public Guid DuelId { get; set; }

        [Required]
        public Guid QuestionId { get; set; }

        public Guid? SelectedOptionId { get; set; }

        [Range(0, int.MaxValue)]
        public int TimeMs { get; set; }
    }

    public class DuelDto
    {
        public Guid Id { get; set; }
        public SubjectDto Subject { get; set; } = null!;
        public LevelDto? Level { get; set; }
        public DuelStatus Status { get; set; }
        public int BestOf { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public List<DuelParticipantDto> Participants { get; set; } = new();
        public List<DuelRoundDto> Rounds { get; set; } = new();
        public DuelRoundDto? CurrentRound { get; set; }
    }

    public class DuelParticipantDto
    {
        public Guid Id { get; set; }
        public UserDto User { get; set; } = null!;
        public UserDto? InvitedBy { get; set; }
        public int Score { get; set; }
        public DuelResult? Result { get; set; }
        public bool IsCurrentUser { get; set; }
    }

    public class DuelRoundDto
    {
        public Guid Id { get; set; }
        public int RoundNumber { get; set; }
        public QuestionDto Question { get; set; } = null!;
        public int TimeLimitSeconds { get; set; }
    }

    public class DuelInvitationDto
    {
        public Guid Id { get; set; }
        public SubjectDto Subject { get; set; } = null!;
        public LevelDto? Level { get; set; }
        public UserDto InvitedBy { get; set; } = null!;
        public int BestOf { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ClassmateDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public bool IsAvailable { get; set; } = true;
    }

    public class DuelStatsDto
    {
        public int TotalDuels { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Draws { get; set; }
        public double WinRate { get; set; }
        public int CurrentStreak { get; set; }
        public int BestStreak { get; set; }
    }

    // Supporting DTOs (you might already have these)
    public class SubjectDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class LevelDto
    {
        public Guid Id { get; set; }
        public int LevelNumber { get; set; }
        public string? Title { get; set; }
        public int MinXpUnlock { get; set; }
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = null!;
        public string? AvatarUrl { get; set; }
    }

    public class QuestionDto
    {
        public Guid Id { get; set; }
        public QuestionType Type { get; set; }
        public Difficulty Difficulty { get; set; }
        public string Stem { get; set; } = null!;
        public string? Explanation { get; set; }
        public List<QuestionOptionDto> Options { get; set; } = new();
    }

    public class QuestionOptionDto
    {
        public Guid Id { get; set; }
        public string OptionText { get; set; } = null!;
        public int SortOrder { get; set; }
    }
}