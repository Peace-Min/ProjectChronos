using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace ProjectChronos.ViewModels;

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
