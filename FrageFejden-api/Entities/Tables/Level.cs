using System;
using System.Collections.Generic;

namespace FrageFejden.Entities
{
    public class Level
    {
        public Guid Id { get; set; }

        public Guid TopicId { get; set; }
        public Topic Topic { get; set; } = null!;

        public int LevelNumber { get; set; }
        public string? Title { get; set; }
        public int MinXpUnlock { get; set; } = 0;

        // NEW: Text that must be studied
        public string? StudyText { get; set; }

        // NEW: Flag to track if user has studied this level
        public ICollection<UserProgress> UserProgress { get; set; } = new List<UserProgress>();

        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    }
}
