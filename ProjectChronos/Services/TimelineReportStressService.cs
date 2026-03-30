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
using ProjectChronos.Views;

namespace ProjectChronos.Services
{
    public sealed partial class TimelineReportStressService
    {
        private const double RootHorizontalMargin = 16.0;
        private const double RootBottomMargin = 16.0;
        private const double DetailBandTopMargin = 8.0;
        private const double FooterTopMargin = 34.0;
        private const double FooterHorizontalPadding = 34.0;
        private const double FooterVerticalPadding = 24.0;
        private const double FooterFontSize = 28.0;
        private const double FooterNoteGap = 8.0;
        private const double WarningCardWidthThreshold = 190.0;
        private const double WarningBodyWidthThreshold = 140.0;
        private const double WarningHeightRatio = 1.8;
        private const double PixelsPerDip = 1.0;

        private static readonly Typeface UiTypeface =
            new Typeface(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Typeface UiBoldTypeface =
            new Typeface(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private readonly TimelineReportExportService _exportService = new TimelineReportExportService();

        public TimelineReportStressSummary Run(TimelineReportStressOptions options)
        {
            TimelineReportStressOptions normalizedOptions = NormalizeOptions(options);
            string artifactRoot = normalizedOptions.ArtifactRoot;
            string flaggedDirectory = Path.Combine(artifactRoot, "flagged");
            string warningsDirectory = Path.Combine(artifactRoot, "warnings");
            string summaryJsonPath = Path.Combine(artifactRoot, "summary.json");
            string summaryMarkdownPath = Path.Combine(artifactRoot, "summary.md");
            string reviewMarkdownPath = Path.Combine(artifactRoot, "review.md");

            Directory.CreateDirectory(artifactRoot);
            Directory.CreateDirectory(flaggedDirectory);
            Directory.CreateDirectory(warningsDirectory);

            var deterministicCases = normalizedOptions.IncludeDeterministic
                ? BuildDeterministicCases(normalizedOptions).ToList()
                : new List<StressCaseSpec>();
            var randomCases = normalizedOptions.IncludeRandom
                ? BuildRandomCases(normalizedOptions).ToList()
                : new List<StressCaseSpec>();
            var allCases = new List<StressCaseSpec>(deterministicCases.Count + randomCases.Count);
            allCases.AddRange(deterministicCases);
            allCases.AddRange(randomCases);
            var results = new List<TimelineReportStressCaseResult>(allCases.Count);
            TimelineReportStressProgress progress = CreateProgress(normalizedOptions, allCases.Count, summaryJsonPath);

            WriteProgress(normalizedOptions.ProgressJsonPath, progress);

            try
            {
                for (int index = 0; index < allCases.Count; index++)
                {
                    StressCaseSpec stressCase = allCases[index];
                    progress.State = "running";
                    progress.CompletedCaseCount = index;
                    progress.CurrentCaseId = stressCase.CaseId;
                    progress.CurrentSuite = stressCase.Suite;
                    progress.CurrentProfile = stressCase.ProfileName;
                    progress.CurrentWidth = stressCase.Width;
                    progress.CurrentSeed = stressCase.Seed;
                    progress.HardFailureCount = results.Count(item => item.HardFail);
                    progress.WarningCount = results.Count(item => item.Warning);
                    progress.UpdatedAtLocal = GetCurrentTimestamp();
                    WriteProgress(normalizedOptions.ProgressJsonPath, progress);

                    TimelineReportStressCaseResult caseResult = RunCase(stressCase, normalizedOptions, flaggedDirectory, warningsDirectory);
                    results.Add(caseResult);

                    progress.CompletedCaseCount = index + 1;
                    progress.HardFailureCount = results.Count(item => item.HardFail);
                    progress.WarningCount = results.Count(item => item.Warning);
                    progress.UpdatedAtLocal = GetCurrentTimestamp();
                    progress.Note = caseResult.HardFail || caseResult.Warning
                        ? "problem-case-detected"
                        : "ok";
                    WriteProgress(normalizedOptions.ProgressJsonPath, progress);
                }

                var summary = BuildSummary(
                    normalizedOptions,
                    results,
                    artifactRoot,
                    flaggedDirectory,
                    warningsDirectory,
                    summaryJsonPath,
                    summaryMarkdownPath,
                    reviewMarkdownPath);

                WriteSummaryJson(summary, summaryJsonPath);
                WriteSummaryMarkdown(summary, summaryMarkdownPath);
                WriteLatestPointer(artifactRoot);

                progress.State = "completed";
                progress.CompletedCaseCount = results.Count;
                progress.HardFailureCount = summary.HardFailureCount;
                progress.WarningCount = summary.WarningCount;
                progress.CurrentCaseId = null;
                progress.CurrentSuite = null;
                progress.CurrentProfile = null;
                progress.CurrentWidth = 0.0;
                progress.CurrentSeed = 0;
                progress.SummaryJsonPath = summaryJsonPath;
                progress.Note = "summary-written";
                progress.UpdatedAtLocal = GetCurrentTimestamp();
                WriteProgress(normalizedOptions.ProgressJsonPath, progress);

                return summary;
            }
            catch (Exception ex)
            {
                progress.State = "failed";
                progress.CompletedCaseCount = results.Count;
                progress.HardFailureCount = results.Count(item => item.HardFail);
                progress.WarningCount = results.Count(item => item.Warning);
                progress.Note = ex.Message;
                progress.UpdatedAtLocal = GetCurrentTimestamp();
                WriteProgress(normalizedOptions.ProgressJsonPath, progress);
                throw;
            }
        }

        public TimelineReportStressSummary ReadSummaryFromJson(string summaryJsonPath)
        {
            if (string.IsNullOrWhiteSpace(summaryJsonPath))
            {
                throw new ArgumentException("Summary path is required.", nameof(summaryJsonPath));
            }

            var serializer = new DataContractJsonSerializer(typeof(TimelineReportStressSummary));
            using (var stream = new FileStream(summaryJsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                return (TimelineReportStressSummary)serializer.ReadObject(stream);
            }
        }

        public TimelineReportStressProgress ReadProgressFromJson(string progressJsonPath)
        {
            if (string.IsNullOrWhiteSpace(progressJsonPath))
            {
                throw new ArgumentException("Progress path is required.", nameof(progressJsonPath));
            }

            var serializer = new DataContractJsonSerializer(typeof(TimelineReportStressProgress));
            using (var stream = new FileStream(progressJsonPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                return (TimelineReportStressProgress)serializer.ReadObject(stream);
            }
        }

        public TimelineReportStressSummary WriteAggregateSummary(
            TimelineReportStressOptions options,
            IReadOnlyList<TimelineReportStressCaseResult> results,
            string artifactRoot,
            string flaggedDirectory,
            string warningsDirectory)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));

            TimelineReportStressOptions normalizedOptions = NormalizeOptions(options);
            string resolvedArtifactRoot = string.IsNullOrWhiteSpace(artifactRoot)
                ? normalizedOptions.ArtifactRoot
                : Path.GetFullPath(artifactRoot);
            string resolvedFlaggedDirectory = string.IsNullOrWhiteSpace(flaggedDirectory)
                ? Path.Combine(resolvedArtifactRoot, "flagged")
                : Path.GetFullPath(flaggedDirectory);
            string resolvedWarningsDirectory = string.IsNullOrWhiteSpace(warningsDirectory)
                ? Path.Combine(resolvedArtifactRoot, "warnings")
                : Path.GetFullPath(warningsDirectory);
            string summaryJsonPath = Path.Combine(resolvedArtifactRoot, "summary.json");
            string summaryMarkdownPath = Path.Combine(resolvedArtifactRoot, "summary.md");
            string reviewMarkdownPath = Path.Combine(resolvedArtifactRoot, "review.md");

            Directory.CreateDirectory(resolvedArtifactRoot);

            var summary = BuildSummary(
                normalizedOptions,
                results,
                resolvedArtifactRoot,
                resolvedFlaggedDirectory,
                resolvedWarningsDirectory,
                summaryJsonPath,
                summaryMarkdownPath,
                reviewMarkdownPath);

            WriteSummaryJson(summary, summaryJsonPath);
            WriteSummaryMarkdown(summary, summaryMarkdownPath);
            WriteLatestPointer(resolvedArtifactRoot);

            return summary;
        }

        private static TimelineReportStressOptions NormalizeOptions(TimelineReportStressOptions options)
        {
            TimelineReportStressOptions normalized = options ?? TimelineReportStressOptions.CreateDefault();

            if (!normalized.IncludeDeterministic && !normalized.IncludeRandom)
            {
                normalized.IncludeDeterministic = true;
                normalized.IncludeRandom = true;
            }

            if ((normalized.IncludeDeterministic &&
                (normalized.DeterministicWidths == null || normalized.DeterministicWidths.Count == 0 ||
                 normalized.DeterministicUniqueGroupCounts == null || normalized.DeterministicUniqueGroupCounts.Count == 0 ||
                 normalized.DeterministicDuplicateDepths == null || normalized.DeterministicDuplicateDepths.Count == 0)) ||
                (normalized.IncludeRandom &&
                (normalized.RandomWidths == null || normalized.RandomWidths.Count == 0 ||
                 normalized.RandomUniqueGroupCounts == null || normalized.RandomUniqueGroupCounts.Count == 0 ||
                 normalized.RandomSeedCount <= 0)))
            {
                normalized = TimelineReportStressOptions.CreateDefault(normalized.ArtifactRoot);
            }

            normalized.DeterministicWidths = normalized.IncludeDeterministic
                ? normalized.DeterministicWidths.Distinct().OrderBy(value => value).ToList()
                : new List<double>();
            normalized.DeterministicUniqueGroupCounts = normalized.IncludeDeterministic
                ? normalized.DeterministicUniqueGroupCounts.Distinct().OrderBy(value => value).ToList()
                : new List<int>();
            normalized.DeterministicDuplicateDepths = normalized.IncludeDeterministic
                ? normalized.DeterministicDuplicateDepths.Distinct().OrderBy(value => value).ToList()
                : new List<int>();
            normalized.RandomWidths = normalized.IncludeRandom
                ? normalized.RandomWidths.Distinct().OrderBy(value => value).ToList()
                : new List<double>();
            normalized.RandomUniqueGroupCounts = normalized.IncludeRandom
                ? normalized.RandomUniqueGroupCounts.Distinct().OrderBy(value => value).ToList()
                : new List<int>();
            normalized.ArtifactRoot = string.IsNullOrWhiteSpace(normalized.ArtifactRoot)
                ? TimelineReportStressOptions.CreateDefault().ArtifactRoot
                : Path.GetFullPath(normalized.ArtifactRoot);
            normalized.ProgressJsonPath = string.IsNullOrWhiteSpace(normalized.ProgressJsonPath)
                ? Path.Combine(normalized.ArtifactRoot, "progress.json")
                : Path.GetFullPath(normalized.ProgressJsonPath);
            normalized.RandomSeedStart = normalized.RandomSeedStart <= 0 ? 1 : normalized.RandomSeedStart;
            normalized.RandomSeedCount = normalized.IncludeRandom && normalized.RandomSeedCount <= 0 ? 20 : normalized.RandomSeedCount;

            return normalized;
        }

        private static TimelineReportStressProgress CreateProgress(
            TimelineReportStressOptions options,
            int totalCaseCount,
            string summaryJsonPath)
        {
            string timestamp = GetCurrentTimestamp();

            return new TimelineReportStressProgress
            {
                ArtifactRoot = options.ArtifactRoot,
                ChunkId = options.ChunkId,
                State = "starting",
                StartedAtLocal = timestamp,
                UpdatedAtLocal = timestamp,
                TotalCaseCount = totalCaseCount,
                CompletedCaseCount = 0,
                HardFailureCount = 0,
                WarningCount = 0,
                CurrentCaseId = null,
                CurrentSuite = null,
                CurrentProfile = null,
                CurrentWidth = 0.0,
                CurrentSeed = 0,
                SummaryJsonPath = summaryJsonPath,
                Note = "initialized"
            };
        }

        private static string GetCurrentTimestamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private TimelineReportStressCaseResult RunCase(
            StressCaseSpec stressCase,
            TimelineReportStressOptions options,
            string flaggedDirectory,
            string warningsDirectory)
        {
            var result = new TimelineReportStressCaseResult
            {
                CaseId = stressCase.CaseId,
                Suite = stressCase.Suite,
                Seed = stressCase.Seed,
                Profile = stressCase.ProfileName,
                Width = stressCase.Width,
                UniqueGroupCount = stressCase.UniqueGroupCount,
                DuplicateDepth = stressCase.MaximumTimestampStack,
                TotalEventCount = stressCase.Events.Count,
                FailureReasons = new List<string>(),
                WarningReasons = new List<string>(),
                MaximumTimestampStack = stressCase.MaximumTimestampStack
            };

            try
            {
                var input = CreateInput(stressCase.Width, stressCase.Events, Path.Combine(flaggedDirectory, stressCase.CaseId + ".png"));
                var viewModel = new ReportTimelineExportViewModel(input);

                result.TimelineWidth = viewModel.TimelineWidth;
                result.RenderedHeight = viewModel.RenderedCanvasHeight;

                ExerciseLayout(viewModel);
                ValidateGeometry(viewModel, result);
            }
            catch (Exception ex)
            {
                result.FailureReasons.Add("viewmodel_or_layout_exception: " + ex.Message);
            }

            result.HardFail = result.FailureReasons.Count > 0;
            result.Warning = result.WarningReasons.Count > 0;

            if (options.RenderProblemCases && (result.HardFail || result.Warning))
            {
                string outputDirectory = result.HardFail ? flaggedDirectory : warningsDirectory;
                string outputPath = Path.Combine(outputDirectory, stressCase.CaseId + ".png");

                try
                {
                    var exportInput = CreateInput(stressCase.Width, stressCase.Events, outputPath);
                    _exportService.Export(exportInput);
                    result.GeneratedPngPath = outputPath;
                }
                catch (Exception ex)
                {
                    result.FailureReasons.Add("render_exception: " + ex.Message);
                    result.GeneratedPngPath = null;
                }
            }

            result.FailureReason = JoinReasons(result.FailureReasons);
            result.WarningReason = JoinReasons(result.WarningReasons);
            result.HardFail = result.FailureReasons.Count > 0;
            result.Warning = result.WarningReasons.Count > 0;

            return result;
        }

        private static TimelineReportExportInput CreateInput(double width, IReadOnlyList<SimulationEventMarker> events, string outputPath)
        {
            return new TimelineReportExportInput(
                "스트레스 테스트 보고서",
                "Time-Event",
                Array.Empty<string>(),
                width,
                0.0,
                events ?? Array.Empty<SimulationEventMarker>(),
                outputPath);
        }

        private static void ExerciseLayout(ReportTimelineExportViewModel viewModel)
        {
            var view = new ReportTimelineExportView
            {
                DataContext = viewModel,
                Width = viewModel.RenderedCanvasWidth,
                Height = viewModel.RenderedCanvasHeight
            };

            view.Measure(new Size(viewModel.RenderedCanvasWidth, viewModel.RenderedCanvasHeight));
            view.Arrange(new Rect(0.0, 0.0, viewModel.RenderedCanvasWidth, viewModel.RenderedCanvasHeight));
            view.UpdateLayout();
        }

        private void ValidateGeometry(ReportTimelineExportViewModel viewModel, TimelineReportStressCaseResult result)
        {
            double availableContentWidth = Math.Max(0.0, viewModel.RenderedCanvasWidth - (RootHorizontalMargin * 2.0));
            double rootContentWidth = viewModel.RootContentWidth;

            if (rootContentWidth > availableContentWidth + 0.5)
            {
                result.FailureReasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "content_right_overflow: content width {0:0.##} exceeds available width {1:0.##}.",
                    rootContentWidth,
                    availableContentWidth));
            }

