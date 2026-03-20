using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;
using System.Windows.Media;
using ProjectChronos.Models;
using ProjectChronos.ViewModels;

namespace ProjectChronos.Services
{
    public sealed partial class TimelineReportStressService
    {
        private static double CalculateFooterHeight(IReadOnlyCollection<string> footerNotes, double footerWidth)
        {
            double textWidth = Math.Max(120.0, footerWidth - (FooterHorizontalPadding * 2.0));
            double height = FooterVerticalPadding * 2.0;
            int index = 0;

            foreach (string note in footerNotes)
            {
                Size noteSize = MeasureText(note, UiTypeface, FooterFontSize, textWidth);
                height += noteSize.Height;

                if (index < footerNotes.Count - 1)
                {
                    height += FooterNoteGap;
                }

                index++;
            }

            return Math.Ceiling(height);
        }

        private static Size MeasureText(string text, Typeface typeface, double fontSize, double maxWidth)
        {
            var formattedText = new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                PixelsPerDip);

            if (!double.IsInfinity(maxWidth))
            {
                formattedText.MaxTextWidth = maxWidth;
            }

            return new Size(Math.Ceiling(formattedText.Width), Math.Ceiling(formattedText.Height));
        }

        private static bool HasOverlap(ReportTimelineDetailCardItem leftCard, ReportTimelineDetailCardItem rightCard, double tolerance)
        {
            double horizontalOverlap = Math.Min(leftCard.Left + leftCard.Width, rightCard.Left + rightCard.Width) - Math.Max(leftCard.Left, rightCard.Left);
            double verticalOverlap = Math.Min(leftCard.Top + leftCard.Height, rightCard.Top + rightCard.Height) - Math.Max(leftCard.Top, rightCard.Top);
            return horizontalOverlap > tolerance && verticalOverlap > tolerance;
        }

        private TimelineReportStressSummary BuildSummary(
            TimelineReportStressOptions options,
            IReadOnlyList<TimelineReportStressCaseResult> results,
            string artifactRoot,
            string flaggedDirectory,
            string warningsDirectory,
            string summaryJsonPath,
            string summaryMarkdownPath,
            string reviewMarkdownPath)
        {
            var deterministicResults = results.Where(item => string.Equals(item.Suite, "deterministic", StringComparison.OrdinalIgnoreCase)).ToList();
            var randomResults = results.Where(item => string.Equals(item.Suite, "random", StringComparison.OrdinalIgnoreCase)).ToList();
            var widthCandidates = options.DeterministicWidths.Intersect(options.RandomWidths).OrderBy(value => value).ToList();
            var minimumSafeWidthCandidates = new List<double>();
            var recommendedWidthCandidates = new List<double>();

            foreach (double width in widthCandidates)
            {
                var matchingDeterministic = deterministicResults.Where(item => AreSameWidth(item.Width, width)).ToList();
                var matchingRandom = randomResults.Where(item => AreSameWidth(item.Width, width)).ToList();

                if (matchingDeterministic.Count == 0 || matchingRandom.Count == 0)
                {
                    continue;
                }

                bool hasHardFailure = matchingDeterministic.Any(item => item.HardFail) || matchingRandom.Any(item => item.HardFail);
                if (!hasHardFailure)
                {
                    minimumSafeWidthCandidates.Add(width);
                }

                bool hasWarning = matchingDeterministic.Any(item => item.Warning) || matchingRandom.Any(item => item.Warning);
                if (!hasHardFailure && !hasWarning)
                {
                    recommendedWidthCandidates.Add(width);
                }
            }

            var maxUniqueGroupsByWidth = new Dictionary<string, int>();
            var maxTotalEventsByWidth = new Dictionary<string, int>();

            foreach (double width in options.DeterministicWidths)
            {
                string key = FormatWidthKey(width);
                int maxGroups = deterministicResults
                    .Where(item => AreSameWidth(item.Width, width) && item.DuplicateDepth == 1 && !item.HardFail)
                    .Select(item => item.UniqueGroupCount)
                    .DefaultIfEmpty(0)
                    .Max();
                int maxEvents = deterministicResults
                    .Where(item => AreSameWidth(item.Width, width) && !item.HardFail)
                    .Select(item => item.TotalEventCount)
                    .DefaultIfEmpty(0)
                    .Max();

                maxUniqueGroupsByWidth[key] = maxGroups;
                maxTotalEventsByWidth[key] = maxEvents;
            }

            int hardFailureCount = results.Count(item => item.HardFail);
            int warningCount = results.Count(item => item.Warning);

            return new TimelineReportStressSummary
            {
                ArtifactRoot = artifactRoot,
                FlaggedDirectory = flaggedDirectory,
                WarningsDirectory = warningsDirectory,
                SummaryJsonPath = summaryJsonPath,
                SummaryMarkdownPath = summaryMarkdownPath,
                ReviewMarkdownPath = reviewMarkdownPath,
                GeneratedAtLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                ExitCode = hardFailureCount > 0 ? 1 : (warningCount > 0 ? 2 : 0),
                TotalCaseCount = results.Count,
                DeterministicCaseCount = deterministicResults.Count,
                RandomCaseCount = randomResults.Count,
                HardFailureCount = hardFailureCount,
                WarningCount = warningCount,
                MinimumSafeWidth = minimumSafeWidthCandidates.Count > 0 ? (double?)minimumSafeWidthCandidates[0] : null,
                RecommendedWidth = recommendedWidthCandidates.Count > 0 ? (double?)recommendedWidthCandidates[0] : null,
                MinimumSafeWidthCandidates = minimumSafeWidthCandidates,
                RecommendedWidthCandidates = recommendedWidthCandidates,
                MaxUniqueGroupsByWidth = maxUniqueGroupsByWidth,
                MaxTotalEventsByWidth = maxTotalEventsByWidth,
                RepresentativeIssues = BuildRepresentativeIssues(results),
                Cases = results.ToList()
            };
        }

