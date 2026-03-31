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

namespace ProjectChronos.ViewModels;

public class ReportTimelineExportViewModel : ViewModelBase
{
	private enum EdgeCanvasPolicy
	{
		None,
		DetailBandOverscan,
		RootOverscan
	}

	private sealed class SlotGroup
	{
		public List<SimulationEventMarker> Events { get; set; }

		public int Index { get; set; }

		public EventPriority Priority { get; set; }

		public double Timestamp { get; set; }

		public double CenterX { get; set; }
	}

	private sealed class IntervalLayout
	{
		public double Duration { get; set; }

		public SlotGroup EndGroup { get; set; }

		public double IdealWidth { get; set; }

		public int Index { get; set; }

		public SlotGroup StartGroup { get; set; }

		public bool UsesMinimumGap { get; set; }

		public double Width { get; set; }
	}

	private sealed class DetailColumnPlacement
	{
		public double Left { get; set; }

		public double Top { get; set; }
	}

	private sealed class MicroLabelPlacement
	{
		public double Left { get; set; }

		public double Top { get; set; }

		public double Width { get; set; }
	}

	private sealed class SlotMeasurement
	{
		public SlotGroup Group { get; set; }

		public int LaneIndex { get; set; }

		public double Left { get; set; }

		public Size TimeSize { get; set; }

		public string TimeText { get; set; }

		public ObservableCollection<ReportTimelineSlotTitleItem> Titles { get; set; }

		public double TitleWidth { get; set; }

		public double TitlesHeight { get; set; }

		public double Width { get; set; }
	}

	private sealed class IndexedEvent
	{
		public SimulationEventMarker Event { get; }

		public int Index { get; }

		public IndexedEvent(SimulationEventMarker simulationEvent, int index)
		{
			Event = simulationEvent;
			Index = index;
		}
	}

	private sealed class Range
	{
		public double Start { get; }

		public double End { get; }

		public Range(double start, double end)
		{
			Start = start;
			End = end;
		}

		public bool Overlaps(double start, double end)
		{
			return Math.Max(Start, start) < Math.Min(End, end);
		}
	}

	private const double TimelineSideMargin = 56.0;

	private const double BaselineInset = 88.0;

	private const double BaselineYPosition = 224.0;

	private const double MinimumTimelineWidth = 900.0;

	private const double BaseSlotNodeDiameter = 14.0;

	private const double BaseSlotTimeGap = 4.0;

	private const double BaseStackRhythmGap = 8.0;

	private const double BaseSlotTimeToTitleGap = 8.0;

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

	private const double BaseIntervalAnchorGap = 4.0;

	private const double BaseIntervalChevronThreshold = 20.0;

	private const double BaseIntervalDashLength = 4.0;

	private const double BaseIntervalDashGap = 8.0;

	private const double BaseIntervalAnchorCutout = 2.0;

	private const double BaseStandardIntervalFontSize = 18.0;

	private const double BaseMicroIntervalFontSize = 18.0;

	private const double BaseMinimumReservedGap = 28.0;

	private const double BaseSlotCollisionLaneGap = 8.0;

	private const double BaseMicroIntervalLift = 54.0;

	private const double BaseMicroCursorReach = 16.0;

	private const double BaseMicroCursorTailGap = 3.0;

	private const double BaseMicroCursorArmLength = 4.8;

	private const double BaseMicroCursorHeadHeight = 2.6;

	private const double BaseMicroLabelTopGap = 10.0;

	private const double BaseMicroLabelLaneGap = 8.0;

	private const double BaseMicroLabelCollisionGap = 6.0;

	private const double BaseScaleBreakWidth = 30.0;

	private const double BaseScaleBreakHeight = 24.0;

	private const double BaseScaleBreakMinimumNeighborSpan = 96.0;

	private const double BaseScaleBreakMinimumDistortionPixels = 1.0;

	private const double BaseScaleBreakMinimumDistortionRatio = 0.005;

	private const double DetailTopPadding = 8.0;

	private const double DetailBottomPadding = 8.0;

	private const double DetailCardGap = 8.0;

	private const double SameTimestampDetailCardGap = 8.0;

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

	private static readonly Typeface UiTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

	private static readonly Typeface UiBoldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

	private static readonly Typeface MonoTypeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

	public string Title { get; }

	public string SectionLabel { get; }

	public double CanvasWidth { get; }

	public double CanvasHeight { get; }

	public double RenderedCanvasWidth { get; }

	public double TimelineWidth { get; private set; }

	public double OverviewCanvasWidth { get; private set; }

	public double DetailCanvasWidth { get; private set; }

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

	public ObservableCollection<ReportTimelineMicroLabelItem> MicroLabelItems { get; }

	public ObservableCollection<ReportTimelineScaleBreakItem> ScaleBreakItems { get; }

	public ObservableCollection<ReportTimelineDetailCardItem> DetailCardItems { get; }

	private double AxisLabelWidth { get; set; }

	private double AxisLabelHeight { get; set; }

	private EdgeCanvasPolicy ActiveEdgeCanvasPolicy { get; }

	private double DetailCanvasInset { get; set; }

	public ReportTimelineExportViewModel(TimelineReportExportInput input)
	{
		if (input == null)
		{
			throw new ArgumentNullException("input");
		}
		Title = input.Title;
		SectionLabel = input.SectionLabel;
		CanvasWidth = input.CanvasWidth;
		CanvasHeight = input.CanvasHeight;
		TimelineWidth = Math.Max(900.0, CanvasWidth - 112.0);
		OverviewCanvasWidth = TimelineWidth;
		DetailCanvasWidth = TimelineWidth;
		BaselineStartX = 88.0;
		BaselineEndX = TimelineWidth - 88.0;
		BaselineY = 224.0;
		FooterWidth = Math.Max(680.0, TimelineWidth - 260.0);
		ActiveEdgeCanvasPolicy = ResolveEdgeCanvasPolicy();
		FooterNotes = new ObservableCollection<string>((input.FooterNotes ?? Array.Empty<string>()).Take(3));
		SlotItems = new ObservableCollection<ReportTimelineSlotGroupItem>();
		IntervalAnchorItems = new ObservableCollection<ReportTimelineIntervalAnchorItem>();
		IntervalItems = new ObservableCollection<ReportTimelineSlotIntervalItem>();
		MicroLabelItems = new ObservableCollection<ReportTimelineMicroLabelItem>();
		ScaleBreakItems = new ObservableCollection<ReportTimelineScaleBreakItem>();
		DetailCardItems = new ObservableCollection<ReportTimelineDetailCardItem>();
		OverviewHeight = BaselineY + 220.0;
		BuildLayout(input.Events ?? Array.Empty<SimulationEventMarker>());
		RootContentWidth = CalculateRootContentWidth();
		RenderedCanvasWidth = CalculateRenderedCanvasWidth();
		RenderedCanvasHeight = CalculateRenderedCanvasHeight();
	}

