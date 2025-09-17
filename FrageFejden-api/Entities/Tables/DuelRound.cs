using FrageFejden_api.Entities.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class DuelRound
    {
        public Guid Id { get; set; }
        public Guid DuelId { get; set; }
        public int RoundNumber { get; set; }
        public Guid QuestionId { get; set; }
        public int TimeLimitSeconds { get; set; } = 30;

        
        public string TextSnapshot { get; set; } = null!;
        public List<string> AlternativesSnapshot { get; set; } = new(); 
        public int CorrectIndexSnapshot { get; set; }

       
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndedAt { get; set; }

        public Duel Duel { get; set; } = null!;
        public Question Question { get; set; } = null!;
        public ICollection<DuelAnswer> Answers { get; set; } = new List<DuelAnswer>();
    }
}