        private static List<TimelineReportStressRepresentativeIssue> BuildRepresentativeIssues(IReadOnlyList<TimelineReportStressCaseResult> results)
        {
            var representativeIssues = new List<TimelineReportStressRepresentativeIssue>();

            foreach (TimelineReportStressCaseResult result in results.Where(item => item.HardFail))
            {
                foreach (string reason in result.FailureReasons)
                {
                    string reasonKey = GetReasonKey(reason);
                    if (representativeIssues.Any(item => item.Severity == "hard-fail" && item.ReasonKey == reasonKey))
                    {
                        continue;
                    }

                    representativeIssues.Add(new TimelineReportStressRepresentativeIssue
                    {
                        Severity = "hard-fail",
                        ReasonKey = reasonKey,
                        CaseId = result.CaseId,
                        GeneratedPngPath = result.GeneratedPngPath
                    });
                }
            }

            foreach (TimelineReportStressCaseResult result in results.Where(item => item.Warning))
            {
                foreach (string reason in result.WarningReasons)
                {
                    string reasonKey = GetReasonKey(reason);
                    if (representativeIssues.Any(item => item.Severity == "warning" && item.ReasonKey == reasonKey))
                    {
                        continue;
                    }

                    representativeIssues.Add(new TimelineReportStressRepresentativeIssue
                    {
                        Severity = "warning",
                        ReasonKey = reasonKey,
                        CaseId = result.CaseId,
                        GeneratedPngPath = result.GeneratedPngPath
                    });
                }
            }

            return representativeIssues;
        }

        private static string GetReasonKey(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return "unknown";
            }