	private void BuildLayout(IReadOnlyList<SimulationEventMarker> sourceEvents)
	{
		List<IndexedEvent> orderedEvents = (from entry in sourceEvents.Select((SimulationEventMarker item, int index2) => new IndexedEvent(item, index2))
			where entry.Event != null
			orderby entry.Event.Timestamp, entry.Index
			select entry).ToList();
		if (orderedEvents.Count == 0)
		{
			DetailBandHeight = 0.0;
			return;
		}
		List<SlotGroup> groups = (from entry in orderedEvents
			group entry by Math.Round(entry.Event.Timestamp, 3) into @group
			orderby @group.Key
			select @group).Select((IGrouping<double, IndexedEvent> group, int index2) => new SlotGroup
		{
			Index = index2,
			Timestamp = group.First().Event.Timestamp,
			Events = group.Select((IndexedEvent entry) => entry.Event).ToList(),
			Priority = ResolveGroupPriority(group.Select((IndexedEvent entry) => entry.Event))
		}).ToList();
		double usableWidth = Math.Max(1.0, BaselineEndX - BaselineStartX);
		double slotPitch = ((groups.Count == 1) ? usableWidth : (usableWidth / (double)(groups.Count - 1)));
		double layoutScale = DetermineLayoutScale(slotPitch);
		double minimumReservedGap = DetermineMinimumReservedGap(groups.Count, usableWidth, layoutScale);
		IReadOnlyList<IntervalLayout> intervalLayouts = BuildIntervalLayouts(groups, usableWidth, minimumReservedGap);
		if (groups.Count == 1)
		{
			groups[0].CenterX = (BaselineStartX + BaselineEndX) / 2.0;
		}
		else
		{
			groups[0].CenterX = BaselineStartX;
			for (int index = 0; index < intervalLayouts.Count; index++)
			{
				groups[index + 1].CenterX = groups[index].CenterX + intervalLayouts[index].Width;
			}
		}
		BuildSlotItems(groups, slotPitch, layoutScale);
		BuildIntervalItems(groups, intervalLayouts, layoutScale);
		BuildScaleBreakItems(intervalLayouts, layoutScale);
		ConfigureAxisLabel(layoutScale);
		NormalizeOverviewMargins(groups);
		BuildDetailCards(groups, slotPitch, layoutScale);
	}

	private void BuildSlotItems(IReadOnlyList<SlotGroup> groups, double slotPitch, double layoutScale)
	{
		double slotTitleWidth = ((groups.Count == 1) ? 240.0 : Clamp(slotPitch - 70.0 * layoutScale, 160.0 * layoutScale, 240.0));
		double slotTitleFontSize = 15.0 * layoutScale;
		double slotTimeFontSize = 12.0 * layoutScale;
		double slotNodeDiameter = 14.0 * layoutScale;
		double slotTimeGap = 4.0 * layoutScale;
		double slotTimeToTitleGap = 8.0 * layoutScale;
		double slotStemEndGap = 8.0 * layoutScale;
		double itemGap = 6.0 * layoutScale;
		double laneGap = 8.0 * layoutScale;
		double maxBottom = BaselineY;
		List<List<Range>> lanes = new List<List<Range>>();
		List<double> laneHeights = new List<double>();
		List<SlotMeasurement> measurements = new List<SlotMeasurement>(groups.Count);
		foreach (SlotGroup group in groups)
		{
			ObservableCollection<ReportTimelineSlotTitleItem> titles = new ObservableCollection<ReportTimelineSlotTitleItem>();
			double titleFrameWidth = 0.0;
			double titlesHeight = 0.0;
			string time = FormatTimestamp(group.Timestamp);
			Size timeSize = MeasureSingleLineText(time, MonoTypeface, slotTimeFontSize);
			for (int eventIndex = 0; eventIndex < group.Events.Count; eventIndex++)
			{
				SimulationEventMarker simulationEvent = group.Events[eventIndex];
				string title = TrimTextToWidth(FormatSlotTitle(simulationEvent), UiBoldTypeface, slotTitleFontSize, slotTitleWidth);
				Size titleSize = MeasureSingleLineText(title, UiBoldTypeface, slotTitleFontSize);
				double itemHeight = MeasureSingleLineText(title, UiBoldTypeface, slotTitleFontSize).Height;
				double bottomGap = ((eventIndex < group.Events.Count - 1) ? itemGap : 0.0);
				titles.Add(new ReportTimelineSlotTitleItem
				{
					Margin = new Thickness(0.0, 0.0, 0.0, bottomGap),
					TitleFontSize = slotTitleFontSize,
					TitleForeground = GetPriorityBrush(simulationEvent.Priority),
					TitleText = title,
					Width = 0.0
				});
				titleFrameWidth = Math.Max(titleFrameWidth, Math.Min(slotTitleWidth, Math.Ceiling(titleSize.Width + 2.0 * layoutScale)));
				titlesHeight += itemHeight + bottomGap;
			}
			titleFrameWidth = Math.Max(titleFrameWidth, Math.Min(slotTitleWidth, Math.Ceiling(timeSize.Width + 18.0 * layoutScale)));
			foreach (ReportTimelineSlotTitleItem titleItem in titles)
			{
				titleItem.Width = titleFrameWidth;
			}
			double width = Math.Max(titleFrameWidth, timeSize.Width + 18.0 * layoutScale) + 24.0 * layoutScale;
			double left = group.CenterX - width / 2.0;
			int laneIndex = AssignLane(lanes, left, left + width);
			double contentHeight = timeSize.Height + slotTimeToTitleGap + titlesHeight;
			while (laneHeights.Count <= laneIndex)
			{
				laneHeights.Add(0.0);
			}
			laneHeights[laneIndex] = Math.Max(laneHeights[laneIndex], contentHeight);
			measurements.Add(new SlotMeasurement
			{
				Group = group,
				Left = left,
				TimeSize = timeSize,
				TimeText = time,
				Titles = titles,
				TitleWidth = titleFrameWidth,
				TitlesHeight = titlesHeight,
				Width = width,
				LaneIndex = laneIndex
			});
		}
		double[] laneOffsets = new double[laneHeights.Count];
		for (int i = 1; i < laneHeights.Count; i++)
		{
			laneOffsets[i] = laneOffsets[i - 1] + laneHeights[i - 1] + laneGap;
		}
		foreach (SlotMeasurement measurement in measurements)
		{
			double lineX = measurement.Width / 2.0;
			double top = BaselineY - slotNodeDiameter / 2.0;
			double laneOffset = laneOffsets[measurement.LaneIndex];
			double timeTop = slotNodeDiameter + slotTimeGap + laneOffset;
			double titleTop = timeTop + measurement.TimeSize.Height + slotTimeToTitleGap;
			double stemTop = slotNodeDiameter;
			double stemBottom = Math.Max(stemTop, titleTop - slotStemEndGap);
			double height = titleTop + measurement.TitlesHeight;
			SlotItems.Add(new ReportTimelineSlotGroupItem
			{
				Height = height,
				Left = measurement.Left,
				LineX = lineX,
				NodeDiameter = slotNodeDiameter,
				NodeLeft = lineX - slotNodeDiameter / 2.0,
				NodeTop = 0.0,
				StemBottom = stemBottom,
				StemTop = stemTop,
				TimeFontSize = slotTimeFontSize,
				TimeForeground = GetSlotTimeBrush(),
				TimeLeft = (measurement.Width - measurement.TimeSize.Width) / 2.0,
				TimeText = measurement.TimeText,
				TimeTop = timeTop,
				TitleLeft = (measurement.Width - measurement.TitleWidth) / 2.0,
				TitleTop = titleTop,
				TitleWidth = measurement.TitleWidth,
				Top = top,
				Width = measurement.Width,
				Titles = measurement.Titles
			});
			maxBottom = Math.Max(maxBottom, top + height);
		}
		OverviewHeight = Math.Max(BaselineY + 180.0, maxBottom + 20.0);
	}