            if (viewModel.RenderedCanvasWidth < rootContentWidth + (RootHorizontalMargin * 2.0))
            {
                result.WarningReasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "timeline_width_margin_shortage: rendered width {0:0.##} is smaller than root content width + 32 ({1:0.##}).",
                    viewModel.RenderedCanvasWidth,
                    rootContentWidth + (RootHorizontalMargin * 2.0)));
            }

            if (viewModel.RenderedCanvasHeight > (viewModel.RenderedCanvasWidth * WarningHeightRatio))
            {
                result.WarningReasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "rendered_height_ratio_exceeded: height {0:0.##} exceeds width x 1.8 ({1:0.##}).",
                    viewModel.RenderedCanvasHeight,
                    viewModel.RenderedCanvasWidth * WarningHeightRatio));
            }

            ValidateBaseline(viewModel, result);
            ValidateOverviewItems(viewModel, result);
            ValidateDetailCards(viewModel, result);
            ValidateFooter(viewModel, result, availableContentWidth);

            if (result.MaximumTimestampStack > 4)
            {
                result.WarningReasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "timestamp_stack_over_4: stack depth {0} exceeds 4.",
                    result.MaximumTimestampStack));
            }
        }

        private void ValidateBaseline(ReportTimelineExportViewModel viewModel, TimelineReportStressCaseResult result)
        {
            if (viewModel.BaselineStartX < 0.0 || viewModel.BaselineEndX > viewModel.TimelineWidth + 0.5 || viewModel.BaselineY < 0.0 || viewModel.BaselineY > viewModel.OverviewHeight + 0.5)
            {
                result.FailureReasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "baseline_out_of_bounds: baseline ({0:0.##}, {1:0.##}, {2:0.##}) is outside overview bounds {3:0.##} x {4:0.##}.",
                    viewModel.BaselineStartX,
                    viewModel.BaselineEndX,
                    viewModel.BaselineY,
                    viewModel.TimelineWidth,
                    viewModel.OverviewHeight));
            }
        }

        private void ValidateOverviewItems(ReportTimelineExportViewModel viewModel, TimelineReportStressCaseResult result)
        {
            foreach (ReportTimelineSlotGroupItem slotItem in viewModel.SlotItems)
            {
                ValidateRect("slot_item", slotItem.Left, slotItem.Top, slotItem.Width, slotItem.Height, viewModel.TimelineWidth, viewModel.OverviewHeight, result);

                if (slotItem.TitleLeft < -0.5 || slotItem.TitleTop < -0.5 || slotItem.TitleLeft + slotItem.TitleWidth > slotItem.Width + 0.5)
                {
                    result.FailureReasons.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "slot_title_out_of_bounds: title frame ({0:0.##}, {1:0.##}, {2:0.##}) exceeds slot width {3:0.##}.",
                        slotItem.TitleLeft,
                        slotItem.TitleTop,
                        slotItem.TitleWidth,
                        slotItem.Width));
                }
            }

            foreach (ReportTimelineIntervalAnchorItem anchorItem in viewModel.IntervalAnchorItems)
            {
                if (anchorItem.X < -0.5 || anchorItem.X > viewModel.TimelineWidth + 0.5 || anchorItem.Top < -0.5 || anchorItem.Top + anchorItem.Height > viewModel.OverviewHeight + 0.5)
                {
                    result.FailureReasons.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "interval_anchor_out_of_bounds: anchor ({0:0.##}, {1:0.##}, {2:0.##}) exceeds overview bounds.",
                        anchorItem.X,
                        anchorItem.Top,
                        anchorItem.Height));
                }
            }

            foreach (ReportTimelineScaleBreakItem scaleBreakItem in viewModel.ScaleBreakItems)
            {
                ValidateRect("scale_break", scaleBreakItem.Left, scaleBreakItem.Top, scaleBreakItem.Width, scaleBreakItem.Height, viewModel.TimelineWidth, viewModel.OverviewHeight, result);
            }

            foreach (ReportTimelineMicroLabelItem microLabelItem in viewModel.MicroLabelItems)
            {
                Size labelSize = MeasureText(microLabelItem.Text, UiBoldTypeface, microLabelItem.FontSize, double.PositiveInfinity);
                ValidateRect("micro_label", microLabelItem.Left, microLabelItem.Top, labelSize.Width, labelSize.Height, viewModel.TimelineWidth, viewModel.OverviewHeight, result);
            }

            foreach (ReportTimelineSlotIntervalItem intervalItem in viewModel.IntervalItems)
            {
                ValidateRect("interval_item", intervalItem.Left, intervalItem.Top, intervalItem.Width, intervalItem.Height, viewModel.TimelineWidth, viewModel.OverviewHeight, result);

                Size labelSize = MeasureText(intervalItem.Label, UiBoldTypeface, intervalItem.FontSize, double.PositiveInfinity);
                if (intervalItem.LabelLeft < -0.5 || intervalItem.LabelLeft + labelSize.Width > intervalItem.Width + 0.5)
                {
                    result.FailureReasons.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "interval_label_out_of_bounds: label frame {0:0.##}-{1:0.##} exceeds interval width {2:0.##}.",
                        intervalItem.LabelLeft,
                        intervalItem.LabelLeft + labelSize.Width,
                        intervalItem.Width));
                }
            }
        }

        private void ValidateDetailCards(ReportTimelineExportViewModel viewModel, TimelineReportStressCaseResult result)
        {
            var cards = viewModel.DetailCardItems.ToList();

            foreach (ReportTimelineDetailCardItem card in cards)
            {
                if (card.Left < -0.5 || card.Left + card.Width > viewModel.TimelineWidth + 0.5)
                {
                    result.FailureReasons.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "detail_card_out_of_bounds_x: rect ({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##}) exceeds detail width {4:0.##}.",
                        card.Left,
                        card.Top,
                        card.Width,
                        card.Height,
                        viewModel.TimelineWidth));
                }

                if (card.Top + card.Height > viewModel.DetailBandHeight + 0.5)
                {
                    result.FailureReasons.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "detail_card_out_of_bounds_bottom: rect ({0:0.##}, {1:0.##}, {2:0.##}, {3:0.##}) exceeds detail height {4:0.##}.",
                        card.Left,
                        card.Top,
                        card.Width,
                        card.Height,
                        viewModel.DetailBandHeight));
                }

                if (card.Width < WarningCardWidthThreshold)
                {
                    result.WarningReasons.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "card_width_under_190: card width {0:0.##} is below 190.",
                        card.Width));
                }

                if (card.BodyTextWidth < WarningBodyWidthThreshold)
                {
                    result.WarningReasons.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "body_text_width_under_140: body width {0:0.##} is below 140.",
                        card.BodyTextWidth));
                }
            }

            for (int leftIndex = 0; leftIndex < cards.Count; leftIndex++)
            {
                for (int rightIndex = leftIndex + 1; rightIndex < cards.Count; rightIndex++)
                {
                    if (HasOverlap(cards[leftIndex], cards[rightIndex], 1.0))
                    {
                        result.FailureReasons.Add(string.Format(
                            CultureInfo.InvariantCulture,
                            "detail_card_overlap: cards '{0}' and '{1}' overlap by more than 1px.",
                            cards[leftIndex].Title ?? "Event",
                            cards[rightIndex].Title ?? "Event"));
                    }
                }
            }
        }

        private void ValidateFooter(ReportTimelineExportViewModel viewModel, TimelineReportStressCaseResult result, double availableContentWidth)
        {
            if (!viewModel.HasFooterNotes)
            {
                return;
            }

            double footerHeight = CalculateFooterHeight(viewModel.FooterNotes, viewModel.FooterWidth);
            double footerTop = viewModel.OverviewHeight;

            if (viewModel.DetailBandHeight > 0.0)
            {
                footerTop += DetailBandTopMargin + viewModel.DetailBandHeight;
            }

            footerTop += FooterTopMargin;

            if (viewModel.FooterWidth > availableContentWidth + 0.5)
            {
                result.FailureReasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "footer_right_overflow: footer width {0:0.##} exceeds available width {1:0.##}.",
                    viewModel.FooterWidth,
                    availableContentWidth));
            }

            if (footerTop < -0.5 || footerTop + footerHeight > viewModel.RenderedCanvasHeight - RootBottomMargin + 0.5)
            {
                result.FailureReasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "footer_bottom_overflow: footer bottom {0:0.##} exceeds allowed height {1:0.##}.",
                    footerTop + footerHeight,
                    viewModel.RenderedCanvasHeight - RootBottomMargin));
            }
        }

        private void ValidateRect(
            string prefix,
            double left,
            double top,
            double width,
            double height,
            double canvasWidth,
            double canvasHeight,
            TimelineReportStressCaseResult result)
        {
            if (left < -0.5 || top < -0.5)
            {
                result.FailureReasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}_negative_coordinate: rect ({1:0.##}, {2:0.##}, {3:0.##}, {4:0.##}) contains a negative coordinate.",
                    prefix,
                    left,
                    top,
                    width,
                    height));
            }

            if (left + width > canvasWidth + 0.5)
            {
                result.FailureReasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}_right_overflow: rect right {1:0.##} exceeds width {2:0.##}.",
                    prefix,
                    left + width,
                    canvasWidth));
            }

            if (top + height > canvasHeight + 0.5)
            {
                result.FailureReasons.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}_bottom_overflow: rect bottom {1:0.##} exceeds height {2:0.##}.",
                    prefix,
                    top + height,
                    canvasHeight));
            }
        }
    }
}
