using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class UserProgress
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }      
        public long SubjectId { get; set; }
        public long? LevelId { get; set; }
        public int Xp { get; set; }
        public DateTime? LastActivity { get; set; }

        public AppUser User { get; set; } = null!;
        public Subject Subject { get; set; } = null!;
        public Level? Level { get; set; }
    }
}
