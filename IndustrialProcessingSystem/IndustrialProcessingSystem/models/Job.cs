using IndustrialProcessingSystem.enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialProcessingSystem.models
{
    public class Job
    {
        public Guid Id { get; set; }
        public JobType Type { get; set; }
        public string Payload { get; set; }
        public int Priority { get; set; }

        public Job(Guid id, JobType type, string payload, int priority)
        {
            Id = id;
            Type = type;
            Payload = payload;
            Priority = priority;
        }
    }
}
