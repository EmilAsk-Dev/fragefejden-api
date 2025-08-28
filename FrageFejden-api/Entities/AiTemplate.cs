using FrageFejden.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class AiTemplate
    {
        public long Id { get; set; }
        public long SubjectId { get; set; }
        public long? TopicId { get; set; }
        public string Prompt { get; set; } = null!;
        public Difficulty? DifficultyMin { get; set; }
        public Difficulty? DifficultyMax { get; set; }
        public Guid? CreatedById { get; set; }   // Changed from long to Guid
        public DateTime CreatedAt { get; set; }

        public Subject Subject { get; set; } = null!;
        public Topic? Topic { get; set; }
        public AppUser? CreatedBy { get; set; }
        public ICollection<AiGeneration> Generations { get; set; } = new List<AiGeneration>();
    }
}
