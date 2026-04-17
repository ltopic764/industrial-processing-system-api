using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialProcessingSystem.events
{
    public class JobFailedEvent
    {
        public Guid JobId { get; set; }
        public string Reason { get; set; }
        public DateTime FailedAt { get; set; }
    }
}
