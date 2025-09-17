using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class AiGeneration
    {
        public Guid Id { get; set; }
        public Guid? TemplateId { get; set; }
        public Guid QuestionId { get; set; }
        public string? ModelName { get; set; }
        public string? ModelVersion { get; set; }
        public string? Metadata { get; set; }    
        public DateTime GeneratedAt { get; set; }

        public AiTemplate? Template { get; set; }
        public Question Question { get; set; } = null!;
    }

}
