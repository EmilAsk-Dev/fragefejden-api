using FrageFejden.Entities.Enums;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities;

public class AppUser : IdentityUser<Guid>
{
    
    public string FullName { get; set; } = null!;
    
    public Role Role { get; set; } = Role.student;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    public double experiencePoints { get; set; }

    public ICollection<Class> ClassesCreated { get; set; } = new List<Class>();
    public ICollection<ClassMembership> ClassMemberships { get; set; } = new List<ClassMembership>();
    public ICollection<Subject> SubjectsCreated { get; set; } = new List<Subject>();
}

