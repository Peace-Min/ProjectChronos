using System.Collections.ObjectModel;
using System.Windows.Media;

namespace ProjectChronos.ViewModels;

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
