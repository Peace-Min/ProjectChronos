using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using ProjectChronos.ViewModels;

namespace ProjectChronos.Diagnostics
{
    /// <summary>
    /// 커맨드라인 프리셋 기반 무인 리플레이 진단 러너.
    /// 지정된 시나리오(스로틀/해상도/배속/수신부 우선순위/렌더비용/UI부하)를 적용하고
    /// 자동 재생하면서 1초 간격으로 송신/수신 카운터의 초당 델타를 CSV로 기록한 뒤 앱을 종료한다.
    ///
    /// 저성능 PC에서 Background 우선순위 수신부가 기아 상태에 빠지는 현상을
    /// 재현/검증하기 위한 회귀 테스트용 도구다. 실제 UI(SimulationReplayView)의
    /// CompositionTarget.Rendering이 VM.Tick()을 구동하므로 MainWindow가 표시된 이후에 실행해야 한다.
    /// </summary>
    public sealed class ReplayDiagnosticsRunner
    {
        /// <summary>샘플링 간격 (밀리초)</summary>
        private const int SampleIntervalMs = 1000;

        /// <summary>총 샘플 횟수</summary>
        private const int SampleCount = 12;

        private readonly SimulationReplayViewModel _vm;
        private readonly string _preset;
        private readonly SyntheticReplayReceiver _receiver = new SyntheticReplayReceiver();
        private readonly UiLoadGenerator _uiLoad = new UiLoadGenerator();

        private DispatcherTimer _timer;
        private int _sampleIndex;
        private DateTime _startedAtUtc;

        // 이전 샘플 시점의 누적 카운터 스냅샷 (초당 델타 계산용).
        private long _prevTick;
        private long _prevCtChanged;
        private long _prevDispatcherPost;
        private long _prevSliderNotify;
        private long _prevDisplayNotify;
        private long _prevMsgSent;
        private long _prevMsgThrottled;
        private long _prevRcvReceived;
        private long _prevRcvScheduled;
        private long _prevRcvCompleted;
        private long _prevRcvCoalesced;

        private readonly List<string> _csvRows = new List<string>();

        /// <summary>
        /// 러너를 생성한다.
        /// </summary>
        /// <param name="vm">대상 송신부 VM (MainWindowViewModel.SimulationReplayViewModel)</param>
        /// <param name="preset">프리셋 이름 (repro/fixed/coarse)</param>
        public ReplayDiagnosticsRunner(SimulationReplayViewModel vm, string preset)
        {
            if (vm == null)
            {
                throw new ArgumentNullException(nameof(vm));
            }

            _vm = vm;
            _preset = (preset ?? string.Empty).Trim().ToLowerInvariant();
        }

        /// <summary>
        /// 프리셋을 적용하고 자동 재생/샘플링을 시작한다.
        /// </summary>
        public void Start()
        {
            ApplyCommonSettings();
            ApplyPreset(_preset);

            // 수신부 연결: 송신부 SimulationTimeChanged → 합성 수신부.
            _vm.SimulationTimeChanged += _receiver.OnSimulationTimeChanged;

            // 초기 스냅샷 확보 (첫 샘플의 델타가 재생 시작 이후만 반영되도록).
            CaptureSnapshot();
            _receiver.DrainLatencySamplesMs();

            _startedAtUtc = DateTime.UtcNow;

            // 재생 시작.
            _vm.IsPlaying = true;

            // 타이머 우선순위는 반드시 Normal.
            // 기본(Background)이면 UI 부하 하에서 타이머 자체가 기아되어 샘플링이 멈춘다.
            _timer = new DispatcherTimer(DispatcherPriority.Normal)
            {
                Interval = TimeSpan.FromMilliseconds(SampleIntervalMs)
            };
            _timer.Tick += OnSampleTick;
            _timer.Start();
        }

        /// <summary>
        /// 프리셋 공통 설정.
        /// </summary>
        private void ApplyCommonSettings()
        {
            _vm.IsAutoPauseEnabled = false;
            _vm.IsRealtimeRenderingEnabled = true;
            _vm.CurrentTime = 0.0;
        }

