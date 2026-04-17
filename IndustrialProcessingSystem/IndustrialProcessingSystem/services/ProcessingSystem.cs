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
        private readonly Dictionary<Guid, Job> _jobsById = new(); // svi poslovi ikad


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
                _jobsById[job.Id] = job;

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
                            // Pokusaj izvrsenja
                            int result = ExecuteWithRetry(job);

                            // Ako smo dobili rezultat, posao je uspesno zavrsen
                            handle?.Complete(result);
                            // Emitujemo event da je uspesno posao gotov
                            JobCompleted?.Invoke(new JobCompletedEvent { JobId = job.Id, Result = result, CompletedAt = DateTime.Now });
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message == "ABORT")
                            {
                                Console.WriteLine($"Job {job.Id} je abortiran nakon 3 neuspesna pokusaja.");
                                Console.WriteLine($"Job payload: {job.Payload}");
                            }
                            else
                            {
                                Console.WriteLine($"GRESKA U WORKERU: {ex.Message}");
                                Console.WriteLine($"Job payload: {job.Payload}");
                            }

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

        private int ExecuteJob(Job job)
        {
            return job.Type switch
            {
                JobType.Prime => PrimeJobProcessor.Process(job.Payload),
                JobType.IO => IoJobProcessor.Process(job.Payload),
                _ => throw new Exception("Nepoznat tip posla")
            };
        }

        public Job? GetJob(Guid id)
        {
            lock(_lock)
            {
                if (_jobsById.TryGetValue(id, out Job? job))
                {
                    return job;
                }

                return null;
            }
        }

        public IEnumerable<Job> GetTopJobs(int n)
        {
            lock (_lock)
            {
                return _queue.UnorderedItems
                    .Select(item => item.Element)
                    .OrderBy(job => job.Priority)
                    .Take(n)
                    .ToList();
            }
        }

        private int ExecuteWithRetry(Job job)
        {
            int maxAttempts = 3;
            int timeoutMillisec = 2000;
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    Task<int> task = Task.Run(() => ExecuteJob(job));

                    // Cekamo najvise 2 sekunde
                    bool finishedInTime = task.Wait(timeoutMillisec);

                    if (finishedInTime)
                    {
                        // Ako je posao zavrsen, uzmemo rezultat
                        return task.Result;
                    }

                    // Ako nije zavrsio za 2 sekunde, fail
                    lastException = new TimeoutException($"Job exceeded timeout of {timeoutMillisec} ms. Try {attempt}/{maxAttempts}");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }

                // Ako nije poslednji pokusaj, prijavi fail
                if (attempt < maxAttempts)
                {
                    JobFailed?.Invoke(new JobFailedEvent { JobId = job.Id, Reason = $"Attempt failed: {lastException.Message}", FailedAt = DateTime.Now });
                }
            }

            // Sva tri pokusaja su propala
            throw new Exception("ABORT");
        }


        public void Shutdown()
        {
            _cts.Cancel();
        }
    }
}