	private void BuildIntervalItems(IReadOnlyList<SlotGroup> groups, IReadOnlyList<IntervalLayout> intervalLayouts, double layoutScale)
	{
		if (groups.Count < 2)
		{
			return;
		}
		double intervalOffset = 92.0 * layoutScale;
		double standardFontSize = 18.0 * layoutScale;
		double microFontSize = 18.0 * layoutScale;
		double absoluteLineY = BaselineY - intervalOffset;
		double microLineYAbsolute = absoluteLineY - 54.0 * layoutScale;
		double anchorCutout = Clamp(2.0 * layoutScale, 1.5, 2.5);
		double standardAnchorTopAbsolute = absoluteLineY + anchorCutout;
		double anchorBottomAbsolute = BaselineY - 6.0 * layoutScale;
		double standardPadding = 18.0 * layoutScale;
		double microPadding = 12.0 * layoutScale;
		List<MicroLabelPlacement> microLabelPlacements = BuildMicroLabelPlacements(intervalLayouts, microFontSize, layoutScale, microLineYAbsolute);
		bool[] elevateAnchors = BuildElevatedAnchorMap(groups.Count, intervalLayouts);
		IntervalAnchorItems.Clear();
		IntervalItems.Clear();
		MicroLabelItems.Clear();
		for (int index = 0; index < groups.Count; index++)
		{
			double anchorTopAbsolute = (elevateAnchors[index] ? microLineYAbsolute : standardAnchorTopAbsolute);
			IntervalAnchorItems.Add(new ReportTimelineIntervalAnchorItem
			{
				Height = Math.Max(1.0, anchorBottomAbsolute - anchorTopAbsolute),
				Stroke = GetIntervalBrush(0),
				Top = anchorTopAbsolute,
				X = groups[index].CenterX
			});
		}
		for (int i = 0; i < intervalLayouts.Count; i++)
		{
			IntervalLayout intervalLayout = intervalLayouts[i];
			SlotGroup current = intervalLayout.StartGroup;
			SlotGroup next = intervalLayout.EndGroup;
			double duration = intervalLayout.Duration;
			SlotIntervalLaneKind laneKind = (intervalLayout.UsesMinimumGap ? SlotIntervalLaneKind.Micro : SlotIntervalLaneKind.Standard);
			double fontSize = ((laneKind == SlotIntervalLaneKind.Micro) ? microFontSize : standardFontSize);
			double anchorGap = Clamp(4.0 * layoutScale, 3.0, 5.0);
			string label = FormatDuration(duration);
			Size labelSize = MeasureSingleLineText(label, UiBoldTypeface, fontSize);
			double actualStart = current.CenterX;
			double actualEnd = next.CenterX;
			if (laneKind == SlotIntervalLaneKind.Micro)
			{
				MicroLabelPlacement labelPlacement = microLabelPlacements[i];
				double left = actualStart - microPadding;
				double right = actualEnd + microPadding;
				double top = microLineYAbsolute - microPadding;
				double width = Math.Max(1.0, right - left);
				double height = Math.Max(1.0, anchorBottomAbsolute - top);
				double lineYLocal = microLineYAbsolute - top;
				double leftAnchorLocal = actualStart - left;
				double rightAnchorLocal = actualEnd - left;
				IntervalItems.Add(new ReportTimelineSlotIntervalItem
				{
					ArrowGeometry = BuildMicroArrowGeometry(leftAnchorLocal, rightAnchorLocal, lineYLocal, layoutScale),
					FontSize = fontSize,
					FrameGeometry = BuildMicroIntervalFrameGeometry(leftAnchorLocal, rightAnchorLocal, lineYLocal, layoutScale),
					Height = height,
					Label = string.Empty,
					LabelLeft = 0.0,
					LabelTop = 0.0,
					Left = left,
					ShowChevronHeads = true,
					Stroke = GetIntervalBrush(i),
					StrokeThickness = 1.5,
					Top = top,
					Width = width
				});
				MicroLabelItems.Add(new ReportTimelineMicroLabelItem
				{
					FontSize = fontSize,
					Foreground = GetIntervalBrush(i),
					Left = labelPlacement.Left,
					Text = label,
					Top = labelPlacement.Top
				});
			}
			else
			{
				double topStandard = absoluteLineY - labelSize.Height - 16.0 * layoutScale;
				double baselineLocal = Math.Max(0.0, anchorBottomAbsolute - topStandard);
				double lineYLocalStandard = absoluteLineY - topStandard;
				double labelLeftAbsoluteStandard = (actualStart + actualEnd) / 2.0 - labelSize.Width / 2.0;
				double leftStandard = Math.Min(actualStart, labelLeftAbsoluteStandard) - standardPadding;
				double rightStandard = Math.Max(actualEnd, labelLeftAbsoluteStandard + labelSize.Width) + standardPadding;
				double widthStandard = Math.Max(1.0, rightStandard - leftStandard);
				double lineStartLocal = actualStart - leftStandard;
				double lineEndLocal = actualEnd - leftStandard;
				double arrowStartLocal = lineStartLocal + anchorGap;
				double arrowEndLocal = lineEndLocal - anchorGap;
				double availableArrowSpan = Math.Max(0.0, arrowEndLocal - arrowStartLocal);
				bool showChevronHeads = availableArrowSpan > 20.0;
				Geometry arrowGeometry = (showChevronHeads ? BuildArrowGeometry(arrowStartLocal, arrowEndLocal, lineYLocalStandard) : Geometry.Empty);
				IntervalItems.Add(new ReportTimelineSlotIntervalItem
				{
					ArrowGeometry = arrowGeometry,
					FontSize = fontSize,
					FrameGeometry = BuildIntervalFrameGeometry(arrowStartLocal, arrowEndLocal, lineYLocalStandard, layoutScale, showChevronHeads),
					Height = Math.Max(1.0, baselineLocal + 8.0 * layoutScale),
					Label = label,
					LabelLeft = labelLeftAbsoluteStandard - leftStandard,
					LabelTop = 0.0,
					Left = leftStandard,
					ShowChevronHeads = showChevronHeads,
					Stroke = GetIntervalBrush(i),
					StrokeThickness = 1.5,
					Top = topStandard,
					Width = widthStandard
				});
			}
		}
	}

	private void BuildScaleBreakItems(IReadOnlyList<IntervalLayout> intervalLayouts, double layoutScale)
	{
		ScaleBreakItems.Clear();
		if (intervalLayouts.Count == 0)
		{
			return;
		}
		double symbolWidth = 30.0 * layoutScale;
		double symbolHeight = 24.0 * layoutScale;
		double minimumNeighborSpan = 96.0 * layoutScale;
		foreach (IntervalLayout intervalLayout in intervalLayouts)
		{
			if (!intervalLayout.UsesMinimumGap && !(intervalLayout.Width < minimumNeighborSpan) && ShouldShowScaleBreak(intervalLayout))
			{
				AddScaleBreak(intervalLayout, symbolWidth, symbolHeight);
			}
		}
	}

