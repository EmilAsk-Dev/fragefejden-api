using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class Topic
    {
        public long Id { get; set; }
        public long SubjectId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public Subject Subject { get; set; } = null!;
        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<AiTemplate> AiTemplates { get; set; } = new List<AiTemplate>();
    }
}
