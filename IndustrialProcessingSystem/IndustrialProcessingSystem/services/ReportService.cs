using IndustrialProcessingSystem.enums;
using IndustrialProcessingSystem.events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace IndustrialProcessingSystem.services
{
    public class ReportService
    {
        private readonly List<ReportItem> _items = new();
        private readonly object _lock = new();

        private readonly Timer _timer;
        private int _reportIndex = 0;

        private readonly string _reportsFolder =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\reports"));

        public ReportService()
        {
            if (!Directory.Exists(_reportsFolder))
            {
                Directory.CreateDirectory(_reportsFolder);
            }

            // Na svaki minut
            _timer = new Timer(GenerateReport, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public void Subscribe(ProcessingSystem system)
        {
            system.JobCompleted += OnJobCompleted;
            system.JobFailed += OnJobFailed;
        }

        private void OnJobCompleted(JobCompletedEvent args)
        {
            lock (_lock)
            {
                _items.Add(new ReportItem
                {
                    JobId = args.JobId,
                    Type = args.Type,
                    Success = true,
                    DurationMs = args.Duration,
                    Timestamp = args.CompletedAt
                });
            }
        }

        private void OnJobFailed(JobFailedEvent args)
        {
            lock (_lock)
            {
                _items.Add(new ReportItem
                {
                    JobId = args.JobId,
                    Type = args.Type,
                    Success = false,
                    DurationMs = args.Duration,
                    Timestamp = args.FailedAt
                });
            }
        }

        private void GenerateReport(object? state)
        {
            List<ReportItem> snapshot;

            lock (_lock)
            {
                snapshot = _items.ToList();
            }

            var completedByType = snapshot
                .Where(x => x.Success)
                .GroupBy(x => x.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Type)
                .ToList();

            var averageDurationByType = snapshot
                .Where(x => x.Success)
                .GroupBy(x => x.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    AverageDuration = g.Average(x => x.DurationMs)
                })
                .OrderBy(x => x.Type)
                .ToList();

            var failedByType = snapshot
                .Where(x => !x.Success)
                .GroupBy(x => x.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Type)
                .ToList();

            XElement xml = new XElement("Report",
                new XAttribute("GeneratedAt", DateTime.Now),

                new XElement("CompletedJobsByType",
                    completedByType.Select(x =>
                        new XElement("Type",
                            new XAttribute("Name", x.Type),
                            new XAttribute("Count", x.Count)
                        )
                    )
                ),

                new XElement("AverageExecutionTimeByType",
                    averageDurationByType.Select(x =>
                        new XElement("Type",
                            new XAttribute("Name", x.Type),
                            new XAttribute("AverageDurationMs", x.AverageDuration)
                        )
                    )
                ),

                new XElement("FailedJobsByType",
                    failedByType.Select(x =>
                        new XElement("Type",
                            new XAttribute("Name", x.Type),
                            new XAttribute("Count", x.Count)
                        )
                    )
                )
            );

            string filePath = Path.Combine(_reportsFolder, $"report_{_reportIndex}.xml");
            xml.Save(filePath);

            Console.WriteLine($"Generisan izvestaj: {filePath}");

            // Rotacija 10 fajlova
            _reportIndex = (_reportIndex + 1) % 10;
        }

        public void Stop()
        {
            _timer.Dispose();
        }

        private class ReportItem
        {
            public Guid JobId { get; set; }
            public JobType Type { get; set; }
            public bool Success { get; set; }
            public long DurationMs { get; set; }
            public DateTime Timestamp { get; set; }
        }

    }
}