	private void BuildDetailCards(IReadOnlyList<SlotGroup> groups, double slotPitch, double layoutScale)
	{
		double titleFontSize = 15.0 * layoutScale;
		double labelFontSize = 12.0 * layoutScale;
		double bodyFontSize = 14.0 * layoutScale;
		double preferredCardWidth = MeasurePreferredCardWidth(groups, titleFontSize, labelFontSize, bodyFontSize);
		double minimumColumnGap = 28.0 * layoutScale;
		double clusterStackGap = 8.0 * layoutScale;
		double availableCardBandWidth = Math.Max(1.0, TimelineWidth - 40.0);
		double maximumCardWidthToFit = ((groups.Count <= 1) ? Math.Max(360.0, preferredCardWidth) : Math.Max(1.0, (availableCardBandWidth - minimumColumnGap * (double)(groups.Count - 1)) / (double)groups.Count));
		double maximumCardWidth = ((groups.Count <= 1) ? Math.Max(360.0, preferredCardWidth) : Math.Min(360.0, maximumCardWidthToFit));
		double minimumCardWidth = Math.Min(280.0, maximumCardWidth);
		double cardWidth = Clamp(preferredCardWidth, minimumCardWidth, maximumCardWidth);
		ConfigureEdgeCanvasLayout(groups, cardWidth, 20.0);
		double bodyTextWidth;
		double textWidth = (bodyTextWidth = Math.Max(120.0, cardWidth - 10.0 - 10.0));
		double sameTimestampGap = 8.0 * layoutScale;
		List<double> slotBottoms = SlotItems.Select((ReportTimelineSlotGroupItem item) => item.Top + item.Height).ToList();
		List<double> groupColumnHeights = groups.Select((SlotGroup slotGroup) => MeasureDetailColumnHeight(slotGroup, bodyTextWidth, titleFontSize, labelFontSize, bodyFontSize, sameTimestampGap)).ToList();
		List<DetailColumnPlacement> columnPlacements = BuildDetailColumnPlacements(groups, slotBottoms, groupColumnHeights, cardWidth, minimumColumnGap, clusterStackGap, 20.0, DetailCanvasWidth, DetailCanvasInset);
		double columnBottom = 0.0;
		for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
		{
			SlotGroup group = groups[groupIndex];
			DetailColumnPlacement placement = columnPlacements[groupIndex];
			double columnTop = placement.Top;
			double left = placement.Left;
			double lastGap = 0.0;
			foreach (SimulationEventMarker simulationEvent in group.Events)
			{
				string title = TrimTextToWidth(string.IsNullOrWhiteSpace(simulationEvent.Title) ? "Event" : simulationEvent.Title.Trim(), UiBoldTypeface, titleFontSize, textWidth);
				Size titleSize = MeasureSingleLineText(title, UiBoldTypeface, titleFontSize);
				double fieldsHeight;
				ObservableCollection<ReportTimelineDetailFieldItem> fields = BuildDetailFields(simulationEvent, bodyTextWidth, labelFontSize, bodyFontSize, out fieldsHeight);
				double height = 0.0;
				height += 8.0;
				height += titleSize.Height;
				height += 8.0;
				height += 0.0;
				height += 1.0;
				height += 4.0;
				height += fieldsHeight;
				height += 6.0;
				DetailCardItems.Add(new ReportTimelineDetailCardItem
				{
					AccentBrush = GetPriorityBrush(simulationEvent.Priority),
					BodyFontSize = bodyFontSize,
					BodyTextWidth = bodyTextWidth,
					ContentPadding = new Thickness(10.0, 0.0, 10.0, 6.0),
					DividerMargin = new Thickness(0.0, 0.0, 0.0, 4.0),
					Fields = fields,
					Height = height,
					Left = left,
					TextContentWidth = textWidth,
					TitleMargin = new Thickness(0.0, 8.0, 0.0, 8.0),
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
		DetailBandHeight = ((columnBottom <= 0.0) ? 0.0 : (columnBottom + 8.0));
	}

	private double MeasurePreferredCardWidth(IReadOnlyList<SlotGroup> groups, double titleFontSize, double labelFontSize, double valueFontSize)
	{
		double requiredContentWidth = 0.0;
		foreach (SlotGroup group in groups)
		{
			foreach (SimulationEventMarker simulationEvent in group.Events)
			{
				requiredContentWidth = Math.Max(requiredContentWidth, MeasureRequiredCardContentWidth(simulationEvent, titleFontSize, labelFontSize, valueFontSize));
			}
		}
		double paddedWidth = requiredContentWidth + 10.0 + 10.0 + 12.0;
		return Math.Ceiling(Math.Max(280.0, paddedWidth));
	}

	private double MeasureDetailColumnHeight(SlotGroup group, double fieldWidth, double titleFontSize, double labelFontSize, double valueFontSize, double cardGap)
	{
		double totalHeight = 0.0;
		for (int eventIndex = 0; eventIndex < group.Events.Count; eventIndex++)
		{
			totalHeight += MeasureDetailCardHeight(group.Events[eventIndex], fieldWidth, titleFontSize, labelFontSize, valueFontSize);
			if (eventIndex < group.Events.Count - 1)
			{
				totalHeight += cardGap;
			}
		}
		return totalHeight;
	}

	private double MeasureDetailCardHeight(SimulationEventMarker simulationEvent, double fieldWidth, double titleFontSize, double labelFontSize, double valueFontSize)
	{
		string title = TrimTextToWidth(string.IsNullOrWhiteSpace(simulationEvent.Title) ? "Event" : simulationEvent.Title.Trim(), UiBoldTypeface, titleFontSize, fieldWidth);
		Size titleSize = MeasureSingleLineText(title, UiBoldTypeface, titleFontSize);
		BuildDetailFields(simulationEvent, fieldWidth, labelFontSize, valueFontSize, out var fieldsHeight);
		double height = 0.0;
		height += 8.0;
		height += titleSize.Height;
		height += 8.0;
		height += 0.0;
		height += 1.0;
		height += 4.0;
		height += fieldsHeight;
		return height + 6.0;
	}

	private void AddScaleBreak(IntervalLayout intervalLayout, double symbolWidth, double symbolHeight)
	{
		double centerX = (intervalLayout.StartGroup.CenterX + intervalLayout.EndGroup.CenterX) / 2.0;
		ScaleBreakItems.Add(new ReportTimelineScaleBreakItem
		{
			Geometry = BuildScaleBreakGeometry(symbolWidth, symbolHeight),
			Height = symbolHeight,
			Left = centerX - symbolWidth / 2.0,
			Top = BaselineY - symbolHeight / 2.0,
			Width = symbolWidth
		});
	}

	private static bool[] BuildElevatedAnchorMap(int groupCount, IReadOnlyList<IntervalLayout> intervalLayouts)
	{
		bool[] elevatedAnchors = new bool[groupCount];
		foreach (IntervalLayout intervalLayout in intervalLayouts.Where((IntervalLayout item) => item.UsesMinimumGap))
		{
			elevatedAnchors[intervalLayout.Index] = true;
			elevatedAnchors[intervalLayout.Index + 1] = true;
		}
		return elevatedAnchors;
	}

	private static bool ShouldShowScaleBreak(IntervalLayout intervalLayout)
	{
		if (intervalLayout.IdealWidth <= 0.0)
		{
			return false;
		}
		double distortionPixels = Math.Abs(intervalLayout.Width - intervalLayout.IdealWidth);
		double distortionRatio = distortionPixels / intervalLayout.IdealWidth;
		return distortionPixels >= 1.0 && distortionRatio >= 0.005;
	}

	private List<MicroLabelPlacement> BuildMicroLabelPlacements(IReadOnlyList<IntervalLayout> intervalLayouts, double fontSize, double layoutScale, double microLineYAbsolute)
	{
		List<MicroLabelPlacement> placements = Enumerable.Repeat<MicroLabelPlacement>(null, intervalLayouts.Count).ToList();
		double labelTopGap = 10.0 * layoutScale;
		double laneGap = 8.0 * layoutScale;
		double collisionGap = 6.0 * layoutScale;
		List<double> laneRightEdges = new List<double>();
		for (int index = 0; index < intervalLayouts.Count; index++)
		{
			IntervalLayout intervalLayout = intervalLayouts[index];
			string label = FormatDuration(intervalLayout.Duration);
			Size labelSize = MeasureSingleLineText(label, UiBoldTypeface, fontSize);
			double labelLeftAbsolute = (intervalLayout.StartGroup.CenterX + intervalLayout.EndGroup.CenterX) / 2.0 - labelSize.Width / 2.0;
			double labelRightAbsolute = labelLeftAbsolute + labelSize.Width;
			int laneIndex = 0;
			if (intervalLayout.UsesMinimumGap)
			{
				for (; laneIndex < laneRightEdges.Count && labelLeftAbsolute < laneRightEdges[laneIndex] + collisionGap; laneIndex++)
				{
				}
				if (laneIndex == laneRightEdges.Count)
				{
					laneRightEdges.Add(labelRightAbsolute);
				}
				else
				{
					laneRightEdges[laneIndex] = labelRightAbsolute;
				}
			}
			double labelTopAbsolute = microLineYAbsolute - labelTopGap - labelSize.Height;
			if (laneIndex > 0)
			{
				labelTopAbsolute -= (double)laneIndex * (labelSize.Height + laneGap);
			}
			placements[index] = new MicroLabelPlacement
			{
				Left = labelLeftAbsolute,
				Top = labelTopAbsolute,
				Width = labelSize.Width
			};
		}
		return placements;
	}

	private IReadOnlyList<IntervalLayout> BuildIntervalLayouts(IReadOnlyList<SlotGroup> groups, double usableWidth, double minimumReservedGap)
	{
		if (groups.Count < 2)
		{
			return Array.Empty<IntervalLayout>();
		}
		List<IntervalLayout> layouts = new List<IntervalLayout>(groups.Count - 1);
		for (int index = 0; index < groups.Count - 1; index++)
		{
			layouts.Add(new IntervalLayout
			{
				Duration = Math.Max(0.0, groups[index + 1].Timestamp - groups[index].Timestamp),
				EndGroup = groups[index + 1],
				Index = index,
				StartGroup = groups[index]
			});
		}
		double totalDuration = layouts.Sum((IntervalLayout item) => item.Duration);
		if (totalDuration > 0.0)
		{
			foreach (IntervalLayout layout in layouts)
			{
				layout.IdealWidth = layout.Duration * usableWidth / totalDuration;
			}
		}
		double effectiveMinimumGap = Math.Min(minimumReservedGap, usableWidth / (double)Math.Max(1, layouts.Count));
		HashSet<int> remainingIndices = new HashSet<int>(Enumerable.Range(0, layouts.Count));
		double remainingWidth = usableWidth;
		double remainingDuration = layouts.Sum((IntervalLayout item) => item.Duration);
		while (remainingIndices.Count > 0)
		{
			if (remainingWidth <= 0.0)
			{
				double widthPerInterval = 0.0;
				foreach (int remainingIndex in remainingIndices)
				{
					layouts[remainingIndex].Width = widthPerInterval;
				}
				break;
			}
			if (remainingDuration <= 0.0)
			{
				double widthPerInterval2 = remainingWidth / (double)remainingIndices.Count;
				foreach (int remainingIndex2 in remainingIndices)
				{
					layouts[remainingIndex2].Width = widthPerInterval2;
				}
				break;
			}
			double widthPerSecond = remainingWidth / remainingDuration;
			List<int> newlyClamped = remainingIndices.Where((int index2) => layouts[index2].Duration * widthPerSecond < effectiveMinimumGap).ToList();
			if (newlyClamped.Count == 0)
			{
				foreach (int remainingIndex3 in remainingIndices)
				{
					layouts[remainingIndex3].Width = layouts[remainingIndex3].Duration * widthPerSecond;
				}
				break;
			}
			foreach (int clampedIndex in newlyClamped)
			{
				layouts[clampedIndex].UsesMinimumGap = true;
				layouts[clampedIndex].Width = effectiveMinimumGap;
				remainingWidth -= effectiveMinimumGap;
				remainingDuration -= layouts[clampedIndex].Duration;
				remainingIndices.Remove(clampedIndex);
			}
		}
		double roundingDelta = usableWidth - layouts.Sum((IntervalLayout item) => item.Width);
		if (Math.Abs(roundingDelta) > 0.001)
		{
			layouts[layouts.Count - 1].Width = Math.Max(0.0, layouts[layouts.Count - 1].Width + roundingDelta);
		}
		return layouts;
	}

	private double DetermineMinimumReservedGap(int groupCount, double usableWidth, double layoutScale)
	{
		if (groupCount <= 1)
		{
			return 0.0;
		}
		double scaledMinimumGap = Clamp(42.0 * layoutScale, 30.0, 42.0);
		double maximumFitGap = usableWidth / (double)Math.Max(1, groupCount - 1);
		return Math.Max(0.0, Math.Min(scaledMinimumGap, maximumFitGap));
	}

	private void ConfigureEdgeCanvasLayout(IReadOnlyList<SlotGroup> groups, double cardWidth, double outerMargin)
	{
		OverviewCanvasWidth = TimelineWidth;
		DetailCanvasWidth = TimelineWidth;
		DetailCanvasInset = 0.0;
		if (ActiveEdgeCanvasPolicy == EdgeCanvasPolicy.None || groups.Count == 0)
		{
			return;
		}
		double leftOverscan = Math.Max(0.0, outerMargin + cardWidth / 2.0 - groups[0].CenterX);
		double rightOverscan = Math.Max(0.0, groups[groups.Count - 1].CenterX + cardWidth / 2.0 + outerMargin - TimelineWidth);
		if (leftOverscan <= 0.0 && rightOverscan <= 0.0)
		{
			return;
		}
		leftOverscan = Math.Ceiling(leftOverscan);
		rightOverscan = Math.Ceiling(rightOverscan);
		double expandedWidth = TimelineWidth + leftOverscan + rightOverscan;
		switch (ActiveEdgeCanvasPolicy)
		{
		case EdgeCanvasPolicy.DetailBandOverscan:
			DetailCanvasWidth = expandedWidth;
			DetailCanvasInset = leftOverscan;
			break;
		case EdgeCanvasPolicy.RootOverscan:
			ApplyOverviewHorizontalShift(groups, leftOverscan);
			OverviewCanvasWidth = expandedWidth;
			DetailCanvasWidth = expandedWidth;
			break;
		}
	}

	private void ApplyOverviewHorizontalShift(IReadOnlyList<SlotGroup> groups, double shiftX)
	{
		if (shiftX == 0.0)
		{
			return;
		}
		BaselineStartX += shiftX;
		BaselineEndX += shiftX;
		foreach (ReportTimelineSlotIntervalItem intervalItem in IntervalItems)
		{
			intervalItem.Left += shiftX;
		}
		foreach (ReportTimelineIntervalAnchorItem anchorItem in IntervalAnchorItems)
		{
			anchorItem.X += shiftX;
		}
		foreach (ReportTimelineSlotGroupItem slotItem in SlotItems)
		{
			slotItem.Left += shiftX;
		}
		foreach (ReportTimelineScaleBreakItem scaleBreakItem in ScaleBreakItems)
		{
			scaleBreakItem.Left += shiftX;
		}
		foreach (ReportTimelineMicroLabelItem microLabelItem in MicroLabelItems)
		{
			microLabelItem.Left += shiftX;
		}
		foreach (SlotGroup group in groups)
		{
			group.CenterX += shiftX;
		}
		AxisLabelLeft += shiftX;
	}

	private List<DetailColumnPlacement> BuildDetailColumnPlacements(IReadOnlyList<SlotGroup> groups, IReadOnlyList<double> slotBottoms, IReadOnlyList<double> groupColumnHeights, double cardWidth, double minimumColumnGap, double clusterStackGap, double outerMargin, double canvasWidth, double centerOffset)
	{
		List<DetailColumnPlacement> placements = new List<DetailColumnPlacement>(groups.Count);
		if (groups.Count == 0)
		{
			return placements;
		}
		double minimumLeft = outerMargin;
		double maximumLeft = Math.Max(minimumLeft, canvasWidth - cardWidth - outerMargin);
		List<double> centeredLefts = groups.Select((SlotGroup group) => Clamp(group.CenterX + centerOffset - cardWidth / 2.0, minimumLeft, maximumLeft)).ToList();
		double globalReferenceBottom = ((slotBottoms.Count == 0) ? 0.0 : slotBottoms.Max());
		int clusterStartIndex = 0;
		while (clusterStartIndex < groups.Count)
		{
			int clusterEndIndex = clusterStartIndex;
			double clusterRight = centeredLefts[clusterStartIndex] + cardWidth;
			while (clusterEndIndex + 1 < groups.Count)
			{
				double nextLeft = centeredLefts[clusterEndIndex + 1];
				if (nextLeft >= clusterRight + minimumColumnGap)
				{
					break;
				}
				clusterEndIndex++;
				clusterRight = Math.Max(clusterRight, centeredLefts[clusterEndIndex] + cardWidth);
			}
			double clusterReferenceBottom = slotBottoms.Skip(clusterStartIndex).Take(clusterEndIndex - clusterStartIndex + 1).DefaultIfEmpty(globalReferenceBottom)
				.Max();
			double clusterTop = 8.0 + (clusterReferenceBottom - globalReferenceBottom);
			for (int groupIndex = clusterStartIndex; groupIndex <= clusterEndIndex; groupIndex++)
			{
				placements.Add(new DetailColumnPlacement
				{
					Left = centeredLefts[groupIndex],
					Top = clusterTop
				});
				clusterTop += groupColumnHeights[groupIndex];
				if (groupIndex < clusterEndIndex)
				{
					clusterTop += clusterStackGap;
				}
			}
			clusterStartIndex = clusterEndIndex + 1;
		}
		return placements;
	}

	private double MeasureRequiredCardContentWidth(SimulationEventMarker simulationEvent, double titleFontSize, double labelFontSize, double valueFontSize)
	{
		string title = (string.IsNullOrWhiteSpace(simulationEvent.Title) ? "Event" : simulationEvent.Title.Trim());
		double requiredWidth = MeasureSingleLineText(title, UiBoldTypeface, titleFontSize).Width;
		requiredWidth = Math.Max(requiredWidth, MeasureRequiredDetailFieldWidth(simulationEvent.DescriptionLabel, simulationEvent.Description, labelFontSize, valueFontSize));
		requiredWidth = Math.Max(requiredWidth, MeasureRequiredDetailFieldWidth(simulationEvent.RangeBTWLabel, simulationEvent.RangeBTW, labelFontSize, valueFontSize));
		return Math.Max(requiredWidth, MeasureRequiredDetailFieldWidth(simulationEvent.SourceLabel, simulationEvent.Source, labelFontSize, valueFontSize));
	}

	private double MeasureRequiredDetailFieldWidth(string label, string value, double labelFontSize, double valueFontSize)
	{
		if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value))
		{
			return 0.0;
		}
		double labelWidth = MeasureSingleLineText(label.Trim(), UiTypeface, labelFontSize).Width;
		double valueWidth = MeasureSingleLineText(value.Trim(), MonoTypeface, valueFontSize).Width + 12.0;
		return Math.Max(labelWidth, valueWidth);
	}

	private ObservableCollection<ReportTimelineDetailFieldItem> BuildDetailFields(SimulationEventMarker simulationEvent, double fieldWidth, double labelFontSize, double valueFontSize, out double totalHeight)
	{
		ObservableCollection<ReportTimelineDetailFieldItem> fields = new ObservableCollection<ReportTimelineDetailFieldItem>();
		totalHeight = 0.0;
		AppendDetailField(fields, simulationEvent.DescriptionLabel, simulationEvent.Description, fieldWidth, labelFontSize, valueFontSize, ref totalHeight);
		AppendDetailField(fields, simulationEvent.RangeBTWLabel, simulationEvent.RangeBTW, fieldWidth, labelFontSize, valueFontSize, ref totalHeight);
		AppendDetailField(fields, simulationEvent.SourceLabel, simulationEvent.Source, fieldWidth, labelFontSize, valueFontSize, ref totalHeight);
		return fields;
	}

	private void AppendDetailField(ObservableCollection<ReportTimelineDetailFieldItem> fields, string label, string value, double fieldWidth, double labelFontSize, double valueFontSize, ref double totalHeight)
	{
		if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value))
		{
			string normalizedLabel = label.Trim();
			string normalizedValue = value.Trim();
			double topMargin = ((fields.Count == 0) ? 0.0 : 4.0);
			Size labelSize = MeasureSingleLineText(normalizedLabel, UiTypeface, labelFontSize);
			Size valueSize = MeasureSingleLineText(normalizedValue, MonoTypeface, valueFontSize);
			fields.Add(new ReportTimelineDetailFieldItem
			{
				Label = normalizedLabel,
				LabelFontSize = labelFontSize,
				Margin = new Thickness(0.0, topMargin, 0.0, 0.0),
				Value = normalizedValue,
				ValueFontSize = valueFontSize,
				ValueMargin = new Thickness(12.0, 4.0, 0.0, 0.0),
				ValueWidth = Math.Max(1.0, fieldWidth - 12.0),
				Width = fieldWidth
			});
			totalHeight += topMargin + labelSize.Height + 4.0 + valueSize.Height;
		}
	}

