using System;
using System.Diagnostics;
using System.Windows.Media;

namespace ProjectChronos.Diagnostics
{
    /// <summary>
    /// 저성능 PC 모사용 UI 스레드 부하 생성기.
    /// CompositionTarget.Rendering에서 프레임당 N ms busy-wait하여
    /// 프레임 예산이 빠듯한 환경(Background 슬롯 부족)을 재현한다.
    /// </summary>
    public sealed class UiLoadGenerator
    {
        private int _loadPerFrameMs;
        private bool _isSubscribed;

        /// <summary>프레임당 UI 스레드 점유 시간 (밀리초). 0이면 비활성.</summary>
        public int LoadPerFrameMs
        {
            get => _loadPerFrameMs;
            set
            {
                _loadPerFrameMs = Math.Max(0, value);
                UpdateSubscription();
            }
        }

        private void UpdateSubscription()
        {
            bool shouldSubscribe = _loadPerFrameMs > 0;
            if (shouldSubscribe == _isSubscribed) { return; }

            if (shouldSubscribe)
            {
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
            }

            _isSubscribed = shouldSubscribe;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            int budget = _loadPerFrameMs;
            if (budget <= 0) { return; }

            var spin = Stopwatch.StartNew();
            while (spin.ElapsedMilliseconds < budget) { /* busy-wait */ }
        }
    }
}
