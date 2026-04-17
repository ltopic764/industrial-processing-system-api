using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndustrialProcessingSystem.services
{
    public class PrimeJobProcessor
    {
        public static int Process(string payload)
        {
            // Parsiranje payload-a iz XML
            string[] parts = payload.Split(',');
            int limit = int.Parse(parts[0].Split(':')[1].Replace("_", ""));
            int threadCount = int.Parse(parts[1].Split(":")[1]);

            // Ograniciti broj niti
            threadCount = Math.Clamp(threadCount, 1, 8);

            // Delimo posao na threadCount delova
            int chunkSize = limit / threadCount;
            int primeCount = 0; // brojac

            object countLock = new object();

            Thread[] threads = new Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int start = i * chunkSize + 2;
                int end = (i == threadCount - 1) ? limit : start + chunkSize;

                threads[i] = new Thread(() =>
                {
                    int localCount = 0;

                    for (int num = start; num < end; num++)
                    {
                        if (IsPrime(num))
                        {
                            localCount++;
                        }
                    }

                    // Dodaj lokalni rezultat u globalni brojac
                    lock (countLock)
                    {
                        primeCount += localCount;
                    }
                });

                threads[i].Start();
            }

            // Cekamo zavrsetak svih niti
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            return primeCount;
        }

        private static bool IsPrime(int n)
        {
            if (n < 2) return false;
            if (n == 2) return true;
            if (n % 2 == 0) return false;

            for (int i = 3; i*i <= n; i+=2)
            {
                if (n % i == 0) return false;
            }

            return true;
        }
    }
}
