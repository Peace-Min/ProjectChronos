using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ProjectChronos.Models;

namespace ProjectChronos.Services
{
    public class TimelineAnalysisReportInput
    {
        public TimelineAnalysisReportInput(
            string title,
            string imagePath,
            string reportPath,
            IReadOnlyList<SimulationEventMarker> events)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Timeline Analysis Report" : title;
            ImagePath = imagePath;
            ReportPath = reportPath;
            Events = events ?? Array.Empty<SimulationEventMarker>();
        }

        public string Title { get; }
        public string ImagePath { get; }
        public string ReportPath { get; }
        public IReadOnlyList<SimulationEventMarker> Events { get; }
    }

    public class TimelineAnalysisReportService
    {
        public string Export(TimelineAnalysisReportInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrWhiteSpace(input.ReportPath)) throw new ArgumentException("Report path is required.", nameof(input));

            string reportPath = Path.GetFullPath(input.ReportPath);
            string reportDirectory = Path.GetDirectoryName(reportPath);

            if (!string.IsNullOrWhiteSpace(reportDirectory))
            {
                Directory.CreateDirectory(reportDirectory);
            }

            string imageFileName = string.IsNullOrWhiteSpace(input.ImagePath)
                ? string.Empty
                : Path.GetFileName(input.ImagePath);
            var orderedEvents = input.Events
                .Where(item => item != null)
                .OrderBy(item => item.Timestamp)
                .ThenBy(item => item.Title, StringComparer.CurrentCulture)
                .ToList();
            var groupedEvents = orderedEvents
                .GroupBy(item => Math.Round(item.Timestamp, 3))
                .Select(group => group.ToList())
                .ToList();

            var builder = new StringBuilder();
            builder.AppendLine("# " + input.Title + " \uBD84\uC11D\uBCF4\uACE0\uC11C");
            builder.AppendLine();
            builder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "- \uC0DD\uC131 \uC2DC\uAC01: {0:yyyy-MM-dd HH:mm:ss}",
                DateTime.Now));
            builder.AppendLine("- \uCD9C\uB825 \uC774\uBBF8\uC9C0: " + imageFileName);
            builder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "- \uCD1D \uC774\uBCA4\uD2B8 \uC218: {0}",
                orderedEvents.Count));
            builder.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "- \uC2DC\uAC04 \uADF8\uB8F9 \uC218: {0}",
                groupedEvents.Count));
            builder.AppendLine();

            builder.AppendLine("## \uC774\uBCA4\uD2B8 \uBAA9\uB85D");
            builder.AppendLine();
            builder.AppendLine("| \uC21C\uC11C | \uC2DC\uAC01(\uCD08) | \uC774\uBCA4\uD2B8 | \uC0C1\uC138 \uC124\uBA85 | \uD0C0\uAC9F\uAC04 \uAC70\uB9AC |");
            builder.AppendLine("| --- | ---: | --- | --- | --- |");

            for (int index = 0; index < orderedEvents.Count; index++)
            {
                var item = orderedEvents[index];
                builder.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} | {1:F2} | {2} | {3}: {4} | {5}: {6} |",
                    index + 1,
                    item.Timestamp,
                    EscapePipe(item.Title),
                    EscapePipe(item.DescriptionLabel),
                    EscapePipe(item.Description),
                    EscapePipe(item.RangeBTWLabel),
                    EscapePipe(item.RangeBTW)));
            }

            builder.AppendLine();
            builder.AppendLine("## \uC2DC\uAC04 \uAC04\uACA9 \uBD84\uC11D");
            builder.AppendLine();
            builder.AppendLine("| \uAD6C\uAC04 | \uC2DC\uC791(\uCD08) | \uC885\uB8CC(\uCD08) | \uAC04\uACA9 |");
            builder.AppendLine("| --- | ---: | ---: | ---: |");

            for (int index = 0; index < groupedEvents.Count - 1; index++)
            {
                var currentGroup = groupedEvents[index];
                var nextGroup = groupedEvents[index + 1];
                string currentTitle = string.Join(", ", currentGroup.Select(item => item.Title));
                string nextTitle = string.Join(", ", nextGroup.Select(item => item.Title));
                double startTime = currentGroup[0].Timestamp;
                double endTime = nextGroup[0].Timestamp;
                double delta = endTime - startTime;

                builder.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} -> {1} | {2:F2} | {3:F2} | {4} |",
                    EscapePipe(currentTitle),
                    EscapePipe(nextTitle),
                    startTime,
                    endTime,
                    FormatSeconds(delta)));
            }

            builder.AppendLine();
            builder.AppendLine("## \uD575\uC2EC \uD574\uC11D");
            builder.AppendLine();

            if (groupedEvents.Count > 0)
            {
                builder.AppendLine("- \uD0D0\uC0C9\uB808\uC774\uB354\uC5D0\uC11C \uC694\uACA9\uAE4C\uC9C0 \uC2E4\uC81C \uAD50\uC804 \uC21C\uC11C\uAC00 \uC2DC\uAC04 \uCD95 \uC0C1\uC5D0 \uC815\uB82C\uB418\uC5B4 \uC804\uC2DC\uB429\uB2C8\uB2E4.");
            }

            if (groupedEvents.Count > 1)
            {
                double shortestDelta = double.MaxValue;
                List<SimulationEventMarker> shortestCurrent = null;
                List<SimulationEventMarker> shortestNext = null;

                for (int index = 0; index < groupedEvents.Count - 1; index++)
                {
                    double delta = groupedEvents[index + 1][0].Timestamp - groupedEvents[index][0].Timestamp;
                    if (delta < shortestDelta)
                    {
                        shortestDelta = delta;
                        shortestCurrent = groupedEvents[index];
                        shortestNext = groupedEvents[index + 1];
                    }
                }

                if (shortestCurrent != null && shortestNext != null)
                {
                    builder.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "- \uAC00\uC7A5 \uC9E7\uC740 \uAC04\uACA9\uC740 `{0}` \u2192 `{1}` \uAD6C\uAC04\uC758 {2}\uB85C, \uC0AC\uC2E4\uC0C1 \uC5F0\uC18D \uC808\uCC28\uC5D0 \uAC00\uAE5D\uC2B5\uB2C8\uB2E4.",
                        string.Join(", ", shortestCurrent.Select(item => item.Title)),
                        string.Join(", ", shortestNext.Select(item => item.Title)),
                        FormatSeconds(shortestDelta)));
                }
            }

            if (groupedEvents.Count > 0)
            {
                var lastGroup = groupedEvents[groupedEvents.Count - 1];
                builder.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "- \uCD5C\uC885 \uC2DC\uC810 `{0}`\uC5D0\uC11C `{1}` \uC774\uBCA4\uD2B8\uAC00 \uBC1C\uC0DD\uD558\uBA70 \uC2DC\uB098\uB9AC\uC624\uAC00 \uB9C8\uBB34\uB9AC\uB429\uB2C8\uB2E4.",
                    lastGroup[0].Timestamp.ToString("F2", CultureInfo.InvariantCulture) + "\uCD08",
                    string.Join(", ", lastGroup.Select(item => item.Title))));
            }

            File.WriteAllText(reportPath, builder.ToString(), new UTF8Encoding(false));
            return reportPath;
        }

        private static string EscapePipe(string text)
        {
            return (text ?? string.Empty).Replace("|", "\\|");
        }

        private static string FormatSeconds(double seconds)
        {
            if (Math.Abs(seconds - Math.Round(seconds)) < 0.005)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0}\uCD08", seconds);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.##}\uCD08", seconds);
        }
    }
}
