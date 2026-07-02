using System;
using System.Windows;
using System.Windows.Threading;
using ProjectChronos.Diagnostics;
using ProjectChronos.Services;
using ProjectChronos.ViewModels;

namespace ProjectChronos
{
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application
    {
        /// <summary>--replay-diag로 지정된 프리셋 (없으면 null).</summary>
        private string _replayDiagPreset;

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

            _replayDiagPreset = ParseReplayDiagPreset(e);

            base.OnStartup(e);

            if (_replayDiagPreset != null)
            {
                ScheduleReplayDiagnosticsRunner();
            }
        }

        /// <summary>
        /// e.Args에서 "--replay-diag &lt;preset&gt;" 또는 "--replay-diag=&lt;preset&gt;" 형태를 파싱한다.
        /// 인자가 없으면 null을 반환한다.
        /// </summary>
        private static string ParseReplayDiagPreset(StartupEventArgs e)
        {
            if (e == null || e.Args == null)
            {
                return null;
            }

            const string flag = "--replay-diag";

            for (int i = 0; i < e.Args.Length; i++)
            {
                string arg = e.Args[i];
                if (string.IsNullOrEmpty(arg))
                {
                    continue;
                }

                if (arg.StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return arg.Substring(flag.Length + 1);
                }

                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < e.Args.Length)
                    {
                        return e.Args[i + 1];
                    }

                    // 프리셋 미지정 시 기본 repro.
                    return "repro";
                }
            }

            return null;
        }

        /// <summary>
        /// MainWindow가 로드된 이후 진단 러너를 시작하도록 예약한다.
        /// SimulationReplayView의 CompositionTarget.Rendering이 VM.Tick()을 구동하려면
        /// 창이 표시되어 있어야 하므로 ContentRendered 이후 Background로 실행한다.
        /// </summary>
        private void ScheduleReplayDiagnosticsRunner()
        {
            EventHandler startHandler = null;
            startHandler = (s, args) =>
            {
                Window window = MainWindow;
                if (window == null)
                {
                    return;
                }

                window.ContentRendered -= startHandler;

                var vm = window.DataContext as MainWindowViewModel;
                if (vm == null)
                {
                    return;
                }

                // 레이아웃/렌더 파이프라인이 안정된 뒤 시작.
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        var runner = new ReplayDiagnosticsRunner(
                            vm.SimulationReplayViewModel,
                            _replayDiagPreset);
                        runner.Start();
                    }),
                    DispatcherPriority.Background);
            };

            // MainWindow는 base.OnStartup 이후 생성되므로 즉시 접근 가능하나,
            // 아직 null일 수 있어 Loaded 대신 ContentRendered에 안전하게 훅한다.
            if (MainWindow != null)
            {
                MainWindow.ContentRendered += startHandler;
            }
            else
            {
                // MainWindow가 아직 없으면 다음 디스패처 사이클에 재시도.
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        if (MainWindow != null)
                        {
                            MainWindow.ContentRendered += startHandler;
                        }
                    }),
                    DispatcherPriority.Loaded);
            }
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
