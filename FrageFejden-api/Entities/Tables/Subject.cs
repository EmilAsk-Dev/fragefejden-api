using System;
using System.Collections.Generic;

namespace FrageFejden.Entities
{
    public class Subject
    {
        public Guid Id { get; set; }

        
        public Guid? ClassId { get; set; }
        public Class? Class { get; set; }

        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public Guid CreatedById { get; set; }
        public DateTime CreatedAt { get; set; }
        public string IconUrl { get; set; } = null!;

        public AppUser? CreatedBy { get; set; }
        public ICollection<Topic> Topics { get; set; } = new List<Topic>();
        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<AiTemplate> AiTemplates { get; set; } = new List<AiTemplate>();
        public ICollection<Duel> Duels { get; set; } = new List<Duel>();
        public ICollection<UnlockRule> UnlockRules { get; set; } = new List<UnlockRule>();
        public ICollection<UserProgress> UserProgresses { get; set; } = new List<UserProgress>();
    }
}
