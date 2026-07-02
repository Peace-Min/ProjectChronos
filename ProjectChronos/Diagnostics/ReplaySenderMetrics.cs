using System.Threading;

namespace ProjectChronos.Diagnostics
{
    /// <summary>
    /// 송신부(SimulationReplayViewModel) 재생 파이프라인 누적 카운터.
    /// 모든 증가는 Interlocked라 UI/스레드풀 어디서든 안전하며 비용은 무시 가능 수준.
    /// 소비자(하네스)는 주기적으로 스냅샷을 읽어 초당 비율을 계산한다.
    /// </summary>
    public sealed class ReplaySenderMetrics
    {
        private long _tickCount;
        private long _currentTimeChangedCount;
        private long _dispatcherPostCount;
        private long _sliderNotifyCount;
        private long _displayNotifyCount;
        private long _messageSentCount;
        private long _messageThrottledCount;

        public long TickCount => Interlocked.Read(ref _tickCount);
        public long CurrentTimeChangedCount => Interlocked.Read(ref _currentTimeChangedCount);
        public long DispatcherPostCount => Interlocked.Read(ref _dispatcherPostCount);
        public long SliderNotifyCount => Interlocked.Read(ref _sliderNotifyCount);
        public long DisplayNotifyCount => Interlocked.Read(ref _displayNotifyCount);
        public long MessageSentCount => Interlocked.Read(ref _messageSentCount);
        public long MessageThrottledCount => Interlocked.Read(ref _messageThrottledCount);

        public void OnTick() => Interlocked.Increment(ref _tickCount);
        public void OnCurrentTimeChanged() => Interlocked.Increment(ref _currentTimeChangedCount);
        public void OnDispatcherPost() => Interlocked.Increment(ref _dispatcherPostCount);
        public void OnSliderNotify() => Interlocked.Increment(ref _sliderNotifyCount);
        public void OnDisplayNotify() => Interlocked.Increment(ref _displayNotifyCount);
        public void OnMessageSent() => Interlocked.Increment(ref _messageSentCount);
        public void OnMessageThrottled() => Interlocked.Increment(ref _messageThrottledCount);
    }
}
