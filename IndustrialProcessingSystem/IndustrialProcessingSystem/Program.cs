using IndustrialProcessingSystem.config;
using IndustrialProcessingSystem.enums;
using IndustrialProcessingSystem.events;
using IndustrialProcessingSystem.models;
using IndustrialProcessingSystem.services;
using System;

namespace IndustrialProcessingSystem
{
    internal class Program
    {
        private static readonly object _randomLock = new();
        private static readonly Random _random = new();

        static void Main(string[] args)
        {
            try
            {
                // Ucitavanje konfiguracije iz XML
                SystemConfig config = SystemConfig.LoadFromXml(@"..\..\..\config\SystemConfig.xml");
                Console.WriteLine($"Ucitano {config.Jobs.Count} polaznih poslova");
                Console.WriteLine($"Worker niti: {config.WorkerCount}");
                Console.WriteLine($"Max queue size: {config.MaxQueueSize}");

                ProcessingSystem system = new ProcessingSystem(config);

                EventLogger logger = new EventLogger();
                logger.Subscribe(system);

                system.LoadInitialJobs(config.Jobs);

                // Producer niti
                List<Thread> producerThreads = new List<Thread>();

                for (int i = 0; i < config.WorkerCount; i++)
                {
                    int producerId = i;

                    Thread producerThread = new Thread(() => ProducerLoop(system, producerId))
                    {
                        IsBackground = true
                    };

                    producerThreads.Add(producerThread);
                    producerThread.Start();
                }

                Console.WriteLine("Pritisni taster za zavrsetak programa");
                Console.ReadKey();

                system.Shutdown();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greska u Main: {ex.Message}");
            }
        }

        private static void ProducerLoop(ProcessingSystem system, int producerId)
        {
            while (true)
            {
                try
                {
                    Job newJob = CreateRandomJob();

                    JobHandle? handle = system.Submit(newJob);

                    if (handle != null)
                    {
                        Console.WriteLine(
                            $"Producer-{producerId} dodao posao | Id: {newJob.Id} | Type: {newJob.Type} | Priority: {newJob.Priority} | Payload: {newJob.Payload}"
                        );
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Producer-{producerId} NIJE dodao posao (queue je pun ili job vec postoji)."
                        );
                    }

                    int sleepTime;
                    lock (_randomLock)
                    {
                        sleepTime = _random.Next(500, 1501);
                    }

                    Thread.Sleep(sleepTime);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greska u producer niti {producerId}: {ex.Message}");
                }
            }
        }

        private static Job CreateRandomJob()
        {
            lock (_randomLock)
            {
                JobType type = _random.Next(0, 2) == 0 ? JobType.Prime : JobType.IO;
                int priority = _random.Next(1, 6);

                string payload;

                if (type == JobType.Prime)
                {
                    int numberLimit = _random.Next(5000, 20001);
                    int threadCount = _random.Next(1, 9);      

                    payload = $"numbers:{numberLimit},threads:{threadCount}";
                }
                else
                {
                    int delay = _random.Next(500, 4001);
                    payload = $"delay:{delay}";
                }

                return new Job
                (
                    Guid.NewGuid(),
                    type,
                    payload,
                    priority
                );
            }
        }
    }
}
