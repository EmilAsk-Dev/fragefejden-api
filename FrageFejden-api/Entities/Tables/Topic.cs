using System;
using System.Collections.Generic;

namespace FrageFejden.Entities
{
    public class Topic
    {
        public Guid Id { get; set; }
        public Guid SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;

        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int SortOrder { get; set; } = 0;

        public ICollection<Level> Levels { get; set; } = new List<Level>();
        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<AiTemplate> AiTemplates { get; set; } = new List<AiTemplate>();
        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();

        public ICollection<UserProgress> UserProgresses { get; set; } = new List<UserProgress>();

    }
}
