using System;
using System.Windows;
using ProjectChronos.Services;

namespace ProjectChronos
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            if (TryRunStressReportExport(e))
            {
                return;
            }

            if (TryRunRealDataExport(e))
            {
                return;
            }

            if (TryRunPrototypeExport(e))
            {
                return;
            }

            base.OnStartup(e);
        }

        private bool TryRunPrototypeExport(StartupEventArgs e)
        {
            if (e == null || e.Args == null || e.Args.Length == 0)
            {
                return false;
            }

            if (!string.Equals(e.Args[0], "--export-report-prototype", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var definitionService = new TimelineReportDefinitionService();
            string outputPath = e.Args.Length > 1
                ? e.Args[1]
                : definitionService.GetDefaultPrototypeExportPath();

            try
            {
                var input = definitionService.CreatePrototypeReportExportInput(outputPath);
                var service = new TimelineReportExportService();
                service.Export(input);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Report Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(-1);
            }

            return true;
        }

        private bool TryRunRealDataExport(StartupEventArgs e)
        {
            if (e == null || e.Args == null || e.Args.Length == 0)
            {
                return false;
            }

            if (!string.Equals(e.Args[0], "--export-report-realdata", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var definitionService = new TimelineReportDefinitionService();
            string outputPath = e.Args.Length > 1
                ? e.Args[1]
                : definitionService.GetDefaultRealDataExportPath();

            try
            {
                var input = definitionService.CreateRealDataReportExportInput(outputPath);
                var service = new TimelineReportExportService();
                service.Export(input);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Real Data Report Export Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(-1);
            }

            return true;
        }

        private bool TryRunStressReportExport(StartupEventArgs e)
        {
            if (e == null || e.Args == null || e.Args.Length == 0)
            {
                return false;
            }

            if (!string.Equals(e.Args[0], "--stress-report-export", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string artifactRoot = e.Args.Length > 1 ? e.Args[1] : null;

            try
            {
                var options = TimelineReportStressOptions.CreateDefault(artifactRoot);
                var service = new TimelineReportStressService();
                var summary = service.Run(options);
                Shutdown(summary.ExitCode);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Report Stress Test Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(1);
            }

            return true;
        }
    }
}
