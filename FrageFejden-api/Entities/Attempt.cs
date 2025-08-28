using Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class Attempt
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }     
        public long QuizId { get; set; }
        public long? LevelIdAtTime { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? Score { get; set; }
        public int XpEarned { get; set; }

        public AppUser User { get; set; } = null!;
        public Quiz Quiz { get; set; } = null!;
        public Level? LevelAtTime { get; set; }
        public ICollection<Response> Responses { get; set; } = new List<Response>();
    }
}
