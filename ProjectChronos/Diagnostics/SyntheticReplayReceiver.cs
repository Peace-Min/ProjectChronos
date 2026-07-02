using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ProjectChronos.Messages;

namespace ProjectChronos.Diagnostics
{
    /// <summary>
    /// 메인 SW 수신부(SingleSimChartControlViewModel)의 **현행 개선판** 패턴을 미러링한 합성 수신부.
    /// 미러 대상: latest-state 통합 + 단일 pending + 재생 중 33ms cadence 구조
    /// (저장소 내 ExternalDrafts/OriginalSource는 개선 전 cancel-per-message 구버전이므로 대조 금지).
    /// 원본과의 의도적 차이 1가지: force(Seek/Stopped) 메시지가 pending 중 도착해도
    /// 의미가 유실되지 않도록 _isForcePending 래치를 추가함 (원본 개선 권장사항 선반영).
    /// Dispatcher 우선순위와 합성 렌더 비용(UI 스레드 busy-wait)을 설정할 수 있다.
    ///
    /// 측정 목적: 송신부 스로틀 적용 전/후로 Background 우선순위 수신부가
    /// 실행 슬롯을 얻는지(render completed/sec)와 post→run 레이턴시를 비교한다.
    /// </summary>
    public sealed class SyntheticReplayReceiver
    {
        /// <summary>재생 중 수신부 렌더링 최소 간격 (밀리초, 메인 SW와 동일 30Hz)</summary>
        private const int ReplayRenderIntervalMs = 33;

        private readonly object _gate = new object();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<double> _latencySamplesMs = new List<double>();

        private double _latestReplayTime = double.NaN;
        private bool _isForcePending;
        private bool _isRenderPending;
        private DateTime _lastPlaybackRenderAt = DateTime.MinValue;

        private long _receivedCount;
        private long _renderScheduledCount;
        private long _renderCompletedCount;
        private long _renderCoalescedCount;

        public DispatcherPriority Priority { get; set; } = DispatcherPriority.Background;

        /// <summary>합성 렌더 비용 (UI 스레드 busy-wait, 밀리초)</summary>
        public int RenderCostMs { get; set; }

        public bool IsEnabled { get; set; } = true;

        public long ReceivedCount => Interlocked.Read(ref _receivedCount);
        public long RenderScheduledCount => Interlocked.Read(ref _renderScheduledCount);
        public long RenderCompletedCount => Interlocked.Read(ref _renderCompletedCount);
        public long RenderCoalescedCount => Interlocked.Read(ref _renderCoalescedCount);

        /// <summary>마지막으로 실제 렌더된 재생 시간 (하네스 표시용)</summary>
        public double LastRenderedTime { get; private set; } = double.NaN;

        /// <summary>
        /// 누적된 레이턴시 샘플을 꺼내고 비운다. (p50/p95/max 계산은 호출자 몫)
        /// </summary>
        public List<double> DrainLatencySamplesMs()
        {
            lock (_gate)
            {
                var drained = new List<double>(_latencySamplesMs);
                _latencySamplesMs.Clear();
                return drained;
            }
        }

        /// <summary>
        /// 송신부 SimulationTimeChanged 이벤트 핸들러.
        /// 재생 중에는 스레드풀(Task.Run 발행), 시크 시에는 UI 스레드에서 호출된다.
        /// </summary>
        public void OnSimulationTimeChanged(SimulationTimeChangedMessage message)
        {
            if (!IsEnabled) { return; }

            Interlocked.Increment(ref _receivedCount);

            bool forceRender = message.ChangeKind != SimulationTimeChangeKind.Playback;

            lock (_gate)
            {
                // 1. 최신 시간 갱신 (latest-state).
                _latestReplayTime = message.NewTime;
                if (forceRender) { _isForcePending = true; }

                // 2. 재생 중이면 렌더링 cadence 제한.
                if (!forceRender && !_isForcePending)
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastPlaybackRenderAt).TotalMilliseconds < ReplayRenderIntervalMs)
                    {
                        return;
                    }
                }

                // 3. pending 렌더링이 있으면 최신 시간만 갱신된 상태이므로 추가 예약 불필요.
                if (_isRenderPending)
                {
                    Interlocked.Increment(ref _renderCoalescedCount);
                    return;
                }

                _isRenderPending = true;
            }

            Interlocked.Increment(ref _renderScheduledCount);
            double postedAtMs = _clock.Elapsed.TotalMilliseconds;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                double renderTime;
                lock (_gate)
                {
                    _isRenderPending = false;
                    _isForcePending = false;
                    _lastPlaybackRenderAt = DateTime.UtcNow;
                    renderTime = _latestReplayTime;

                    // post → run 레이턴시 기록 (Background 기아 정도의 직접 지표).
                    _latencySamplesMs.Add(_clock.Elapsed.TotalMilliseconds - postedAtMs);
                }

                // 합성 렌더 비용: 실제 차트/지도 갱신의 UI 스레드 점유를 모사.
                if (RenderCostMs > 0)
                {
                    var spin = Stopwatch.StartNew();
                    while (spin.ElapsedMilliseconds < RenderCostMs) { /* busy-wait */ }
                }

                LastRenderedTime = renderTime;
                Interlocked.Increment(ref _renderCompletedCount);
            }), Priority);
        }
    }
}
