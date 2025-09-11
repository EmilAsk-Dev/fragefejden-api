using System;
using FrageFejden.Entities.Enums;

namespace FrageFejden.Entities
{
    public class UnlockRule
    {
        public Guid Id { get; set; }
        public Guid SubjectId { get; set; }

        public Guid? FromLevelId { get; set; }
        public Guid ToLevelId { get; set; }

        public UnlockConditionType Condition { get; set; }
        public int Threshold { get; set; }

        public Subject Subject { get; set; } = null!;
        public Level? FromLevel { get; set; }
        public Level ToLevel { get; set; } = null!;
    }
}
