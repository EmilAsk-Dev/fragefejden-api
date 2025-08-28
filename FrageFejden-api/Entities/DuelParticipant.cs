using FrageFejden.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class DuelParticipant
    {
        public long Id { get; set; }
        public long DuelId { get; set; }
        public Guid UserId { get; set; }       
        public Guid? InvitedById { get; set; }  
        public int Score { get; set; }
        public DuelResult? Result { get; set; }

        public Duel Duel { get; set; } = null!;
        public AppUser User { get; set; } = null!;
        public AppUser? InvitedBy { get; set; }
    }
}
