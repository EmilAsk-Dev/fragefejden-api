using FrageFejden.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
