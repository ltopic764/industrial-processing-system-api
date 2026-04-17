using IndustrialProcessingSystem.enums;
using IndustrialProcessingSystem.models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IndustrialProcessingSystem.config
{
    public class SystemConfig
    {
        public int WorkerCount { get; set; }
        public int MaxQueueSize { get; set; }
        public List<Job> Jobs { get; set; } = new();

        public static SystemConfig LoadFromXml(string path)
        {
            var config = new SystemConfig();
            XElement xmlData = XElement.Load(path);

            config.WorkerCount = int.Parse(xmlData.Element("WorkerCount").Value);
            config.MaxQueueSize = int.Parse(xmlData.Element("MaxQueueSize").Value);

            config.Jobs = (from jobEl in xmlData.Descendants("Job")
                           select new Job
                           (Guid.NewGuid(), Enum.Parse<JobType>(jobEl.Attribute("Type").Value), jobEl.Attribute("Payload").Value, int.Parse(jobEl.Attribute("Priority").Value))).ToList();
            return config;
        }
    }
}