        /// <summary>
        /// 프리셋별 설정. 알 수 없는 프리셋은 repro로 대체한다.
        /// </summary>
        private void ApplyPreset(string preset)
        {
            switch (preset)
            {
                case "fixed":
                    _vm.IsPlaybackSliderThrottleEnabled = true;
                    _vm.SetTimeResolution(0.00001);
                    break;

                case "coarse":
                    _vm.IsPlaybackSliderThrottleEnabled = false;
                    _vm.SetTimeResolution(0.01);
                    break;

                case "repro":
                default:
                    _vm.IsPlaybackSliderThrottleEnabled = false;
                    _vm.SetTimeResolution(0.00001);
                    break;
            }

            _vm.PlaybackSpeed = 0.1;

            _receiver.Priority = DispatcherPriority.Background;
            _receiver.RenderCostMs = 30;
            _receiver.IsEnabled = true;

            _uiLoad.LoadPerFrameMs = 8;

            // SetTimeResolution/재생 준비 과정에서 CurrentTime이 다시 흔들릴 수 있으므로
            // 재생 시작 직전 0으로 재고정한다.
            _vm.CurrentTime = 0.0;
        }

        private void OnSampleTick(object sender, EventArgs e)
        {
            RecordSample();

            _sampleIndex++;
            if (_sampleIndex >= SampleCount)
            {
                Finish();
            }
        }

        /// <summary>
        /// 현재 누적 카운터를 이전 스냅샷과 비교해 초당 델타 행을 기록한다.
        /// </summary>
        private void RecordSample()
        {
            var m = _vm.Metrics;

            long tick = m.TickCount;
            long ctChanged = m.CurrentTimeChangedCount;
            long dispatcherPost = m.DispatcherPostCount;
            long sliderNotify = m.SliderNotifyCount;
            long displayNotify = m.DisplayNotifyCount;
            long msgSent = m.MessageSentCount;
            long msgThrottled = m.MessageThrottledCount;

            long rcvReceived = _receiver.ReceivedCount;
            long rcvScheduled = _receiver.RenderScheduledCount;
            long rcvCompleted = _receiver.RenderCompletedCount;
            long rcvCoalesced = _receiver.RenderCoalescedCount;

            var latencies = _receiver.DrainLatencySamplesMs();
            double latP50 = Percentile(latencies, 0.50);
            double latP95 = Percentile(latencies, 0.95);
            double latMax = Max(latencies);

            double elapsedS = (DateTime.UtcNow - _startedAtUtc).TotalSeconds;

            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append((_sampleIndex + 1).ToString(ci)).Append(',');
            sb.Append(elapsedS.ToString("0.000", ci)).Append(',');
            sb.Append(_vm.CurrentTime.ToString("0.######", ci)).Append(',');
            sb.Append((tick - _prevTick).ToString(ci)).Append(',');
            sb.Append((ctChanged - _prevCtChanged).ToString(ci)).Append(',');
            sb.Append((dispatcherPost - _prevDispatcherPost).ToString(ci)).Append(',');
            sb.Append((sliderNotify - _prevSliderNotify).ToString(ci)).Append(',');
            sb.Append((displayNotify - _prevDisplayNotify).ToString(ci)).Append(',');
            sb.Append((msgSent - _prevMsgSent).ToString(ci)).Append(',');
            sb.Append((msgThrottled - _prevMsgThrottled).ToString(ci)).Append(',');
            sb.Append((rcvReceived - _prevRcvReceived).ToString(ci)).Append(',');
            sb.Append((rcvScheduled - _prevRcvScheduled).ToString(ci)).Append(',');
            sb.Append((rcvCompleted - _prevRcvCompleted).ToString(ci)).Append(',');
            sb.Append((rcvCoalesced - _prevRcvCoalesced).ToString(ci)).Append(',');
            sb.Append(latP50.ToString("0.###", ci)).Append(',');
            sb.Append(latP95.ToString("0.###", ci)).Append(',');
            sb.Append(latMax.ToString("0.###", ci));
            _csvRows.Add(sb.ToString());

            _prevTick = tick;
            _prevCtChanged = ctChanged;
            _prevDispatcherPost = dispatcherPost;
            _prevSliderNotify = sliderNotify;
            _prevDisplayNotify = displayNotify;
            _prevMsgSent = msgSent;
            _prevMsgThrottled = msgThrottled;
            _prevRcvReceived = rcvReceived;
            _prevRcvScheduled = rcvScheduled;
            _prevRcvCompleted = rcvCompleted;
            _prevRcvCoalesced = rcvCoalesced;
        }

