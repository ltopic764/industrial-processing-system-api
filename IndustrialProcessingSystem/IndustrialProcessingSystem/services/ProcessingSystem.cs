using IndustrialProcessingSystem.config;
using IndustrialProcessingSystem.enums;
using IndustrialProcessingSystem.events;
using IndustrialProcessingSystem.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialProcessingSystem.services
{

    public delegate void JobCompletedHandler(JobCompletedEvent args);
    public delegate void JobFailedHandler(JobFailedEvent args);

    public class ProcessingSystem
    {
        public event JobCompletedHandler JobCompleted;
        public event JobFailedHandler JobFailed;
        private readonly PriorityQueue<Job, int> _queue = new();
        private readonly HashSet<Guid> _submittedIds = new();
        private readonly int _maxQueueSize;
        private readonly object _lock = new();
        private readonly SemaphoreSlim _semaphore;
        private readonly List<Thread> _workers = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<Guid, JobHandle> _handles = new();


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

                JobHandle handle = new JobHandle(job.Id);
                _queue.Enqueue(job, job.Priority);
                _submittedIds.Add(job.Id);

                _handles[job.Id] = handle;

                // Obavesti nit da ima posla
                _semaphore.Release();

                return handle;
            }
        }

        // Ucitaj poslove iz xml
        public void LoadInitialJobs(IEnumerable<Job> jobs)
        {
            foreach (Job job in jobs)
            {
                Submit(job);
            }
        }

        public void WorkerLoop()
        {
            Console.WriteLine($"{Thread.CurrentThread.Name} pokrenut!");
            while (!_cts.Token.IsCancellationRequested)
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
                        JobHandle? handle;
                        lock (_lock)
                        {
                            _handles.TryGetValue(job.Id, out handle);
                        }

                        try
                        {
                            int result = job.Type switch
                            {
                                JobType.Prime => PrimeJobProcessor.Process(job.Payload),
                                JobType.IO => IoJobProcessor.Process(job.Payload),
                                _ => throw new Exception($"Nepoznat tip posla!")
                            };

                            // Obavestimo klijenta da je posao zavrsen
                            handle?.Complete(result);
                            // Emitujemo event da je uspesno posao gotov
                            JobCompleted?.Invoke(new JobCompletedEvent { JobId = job.Id, Result = result, CompletedAt = DateTime.Now });
                        }
                        catch (Exception ex)
                        {
                            handle?.Fail(ex);
                            // Emitujemo event da nije uspesan posao
                            JobFailed?.Invoke(new JobFailedEvent { JobId = job.Id, Reason = ex.Message, FailedAt = DateTime.Now });
                        }
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
