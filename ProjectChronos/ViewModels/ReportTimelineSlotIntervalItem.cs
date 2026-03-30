using System.Windows.Media;

namespace ProjectChronos.ViewModels;

public class ReportTimelineSlotIntervalItem
{
	public Geometry ArrowGeometry { get; set; }

	public double FontSize { get; set; }

	public Geometry FrameGeometry { get; set; }

	public double Height { get; set; }

	public string Label { get; set; }

	public double LabelLeft { get; set; }

	public double LabelTop { get; set; }

	public double Left { get; set; }

	public bool ShowChevronHeads { get; set; }

	public Brush Stroke { get; set; }

	public double StrokeThickness { get; set; }

	public double Top { get; set; }

	public double Width { get; set; }
}
