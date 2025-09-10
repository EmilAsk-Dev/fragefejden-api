namespace FrageFejden_api.Api
{
    public class TopicLevelStatusDto
    {
        public Guid LevelId { get; set; }
        public int LevelNumber { get; set; }
        public string? Title { get; set; }
        public int MinXpUnlock { get; set; }
        public bool IsUnlocked { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class TopicProgressDto
    {
        public Guid SubjectId { get; set; }
        public string SubjectName { get; set; } = null!;
        public Guid TopicId { get; set; }
        public string TopicName { get; set; } = null!;
        public int TotalLevels { get; set; }
        public int CompletedLevels { get; set; }
        public int UserXp { get; set; }
        public List<TopicLevelStatusDto> Levels { get; set; } = new();
    }

    public class TopicSummaryDto
    {
        public Guid TopicId { get; set; }
        public Guid SubjectId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public int LevelCount { get; set; }
    }
}
