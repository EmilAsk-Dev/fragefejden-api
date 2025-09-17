using System;

namespace FrageFejden.Entities
{
    public class UserProgress
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid SubjectId { get; set; }
        public Guid? TopicId { get; set; }
        public Guid? LevelId { get; set; }

        public int Xp { get; set; }
        public DateTime? LastActivity { get; set; }

      
        public bool HasReadStudyText { get; set; } = false;
        public DateTime? ReadAt { get; set; }

        
        public bool IsLevelCompleted { get; set; } = false;
        public DateTime? CompletedAt { get; set; }
        public int? BestScore { get; set; }

       
        public bool CanRetry { get; set; } = false;
        public int RetryCount { get; set; } = 0;
        public DateTime? LastRetryAt { get; set; }

      
        public AppUser User { get; set; } = null!;
        public Subject Subject { get; set; } = null!;
        public Topic? Topic { get; set; }
        public Level? Level { get; set; }
    }
}