	private List<double> BuildDetailColumnLefts(IReadOnlyList<SlotGroup> groups, double cardWidth, double minimumColumnGap, double outerMargin)
	{
		List<double> lefts = new List<double>(groups.Count);
		double maximumLeft = Math.Max(outerMargin, TimelineWidth - cardWidth - outerMargin);
		for (int index = 0; index < groups.Count; index++)
		{
			double idealLeft = Clamp(groups[index].CenterX - cardWidth / 2.0, outerMargin, maximumLeft);
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
		double overflow = lefts[lefts.Count - 1] + cardWidth + outerMargin - TimelineWidth;
		if (overflow <= 0.0)
		{
			return lefts;
		}
		lefts[lefts.Count - 1] -= overflow;
		for (int index2 = lefts.Count - 2; index2 >= 0; index2--)
		{
			double maximumAllowedLeft = lefts[index2 + 1] - cardWidth - minimumColumnGap;
			lefts[index2] = Math.Min(lefts[index2], maximumAllowedLeft);
		}
		if (lefts[0] >= outerMargin)
		{
			return lefts;
		}
		double shiftRight = outerMargin - lefts[0];
		for (int i = 0; i < lefts.Count; i++)
		{
			lefts[i] += shiftRight;
		}
		return lefts;
	}

	private static EventPriority ResolveGroupPriority(IEnumerable<SimulationEventMarker> events)
	{
		if (events.Any((SimulationEventMarker item) => item.Priority == EventPriority.High))
		{
			return EventPriority.High;
		}
		if (events.Any((SimulationEventMarker item) => item.Priority == EventPriority.Medium))
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
		foreach (ReportTimelineSlotIntervalItem intervalItem in IntervalItems)
		{
			minTop = Math.Min(minTop, intervalItem.Top);
			maxBottom = Math.Max(maxBottom, intervalItem.Top + intervalItem.Height);
			minLeft = Math.Min(minLeft, intervalItem.Left);
			maxRight = Math.Max(maxRight, intervalItem.Left + intervalItem.Width);
		}
		foreach (ReportTimelineIntervalAnchorItem anchorItem in IntervalAnchorItems)
		{
			minTop = Math.Min(minTop, anchorItem.Top);
			maxBottom = Math.Max(maxBottom, anchorItem.Top + anchorItem.Height);
			minLeft = Math.Min(minLeft, anchorItem.X);
			maxRight = Math.Max(maxRight, anchorItem.X);
		}
		foreach (ReportTimelineSlotGroupItem slotItem in SlotItems)
		{
			minTop = Math.Min(minTop, slotItem.Top);
			maxBottom = Math.Max(maxBottom, slotItem.Top + slotItem.Height);
			minLeft = Math.Min(minLeft, slotItem.Left);
			maxRight = Math.Max(maxRight, slotItem.Left + slotItem.Width);
		}
		foreach (ReportTimelineScaleBreakItem scaleBreakItem in ScaleBreakItems)
		{
			minTop = Math.Min(minTop, scaleBreakItem.Top);
			maxBottom = Math.Max(maxBottom, scaleBreakItem.Top + scaleBreakItem.Height);
			minLeft = Math.Min(minLeft, scaleBreakItem.Left);
			maxRight = Math.Max(maxRight, scaleBreakItem.Left + scaleBreakItem.Width);
		}
		foreach (ReportTimelineMicroLabelItem microLabelItem in MicroLabelItems)
		{
			Size labelSize = MeasureSingleLineText(microLabelItem.Text, UiBoldTypeface, microLabelItem.FontSize);
			minTop = Math.Min(minTop, microLabelItem.Top);
			maxBottom = Math.Max(maxBottom, microLabelItem.Top + labelSize.Height);
			minLeft = Math.Min(minLeft, microLabelItem.Left);
			maxRight = Math.Max(maxRight, microLabelItem.Left + labelSize.Width);
		}
		minTop = Math.Min(minTop, AxisLabelTop);
		maxBottom = Math.Max(maxBottom, AxisLabelTop + AxisLabelHeight);
		minLeft = Math.Min(minLeft, AxisLabelLeft);
		maxRight = Math.Max(maxRight, AxisLabelLeft + AxisLabelWidth);
		if (double.IsInfinity(minTop) || double.IsInfinity(maxBottom))
		{
			return;
		}
		double shiftY = 16.0 - minTop;
		double shiftX = 16.0 - minLeft;
		BaselineY += shiftY;
		BaselineStartX += shiftX;
		BaselineEndX += shiftX;
		foreach (ReportTimelineSlotIntervalItem intervalItem2 in IntervalItems)
		{
			intervalItem2.Top += shiftY;
			intervalItem2.Left += shiftX;
		}
		foreach (ReportTimelineIntervalAnchorItem anchorItem2 in IntervalAnchorItems)
		{
			anchorItem2.Top += shiftY;
			anchorItem2.X += shiftX;
		}
		foreach (ReportTimelineSlotGroupItem slotItem2 in SlotItems)
		{
			slotItem2.Top += shiftY;
			slotItem2.Left += shiftX;
		}
		foreach (ReportTimelineScaleBreakItem scaleBreakItem2 in ScaleBreakItems)
		{
			scaleBreakItem2.Top += shiftY;
			scaleBreakItem2.Left += shiftX;
		}
		foreach (ReportTimelineMicroLabelItem microLabelItem2 in MicroLabelItems)
		{
			microLabelItem2.Top += shiftY;
			microLabelItem2.Left += shiftX;
		}
		foreach (SlotGroup group2 in groups)
		{
			group2.CenterX += shiftX;
		}
		AxisLabelTop += shiftY;
		AxisLabelLeft += shiftX;
		TimelineWidth = Math.Ceiling(maxRight + shiftX + 16.0);
		FooterWidth = Math.Max(680.0, TimelineWidth - 260.0);
		OverviewHeight = Math.Ceiling(maxBottom + shiftY + 16.0);
	}

	private double CalculateRenderedCanvasWidth()
	{
		return Math.Ceiling(Math.Max(CanvasWidth, RootContentWidth + 32.0));
	}

	private double CalculateRenderedCanvasHeight()
	{
		double totalHeight = OverviewHeight;
		if (DetailBandHeight > 0.0)
		{
			totalHeight += 8.0 + DetailBandHeight;
		}
		if (HasFooterNotes)
		{
			totalHeight += 34.0 + CalculateFooterContentHeight();
		}
		totalHeight += 16.0;
		return Math.Ceiling(totalHeight);
	}

	private static EdgeCanvasPolicy ResolveEdgeCanvasPolicy()
	{
		string text = (Environment.GetEnvironmentVariable("PC_TIMELINE_EDGE_CANVAS_POLICY") ?? string.Empty).Trim().ToLowerInvariant();
		switch (text)
		{
		case "none":
		case "off":
		case "disabled":
			return EdgeCanvasPolicy.None;
		case "detail":
		case "detail-band":
		case "detailband":
		case "detail_band":
			return EdgeCanvasPolicy.DetailBandOverscan;
		case "root":
		case "root-expand":
		case "rootexpand":
		case "root_expand":
			return EdgeCanvasPolicy.RootOverscan;
		default:
			return EdgeCanvasPolicy.RootOverscan;
		}
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
		foreach (ReportTimelineSlotGroupItem slotItem in SlotItems)
		{
			minLeft = Math.Min(minLeft, slotItem.Left);
			maxRight = Math.Max(maxRight, slotItem.Left + slotItem.Width);
		}
		foreach (ReportTimelineSlotIntervalItem intervalItem in IntervalItems)
		{
			minLeft = Math.Min(minLeft, intervalItem.Left);
			maxRight = Math.Max(maxRight, intervalItem.Left + intervalItem.Width);
		}
		foreach (ReportTimelineIntervalAnchorItem anchorItem in IntervalAnchorItems)
		{
			minLeft = Math.Min(minLeft, anchorItem.X);
			maxRight = Math.Max(maxRight, anchorItem.X);
		}
		foreach (ReportTimelineScaleBreakItem scaleBreakItem in ScaleBreakItems)
		{
			minLeft = Math.Min(minLeft, scaleBreakItem.Left);
			maxRight = Math.Max(maxRight, scaleBreakItem.Left + scaleBreakItem.Width);
		}
		foreach (ReportTimelineMicroLabelItem microLabelItem in MicroLabelItems)
		{
			Size labelSize = MeasureSingleLineText(microLabelItem.Text, UiBoldTypeface, microLabelItem.FontSize);
			minLeft = Math.Min(minLeft, microLabelItem.Left);
			maxRight = Math.Max(maxRight, microLabelItem.Left + labelSize.Width);
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
		double minLeft = DetailCardItems.Min((ReportTimelineDetailCardItem card) => card.Left);
		double maxRight = DetailCardItems.Max((ReportTimelineDetailCardItem card) => card.Left + card.Width);
		return Math.Max(0.0, maxRight - minLeft);
	}

	private double CalculateFooterContentHeight()
	{
		double textWidth = Math.Max(120.0, FooterWidth - 68.0);
		double contentHeight = 48.0;
		for (int index = 0; index < FooterNotes.Count; index++)
		{
			contentHeight += MeasureWrappedText(FooterNotes[index], UiTypeface, 28.0, textWidth).Height;
			if (index < FooterNotes.Count - 1)
			{
				contentHeight += 8.0;
			}
		}
		return contentHeight;
	}

	private static Brush GetPriorityBrush(EventPriority priority)
	{
		return CreateBrush(0, 114, 189);
	}

	private static Brush GetSlotTimeBrush()
	{
		return CreateBrush(51, 58, 66);
	}

	private static Brush GetIntervalBrush(int index)
	{
		return CreateBrush(217, 83, 25);
	}

	private static Brush CreateBrush(byte r, byte g, byte b)
	{
		SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
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
			if (!lanes[laneIndex].Any((Range existing) => existing.Overlaps(start, end)))
			{
				break;
			}
			laneIndex++;
		}
		lanes[laneIndex].Add(new Range(start, end));
		return laneIndex;
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
		FormattedText formattedText = CreateFormattedText(text, typeface, fontSize);
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
		double ellipsisWidth = MeasureSingleLineText("...", typeface, fontSize).Width;
		if (ellipsisWidth >= availableWidth)
		{
			return "...";
		}
		int low = 0;
		int high = normalizedText.Length;
		while (low < high)
		{
			int mid = (low + high + 1) / 2;
			string candidate = normalizedText.Substring(0, mid) + "...";
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
		return normalizedText.Substring(0, low) + "...";
	}

	private static Size MeasureWrappedText(string text, Typeface typeface, double fontSize, double maxWidth)
	{
		FormattedText formattedText = CreateFormattedText(text, typeface, fontSize);
		if (!double.IsInfinity(maxWidth))
		{
			formattedText.MaxTextWidth = maxWidth;
		}
		return new Size(Math.Ceiling(formattedText.Width), Math.Ceiling(formattedText.Height));
	}

	private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize)
	{
		return new FormattedText(text ?? string.Empty, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, fontSize, Brushes.Black, 1.0);
	}

	private static Geometry BuildIntervalFrameGeometry(double startX, double endX, double lineY, double layoutScale, bool showChevronHeads)
	{
		double dashStartX = startX;
		double dashEndX = endX;
		if (showChevronHeads)
		{
			double chevronSpan = Math.Max(0.0, endX - startX);
			double armLength = Clamp(chevronSpan * 0.16, 5.0, 8.4);
			double clearance = Clamp(2.0 * layoutScale, 1.5, 3.0);
			dashStartX = Math.Min(endX, startX + armLength + clearance);
			dashEndX = Math.Max(startX, endX - armLength - clearance);
		}
		double span = Math.Max(0.0, dashEndX - dashStartX);
		if (span <= 0.0)
		{
			return Geometry.Empty;
		}
		double dashLength = Math.Min(4.0, span);
		double gapLength = 8.0;
		int dashCount = Math.Max(1, (int)Math.Floor((span + gapLength) / (dashLength + gapLength)));
		while (dashCount > 1 && !((double)dashCount * dashLength <= span))
		{
			dashCount--;
		}
		StringBuilder builder = new StringBuilder();
		double actualGapLength = ((dashCount > 1) ? Math.Max(0.0, (span - (double)dashCount * dashLength) / (double)(dashCount - 1)) : 0.0);
		double dashStart = dashStartX;
		for (int index = 0; index < dashCount; index++)
		{
			double dashEnd = ((index == dashCount - 1) ? dashEndX : Math.Min(dashEndX, dashStart + dashLength));
			if (dashEnd > dashStart)
			{
				builder.AppendFormat(CultureInfo.InvariantCulture, "M {0:0.##},{2:0.##} L {1:0.##},{2:0.##} ", dashStart, dashEnd, lineY);
			}
			dashStart += dashLength + actualGapLength;
		}
		return (builder.Length == 0) ? Geometry.Empty : Geometry.Parse(builder.ToString().TrimEnd());
	}

	private static Geometry BuildArrowGeometry(double startX, double endX, double y)
	{
		double availableSpan = Math.Max(0.0, endX - startX);
		double armLength = Clamp(availableSpan * 0.16, 5.0, 8.4);
		double headHeight = Clamp(armLength * 0.34, 1.4, 2.4);
		string path = string.Format(CultureInfo.InvariantCulture, "M {0:0.##},{4:0.##} L {1:0.##},{5:0.##} L {0:0.##},{6:0.##} M {2:0.##},{4:0.##} L {3:0.##},{5:0.##} L {2:0.##},{6:0.##}", startX + armLength, startX, endX - armLength, endX, y - headHeight, y, y + headHeight);
		return Geometry.Parse(path);
	}

	private static Geometry BuildMicroIntervalFrameGeometry(double leftAnchorX, double rightAnchorX, double lineY, double layoutScale)
	{
		return Geometry.Empty;
	}

	private static Geometry BuildMicroArrowGeometry(double leftAnchorX, double rightAnchorX, double y, double layoutScale)
	{
		double span = Math.Max(0.0, rightAnchorX - leftAnchorX);
		if (span <= 0.0)
		{
			return Geometry.Empty;
		}
		double baseInset = Clamp(span * 0.16, 1.4, 4.0);
		double availableHalfSpan = Math.Max(1.2, (span - baseInset * 2.0) / 2.0);
		double armLength = Clamp(Math.Min(4.8 * layoutScale, availableHalfSpan), 1.8, 4.8);
		double headHeight = Clamp(2.6 * layoutScale, 1.5, 2.8);
		double leftBaseX = leftAnchorX + baseInset;
		double leftTipX = Math.Min(rightAnchorX - baseInset, leftBaseX + armLength);
		double rightBaseX = rightAnchorX - baseInset;
		double rightTipX = Math.Max(leftAnchorX + baseInset, rightBaseX - armLength);
		string path = string.Format(CultureInfo.InvariantCulture, "M {0:0.##},{4:0.##} L {1:0.##},{5:0.##} L {0:0.##},{6:0.##} M {2:0.##},{4:0.##} L {3:0.##},{5:0.##} L {2:0.##},{6:0.##}", leftBaseX, leftTipX, rightBaseX, rightTipX, y - headHeight, y, y + headHeight);
		return Geometry.Parse(path);
	}

	private static Geometry BuildScaleBreakGeometry(double width, double height)
	{
		double midY = height / 2.0;
		string path = string.Format(CultureInfo.InvariantCulture, "M {0:0.##},{4:0.##} L {1:0.##},{5:0.##} L {2:0.##},{6:0.##} L {3:0.##},{4:0.##}", width * 0.1, width * 0.3, width * 0.7, width * 0.9, midY, height * 0.25, height * 0.75);
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
		return string.IsNullOrWhiteSpace(simulationEvent.Title) ? "Event" : simulationEvent.Title.Trim();
	}

	private void ConfigureAxisLabel(double layoutScale)
	{
		AxisLabelFontSize = 12.0 * layoutScale;
		Size axisLabelSize = MeasureSingleLineText(AxisLabelText, UiBoldTypeface, AxisLabelFontSize);
		AxisLabelWidth = axisLabelSize.Width;
		AxisLabelHeight = axisLabelSize.Height;
		AxisLabelLeft = BaselineEndX + 20.0 * layoutScale;
		AxisLabelTop = BaselineY - axisLabelSize.Height / 2.0;
	}

	private static string FormatTimestamp(double timestamp)
	{
		return string.Format(CultureInfo.InvariantCulture, "{0:F2}", timestamp);
	}
}
