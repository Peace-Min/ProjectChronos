using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using ProjectChronos.Core;
using ProjectChronos.Models;

namespace ProjectChronos.ViewModels
{
    public class TimelineReportExportInput
    {
        public TimelineReportExportInput(
            string title,
            string sectionLabel,
            IEnumerable<string> footerNotes,
            double canvasWidth,
            double canvasHeight,
            IReadOnlyList<SimulationEventMarker> events,
            string outputPath)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Timeline Report" : title;
            SectionLabel = string.IsNullOrWhiteSpace(sectionLabel) ? "Time-Event" : sectionLabel;
            FooterNotes = new List<string>(footerNotes ?? Enumerable.Empty<string>());
            CanvasWidth = canvasWidth > 0 ? canvasWidth : 1100.0;
            CanvasHeight = canvasHeight > 0 ? canvasHeight : 0.0;
            Events = events ?? Array.Empty<SimulationEventMarker>();
            OutputPath = outputPath;
        }

        public string Title { get; }
        public string SectionLabel { get; }
        public IReadOnlyList<string> FooterNotes { get; }
        public double CanvasWidth { get; }
        public double CanvasHeight { get; }
        public IReadOnlyList<SimulationEventMarker> Events { get; }
        public string OutputPath { get; }
    }

    public enum SlotIntervalLaneKind
    {
        Standard,
        Micro
    }

    public class ReportTimelineExportViewModel : ViewModelBase
    {
        private const double TimelineSideMargin = 56.0;
        private const double BaselineInset = 88.0;
        private const double BaselineYPosition = 224.0;
        private const double MinimumTimelineWidth = 900.0;
        private const double BaseSlotStemHeight = 104.0;
        private const double BaseSlotTitleGap = 18.0;
        private const double BaseSlotTitleWidthMin = 160.0;
        private const double BaseSlotTitleWidthMax = 240.0;
        private const double BaseSlotTitleFontSize = 24.0;
        private const double BaseSlotTimeFontSize = 16.0;
        private const double BaseSlotLabelLineGap = 4.0;
        private const double BaseSlotItemGap = 6.0;
        private const double BaseOverviewBottomPadding = 20.0;
        private const double BaseIntervalOffset = 92.0;
        private const double BaseMinimumStandardVisualSpan = 240.0;
        private const double BaseMinimumMicroVisualSpan = 168.0;
        private const double BaseIntervalPadding = 24.0;
        private const double BaseIntervalAnchorGap = 4.0;
        private const double BaseIntervalChevronThreshold = 20.0;
        private const double BaseIntervalMinimumArrowSpan = 60.0;
        private const double BaseStandardIntervalFontSize = 30.0;
        private const double BaseMicroIntervalFontSize = 30.0;
        private const double DetailTopPadding = 8.0;
        private const double DetailBottomPadding = 8.0;
        private const double DetailCardGap = 24.0;
        private const double SameTimestampDetailCardGap = 10.0;
        private const double BaseDetailColumnGap = 28.0;
        private const double OverviewOuterMargin = 16.0;
        private const double RootBottomMargin = 16.0;
        private const double DetailBandTopMargin = 8.0;
        private const double BaseCardWidthMin = 250.0;
        private const double BaseCardWidthMax = 430.0;
        private const double BaseCardTimestampFontSize = 16.0;
        private const double BaseCardTitleFontSize = 24.0;
        private const double BaseCardBodyFontSize = 18.0;
        private const double CardLeftPadding = 18.0;
        private const double CardRightPadding = 18.0;
        private const double CardTopPadding = 16.0;
        private const double CardBottomPadding = 16.0;
        private const double CardBulletWidth = 18.0;
        private const double CardDividerGapBefore = 8.0;
        private const double CardDividerGapAfter = 12.0;
        private const double CardDescriptionGap = 12.0;
        private const double CardRangeGap = 8.0;
        private const double FooterWidthInset = 260.0;
        private const double FooterTopMargin = 34.0;
        private const double FooterHorizontalPadding = 34.0;
        private const double FooterVerticalPadding = 24.0;
        private const double FooterFontSize = 28.0;
        private const double FooterNoteGap = 8.0;
        private const double PixelsPerDip = 1.0;

        private static readonly Typeface UiTypeface =
            new Typeface(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Typeface UiBoldTypeface =
            new Typeface(new FontFamily("Malgun Gothic"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private static readonly Typeface MonoTypeface =
            new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        public ReportTimelineExportViewModel(TimelineReportExportInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            Title = input.Title;
            SectionLabel = input.SectionLabel;
            CanvasWidth = input.CanvasWidth;
            CanvasHeight = input.CanvasHeight;
            TimelineWidth = Math.Max(MinimumTimelineWidth, CanvasWidth - (TimelineSideMargin * 2.0));
            BaselineStartX = BaselineInset;
            BaselineEndX = TimelineWidth - BaselineInset;
            BaselineY = BaselineYPosition;
            FooterWidth = Math.Max(680.0, TimelineWidth - FooterWidthInset);
            FooterNotes = new ObservableCollection<string>((input.FooterNotes ?? Array.Empty<string>()).Take(3));
            SlotItems = new ObservableCollection<ReportTimelineSlotGroupItem>();
            IntervalAnchorItems = new ObservableCollection<ReportTimelineIntervalAnchorItem>();
            IntervalItems = new ObservableCollection<ReportTimelineSlotIntervalItem>();
            DetailCardItems = new ObservableCollection<ReportTimelineDetailCardItem>();
            OverviewHeight = BaselineY + 220.0;

            BuildLayout(input.Events ?? Array.Empty<SimulationEventMarker>());
            RenderedCanvasHeight = CalculateRenderedCanvasHeight();
        }

        public string Title { get; }
        public string SectionLabel { get; }
        public double CanvasWidth { get; }
        public double CanvasHeight { get; }
        public double TimelineWidth { get; private set; }
        public double OverviewHeight { get; private set; }
        public double DetailBandHeight { get; private set; }
        public double FooterWidth { get; private set; }
        public double RenderedCanvasHeight { get; }
        public double BaselineStartX { get; private set; }
        public double BaselineEndX { get; private set; }
        public double BaselineY { get; private set; }
        public bool HasFooterNotes => FooterNotes.Count > 0;
        public ObservableCollection<string> FooterNotes { get; }
        public ObservableCollection<ReportTimelineSlotGroupItem> SlotItems { get; }
        public ObservableCollection<ReportTimelineIntervalAnchorItem> IntervalAnchorItems { get; }
        public ObservableCollection<ReportTimelineSlotIntervalItem> IntervalItems { get; }
        public ObservableCollection<ReportTimelineDetailCardItem> DetailCardItems { get; }

        private void BuildLayout(IReadOnlyList<SimulationEventMarker> sourceEvents)
        {
            var orderedEvents = sourceEvents
                .Select((item, index) => new IndexedEvent(item, index))
                .Where(entry => entry.Event != null)
                .OrderBy(entry => entry.Event.Timestamp)
                .ThenBy(entry => entry.Index)
                .ToList();

            if (orderedEvents.Count == 0)
            {
                DetailBandHeight = 0.0;
                return;
            }

            var groups = orderedEvents
                .GroupBy(entry => Math.Round(entry.Event.Timestamp, 3))
                .OrderBy(group => group.Key)
                .Select((group, index) => new SlotGroup
                {
                    Index = index,
                    Timestamp = group.First().Event.Timestamp,
                    Events = group.Select(entry => entry.Event).ToList(),
                    Priority = ResolveGroupPriority(group.Select(entry => entry.Event))
                })
                .ToList();

            double usableWidth = Math.Max(1.0, BaselineEndX - BaselineStartX);
            double slotPitch = groups.Count == 1 ? usableWidth : usableWidth / (groups.Count - 1);
            double layoutScale = DetermineLayoutScale(slotPitch);

            foreach (var group in groups)
            {
                group.CenterX = groups.Count == 1
                    ? (BaselineStartX + BaselineEndX) / 2.0
                    : BaselineStartX + (slotPitch * group.Index);
            }

            BuildSlotItems(groups, slotPitch, layoutScale);
            BuildIntervalItems(groups, layoutScale);
            NormalizeOverviewMargins();
            BuildDetailCards(groups, slotPitch, layoutScale);
        }

        private void BuildSlotItems(IReadOnlyList<SlotGroup> groups, double slotPitch, double layoutScale)
        {
            double slotTitleWidth = groups.Count == 1
                ? BaseSlotTitleWidthMax
                : Clamp(slotPitch - (70.0 * layoutScale), BaseSlotTitleWidthMin * layoutScale, BaseSlotTitleWidthMax);
            double slotTitleFontSize = BaseSlotTitleFontSize * layoutScale;
            double slotTimeFontSize = BaseSlotTimeFontSize * layoutScale;
            double slotStemHeight = BaseSlotStemHeight * layoutScale;
            double slotTitleGap = BaseSlotTitleGap * layoutScale;
            double lineGap = BaseSlotLabelLineGap * layoutScale;
            double itemGap = BaseSlotItemGap * layoutScale;
            double maxBottom = BaselineY;

            foreach (var group in groups)
            {
                var titles = new ObservableCollection<ReportTimelineSlotTitleItem>();
                double titlesHeight = 0.0;

                for (int eventIndex = 0; eventIndex < group.Events.Count; eventIndex++)
                {
                    SimulationEventMarker simulationEvent = group.Events[eventIndex];
                    string title = FormatSlotTitle(simulationEvent);
                    string time = FormatSlotTime(simulationEvent);
                    Size titleSize = MeasureText(title, UiBoldTypeface, slotTitleFontSize, slotTitleWidth);
                    Size timeSize = MeasureText(time, MonoTypeface, slotTimeFontSize, slotTitleWidth);
                    double itemHeight = titleSize.Height + lineGap + timeSize.Height;
                    double bottomGap = eventIndex < group.Events.Count - 1 ? itemGap : 0.0;

                    titles.Add(new ReportTimelineSlotTitleItem
                    {
                        Margin = new Thickness(0.0, 0.0, 0.0, bottomGap),
                        TimeFontSize = slotTimeFontSize,
                        TimeForeground = GetSlotTimeBrush(),
                        TimeText = time,
                        TitleFontSize = slotTitleFontSize,
                        TitleForeground = GetPriorityBrush(simulationEvent.Priority),
                        TitleText = title,
                        Width = slotTitleWidth
                    });

                    titlesHeight += itemHeight + bottomGap;
                }

                double width = slotTitleWidth + (24.0 * layoutScale);
                double left = Clamp(group.CenterX - (width / 2.0), 0.0, TimelineWidth - width);
                double lineX = group.CenterX - left;
                double height = slotStemHeight + slotTitleGap + titlesHeight;

                SlotItems.Add(new ReportTimelineSlotGroupItem
                {
                    Height = height,
                    Left = left,
                    LineX = lineX,
                    StemHeight = slotStemHeight,
                    TitleLeft = (width - slotTitleWidth) / 2.0,
                    TitleTop = slotStemHeight + slotTitleGap,
                    TitleWidth = slotTitleWidth,
                    Top = BaselineY,
                    Width = width,
                    Titles = titles
                });

                maxBottom = Math.Max(maxBottom, BaselineY + height);
            }

            OverviewHeight = Math.Max(BaselineY + 180.0, maxBottom + BaseOverviewBottomPadding);
        }

        private void BuildIntervalItems(IReadOnlyList<SlotGroup> groups, double layoutScale)
        {
            if (groups.Count < 2)
            {
                return;
            }

            double intervalOffset = BaseIntervalOffset * layoutScale;
            double minimumStandardVisualSpan = BaseMinimumStandardVisualSpan * layoutScale;
            double minimumMicroVisualSpan = BaseMinimumMicroVisualSpan * layoutScale;
            double standardFontSize = BaseStandardIntervalFontSize * layoutScale;
            double microFontSize = BaseMicroIntervalFontSize * layoutScale;
            double absoluteLineY = BaselineY - intervalOffset;
            double anchorBottomAbsolute = BaselineY - (6.0 * layoutScale);

            IntervalAnchorItems.Clear();

            foreach (var group in groups)
            {
                IntervalAnchorItems.Add(new ReportTimelineIntervalAnchorItem
                {
                    Height = Math.Max(1.0, anchorBottomAbsolute - absoluteLineY),
                    Stroke = GetIntervalBrush(0),
                    Top = absoluteLineY,
                    X = group.CenterX
                });
            }

            for (int index = 0; index < groups.Count - 1; index++)
            {
                var current = groups[index];
                var next = groups[index + 1];
                double duration = Math.Max(0.0, next.Timestamp - current.Timestamp);
                var laneKind = duration < 1.0 ? SlotIntervalLaneKind.Micro : SlotIntervalLaneKind.Standard;
                double fontSize = laneKind == SlotIntervalLaneKind.Micro ? microFontSize : standardFontSize;
                double anchorGap = Clamp(BaseIntervalAnchorGap * layoutScale, 3.0, 5.0);
                string label = FormatDuration(duration);
                Size labelSize = MeasureText(label, UiBoldTypeface, fontSize, double.PositiveInfinity);
                double actualStart = current.CenterX;
                double actualEnd = next.CenterX;
                double actualSpan = Math.Abs(actualEnd - actualStart);
                double minimumArrowVisualSpan = BaseIntervalMinimumArrowSpan + (anchorGap * 2.0);
                double minimumVisualSpan = laneKind == SlotIntervalLaneKind.Micro
                    ? Math.Max(Math.Max(labelSize.Width + (52.0 * layoutScale), minimumMicroVisualSpan), minimumArrowVisualSpan)
                    : Math.Max(Math.Max(labelSize.Width + (72.0 * layoutScale), minimumStandardVisualSpan), minimumArrowVisualSpan);
                double visualStart = actualStart;
                double visualEnd = actualEnd;

                if (actualSpan < minimumVisualSpan)
                {
                    double visualLeft = Clamp(
                        ((actualStart + actualEnd) / 2.0) - (minimumVisualSpan / 2.0),
                        BaselineStartX,
                        BaselineEndX - minimumVisualSpan);
                    visualStart = visualLeft;
                    visualEnd = visualLeft + minimumVisualSpan;
                }

                double top = absoluteLineY - labelSize.Height - (16.0 * layoutScale);
                double baselineLocal = Math.Max(0.0, anchorBottomAbsolute - top);
                double lineYLocal = absoluteLineY - top;
                double labelLeftAbsolute = ((visualStart + visualEnd) / 2.0) - (labelSize.Width / 2.0);
                double left = Math.Min(visualStart, labelLeftAbsolute) - (18.0 * layoutScale);
                double right = Math.Max(visualEnd, labelLeftAbsolute + labelSize.Width) + (18.0 * layoutScale);
                double width = Math.Max(1.0, right - left);
                double lineStartLocal = visualStart - left;
                double lineEndLocal = visualEnd - left;
                double arrowStartLocal = lineStartLocal + anchorGap;
                double arrowEndLocal = lineEndLocal - anchorGap;
                double availableArrowSpan = Math.Max(0.0, arrowEndLocal - arrowStartLocal);
                bool showChevronHeads = availableArrowSpan > BaseIntervalChevronThreshold;
                Geometry arrowGeometry = showChevronHeads
                    ? BuildArrowGeometry(arrowStartLocal, arrowEndLocal, lineYLocal)
                    : Geometry.Empty;

                IntervalItems.Add(new ReportTimelineSlotIntervalItem
                {
                    ArrowGeometry = arrowGeometry,
                    FontSize = fontSize,
                    FrameGeometry = BuildIntervalFrameGeometry(
                        arrowStartLocal,
                        arrowEndLocal,
                        lineYLocal),
                    Height = Math.Max(1.0, baselineLocal + (8.0 * layoutScale)),
                    Label = label,
                    LabelLeft = labelLeftAbsolute - left,
                    Left = left,
                    ShowChevronHeads = showChevronHeads,
                    Stroke = GetIntervalBrush(index),
                    Top = top,
                    Width = width
                });
            }
        }

        private void BuildDetailCards(IReadOnlyList<SlotGroup> groups, double slotPitch, double layoutScale)
        {
            double preferredCardWidth = groups.Count == 1
                ? BaseCardWidthMax
                : Clamp(slotPitch - (42.0 * layoutScale), BaseCardWidthMin * layoutScale, BaseCardWidthMax);
            double minimumColumnGap = BaseDetailColumnGap * layoutScale;
            double maximumCardWidthToFit = groups.Count <= 1
                ? preferredCardWidth
                : Math.Max(1.0, (TimelineWidth - (minimumColumnGap * (groups.Count - 1))) / groups.Count);
            double cardWidth = groups.Count == 1
                ? preferredCardWidth
                : Math.Min(preferredCardWidth, maximumCardWidthToFit);
            double timestampFontSize = BaseCardTimestampFontSize * layoutScale;
            double titleFontSize = BaseCardTitleFontSize * layoutScale;
            double bodyFontSize = BaseCardBodyFontSize * layoutScale;
            double columnBottom = 0.0;
            List<double> columnLefts = BuildDetailColumnLefts(groups, cardWidth, minimumColumnGap);

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                double columnTop = DetailTopPadding;
                double left = columnLefts[groupIndex];
                double textWidth = Math.Max(120.0, cardWidth - CardLeftPadding - CardRightPadding);
                double bodyTextWidth = Math.Max(100.0, textWidth - CardBulletWidth);
                double sameTimestampGap = SameTimestampDetailCardGap * layoutScale;
                double lastGap = 0.0;

                foreach (var simulationEvent in group.Events)
                {
                    string title = string.IsNullOrWhiteSpace(simulationEvent.Title)
                        ? "Event"
                        : simulationEvent.Title.Trim();
                    string timestampText = string.Format(
                        CultureInfo.InvariantCulture,
                        "Time: {0:F2}s",
                        simulationEvent.Timestamp);
                    bool hasDescription = !string.IsNullOrWhiteSpace(simulationEvent.DescriptionLabel) &&
                        !string.IsNullOrWhiteSpace(simulationEvent.Description);
                    bool hasRange = !string.IsNullOrWhiteSpace(simulationEvent.RangeBTWLabel) &&
                        !string.IsNullOrWhiteSpace(simulationEvent.RangeBTW);
                    bool hasSourceTarget = !string.IsNullOrWhiteSpace(simulationEvent.SourceTargetLabel) &&
                        !string.IsNullOrWhiteSpace(simulationEvent.SourceTarget);
                    string descriptionText = hasDescription
                        ? string.Format(
                            CultureInfo.CurrentCulture,
                            "{0} : {1}",
                            simulationEvent.DescriptionLabel.Trim(),
                            simulationEvent.Description.Trim())
                        : string.Empty;
                    string rangeText = hasRange
                        ? string.Format(
                            CultureInfo.CurrentCulture,
                            "{0} : {1}",
                            simulationEvent.RangeBTWLabel.Trim(),
                            simulationEvent.RangeBTW.Trim())
                        : string.Empty;
                    string sourceTargetText = hasSourceTarget
                        ? string.Format(
                            CultureInfo.CurrentCulture,
                            "{0} : {1}",
                            simulationEvent.SourceTargetLabel.Trim(),
                            simulationEvent.SourceTarget.Trim())
                        : string.Empty;

                    Size timestampSize = MeasureText(timestampText, MonoTypeface, timestampFontSize, textWidth);
                    double headerTitleWidth = Math.Max(72.0, textWidth - timestampSize.Width - (12.0 * layoutScale));
                    Size titleSize = MeasureText(title, UiBoldTypeface, titleFontSize, headerTitleWidth);
                    Size descriptionSize = hasDescription
                        ? MeasureText(descriptionText, UiTypeface, bodyFontSize, bodyTextWidth)
                        : new Size(0.0, 0.0);
                    Size rangeSize = hasRange
                        ? MeasureText(rangeText, UiTypeface, bodyFontSize, bodyTextWidth)
                        : new Size(0.0, 0.0);
                    Size sourceTargetSize = hasSourceTarget
                        ? MeasureText(sourceTargetText, UiTypeface, bodyFontSize, bodyTextWidth)
                        : new Size(0.0, 0.0);

                    double height = CardTopPadding;
                    height += Math.Max(timestampSize.Height, titleSize.Height);
                    height += CardDividerGapBefore;
                    height += 1.0;
                    height += CardDividerGapAfter;

                    if (hasDescription)
                    {
                        height += CardDescriptionGap;
                        height += descriptionSize.Height;
                    }

                    if (hasRange)
                    {
                        height += hasDescription ? CardRangeGap : CardDescriptionGap;
                        height += rangeSize.Height;
                    }

                    if (hasSourceTarget)
                    {
                        height += (hasDescription || hasRange) ? CardRangeGap : CardDescriptionGap;
                        height += sourceTargetSize.Height;
                    }

                    height += CardBottomPadding;

                    DetailCardItems.Add(new ReportTimelineDetailCardItem
                    {
                        AccentBrush = GetPriorityBrush(simulationEvent.Priority),
                        BodyFontSize = bodyFontSize,
                        BodyTextWidth = bodyTextWidth,
                        DescriptionLabel = hasDescription ? simulationEvent.DescriptionLabel.Trim() : string.Empty,
                        DescriptionValue = hasDescription ? simulationEvent.Description.Trim() : string.Empty,
                        HasDescription = hasDescription,
                        HasRange = hasRange,
                        HasSourceTarget = hasSourceTarget,
                        Height = height,
                        HeaderTitleWidth = headerTitleWidth,
                        Left = left,
                        RangeLabel = hasRange ? simulationEvent.RangeBTWLabel.Trim() : string.Empty,
                        RangeValue = hasRange ? simulationEvent.RangeBTW.Trim() : string.Empty,
                        SourceTargetLabel = hasSourceTarget ? simulationEvent.SourceTargetLabel.Trim() : string.Empty,
                        SourceTargetValue = hasSourceTarget ? simulationEvent.SourceTarget.Trim() : string.Empty,
                        TextContentWidth = textWidth,
                        TimestampFontSize = timestampFontSize,
                        TimestampText = timestampText,
                        Title = title,
                        TitleFontSize = titleFontSize,
                        Top = columnTop,
                        Width = cardWidth
                    });

                    lastGap = sameTimestampGap;
                    columnTop += height + lastGap;
                }

                columnBottom = Math.Max(columnBottom, columnTop - lastGap);
            }

            DetailBandHeight = columnBottom <= 0.0
                ? 0.0
                : columnBottom + DetailBottomPadding;
        }

        private List<double> BuildDetailColumnLefts(IReadOnlyList<SlotGroup> groups, double cardWidth, double minimumColumnGap)
        {
            var lefts = new List<double>(groups.Count);

            for (int index = 0; index < groups.Count; index++)
            {
                double idealLeft = Clamp(groups[index].CenterX - (cardWidth / 2.0), 0.0, TimelineWidth - cardWidth);

                if (index > 0)
                {
                    idealLeft = Math.Max(idealLeft, lefts[index - 1] + cardWidth + minimumColumnGap);
                }

                lefts.Add(idealLeft);
            }

            if (lefts.Count == 0)
            {
                return lefts;
            }

            double overflow = (lefts[lefts.Count - 1] + cardWidth) - TimelineWidth;
            if (overflow <= 0.0)
            {
                return lefts;
            }

            lefts[lefts.Count - 1] -= overflow;

            for (int index = lefts.Count - 2; index >= 0; index--)
            {
                double maximumAllowedLeft = lefts[index + 1] - cardWidth - minimumColumnGap;
                lefts[index] = Math.Min(lefts[index], maximumAllowedLeft);
            }

            if (lefts[0] >= 0.0)
            {
                return lefts;
            }

            double shiftRight = -lefts[0];
            for (int index = 0; index < lefts.Count; index++)
            {
                lefts[index] += shiftRight;
            }

            return lefts;
        }

        private static EventPriority ResolveGroupPriority(IEnumerable<SimulationEventMarker> events)
        {
            if (events.Any(item => item.Priority == EventPriority.High))
            {
                return EventPriority.High;
            }

            if (events.Any(item => item.Priority == EventPriority.Medium))
            {
                return EventPriority.Medium;
            }

            return EventPriority.Low;
        }

        private static double DetermineLayoutScale(double slotPitch)
        {
            if (slotPitch >= 420.0)
            {
                return 1.0;
            }

            if (slotPitch >= 340.0)
            {
                return 0.92;
            }

            if (slotPitch >= 280.0)
            {
                return 0.84;
            }

            return 0.78;
        }

        private void NormalizeOverviewMargins()
        {
            double minTop = double.PositiveInfinity;
            double maxBottom = double.NegativeInfinity;
            double minLeft = Math.Min(BaselineStartX, BaselineEndX);
            double maxRight = Math.Max(BaselineStartX, BaselineEndX);

            foreach (var intervalItem in IntervalItems)
            {
                minTop = Math.Min(minTop, intervalItem.Top);
                maxBottom = Math.Max(maxBottom, intervalItem.Top + intervalItem.Height);
                minLeft = Math.Min(minLeft, intervalItem.Left);
                maxRight = Math.Max(maxRight, intervalItem.Left + intervalItem.Width);
            }

            foreach (var anchorItem in IntervalAnchorItems)
            {
                minTop = Math.Min(minTop, anchorItem.Top);
                maxBottom = Math.Max(maxBottom, anchorItem.Top + anchorItem.Height);
                minLeft = Math.Min(minLeft, anchorItem.X);
                maxRight = Math.Max(maxRight, anchorItem.X);
            }

            foreach (var slotItem in SlotItems)
            {
                minTop = Math.Min(minTop, slotItem.Top);
                maxBottom = Math.Max(maxBottom, slotItem.Top + slotItem.Height);
                minLeft = Math.Min(minLeft, slotItem.Left);
                maxRight = Math.Max(maxRight, slotItem.Left + slotItem.Width);
            }

            if (double.IsInfinity(minTop) || double.IsInfinity(maxBottom))
            {
                return;
            }

            double shiftY = OverviewOuterMargin - minTop;
            double shiftX = OverviewOuterMargin - minLeft;
            BaselineY += shiftY;
            BaselineStartX += shiftX;
            BaselineEndX += shiftX;

            foreach (var intervalItem in IntervalItems)
            {
                intervalItem.Top += shiftY;
                intervalItem.Left += shiftX;
            }

            foreach (var anchorItem in IntervalAnchorItems)
            {
                anchorItem.Top += shiftY;
                anchorItem.X += shiftX;
            }

            foreach (var slotItem in SlotItems)
            {
                slotItem.Top += shiftY;
                slotItem.Left += shiftX;
            }

            TimelineWidth = Math.Ceiling(maxRight + shiftX + OverviewOuterMargin);
            FooterWidth = Math.Max(680.0, TimelineWidth - FooterWidthInset);
            OverviewHeight = Math.Ceiling(maxBottom + shiftY + OverviewOuterMargin);
        }

        private double CalculateRenderedCanvasHeight()
        {
            double totalHeight = OverviewHeight;

            if (DetailBandHeight > 0.0)
            {
                totalHeight += DetailBandTopMargin + DetailBandHeight;
            }

            if (HasFooterNotes)
            {
                totalHeight += FooterTopMargin + CalculateFooterContentHeight();
            }

            totalHeight += RootBottomMargin;
            return Math.Ceiling(totalHeight);
        }

        private double CalculateFooterContentHeight()
        {
            double textWidth = Math.Max(120.0, FooterWidth - (FooterHorizontalPadding * 2.0));
            double contentHeight = FooterVerticalPadding * 2.0;

            for (int index = 0; index < FooterNotes.Count; index++)
            {
                Size noteSize = MeasureText(FooterNotes[index], UiTypeface, FooterFontSize, textWidth);
                contentHeight += noteSize.Height;

                if (index < FooterNotes.Count - 1)
                {
                    contentHeight += FooterNoteGap;
                }
            }

            return contentHeight;
        }

        private static Brush GetPriorityBrush(EventPriority priority)
        {
            return CreateBrush(0x00, 0x72, 0xBD);
        }

        private static Brush GetSlotTimeBrush()
        {
            return CreateBrush(0x5E, 0x74, 0x8C);
        }

        private static Brush GetIntervalBrush(int index)
        {
            return CreateBrush(0xD9, 0x53, 0x19);
        }

        private static Brush CreateBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static int AssignLane(IList<List<Range>> lanes, double start, double end)
        {
            int laneIndex = 0;

            while (true)
            {
                if (lanes.Count <= laneIndex)
                {
                    lanes.Add(new List<Range>());
                    lanes[laneIndex].Add(new Range(start, end));
                    return laneIndex;
                }

                if (!lanes[laneIndex].Any(existing => existing.Overlaps(start, end)))
                {
                    lanes[laneIndex].Add(new Range(start, end));
                    return laneIndex;
                }

                laneIndex++;
            }
        }

        private static string FormatDuration(double seconds)
        {
            if (Math.Abs(seconds - Math.Round(seconds)) < 0.005)
            {
                return string.Format(CultureInfo.InvariantCulture, "{0:0}\uCD08", seconds);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.##}\uCD08", seconds);
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

        private static Geometry BuildIntervalFrameGeometry(
            double startX,
            double endX,
            double lineY)
        {
            string path = string.Format(
                CultureInfo.InvariantCulture,
                "M {0:0.##},{2:0.##} L {1:0.##},{2:0.##}",
                startX,
                endX,
                lineY);
            return Geometry.Parse(path);
        }

        private static Geometry BuildArrowGeometry(
            double startX,
            double endX,
            double y)
        {
            double availableSpan = Math.Max(0.0, endX - startX);
            double armLength = Clamp(availableSpan * 0.14, 4.2, 7.2);
            double headHeight = Clamp(armLength * 0.52, 1.8, 3.2);
            string path = string.Format(
                CultureInfo.InvariantCulture,
                "M {0:0.##},{4:0.##} L {1:0.##},{5:0.##} L {0:0.##},{6:0.##} " +
                "M {2:0.##},{4:0.##} L {3:0.##},{5:0.##} L {2:0.##},{6:0.##}",
                startX + armLength,
                startX,
                endX - armLength,
                endX,
                y - headHeight,
                y,
                y + headHeight);
            return Geometry.Parse(path);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static string FormatSlotTitle(SimulationEventMarker simulationEvent)
        {
            return string.IsNullOrWhiteSpace(simulationEvent.Title)
                ? "Event"
                : simulationEvent.Title.Trim();
        }

        private static string FormatSlotTime(SimulationEventMarker simulationEvent)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F2}s", simulationEvent.Timestamp);
        }

        private sealed class SlotGroup
        {
            public List<SimulationEventMarker> Events { get; set; }
            public int Index { get; set; }
            public EventPriority Priority { get; set; }
            public double Timestamp { get; set; }
            public double CenterX { get; set; }
        }

        private sealed class IndexedEvent
        {
            public IndexedEvent(SimulationEventMarker simulationEvent, int index)
            {
                Event = simulationEvent;
                Index = index;
            }

            public SimulationEventMarker Event { get; }
            public int Index { get; }
        }

        private sealed class Range
        {
            public Range(double start, double end)
            {
                Start = start;
                End = end;
            }

            public double Start { get; }
            public double End { get; }

            public bool Overlaps(double start, double end)
            {
                return Math.Max(Start, start) < Math.Min(End, end);
            }
        }
    }

    public class ReportTimelineSlotGroupItem
    {
        public double Height { get; set; }
        public double Left { get; set; }
        public double LineX { get; set; }
        public double StemHeight { get; set; }
        public ObservableCollection<ReportTimelineSlotTitleItem> Titles { get; set; }
        public double TitleLeft { get; set; }
        public double TitleTop { get; set; }
        public double TitleWidth { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
    }

    public class ReportTimelineSlotTitleItem
    {
        public Thickness Margin { get; set; }
        public double TimeFontSize { get; set; }
        public Brush TimeForeground { get; set; }
        public string TimeText { get; set; }
        public double TitleFontSize { get; set; }
        public Brush TitleForeground { get; set; }
        public string TitleText { get; set; }
        public double Width { get; set; }
    }

    public class ReportTimelineSlotIntervalItem
    {
        public Geometry ArrowGeometry { get; set; }
        public double FontSize { get; set; }
        public Geometry FrameGeometry { get; set; }
        public double Height { get; set; }
        public string Label { get; set; }
        public double LabelLeft { get; set; }
        public double Left { get; set; }
        public bool ShowChevronHeads { get; set; }
        public Brush Stroke { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
    }

    public class ReportTimelineIntervalAnchorItem
    {
        public double Height { get; set; }
        public Brush Stroke { get; set; }
        public double Top { get; set; }
        public double X { get; set; }
    }

    public class ReportTimelineDetailCardItem
    {
        public Brush AccentBrush { get; set; }
        public double BodyFontSize { get; set; }
        public double BodyTextWidth { get; set; }
        public string DescriptionLabel { get; set; }
        public string DescriptionValue { get; set; }
        public bool HasDescription { get; set; }
        public bool HasRange { get; set; }
        public bool HasSourceTarget { get; set; }
        public double Height { get; set; }
        public double HeaderTitleWidth { get; set; }
        public double Left { get; set; }
        public string RangeLabel { get; set; }
        public string RangeValue { get; set; }
        public string SourceTargetLabel { get; set; }
        public string SourceTargetValue { get; set; }
        public double TextContentWidth { get; set; }
        public double TimestampFontSize { get; set; }
        public string TimestampText { get; set; }
        public string Title { get; set; }
        public double TitleFontSize { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
    }
}
