using IndustrialProcessingSystem.config;
using IndustrialProcessingSystem.enums;
using IndustrialProcessingSystem.events;
using IndustrialProcessingSystem.models;
using IndustrialProcessingSystem.services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialProcessingSystem.test
{
    public static class Tests
    {
        public static async Task RunAll()
        {
            Console.WriteLine("Pokrecem testove...");

            await Test_Job_Complete();
            await Test_DuplicateId_IsRejected();
            await Test_SlowJob_TriggerAbort();
            await Test_GetMethods();

            Console.WriteLine("Svi testovi gotovi");
        }

        // Test 1
        private static async Task Test_Job_Complete()
        {
            SystemConfig config = CreateTestConfig();
            ProcessingSystem system = new ProcessingSystem(config);

            Job job = new Job(Guid.NewGuid(), JobType.IO, "delay:500", 1);
            JobHandle? handle = system.Submit(job);

            if (handle == null)
            {
                throw new Exception("Test pao, Submit funkcija vratila null");
            }

            // cekamo rezultat
            int result = await handle.Result;

            if (result < 0 || result > 100)
            {
                throw new Exception("Test pao, IO rezultat nije u opsegu od 0 do 100");
            }

            Console.WriteLine("Test 1 prosao");
            system.Shutdown();
        }

        // Test 2
        private static async Task Test_DuplicateId_IsRejected()
        {
            SystemConfig config = CreateTestConfig();
            ProcessingSystem system = new ProcessingSystem(config);

            Job job = new Job(Guid.NewGuid(), JobType.IO, "delay:500", 1);

            JobHandle? firstHandle = system.Submit(job);
            JobHandle? secondHandle = system.Submit(job);

            if (firstHandle == null)
            {
                throw new Exception("Test pao, prvi Submit je vratio null.");
            }

            if (secondHandle != null)
            {
                throw new Exception("Test pao, dupli job nije odbijen.");
            }

            // Prvi job treba normalno da zavrsi
            await firstHandle.Result;

            Console.WriteLine("Test 2 prosao.");
            system.Shutdown();
        }

        // Test 3
        private static async Task Test_SlowJob_TriggerAbort()
        {
            SystemConfig config = CreateTestConfig();
            ProcessingSystem system = new ProcessingSystem(config);

            Job job = new Job(Guid.NewGuid(), JobType.IO, "delay:3000", 1);

            TaskCompletionSource<JobFailedEvent> tcs = new();

            system.JobFailed += (args) =>
            {
                if (args.JobId == job.Id && args.Reason == "ABORT")
                {
                    tcs.TrySetResult(args);
                }
            };

            JobHandle? handle = system.Submit(job);

            if (handle == null)
            {
                throw new Exception("Test pao, Submit je vratio null.");
            }

            try
            {
                await handle.Result;
                throw new Exception("Test pao, ocekivali smo fail/abort, a job je uspeo.");
            }
            catch
            {
                // Ocekivano
            }

            JobFailedEvent failedEvent = await tcs.Task;

            if (failedEvent.Reason != "ABORT")
            {
                throw new Exception($"Test pao, ocekivan ABORT, dobijeno: {failedEvent.Reason}");
            }

            Console.WriteLine("Test 3 prosao.");
            system.Shutdown();
        }

        // Test 4
        private static async Task Test_GetMethods()
        {
            Console.WriteLine("Test 4: GetTopJobs i GetJob");

            SystemConfig config = CreateTestConfig();
            ProcessingSystem system = new ProcessingSystem(config);

            Job job1 = new Job(Guid.NewGuid(), JobType.IO, "delay:1000", 3);
            Job job2 = new Job(Guid.NewGuid(), JobType.IO, "delay:1000", 1);
            Job job3 = new Job(Guid.NewGuid(), JobType.IO, "delay:1000", 2);

            system.Submit(job1);
            system.Submit(job2);
            system.Submit(job3);

            var topJobs = system.GetTopJobs(2);

            Console.WriteLine("Top jobs:");
            foreach (var job in topJobs)
            {
                Console.WriteLine($"Id: {job.Id}, Priority: {job.Priority}");
            }

            // Provera da li je sortiran po prioritetu
            var topList = topJobs.ToList();

            if (topList.Count > 1 && topList[0].Priority > topList[1].Priority)
            {
                throw new Exception("Test pao, GetTopJobs nije sortirao po prioritetu.");
            }

            var foundJob = system.GetJob(job2.Id);

            if (foundJob == null || foundJob.Id != job2.Id)
            {
                throw new Exception("Test pao, GetJob nije pronasao ispravan job.");
            }

            JobHandle? h1 = system.Submit(job1);
            JobHandle? h2 = system.Submit(job2);
            JobHandle? h3 = system.Submit(job3);

            await Task.WhenAll(
                h1?.Result ?? Task.CompletedTask,
                h2?.Result ?? Task.CompletedTask,
                h3?.Result ?? Task.CompletedTask
            );

            system.Shutdown();

            Console.WriteLine("Test 4 prosao.");
        }
        private static SystemConfig CreateTestConfig()
        {
            return new SystemConfig
            {
                WorkerCount = 2,
                MaxQueueSize = 10,
                Jobs = new List<Job>()
            };
        }
    }
}
