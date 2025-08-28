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
        public long Id { get; set; }
        public long SubjectId { get; set; }
        public long? FromLevelId { get; set; }
        public long ToLevelId { get; set; }
        public UnlockConditionType Condition { get; set; }
        public int Threshold { get; set; }

        public Subject Subject { get; set; } = null!;
        public Level? FromLevel { get; set; }
        public Level ToLevel { get; set; } = null!;
    }
}
