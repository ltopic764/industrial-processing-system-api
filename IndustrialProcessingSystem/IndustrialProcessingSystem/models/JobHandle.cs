using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialProcessingSystem.models
{
    public class JobHandle
    {
        public Guid Id { get; set; }
        public Task<int> Result => _tsc.Task;

        private readonly TaskCompletionSource<int> _tsc = new();

        public JobHandle(Guid id) { Id = id; }

        public void Complete(int result) => _tsc.SetResult(result);
        public void Fail(Exception ex) => _tsc.SetException(ex);
    }
}
