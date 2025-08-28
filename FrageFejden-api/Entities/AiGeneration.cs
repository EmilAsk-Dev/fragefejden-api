using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrageFejden.Entities
{
    public class AiGeneration
    {
        public long Id { get; set; }
        public long? TemplateId { get; set; }
        public long QuestionId { get; set; }
        public string? ModelName { get; set; }
        public string? ModelVersion { get; set; }
        public string? Metadata { get; set; }    // JSON
        public DateTime GeneratedAt { get; set; }

        public AiTemplate? Template { get; set; }
        public Question Question { get; set; } = null!;
    }

}
