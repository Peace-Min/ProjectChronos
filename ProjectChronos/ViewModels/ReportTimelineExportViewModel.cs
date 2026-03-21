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
        private const double BaseSlotNodeDiameter = 14.0;
        private const double BaseSlotTimeGap = 6.0;
        private const double BaseSlotTimeToTitleGap = 12.0;
        private const double BaseSlotStemEndGap = 8.0;
        private const double BaseSlotTitleWidthMin = 160.0;
        private const double BaseSlotTitleWidthMax = 240.0;
        private const double BaseSlotTitleFontSize = 15.0;
        private const double BaseSlotTimeFontSize = 12.0;
        private const double BaseSlotItemGap = 6.0;
        private const double BaseAxisLabelFontSize = 12.0;
        private const double BaseAxisLabelGap = 20.0;
        private const double BaseOverviewBottomPadding = 20.0;
        private const double BaseIntervalOffset = 92.0;
        private const double BaseMinimumStandardVisualSpan = 240.0;
        private const double BaseMinimumMicroVisualSpan = 168.0;
        private const double BaseIntervalPadding = 24.0;
        private const double BaseIntervalAnchorGap = 4.0;
        private const double BaseIntervalChevronThreshold = 20.0;
        private const double BaseIntervalMinimumArrowSpan = 60.0;
        private const double BaseIntervalDashLength = 4.0;
        private const double BaseIntervalDashGap = 8.0;
        private const double BaseIntervalAnchorCutout = 2.0;
        private const double BaseStandardIntervalFontSize = 18.0;
        private const double BaseMicroIntervalFontSize = 18.0;
        private const double DetailTopPadding = 8.0;
        private const double DetailBottomPadding = 8.0;
        private const double DetailCardGap = 24.0;
        private const double SameTimestampDetailCardGap = 10.0;
        private const double BaseDetailColumnGap = 28.0;
        private const double OverviewOuterMargin = 16.0;
        private const double RootHorizontalMargin = 16.0;
        private const double RootBottomMargin = 16.0;
        private const double DetailBandTopMargin = 8.0;
        private const double BaseCardWidthMin = 280.0;
        private const double BaseCardWidthMax = 360.0;
        private const double BaseCardTitleFontSize = 15.0;
        private const double BaseCardLabelFontSize = 12.0;
        private const double BaseCardBodyFontSize = 14.0;
        private const double DetailOuterMargin = 20.0;
        private const double CardLeftPadding = 10.0;
        private const double CardRightPadding = 10.0;
        private const double CardTopPadding = 0.0;
        private const double CardBottomPadding = 6.0;
        private const double CardHeaderTopPadding = 8.0;
        private const double CardHeaderBottomPadding = 8.0;
        private const double CardDividerGapBefore = 0.0;
        private const double CardDividerGapAfter = 4.0;
        private const double CardDescriptionGap = 0.0;
        private const double CardRangeGap = 4.0;
        private const double CardFieldLabelValueGap = 4.0;
        private const double CardFieldValueIndent = 12.0;
        private const double CardWidthBuffer = 12.0;
        private const double FooterWidthInset = 260.0;
        private const double FooterTopMargin = 34.0;
        private const double FooterHorizontalPadding = 34.0;
        private const double FooterVerticalPadding = 24.0;
        private const double FooterFontSize = 28.0;
        private const double FooterNoteGap = 8.0;
        private const double PixelsPerDip = 1.0;

        private static readonly Typeface UiTypeface =
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static readonly Typeface UiBoldTypeface =
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

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
            RootContentWidth = CalculateRootContentWidth();
            RenderedCanvasWidth = CalculateRenderedCanvasWidth();
            RenderedCanvasHeight = CalculateRenderedCanvasHeight();
        }

        public string Title { get; }
        public string SectionLabel { get; }
        public double CanvasWidth { get; }
        public double CanvasHeight { get; }
        public double RenderedCanvasWidth { get; }
        public double TimelineWidth { get; private set; }
        public double OverviewHeight { get; private set; }
        public double DetailBandHeight { get; private set; }
        public double FooterWidth { get; private set; }
        public double RootContentWidth { get; private set; }
        public double RenderedCanvasHeight { get; }
        public double BaselineStartX { get; private set; }
        public double BaselineEndX { get; private set; }
        public double BaselineY { get; private set; }
        public string AxisLabelText => "t [s]";
        public double AxisLabelFontSize { get; private set; }
        public double AxisLabelLeft { get; private set; }
        public double AxisLabelTop { get; private set; }
        public bool HasFooterNotes => FooterNotes.Count > 0;
        public ObservableCollection<string> FooterNotes { get; }
        public ObservableCollection<ReportTimelineSlotGroupItem> SlotItems { get; }
        public ObservableCollection<ReportTimelineIntervalAnchorItem> IntervalAnchorItems { get; }
        public ObservableCollection<ReportTimelineSlotIntervalItem> IntervalItems { get; }
        public ObservableCollection<ReportTimelineDetailCardItem> DetailCardItems { get; }

        private double AxisLabelWidth { get; set; }
        private double AxisLabelHeight { get; set; }

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
            ConfigureAxisLabel(layoutScale);
            NormalizeOverviewMargins(groups);
            BuildDetailCards(groups, slotPitch, layoutScale);
        }

        private void BuildSlotItems(IReadOnlyList<SlotGroup> groups, double slotPitch, double layoutScale)
        {
            double slotTitleWidth = groups.Count == 1
                ? BaseSlotTitleWidthMax
                : Clamp(slotPitch - (70.0 * layoutScale), BaseSlotTitleWidthMin * layoutScale, BaseSlotTitleWidthMax);
            double slotTitleFontSize = BaseSlotTitleFontSize * layoutScale;
            double slotTimeFontSize = BaseSlotTimeFontSize * layoutScale;
            double slotNodeDiameter = BaseSlotNodeDiameter * layoutScale;
            double slotTimeGap = BaseSlotTimeGap * layoutScale;
            double slotTimeToTitleGap = BaseSlotTimeToTitleGap * layoutScale;
            double slotStemEndGap = BaseSlotStemEndGap * layoutScale;
            double itemGap = BaseSlotItemGap * layoutScale;
            double maxBottom = BaselineY;

            foreach (var group in groups)
            {
                var titles = new ObservableCollection<ReportTimelineSlotTitleItem>();
                double titlesHeight = 0.0;
                string time = FormatTimestamp(group.Timestamp);
                Size timeSize = MeasureSingleLineText(time, MonoTypeface, slotTimeFontSize);

                for (int eventIndex = 0; eventIndex < group.Events.Count; eventIndex++)
                {
                    SimulationEventMarker simulationEvent = group.Events[eventIndex];
                    string title = TrimTextToWidth(
                        FormatSlotTitle(simulationEvent),
                        UiBoldTypeface,
                        slotTitleFontSize,
                        slotTitleWidth);
                    Size titleSize = MeasureSingleLineText(title, UiBoldTypeface, slotTitleFontSize);
                    double itemHeight = titleSize.Height;
                    double bottomGap = eventIndex < group.Events.Count - 1 ? itemGap : 0.0;

                    titles.Add(new ReportTimelineSlotTitleItem
                    {
                        Margin = new Thickness(0.0, 0.0, 0.0, bottomGap),
                        TitleFontSize = slotTitleFontSize,
                        TitleForeground = GetPriorityBrush(simulationEvent.Priority),
                        TitleText = title,
                        Width = slotTitleWidth
                    });

                    titlesHeight += itemHeight + bottomGap;
                }

                double width = Math.Max(slotTitleWidth, timeSize.Width + (18.0 * layoutScale)) + (24.0 * layoutScale);
                double left = group.CenterX - (width / 2.0);
                double lineX = width / 2.0;
                double top = BaselineY - (slotNodeDiameter / 2.0);
                double timeTop = slotNodeDiameter + slotTimeGap;
                double titleTop = timeTop + timeSize.Height + slotTimeToTitleGap;
                double stemTop = slotNodeDiameter;
                double stemBottom = Math.Max(stemTop, titleTop - slotStemEndGap);
                double height = titleTop + titlesHeight;

                SlotItems.Add(new ReportTimelineSlotGroupItem
                {
                    Height = height,
                    Left = left,
                    LineX = lineX,
                    NodeDiameter = slotNodeDiameter,
                    NodeLeft = lineX - (slotNodeDiameter / 2.0),
                    NodeTop = 0.0,
                    StemBottom = stemBottom,
                    StemTop = stemTop,
                    TimeFontSize = slotTimeFontSize,
                    TimeForeground = GetSlotTimeBrush(),
                    TimeLeft = (width - timeSize.Width) / 2.0,
                    TimeText = time,
                    TimeTop = timeTop,
                    TitleLeft = (width - slotTitleWidth) / 2.0,
                    TitleTop = titleTop,
                    TitleWidth = slotTitleWidth,
                    Top = top,
                    Width = width,
                    Titles = titles
                });

                maxBottom = Math.Max(maxBottom, top + height);
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
            double anchorCutout = Clamp(BaseIntervalAnchorCutout * layoutScale, 1.5, 2.5);
            double anchorTopAbsolute = absoluteLineY + anchorCutout;
            double anchorBottomAbsolute = BaselineY - (6.0 * layoutScale);

            IntervalAnchorItems.Clear();

            foreach (var group in groups)
            {
                IntervalAnchorItems.Add(new ReportTimelineIntervalAnchorItem
                {
                    Height = Math.Max(1.0, anchorBottomAbsolute - anchorTopAbsolute),
                    Stroke = GetIntervalBrush(0),
                    Top = anchorTopAbsolute,
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
                Size labelSize = MeasureSingleLineText(label, UiBoldTypeface, fontSize);
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
            double titleFontSize = BaseCardTitleFontSize * layoutScale;
            double labelFontSize = BaseCardLabelFontSize * layoutScale;
            double bodyFontSize = BaseCardBodyFontSize * layoutScale;
            double preferredCardWidth = MeasurePreferredCardWidth(groups, titleFontSize, labelFontSize, bodyFontSize);
            double minimumColumnGap = BaseDetailColumnGap * layoutScale;
            double availableCardBandWidth = Math.Max(1.0, TimelineWidth - (DetailOuterMargin * 2.0));
            double maximumCardWidthToFit = groups.Count <= 1
                ? Math.Max(BaseCardWidthMax, preferredCardWidth)
                : Math.Max(1.0, (availableCardBandWidth - (minimumColumnGap * (groups.Count - 1))) / groups.Count);
            double maximumCardWidth = groups.Count <= 1
                ? Math.Max(BaseCardWidthMax, preferredCardWidth)
                : Math.Min(BaseCardWidthMax, maximumCardWidthToFit);
            double minimumCardWidth = Math.Min(BaseCardWidthMin, maximumCardWidth);
            double cardWidth = Clamp(preferredCardWidth, minimumCardWidth, maximumCardWidth);
            double columnBottom = 0.0;
            List<double> columnLefts = BuildDetailColumnLefts(groups, cardWidth, minimumColumnGap, DetailOuterMargin);

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                double columnTop = DetailTopPadding;
                double left = columnLefts[groupIndex];
                double textWidth = Math.Max(120.0, cardWidth - CardLeftPadding - CardRightPadding);
                double bodyTextWidth = textWidth;
                double sameTimestampGap = SameTimestampDetailCardGap * layoutScale;
                double lastGap = 0.0;

                foreach (var simulationEvent in group.Events)
                {
                    string title = TrimTextToWidth(
                        string.IsNullOrWhiteSpace(simulationEvent.Title)
                            ? "Event"
                            : simulationEvent.Title.Trim(),
                        UiBoldTypeface,
                        titleFontSize,
                        textWidth);
                    Size titleSize = MeasureSingleLineText(title, UiBoldTypeface, titleFontSize);
                    ObservableCollection<ReportTimelineDetailFieldItem> fields = BuildDetailFields(
                        simulationEvent,
                        bodyTextWidth,
                        labelFontSize,
                        bodyFontSize,
                        out double fieldsHeight);

                    double height = CardTopPadding;
                    height += CardHeaderTopPadding;
                    height += titleSize.Height;
                    height += CardHeaderBottomPadding;
                    height += CardDividerGapBefore;
                    height += 1.0;
                    height += CardDividerGapAfter;
                    height += fieldsHeight;
                    height += CardBottomPadding;

                    DetailCardItems.Add(new ReportTimelineDetailCardItem
                    {
                        AccentBrush = GetPriorityBrush(simulationEvent.Priority),
                        BodyFontSize = bodyFontSize,
                        BodyTextWidth = bodyTextWidth,
                        ContentPadding = new Thickness(CardLeftPadding, CardTopPadding, CardRightPadding, CardBottomPadding),
                        DividerMargin = new Thickness(0.0, CardDividerGapBefore, 0.0, CardDividerGapAfter),
                        Fields = fields,
                        Height = height,
                        Left = left,
                        TextContentWidth = textWidth,
                        TitleMargin = new Thickness(0.0, CardHeaderTopPadding, 0.0, CardHeaderBottomPadding),
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

        private double MeasurePreferredCardWidth(
            IReadOnlyList<SlotGroup> groups,
            double titleFontSize,
            double labelFontSize,
            double valueFontSize)
        {
            double requiredContentWidth = 0.0;

            foreach (var group in groups)
            {
                foreach (var simulationEvent in group.Events)
                {
                    requiredContentWidth = Math.Max(
                        requiredContentWidth,
                        MeasureRequiredCardContentWidth(
                            simulationEvent,
                            titleFontSize,
                            labelFontSize,
                            valueFontSize));
                }
            }

            double paddedWidth = requiredContentWidth + CardLeftPadding + CardRightPadding + CardWidthBuffer;
            return Math.Ceiling(Math.Max(BaseCardWidthMin, paddedWidth));
        }

        private double MeasureRequiredCardContentWidth(
            SimulationEventMarker simulationEvent,
            double titleFontSize,
            double labelFontSize,
            double valueFontSize)
        {
            string title = string.IsNullOrWhiteSpace(simulationEvent.Title)
                ? "Event"
                : simulationEvent.Title.Trim();
            double requiredWidth = MeasureSingleLineText(title, UiBoldTypeface, titleFontSize).Width;

            requiredWidth = Math.Max(
                requiredWidth,
                MeasureRequiredDetailFieldWidth(
                    simulationEvent.DescriptionLabel,
                    simulationEvent.Description,
                    labelFontSize,
                    valueFontSize));
            requiredWidth = Math.Max(
                requiredWidth,
                MeasureRequiredDetailFieldWidth(
                    simulationEvent.RangeBTWLabel,
                    simulationEvent.RangeBTW,
                    labelFontSize,
                    valueFontSize));
            requiredWidth = Math.Max(
                requiredWidth,
                MeasureRequiredDetailFieldWidth(
                    simulationEvent.SourceTargetLabel,
                    simulationEvent.SourceTarget,
                    labelFontSize,
                    valueFontSize));

            return requiredWidth;
        }

        private double MeasureRequiredDetailFieldWidth(
            string label,
            string value,
            double labelFontSize,
            double valueFontSize)
        {
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value))
            {
                return 0.0;
            }

            double labelWidth = MeasureSingleLineText(label.Trim(), UiTypeface, labelFontSize).Width;
            double valueWidth = MeasureSingleLineText(value.Trim(), MonoTypeface, valueFontSize).Width + CardFieldValueIndent;
            return Math.Max(labelWidth, valueWidth);
        }

        private ObservableCollection<ReportTimelineDetailFieldItem> BuildDetailFields(
            SimulationEventMarker simulationEvent,
            double fieldWidth,
            double labelFontSize,
            double valueFontSize,
            out double totalHeight)
        {
            var fields = new ObservableCollection<ReportTimelineDetailFieldItem>();
            totalHeight = 0.0;

            AppendDetailField(
                fields,
                simulationEvent.DescriptionLabel,
                simulationEvent.Description,
                fieldWidth,
                labelFontSize,
                valueFontSize,
                ref totalHeight);
            AppendDetailField(
                fields,
                simulationEvent.RangeBTWLabel,
                simulationEvent.RangeBTW,
                fieldWidth,
                labelFontSize,
                valueFontSize,
                ref totalHeight);
            AppendDetailField(
                fields,
                simulationEvent.SourceTargetLabel,
                simulationEvent.SourceTarget,
                fieldWidth,
                labelFontSize,
                valueFontSize,
                ref totalHeight);

            return fields;
        }

        private void AppendDetailField(
            ObservableCollection<ReportTimelineDetailFieldItem> fields,
            string label,
            string value,
            double fieldWidth,
            double labelFontSize,
            double valueFontSize,
            ref double totalHeight)
        {
            if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string normalizedLabel = label.Trim();
            string normalizedValue = value.Trim();
            double topMargin = fields.Count == 0 ? CardDescriptionGap : CardRangeGap;
            Size labelSize = MeasureSingleLineText(normalizedLabel, UiTypeface, labelFontSize);
            Size valueSize = MeasureSingleLineText(normalizedValue, MonoTypeface, valueFontSize);

            fields.Add(new ReportTimelineDetailFieldItem
            {
                Label = normalizedLabel,
                LabelFontSize = labelFontSize,
                Margin = new Thickness(0.0, topMargin, 0.0, 0.0),
                Value = normalizedValue,
                ValueFontSize = valueFontSize,
                ValueMargin = new Thickness(CardFieldValueIndent, CardFieldLabelValueGap, 0.0, 0.0),
                ValueWidth = Math.Max(1.0, fieldWidth - CardFieldValueIndent),
                Width = fieldWidth
            });

            totalHeight += topMargin + labelSize.Height + CardFieldLabelValueGap + valueSize.Height;
        }

        private List<double> BuildDetailColumnLefts(
            IReadOnlyList<SlotGroup> groups,
            double cardWidth,
            double minimumColumnGap,
            double outerMargin)
        {
            var lefts = new List<double>(groups.Count);
            double minimumLeft = outerMargin;
            double maximumLeft = Math.Max(minimumLeft, TimelineWidth - cardWidth - outerMargin);

            for (int index = 0; index < groups.Count; index++)
            {
                double idealLeft = Clamp(groups[index].CenterX - (cardWidth / 2.0), minimumLeft, maximumLeft);

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

            double overflow = (lefts[lefts.Count - 1] + cardWidth + outerMargin) - TimelineWidth;
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

            if (lefts[0] >= minimumLeft)
            {
                return lefts;
            }

            double shiftRight = minimumLeft - lefts[0];
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

        private void NormalizeOverviewMargins(IReadOnlyList<SlotGroup> groups)
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

            minTop = Math.Min(minTop, AxisLabelTop);
            maxBottom = Math.Max(maxBottom, AxisLabelTop + AxisLabelHeight);
            minLeft = Math.Min(minLeft, AxisLabelLeft);
            maxRight = Math.Max(maxRight, AxisLabelLeft + AxisLabelWidth);

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

            foreach (var group in groups)
            {
                group.CenterX += shiftX;
            }

            AxisLabelTop += shiftY;
            AxisLabelLeft += shiftX;

            TimelineWidth = Math.Ceiling(maxRight + shiftX + OverviewOuterMargin);
            FooterWidth = Math.Max(680.0, TimelineWidth - FooterWidthInset);
            OverviewHeight = Math.Ceiling(maxBottom + shiftY + OverviewOuterMargin);
        }

        private double CalculateRenderedCanvasWidth()
        {
            return Math.Ceiling(Math.Max(CanvasWidth, RootContentWidth + (RootHorizontalMargin * 2.0)));
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

        private double CalculateRootContentWidth()
        {
            double overviewWidth = CalculateOverviewContentWidth();
            double detailWidth = CalculateDetailContentWidth();
            return Math.Ceiling(Math.Max(Math.Max(overviewWidth, detailWidth), FooterWidth));
        }

        private double CalculateOverviewContentWidth()
        {
            double minLeft = Math.Min(BaselineStartX, BaselineEndX);
            double maxRight = Math.Max(BaselineStartX, BaselineEndX);

            foreach (var slotItem in SlotItems)
            {
                minLeft = Math.Min(minLeft, slotItem.Left);
                maxRight = Math.Max(maxRight, slotItem.Left + slotItem.Width);
            }

            foreach (var intervalItem in IntervalItems)
            {
                minLeft = Math.Min(minLeft, intervalItem.Left);
                maxRight = Math.Max(maxRight, intervalItem.Left + intervalItem.Width);
            }

            foreach (var anchorItem in IntervalAnchorItems)
            {
                minLeft = Math.Min(minLeft, anchorItem.X);
                maxRight = Math.Max(maxRight, anchorItem.X);
            }

            minLeft = Math.Min(minLeft, AxisLabelLeft);
            maxRight = Math.Max(maxRight, AxisLabelLeft + AxisLabelWidth);

            return Math.Max(0.0, maxRight - minLeft);
        }

        private double CalculateDetailContentWidth()
        {
            if (DetailCardItems.Count == 0)
            {
                return 0.0;
            }

            double minLeft = DetailCardItems.Min(card => card.Left);
            double maxRight = DetailCardItems.Max(card => card.Left + card.Width);
            return Math.Max(0.0, maxRight - minLeft);
        }

        private double CalculateFooterContentHeight()
        {
            double textWidth = Math.Max(120.0, FooterWidth - (FooterHorizontalPadding * 2.0));
            double contentHeight = FooterVerticalPadding * 2.0;

            for (int index = 0; index < FooterNotes.Count; index++)
            {
                Size noteSize = MeasureWrappedText(FooterNotes[index], UiTypeface, FooterFontSize, textWidth);
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
            return CreateBrush(0x33, 0x3A, 0x42);
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
                return string.Format(CultureInfo.InvariantCulture, "{0:0}", seconds);
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.##}", seconds);
        }

        private static Size MeasureSingleLineText(string text, Typeface typeface, double fontSize)
        {
            var formattedText = CreateFormattedText(text, typeface, fontSize);
            return new Size(Math.Ceiling(formattedText.Width), Math.Ceiling(formattedText.Height));
        }

        private static string TrimTextToWidth(string text, Typeface typeface, double fontSize, double availableWidth)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            string normalizedText = text.Trim();
            if (availableWidth <= 0.0 || MeasureSingleLineText(normalizedText, typeface, fontSize).Width <= availableWidth)
            {
                return normalizedText;
            }

            const string ellipsis = "...";
            double ellipsisWidth = MeasureSingleLineText(ellipsis, typeface, fontSize).Width;
            if (ellipsisWidth >= availableWidth)
            {
                return ellipsis;
            }

            int low = 0;
            int high = normalizedText.Length;

            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = normalizedText.Substring(0, mid) + ellipsis;
                double candidateWidth = MeasureSingleLineText(candidate, typeface, fontSize).Width;
                if (candidateWidth <= availableWidth)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return normalizedText.Substring(0, low) + ellipsis;
        }

        private static Size MeasureWrappedText(string text, Typeface typeface, double fontSize, double maxWidth)
        {
            var formattedText = CreateFormattedText(text, typeface, fontSize);
            if (!double.IsInfinity(maxWidth))
            {
                formattedText.MaxTextWidth = maxWidth;
            }

            return new Size(Math.Ceiling(formattedText.Width), Math.Ceiling(formattedText.Height));
        }

        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize)
        {
            return new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                PixelsPerDip);
        }

        private static Geometry BuildIntervalFrameGeometry(
            double startX,
            double endX,
            double lineY)
        {
            double span = Math.Max(0.0, endX - startX);
            if (span <= 0.0)
            {
                return Geometry.Empty;
            }

            double dashLength = Math.Min(BaseIntervalDashLength, span);
            double gapLength = BaseIntervalDashGap;
            int dashCount = Math.Max(1, (int)Math.Floor((span + gapLength) / (dashLength + gapLength)));

            while (dashCount > 1)
            {
                double occupiedLength = (dashCount * dashLength) + ((dashCount - 1) * gapLength);
                if (occupiedLength <= span)
                {
                    break;
                }

                dashCount--;
            }

            double totalDashLength = (dashCount * dashLength) + ((dashCount - 1) * gapLength);
            double edgeGap = Math.Max(0.0, (span - totalDashLength) / 2.0);
            var builder = new StringBuilder();
            double dashStart = startX + edgeGap;

            for (int index = 0; index < dashCount; index++)
            {
                double dashEnd = Math.Min(endX, dashStart + dashLength);
                if (dashEnd > dashStart)
                {
                    builder.AppendFormat(
                        CultureInfo.InvariantCulture,
                        "M {0:0.##},{2:0.##} L {1:0.##},{2:0.##} ",
                        dashStart,
                        dashEnd,
                        lineY);
                }

                dashStart += dashLength + gapLength;
            }

            return builder.Length == 0
                ? Geometry.Empty
                : Geometry.Parse(builder.ToString().TrimEnd());
        }

        private static Geometry BuildArrowGeometry(
            double startX,
            double endX,
            double y)
        {
            double availableSpan = Math.Max(0.0, endX - startX);
            double armLength = Clamp(availableSpan * 0.16, 5.0, 8.4);
            double headHeight = Clamp(armLength * 0.34, 1.4, 2.4);
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

        private void ConfigureAxisLabel(double layoutScale)
        {
            AxisLabelFontSize = BaseAxisLabelFontSize * layoutScale;
            Size axisLabelSize = MeasureSingleLineText(AxisLabelText, UiBoldTypeface, AxisLabelFontSize);
            AxisLabelWidth = axisLabelSize.Width;
            AxisLabelHeight = axisLabelSize.Height;
            AxisLabelLeft = BaselineEndX + (BaseAxisLabelGap * layoutScale);
            AxisLabelTop = BaselineY - (axisLabelSize.Height / 2.0);
        }

        private static string FormatTimestamp(double timestamp)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:F2}", timestamp);
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
        public double NodeDiameter { get; set; }
        public double NodeLeft { get; set; }
        public double NodeTop { get; set; }
        public double StemBottom { get; set; }
        public double StemTop { get; set; }
        public double TimeFontSize { get; set; }
        public Brush TimeForeground { get; set; }
        public double TimeLeft { get; set; }
        public string TimeText { get; set; }
        public double TimeTop { get; set; }
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
        public Thickness ContentPadding { get; set; }
        public Thickness DividerMargin { get; set; }
        public ObservableCollection<ReportTimelineDetailFieldItem> Fields { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double TextContentWidth { get; set; }
        public Thickness TitleMargin { get; set; }
        public string Title { get; set; }
        public double TitleFontSize { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
    }

    public class ReportTimelineDetailFieldItem
    {
        public string Label { get; set; }
        public double LabelFontSize { get; set; }
        public Thickness Margin { get; set; }
        public string Value { get; set; }
        public double ValueFontSize { get; set; }
        public Thickness ValueMargin { get; set; }
        public double ValueWidth { get; set; }
        public double Width { get; set; }
    }
}