            int separatorIndex = reason.IndexOf(':');
            return separatorIndex > 0
                ? reason.Substring(0, separatorIndex).Trim()
                : reason.Trim();
        }

        private static string JoinReasons(IEnumerable<string> reasons)
        {
            var normalized = reasons
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct()
                .ToList();

            return normalized.Count == 0 ? null : string.Join("; ", normalized);
        }

        private void WriteSummaryJson(TimelineReportStressSummary summary, string summaryJsonPath)
        {
            File.WriteAllText(summaryJsonPath, BuildSummaryJson(summary), new UTF8Encoding(false));
        }

        private void WriteProgress(string progressJsonPath, TimelineReportStressProgress progress)
        {
            if (string.IsNullOrWhiteSpace(progressJsonPath) || progress == null)
            {
                return;
            }

            string directory = Path.GetDirectoryName(progressJsonPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serializer = new DataContractJsonSerializer(typeof(TimelineReportStressProgress));
            string tempJsonPath = progressJsonPath + ".tmp";
            using (var stream = new FileStream(tempJsonPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                serializer.WriteObject(stream, progress);
            }

            ReplaceFileWithRetry(tempJsonPath, progressJsonPath);

            string markdownPath = Path.ChangeExtension(progressJsonPath, ".md");
            string tempMarkdownPath = markdownPath + ".tmp";
            var builder = new StringBuilder();
            builder.AppendLine("# Stress Chunk Progress");
            builder.AppendLine();
            builder.AppendLine("- state: `" + progress.State + "`");
            builder.AppendLine("- chunkId: `" + (string.IsNullOrWhiteSpace(progress.ChunkId) ? "-" : progress.ChunkId) + "`");
            builder.AppendLine("- artifactRoot: `" + progress.ArtifactRoot + "`");
            builder.AppendLine("- startedAt: `" + progress.StartedAtLocal + "`");
            builder.AppendLine("- updatedAt: `" + progress.UpdatedAtLocal + "`");
            builder.AppendLine("- completed: `" + progress.CompletedCaseCount.ToString(CultureInfo.InvariantCulture) + " / " + progress.TotalCaseCount.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- hardFailures: `" + progress.HardFailureCount.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- warnings: `" + progress.WarningCount.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- currentCaseId: `" + (string.IsNullOrWhiteSpace(progress.CurrentCaseId) ? "-" : progress.CurrentCaseId) + "`");
            builder.AppendLine("- currentSuite: `" + (string.IsNullOrWhiteSpace(progress.CurrentSuite) ? "-" : progress.CurrentSuite) + "`");
            builder.AppendLine("- currentProfile: `" + (string.IsNullOrWhiteSpace(progress.CurrentProfile) ? "-" : progress.CurrentProfile) + "`");
            builder.AppendLine("- currentWidth: `" + FormatWidth(progress.CurrentWidth) + "`");
            builder.AppendLine("- currentSeed: `" + progress.CurrentSeed.ToString(CultureInfo.InvariantCulture) + "`");
            builder.AppendLine("- summaryJsonPath: `" + progress.SummaryJsonPath + "`");
            builder.AppendLine("- note: `" + (string.IsNullOrWhiteSpace(progress.Note) ? "-" : progress.Note) + "`");
            File.WriteAllText(tempMarkdownPath, builder.ToString(), new UTF8Encoding(false));
            ReplaceFileWithRetry(tempMarkdownPath, markdownPath);
        }

        private static void ReplaceFileWithRetry(string sourcePath, string destinationPath)
        {
            const int maxAttempts = 5;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    if (File.Exists(destinationPath))
                    {
                        File.Delete(destinationPath);
                    }

                    File.Move(sourcePath, destinationPath);
                    return;
                }
                catch (IOException)
                {
                    if (attempt == maxAttempts - 1)
                    {
                        throw;
                    }

                    System.Threading.Thread.Sleep(25 * (attempt + 1));
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt == maxAttempts - 1)
                    {
                        throw;
                    }

                    System.Threading.Thread.Sleep(25 * (attempt + 1));
                }
            }
        }

        private void WriteSummaryMarkdown(TimelineReportStressSummary summary, string summaryMarkdownPath)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# 분석보고서 이미지 스트레스 테스트 요약");
            builder.AppendLine();
            builder.AppendLine("- 생성 시각: " + summary.GeneratedAtLocal);
            builder.AppendLine("- 아티팩트 루트: `" + summary.ArtifactRoot + "`");
            builder.AppendLine("- 전체 케이스: " + summary.TotalCaseCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- deterministic 케이스: " + summary.DeterministicCaseCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- random 케이스: " + summary.RandomCaseCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- hard failure 수: " + summary.HardFailureCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- warning 수: " + summary.WarningCount.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("- minimumSafeWidth: " + FormatNullableWidth(summary.MinimumSafeWidth));
            builder.AppendLine("- recommendedWidth(geometry 기준, reviewer 확인 전): " + FormatNullableWidth(summary.RecommendedWidth));
            builder.AppendLine("- flagged 디렉터리: `" + summary.FlaggedDirectory + "`");
            builder.AppendLine("- warnings 디렉터리: `" + summary.WarningsDirectory + "`");
            builder.AppendLine("- review.md 예정 경로: `" + summary.ReviewMarkdownPath + "`");
            builder.AppendLine();
            builder.AppendLine("## 최소 안전 폭 후보");
            AppendWidthList(builder, summary.MinimumSafeWidthCandidates);
            builder.AppendLine();
            builder.AppendLine("## 권장 폭 후보");
            AppendWidthList(builder, summary.RecommendedWidthCandidates);
            builder.AppendLine();
            builder.AppendLine("## 폭별 최대 unique event group 수");
            builder.AppendLine("| Width | Max Unique Groups |");
            builder.AppendLine("| --- | ---: |");
            foreach (KeyValuePair<string, int> pair in summary.MaxUniqueGroupsByWidth.OrderBy(item => ParseWidthKey(item.Key)))
            {
                builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} |", pair.Key, pair.Value));
            }

            builder.AppendLine();
            builder.AppendLine("## 폭별 최대 total event 수");
            builder.AppendLine("| Width | Max Total Events |");
            builder.AppendLine("| --- | ---: |");
            foreach (KeyValuePair<string, int> pair in summary.MaxTotalEventsByWidth.OrderBy(item => ParseWidthKey(item.Key)))
            {
                builder.AppendLine(string.Format(CultureInfo.InvariantCulture, "| {0} | {1} |", pair.Key, pair.Value));
            }

            builder.AppendLine();
            builder.AppendLine("## 대표 실패/경계 유형");
            if (summary.RepresentativeIssues.Count == 0)
            {
                builder.AppendLine("- 없음");
            }
            else
            {
                foreach (TimelineReportStressRepresentativeIssue issue in summary.RepresentativeIssues)
                {
                    builder.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "- {0} / {1} / {2} / {3}",
                        issue.Severity,
                        issue.ReasonKey,
                        issue.CaseId,
                        string.IsNullOrWhiteSpace(issue.GeneratedPngPath) ? "PNG 없음" : issue.GeneratedPngPath));
                }
            }

            builder.AppendLine();
            builder.AppendLine("## 문제 케이스 상위 20개");
            var highlightedCases = summary.Cases
                .Where(item => item.HardFail || item.Warning)
                .OrderByDescending(item => item.HardFail)
                .ThenBy(item => item.Width)
                .ThenBy(item => item.Profile)
                .ThenBy(item => item.UniqueGroupCount)
                .ThenBy(item => item.TotalEventCount)
                .Take(20)
                .ToList();

            if (highlightedCases.Count == 0)
            {
                builder.AppendLine("- 없음");
            }
            else
            {
                foreach (TimelineReportStressCaseResult item in highlightedCases)
                {
                    builder.AppendLine(string.Format(
                        CultureInfo.InvariantCulture,
                        "- {0} | suite={1} | width={2} | groups={3} | total={4} | hardFail={5} | warning={6}",
                        item.CaseId,
                        item.Suite,
                        FormatWidth(item.Width),
                        item.UniqueGroupCount,
                        item.TotalEventCount,
                        item.HardFail,
                        item.Warning));

                    if (!string.IsNullOrWhiteSpace(item.FailureReason))
                    {
                        builder.AppendLine("  - hard: " + item.FailureReason);
                    }

                    if (!string.IsNullOrWhiteSpace(item.WarningReason))
                    {
                        builder.AppendLine("  - warn: " + item.WarningReason);
                    }

                    if (!string.IsNullOrWhiteSpace(item.GeneratedPngPath))
                    {
                        builder.AppendLine("  - png: `" + item.GeneratedPngPath + "`");
                    }
                }
            }

            File.WriteAllText(summaryMarkdownPath, builder.ToString(), new UTF8Encoding(false));
        }

        private static void AppendWidthList(StringBuilder builder, IReadOnlyCollection<double> widths)
        {
            if (widths == null || widths.Count == 0)
            {
                builder.AppendLine("- 없음");
                return;
            }

            foreach (double width in widths)
            {
                builder.AppendLine("- " + FormatWidth(width));
            }
        }

        private static string FormatNullableWidth(double? width)
        {
            return width.HasValue ? FormatWidth(width.Value) : "없음";
        }

        private static string FormatWidth(double width)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0}px", width);
        }

        private static string FormatWidthKey(double width)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0}", width);
        }

        private static double ParseWidthKey(string key)
        {
            double width;
            return double.TryParse(key, NumberStyles.Any, CultureInfo.InvariantCulture, out width)
                ? width
                : double.MaxValue;
        }

        private static bool AreSameWidth(double left, double right)
        {
            return Math.Abs(left - right) < 0.01;
        }

        private static void WriteLatestPointer(string artifactRoot)
        {
            string parentDirectory = Path.GetDirectoryName(artifactRoot);
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                return;
            }

            string latestPointerPath = Path.Combine(parentDirectory, "latest.txt");
            File.WriteAllText(latestPointerPath, artifactRoot, new UTF8Encoding(false));
        }

        private static string BuildSummaryJson(TimelineReportStressSummary summary)
        {
            var builder = new StringBuilder(32768);
            builder.Append('{');
            AppendJsonProperty(builder, "artifactRoot", summary.ArtifactRoot, true);
            AppendJsonProperty(builder, "flaggedDirectory", summary.FlaggedDirectory, true);
            AppendJsonProperty(builder, "warningsDirectory", summary.WarningsDirectory, true);
            AppendJsonProperty(builder, "summaryJsonPath", summary.SummaryJsonPath, true);
            AppendJsonProperty(builder, "summaryMarkdownPath", summary.SummaryMarkdownPath, true);
            AppendJsonProperty(builder, "reviewMarkdownPath", summary.ReviewMarkdownPath, true);
            AppendJsonProperty(builder, "generatedAtLocal", summary.GeneratedAtLocal, true);
            AppendJsonProperty(builder, "exitCode", summary.ExitCode, true);
            AppendJsonProperty(builder, "totalCaseCount", summary.TotalCaseCount, true);
            AppendJsonProperty(builder, "deterministicCaseCount", summary.DeterministicCaseCount, true);
            AppendJsonProperty(builder, "randomCaseCount", summary.RandomCaseCount, true);
            AppendJsonProperty(builder, "hardFailureCount", summary.HardFailureCount, true);
            AppendJsonProperty(builder, "warningCount", summary.WarningCount, true);
            AppendJsonProperty(builder, "minimumSafeWidth", summary.MinimumSafeWidth, true);
            AppendJsonProperty(builder, "recommendedWidth", summary.RecommendedWidth, true);
            AppendJsonDoubleArray(builder, "minimumSafeWidthCandidates", summary.MinimumSafeWidthCandidates, true);
            AppendJsonDoubleArray(builder, "recommendedWidthCandidates", summary.RecommendedWidthCandidates, true);
            AppendJsonDictionary(builder, "maxUniqueGroupsByWidth", summary.MaxUniqueGroupsByWidth, true);
            AppendJsonDictionary(builder, "maxTotalEventsByWidth", summary.MaxTotalEventsByWidth, true);
            AppendRepresentativeIssues(builder, summary.RepresentativeIssues, true);
            AppendCases(builder, summary.Cases, false);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendRepresentativeIssues(StringBuilder builder, IReadOnlyList<TimelineReportStressRepresentativeIssue> issues, bool appendComma)
        {
            builder.Append("\"representativeIssues\":");
            builder.Append('[');

            if (issues != null)
            {
                for (int index = 0; index < issues.Count; index++)
                {
                    TimelineReportStressRepresentativeIssue issue = issues[index];
                    builder.Append('{');
                    AppendJsonProperty(builder, "severity", issue.Severity, true);
                    AppendJsonProperty(builder, "reasonKey", issue.ReasonKey, true);
                    AppendJsonProperty(builder, "caseId", issue.CaseId, true);
                    AppendJsonProperty(builder, "generatedPngPath", issue.GeneratedPngPath, false);
                    builder.Append('}');

                    if (index < issues.Count - 1)
                    {
                        builder.Append(',');
                    }
                }
            }

            builder.Append(']');
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendCases(StringBuilder builder, IReadOnlyList<TimelineReportStressCaseResult> cases, bool appendComma)
        {
            builder.Append("\"cases\":");
            builder.Append('[');

            if (cases != null)
            {
                for (int index = 0; index < cases.Count; index++)
                {
                    TimelineReportStressCaseResult item = cases[index];
                    builder.Append('{');
                    AppendJsonProperty(builder, "caseId", item.CaseId, true);
                    AppendJsonProperty(builder, "suite", item.Suite, true);
                    AppendJsonProperty(builder, "seed", item.Seed, true);
                    AppendJsonProperty(builder, "profile", item.Profile, true);
                    AppendJsonProperty(builder, "width", item.Width, true);
                    AppendJsonProperty(builder, "uniqueGroupCount", item.UniqueGroupCount, true);
                    AppendJsonProperty(builder, "duplicateDepth", item.DuplicateDepth, true);
                    AppendJsonProperty(builder, "totalEventCount", item.TotalEventCount, true);
                    AppendJsonProperty(builder, "hardFail", item.HardFail, true);
                    AppendJsonProperty(builder, "warning", item.Warning, true);
                    AppendJsonProperty(builder, "failureReason", item.FailureReason, true);
                    AppendJsonProperty(builder, "warningReason", item.WarningReason, true);
                    AppendJsonStringArray(builder, "failureReasons", item.FailureReasons, true);
                    AppendJsonStringArray(builder, "warningReasons", item.WarningReasons, true);
                    AppendJsonProperty(builder, "generatedPngPath", item.GeneratedPngPath, true);
                    AppendJsonProperty(builder, "timelineWidth", item.TimelineWidth, true);
                    AppendJsonProperty(builder, "renderedHeight", item.RenderedHeight, true);
                    AppendJsonProperty(builder, "maximumTimestampStack", item.MaximumTimestampStack, false);
                    builder.Append('}');

                    if (index < cases.Count - 1)
                    {
                        builder.Append(',');
                    }
                }
            }

            builder.Append(']');
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendJsonDictionary(StringBuilder builder, string name, IDictionary<string, int> values, bool appendComma)
        {
            AppendJsonPropertyName(builder, name);
            builder.Append('{');

            if (values != null)
            {
                int index = 0;
                foreach (KeyValuePair<string, int> pair in values)
                {
                    AppendEscapedString(builder, pair.Key);
                    builder.Append(':');
                    builder.Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                    if (index < values.Count - 1)
                    {
                        builder.Append(',');
                    }

                    index++;
                }
            }

            builder.Append('}');
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendJsonStringArray(StringBuilder builder, string name, IReadOnlyList<string> values, bool appendComma)
        {
            AppendJsonPropertyName(builder, name);
            builder.Append('[');

            if (values != null)
            {
                for (int index = 0; index < values.Count; index++)
                {
                    AppendEscapedString(builder, values[index]);
                    if (index < values.Count - 1)
                    {
                        builder.Append(',');
                    }
                }
            }

            builder.Append(']');
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendJsonDoubleArray(StringBuilder builder, string name, IReadOnlyList<double> values, bool appendComma)
        {
            AppendJsonPropertyName(builder, name);
            builder.Append('[');

            if (values != null)
            {
                for (int index = 0; index < values.Count; index++)
                {
                    builder.Append(values[index].ToString("0.##", CultureInfo.InvariantCulture));
                    if (index < values.Count - 1)
                    {
                        builder.Append(',');
                    }
                }
            }

            builder.Append(']');
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool appendComma)
        {
            AppendJsonPropertyName(builder, name);
            AppendEscapedString(builder, value);
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, int value, bool appendComma)
        {
            AppendJsonPropertyName(builder, name);
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, bool value, bool appendComma)
        {
            AppendJsonPropertyName(builder, name);
            builder.Append(value ? "true" : "false");
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, double value, bool appendComma)
        {
            AppendJsonPropertyName(builder, name);
            builder.Append(double.IsNaN(value) || double.IsInfinity(value)
                ? "null"
                : value.ToString("0.##", CultureInfo.InvariantCulture));
            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, double? value, bool appendComma)
        {
            AppendJsonPropertyName(builder, name);
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            {
                builder.Append("null");
            }
            else
            {
                builder.Append(value.Value.ToString("0.##", CultureInfo.InvariantCulture));
            }

            if (appendComma)
            {
                builder.Append(',');
            }
        }

        private static void AppendJsonPropertyName(StringBuilder builder, string name)
        {
            AppendEscapedString(builder, name);
            builder.Append(':');
        }

        private static void AppendEscapedString(StringBuilder builder, string value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(character))
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
