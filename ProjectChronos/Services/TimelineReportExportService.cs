using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProjectChronos.ViewModels;
using ProjectChronos.Views;

namespace ProjectChronos.Services
{
    public class TimelineReportExportService
    {
        public string Export(TimelineReportExportInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (string.IsNullOrWhiteSpace(input.OutputPath)) throw new ArgumentException("Output path is required.", nameof(input));

            string outputPath = Path.GetFullPath(input.OutputPath);
            string outputDirectory = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var viewModel = new ReportTimelineExportViewModel(input);
            var view = new ReportTimelineExportView
            {
                DataContext = viewModel,
                Width = viewModel.CanvasWidth,
                Height = viewModel.RenderedCanvasHeight
            };

            double renderedHeight = viewModel.RenderedCanvasHeight;
            view.Measure(new Size(viewModel.CanvasWidth, renderedHeight));
            view.Arrange(new Rect(0, 0, viewModel.CanvasWidth, renderedHeight));
            view.UpdateLayout();

            var bitmap = new RenderTargetBitmap(
                (int)Math.Round(viewModel.CanvasWidth),
                (int)Math.Round(renderedHeight),
                96,
                96,
                PixelFormats.Pbgra32);

            bitmap.Render(view);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using (var fileStream = File.Create(outputPath))
            {
                encoder.Save(fileStream);
            }

            return outputPath;
        }
    }
}
