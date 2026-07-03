using System;
using System.Collections.Generic;

namespace OSTES.Common
{
    /// <summary>
    /// 재생 프레임 시간 인덱스 (수신부 공용).
    ///
    /// 시간(초)을 해상도 그리드 칸 번호(정수 키)로 변환해 저장/조회한다.
    /// double을 완전 일치 키로 쓰면 같은 십진 시간이라도 계산 경로(송신부 양자화 vs DB 원본)에
    /// 따라 마지막 비트(ULP)가 달라 조회가 빗나갈 수 있는데, 반올림 정수 키는 이 노이즈를
    /// 흡수하므로 같은 칸이면 반드시 같은 키가 된다.
    ///
    /// 조회는 2단이다: 정밀 키 → 10ms(해상도 확장 전 기본 기록 그리드) 키 폴백 → 실패.
    /// 실패 시 호출측은 렌더하지 않는다(해당 시간에 데이터 없음 = 마커 미갱신).
    ///
    /// 사용 규약: 데이터 로드 시점에 Configure(시나리오 시간해상도) 후 Add로 구축하고,
    /// 이후에는 조회 전용으로 사용한다(재생 중 구조 변경 금지).
    /// </summary>
    /// <typeparam name="TFrame">프레임 데이터 타입 (예: List&lt;AddSeriesPointDTO&gt;)</typeparam>
    public sealed class ReplayFrameIndex<TFrame>
    {
        /// <summary>해상도 확장 전 기본 기록 그리드 (10ms).</summary>
        private const double LegacyResolutionSeconds = 0.01;

        private readonly Dictionary<long, TFrame> _framesByKey = new Dictionary<long, TFrame>();

        /// <summary>초당 키 단위 수 (= 1 / 시간해상도). Configure에서 확정.</summary>
        private double _unitsPerSecond = 1.0 / LegacyResolutionSeconds;

        /// <summary>10ms 폴백 조회용 키 단위 (10ms가 현재 키 공간에서 몇 칸인지).</summary>
        private long _coarseUnits = 1;

        public int Count => _framesByKey.Count;

        /// <summary>등록된 전체 프레임 열거 (범위 자동 계산 등 전수 순회용).</summary>
        public IEnumerable<TFrame> Frames => _framesByKey.Values;

        /// <summary>
        /// 시나리오 시간해상도(초)로 키 단위를 확정한다.
        /// 송신부 SetTimeResolution과 동일한 소스의 값이어야 한다. (예: 1ms → 0.001)
        /// Add 호출 전에 1회 호출하며, 구축 후 변경하지 않는다.
        /// </summary>
        public void Configure(double timeResolutionSeconds)
        {
            double resolution = timeResolutionSeconds > 0.0 ? timeResolutionSeconds : LegacyResolutionSeconds;

            // 1.0 / 1e-5 = 99999.999... 이므로 반올림으로 단위를 확정한다.
            _unitsPerSecond = Math.Round(1.0 / resolution);
            _coarseUnits = Math.Max(1L, (long)Math.Round(LegacyResolutionSeconds * _unitsPerSecond));
        }

        /// <summary>지정 시간의 프레임을 등록한다. 같은 칸에 재등록하면 덮어쓴다.</summary>
        public void Add(double seconds, TFrame frame)
        {
            _framesByKey[ToKey(seconds)] = frame;
        }

        /// <summary>
        /// 지정 시간의 프레임을 조회한다.
        /// 정밀 키 미스 시 10ms 그리드 키로 폴백하고, 그래도 없으면 false를 반환한다.
        /// </summary>
        public bool TryGetFrame(double seconds, out TFrame frame)
        {
            long fineKey = ToKey(seconds);
            if (_framesByKey.TryGetValue(fineKey, out frame))
            {
                return true;
            }

            long coarseKey = (long)Math.Round(fineKey / (double)_coarseUnits) * _coarseUnits;
            return _framesByKey.TryGetValue(coarseKey, out frame);
        }

        private long ToKey(double seconds)
        {
            return (long)Math.Round(seconds * _unitsPerSecond);
        }
    }
}