        /// <summary>
        /// 이전 스냅샷을 현재 누적값으로 초기화한다 (첫 델타 기준점).
        /// </summary>
        private void CaptureSnapshot()
        {
            var m = _vm.Metrics;
            _prevTick = m.TickCount;
            _prevCtChanged = m.CurrentTimeChangedCount;
            _prevDispatcherPost = m.DispatcherPostCount;
            _prevSliderNotify = m.SliderNotifyCount;
            _prevDisplayNotify = m.DisplayNotifyCount;
            _prevMsgSent = m.MessageSentCount;
            _prevMsgThrottled = m.MessageThrottledCount;
            _prevRcvReceived = _receiver.ReceivedCount;
            _prevRcvScheduled = _receiver.RenderScheduledCount;
            _prevRcvCompleted = _receiver.RenderCompletedCount;
            _prevRcvCoalesced = _receiver.RenderCoalescedCount;
        }

        /// <summary>
        /// 재생 종료 → CSV 저장 → 앱 종료.
        /// </summary>
        private void Finish()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnSampleTick;
                _timer = null;
            }

            _vm.IsPlaying = false;
            _vm.SimulationTimeChanged -= _receiver.OnSimulationTimeChanged;
            _uiLoad.LoadPerFrameMs = 0;

            try
            {
                SaveCsv();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Replay Diagnostics Save Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            if (Application.Current != null)
            {
                Application.Current.Shutdown(0);
            }
        }

        /// <summary>
        /// CSV를 output 폴더에 저장한다.
        /// </summary>
        private void SaveCsv()
        {
            const string header =
                "sample,elapsed_s,current_time,tick,ct_changed,dispatcher_post,slider_notify," +
                "display_notify,msg_sent,msg_throttled,rcv_received,rcv_scheduled,rcv_completed," +
                "rcv_coalesced,lat_p50_ms,lat_p95_ms,lat_max_ms";

            var lines = new List<string>(_csvRows.Count + 1);
            lines.Add(header);
            lines.AddRange(_csvRows);

            string outputDir = ResolveOutputDirectory();
            Directory.CreateDirectory(outputDir);

            string fileName = "replay_diag_" +
                (string.IsNullOrEmpty(_preset) ? "repro" : _preset) + ".csv";
            string path = Path.Combine(outputDir, fileName);

            File.WriteAllLines(path, lines, new UTF8Encoding(false));
        }

        /// <summary>
        /// 출력 폴더 결정: BaseDirectory 기준 ..\..\output 가 존재하면 사용,
        /// 없으면 BaseDirectory 하위 output.
        /// </summary>
        private static string ResolveOutputDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            try
            {
                string projectRootOutput = Path.GetFullPath(
                    Path.Combine(baseDir, "..", "..", "output"));
                if (Directory.Exists(projectRootOutput))
                {
                    return projectRootOutput;
                }

                // 프로젝트 루트가 있으나 output이 없으면 생성해서 사용한다.
                string projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
                if (Directory.Exists(projectRoot))
                {
                    return projectRootOutput;
                }
            }
            catch
            {
                // 경로 계산 실패 시 BaseDirectory로 폴백.
            }

            return Path.Combine(baseDir, "output");
        }

        /// <summary>
        /// 정렬되지 않은 샘플에서 백분위수를 계산한다. 샘플이 없으면 -1.
        /// </summary>
        private static double Percentile(List<double> samples, double fraction)
        {
            if (samples == null || samples.Count == 0)
            {
                return -1.0;
            }

            var sorted = new List<double>(samples);
            sorted.Sort();

            if (sorted.Count == 1)
            {
                return sorted[0];
            }

            double rank = fraction * (sorted.Count - 1);
            int lo = (int)Math.Floor(rank);
            int hi = (int)Math.Ceiling(rank);
            if (lo == hi)
            {
                return sorted[lo];
            }

            double weight = rank - lo;
            return sorted[lo] + (sorted[hi] - sorted[lo]) * weight;
        }

        /// <summary>
        /// 샘플 최댓값. 샘플이 없으면 -1.
        /// </summary>
        private static double Max(List<double> samples)
        {
            if (samples == null || samples.Count == 0)
            {
                return -1.0;
            }

            double max = samples[0];
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i] > max)
                {
                    max = samples[i];
                }
            }

            return max;
        }
    }
}
