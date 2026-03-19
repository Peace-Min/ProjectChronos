using System;
using System.Collections.Generic;
using System.IO;
using ProjectChronos.Models;
using ProjectChronos.ViewModels;

namespace ProjectChronos.Services
{
    public class TimelineReportDefinitionService
    {
        public const double DefaultScenarioDurationSeconds = 300.0;

        public string GetDefaultPrototypeExportPath()
        {
            return Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "output",
                "report_timeline_prototype.png"));
        }

        public TimelineReportExportInput CreatePrototypeReportExportInput(string outputPath)
        {
            return CreateReportExportInput(CreateScenarioEvents(), outputPath);
        }

        public TimelineReportExportInput CreateReportExportInput(IReadOnlyList<SimulationEventMarker> events, string outputPath)
        {
            return new TimelineReportExportInput(
                "\uADF8\uB798\uD504 \uC608\uC2DC 3",
                "Time-Event",
                Array.Empty<string>(),
                1100,
                0,
                events ?? Array.Empty<SimulationEventMarker>(),
                outputPath);
        }

        public IReadOnlyList<SimulationEventMarker> CreateScenarioEvents()
        {
            return new List<SimulationEventMarker>
            {
                new SimulationEventMarker
                {
                    Timestamp = 260.2,
                    Priority = EventPriority.Low,
                    Title = "\uD0D0\uC0C9\uB808\uC774\uB354",
                    DescriptionLabel = "\uD0D0\uC9C0 \uACB0\uACFC",
                    Description = "\uD0D0\uC0C9\uB808\uC774\uB354\uAC00 \uD45C\uC801\uC744 \uCD5C\uCD08 \uD0D0\uC9C0\uD574 \uC704\uD611 \uD3C9\uAC00\uB97C \uC2DC\uC791\uD588\uC2B5\uB2C8\uB2E4.",
                    RangeBTWLabel = "\uD0C0\uAC9F\uAC04 \uAC70\uB9AC",
                    RangeBTW = "128.4 km",
                    SourceTargetLabel = "\uC18C\uC2A4 \uD0C0\uAC9F",
                    SourceTarget = "\uD0D0\uC0C9\uB808\uC774\uB354 -> \uC704\uD611 \uD45C\uC801"
                },
                new SimulationEventMarker
                {
                    Timestamp = 267.16,
                    Priority = EventPriority.Medium,
                    Title = "\uCD94\uC801\uB808\uC774\uB354",
                    DescriptionLabel = "\uCD94\uC801 \uC0C1\uD0DC",
                    Description = "\uCD94\uC801\uB808\uC774\uB354\uAC00 \uD45C\uC801\uC744 \uC815\uBC00 \uCD94\uC801 \uBAA8\uB4DC\uB85C \uC804\uD658\uD588\uC2B5\uB2C8\uB2E4.",
                    RangeBTWLabel = "\uD0C0\uAC9F\uAC04 \uAC70\uB9AC",
                    RangeBTW = "84.7 km",
                    SourceTargetLabel = "\uC18C\uC2A4 \uD0C0\uAC9F",
                    SourceTarget = "\uCD94\uC801\uB808\uC774\uB354 -> \uC704\uD611 \uD45C\uC801"
                },
                new SimulationEventMarker
                {
                    Timestamp = 267.17,
                    Priority = EventPriority.Medium,
                    Title = "\uBC1C\uC0AC\uC2B9\uC778",
                    DescriptionLabel = "\uC2B9\uC778 \uC0C1\uD0DC",
                    Description = "\uAD50\uC804\uD1B5\uC81C\uAE30\uAC00 \uC704\uD611\uB3C4\uB97C \uD310\uB2E8\uD55C \uB4A4 \uBC1C\uC0AC \uC2B9\uC778\uC744 \uC644\uB8CC\uD588\uC2B5\uB2C8\uB2E4.",
                    RangeBTWLabel = "\uD0C0\uAC9F\uAC04 \uAC70\uB9AC",
                    RangeBTW = "41.2 km",
                    SourceTargetLabel = "\uC18C\uC2A4 \uD0C0\uAC9F",
                    SourceTarget = "\uAD50\uC804\uD1B5\uC81C\uAE30 -> \uC694\uACA9\uCCB4"
                },
                new SimulationEventMarker
                {
                    Timestamp = 267.17,
                    Priority = EventPriority.High,
                    Title = "\uBC1C\uC0AC",
                    DescriptionLabel = "\uBC1C\uC0AC \uC0C1\uD0DC",
                    Description = "\uC694\uACA9 \uBBF8\uC0AC\uC77C\uC774 \uBC1C\uC0AC\uB418\uC5B4 \uCD08\uAE30 \uC720\uB3C4 \uAD6C\uAC04\uC5D0 \uC9C4\uC785\uD588\uC2B5\uB2C8\uB2E4.",
                    RangeBTWLabel = "\uD0C0\uAC9F\uAC04 \uAC70\uB9AC",
                    RangeBTW = "41.2 km",
                    SourceTargetLabel = "\uC18C\uC2A4 \uD0C0\uAC9F",
                    SourceTarget = "\uC694\uACA9\uCCB4 -> \uC704\uD611 \uD45C\uC801"
                },
                new SimulationEventMarker
                {
                    Timestamp = 280.2,
                    Priority = EventPriority.High,
                    Title = "\uC694\uACA9",
                    DescriptionLabel = "\uAD50\uC804 \uACB0\uACFC",
                    Description = "\uC694\uACA9\uCCB4\uAC00 \uD45C\uC801\uC5D0 \uB3C4\uB2EC\uD574 \uCD5C\uC885 \uC694\uACA9\uC744 \uC644\uB8CC\uD588\uC2B5\uB2C8\uB2E4.",
                    RangeBTWLabel = "\uD0C0\uAC9F\uAC04 \uAC70\uB9AC",
                    RangeBTW = "0.6 km",
                    SourceTargetLabel = "\uC18C\uC2A4 \uD0C0\uAC9F",
                    SourceTarget = "\uC694\uACA9\uCCB4 -> \uC704\uD611 \uD45C\uC801"
                }
            };
        }
    }
}
