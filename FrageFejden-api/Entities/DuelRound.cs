using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class DuelRound
    {
        public long Id { get; set; }
        public long DuelId { get; set; }
        public int RoundNumber { get; set; }
        public long QuestionId { get; set; }
        public int TimeLimitSeconds { get; set; } = 30;

        public Duel Duel { get; set; } = null!;
        public Question Question { get; set; } = null!;
    }
}
