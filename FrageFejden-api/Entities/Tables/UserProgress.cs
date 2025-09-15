using System;

namespace FrageFejden.Entities
{
    public class UserProgress
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public AppUser User { get; set; } = null!;

        public Guid SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        public Guid? TopicId { get; set; }
        public Topic? Topic { get; set; }

        public Guid? LevelId { get; set; }
        public Level? Level { get; set; }

        public int Xp { get; set; }
        public DateTime? LastActivity { get; set; }

        
        public bool HasReadStudyText { get; set; } = false;

        
        public DateTime? ReadAt { get; set; }
    }
}
