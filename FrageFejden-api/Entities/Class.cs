using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class Class
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public string? GradeLabel { get; set; }
        public string? JoinCode { get; set; }
        public Guid? CreatedById { get; set; }   // nullable to allow historical data if needed
        public DateTime CreatedAt { get; set; }

        public AppUser? CreatedBy { get; set; }
        public ICollection<ClassMembership> Memberships { get; set; } = new List<ClassMembership>();
        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    }
}
