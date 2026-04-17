using IndustrialProcessingSystem.services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialProcessingSystem.events
{
    public class EventLogger
    {
        private readonly string _logPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\log.txt"));
        private readonly SemaphoreSlim _fileLock = new(1, 1); // samo jedna nit pise u fajl u isto vreme

        public void Subscribe(ProcessingSystem system)
        {
            // Pretplata lambdom sa prosledjivanjem argumenata
            system.JobCompleted += async (args) =>
            {
                await WriteToFile(
                    $"[{args.CompletedAt}] [COMPLETED] {args.JobId}, {args.Result}"
                    );
            };

            system.JobFailed += async (args) =>
            {
                await WriteToFile(
                    $"[{args.FailedAt}] [FAILED] {args.JobId}, {args.Reason}"
                    );
            };
        }

        private async Task WriteToFile(string message)
        {
            await _fileLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(_logPath, message + "\n");
            }
            finally
            {
                _fileLock.Release();
            }
        }
    }
}
