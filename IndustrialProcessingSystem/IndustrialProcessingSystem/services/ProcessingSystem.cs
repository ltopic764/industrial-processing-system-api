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
        private readonly SemaphoreSlim _semaphore;
        private readonly List<Thread> _workers = new();
        private readonly CancellationTokenSource _cts = new();

        public ProcessingSystem(SystemConfig config)
        {
            _maxQueueSize = config.MaxQueueSize;
            _semaphore = new SemaphoreSlim(0, _maxQueueSize);

            for (int i = 0; i < config.WorkerCount; i++)
            {
                Thread worker = new Thread(WorkerLoop)
                {
                    IsBackground = true, // nit je pozadinska, ugasice se kada se i program ugasi
                    Name = $"Worker-{i}"
                };
                _workers.Add(worker);
                worker.Start();
            }

            // Ucitaj poslove iz XML
            foreach(Job job in config.Jobs)
            {
                Submit(job);
            }

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

                // Obavesti nit da ima posla
                _semaphore.Release();

                return handle;
            }
        }

        public void WorkerLoop()
        {
            while (_cts.Token.IsCancellationRequested)
            {
                try
                {
                    // Cekamo dok ne bude posla
                    _semaphore.Wait(_cts.Token);

                    Job? job = null;

                    lock(_lock)
                    {
                        if (_queue.Count > 0)
                        {
                            job = _queue.Dequeue();
                        }
                    }

                    if (job != null)
                    {
                        // Obrada posla...
                        Console.WriteLine($"Worker {Thread.CurrentThread.Name} obradjuje job {job.Id}");

                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greska u niti: {ex.Message}");
                }
            }
        }

        public void Shutdown()
        {
            _cts.Cancel();
        }
    }
}
