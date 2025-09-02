using FrageFejden.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class ClassMembership
    {
        public Guid Id { get; set; }
        public Guid ClassId { get; set; }
        public Guid UserId { get; set; }
        public Role RoleInClass { get; set; } = Role.student;
        public DateTime EnrolledAt { get; set; }

        public Class Class { get; set; } = null!;
        public AppUser User { get; set; } = null!;
    }
}
