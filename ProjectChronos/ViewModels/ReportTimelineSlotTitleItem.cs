using System.Windows;
using System.Windows.Media;

namespace ProjectChronos.ViewModels;

public class ReportTimelineSlotTitleItem
{
	public Thickness Margin { get; set; }

	public double TitleFontSize { get; set; }

	public Brush TitleForeground { get; set; }

	public string TitleText { get; set; }

	public double Width { get; set; }
}
