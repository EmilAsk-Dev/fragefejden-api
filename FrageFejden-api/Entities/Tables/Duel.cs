using FrageFejden.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class Duel
    {
        public Guid Id { get; set; }
        public Guid SubjectId { get; set; }
        public Guid? LevelId { get; set; }
        public DuelStatus Status { get; set; } = DuelStatus.pending;
        public int BestOf { get; set; } = 5;
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        public Subject Subject { get; set; } = null!;
        public Level? Level { get; set; }
        public ICollection<DuelParticipant> Participants { get; set; } = new List<DuelParticipant>();
        public ICollection<DuelRound> Rounds { get; set; } = new List<DuelRound>();
    }
}
