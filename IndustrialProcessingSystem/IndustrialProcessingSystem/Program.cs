using IndustrialProcessingSystem.config;
using IndustrialProcessingSystem.events;
using IndustrialProcessingSystem.services;

namespace IndustrialProcessingSystem
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            SystemConfig config = SystemConfig.LoadFromXml(@"..\..\..\config\SystemConfig.xml");

            ProcessingSystem system = new ProcessingSystem(config);

            EventLogger logger = new EventLogger();
            logger.Subscribe(system);

            system.LoadInitialJobs(config.Jobs);

            Console.WriteLine("Began... waiting....");
            Console.ReadKey();

            system.Shutdown();
            Console.WriteLine("Done");
        }
    }
}
