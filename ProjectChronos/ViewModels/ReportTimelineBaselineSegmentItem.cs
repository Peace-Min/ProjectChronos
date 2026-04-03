using System.Windows.Media;

namespace ProjectChronos.ViewModels;

public class ReportTimelineBaselineSegmentItem
{
	public PenLineCap EndLineCap { get; set; }

	public PenLineCap StartLineCap { get; set; }

	public double X1 { get; set; }

	public double X2 { get; set; }

	public double Y { get; set; }
}
