using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialProcessingSystem.services
{
    public class IoJobProcessor
    {
        private static readonly Random _random = new();
        private static readonly object _randomLock = new();

        public static int Process(string payload)
        {
            int delay = int.Parse(payload.Split(":")[1].Replace("_", ""));

            // Simulacija neke io operacije
            Thread.Sleep(delay);

            // Vrati random broj
            lock (_randomLock)
            {
                return _random.Next(0, 101);
            }
        }
    }
}
