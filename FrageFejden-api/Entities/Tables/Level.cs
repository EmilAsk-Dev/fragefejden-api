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

        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    }
}
