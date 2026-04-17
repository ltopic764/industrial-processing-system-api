using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialProcessingSystem.events
{
    public class JobCompletedEvent
    {
        public Guid JobId { get; set; }
        public int Result { get; set; }
        public DateTime CompletedAt { get; set; }
    }
}
