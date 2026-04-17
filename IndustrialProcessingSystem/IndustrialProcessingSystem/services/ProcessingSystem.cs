using IndustrialProcessingSystem.config;
using IndustrialProcessingSystem.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialProcessingSystem.services
{
    public class ProcessingSystem
    {
        private readonly PriorityQueue<Job, int> _queue = new();
        private readonly HashSet<Guid> _submittedIds = new();
        private readonly int _maxQueueSize;
        private readonly object _lock = new();

        public ProcessingSystem(SystemConfig config)
        {
            _maxQueueSize = config.MaxQueueSize;
        }

        public JobHandle? Submit(Job job)
        {
            lock(_lock)
            {
                // Ne sme isti posao da se obavi vise puta
                if (_submittedIds.Contains(job.Id))
                {
                    return null;
                }

                // Red je pun, odbicemo posao
                if (_queue.Count >= _maxQueueSize)
                {
                    return null;
                }

                var handle = new JobHandle(job.Id);
                _queue.Enqueue(job, job.Priority);
                _submittedIds.Add(job.Id);
                return handle;
            }
        }
    }
}
