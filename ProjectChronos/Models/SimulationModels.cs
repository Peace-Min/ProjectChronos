using System;
using ProjectChronos.Core;

namespace ProjectChronos.Models
{
    /// <summary>
    /// 이벤트 중요도 수준
    /// </summary>
    public enum EventPriority
    {
        High,   // 높음 (Critical - 빨강)
        Medium, // 중간 (Warning - 주황)
        Low     // 낮음 (Info - 파랑)
    }

    /// <summary>
    /// 시뮬레이션 타임라인에 표시될 이벤트 마커 데이터 모델
    /// </summary>
    public class SimulationEventMarker : ViewModelBase
    {
        /// <summary>
        /// 이벤트 발생 시점 (초 단위)
        /// </summary>
        public double Timestamp { get; set; }

        /// <summary>
        /// 이벤트 중요도 (마커 색상 및 모양 결정)
        /// </summary>
        public EventPriority Priority { get; set; } = EventPriority.Medium;

        /// <summary>
        /// 이벤트 제목 (간단한 요약)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 이벤트 상세 설명 라벨.
        /// </summary>
        public string DescriptionLabel { get; set; }

        /// <summary>
        /// 이벤트 상세 설명
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 타겟간 거리 라벨명.
        /// </summary>
        public string RangeBTWLabel { get; set; }

        /// <summary>
        /// 타겟간 거리.
        /// </summary>
        public string RangeBTW { get; set; }

        /// <summary>
        /// 소스 타겟 라벨명.
        /// </summary>
        public string SourceTargetLabel { get; set; }

        /// <summary>
        /// 소스 타겟.
        /// </summary>
        public string SourceTarget { get; set; }

        public bool HasDescription =>
            !string.IsNullOrWhiteSpace(DescriptionLabel) &&
            !string.IsNullOrWhiteSpace(Description);

        public bool HasRange =>
            !string.IsNullOrWhiteSpace(RangeBTWLabel) &&
            !string.IsNullOrWhiteSpace(RangeBTW);

        public bool HasSourceTarget =>
            !string.IsNullOrWhiteSpace(SourceTargetLabel) &&
            !string.IsNullOrWhiteSpace(SourceTarget);

        /// <summary>
        /// 동일 시간에 여러 이벤트가 있을 때, 대표 마커(Tick)를 그릴지 여부
        /// </summary>
        public bool IsPrimaryMarker { get; set; } = true;

        /// <summary>
        /// 대표 마커일 경우 표시할 우선순위 (그룹 내 MaxPriority 반영용)
        /// </summary>
        public EventPriority MarkerPriority { get; set; } = EventPriority.Medium;

        private bool _isHighlighted;
        /// <summary>
        /// 현재 시간과 겹쳐서 UI에서 하이라이트 상태인지 여부
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set => SetProperty(ref _isHighlighted, value);
        }

        public SimulationEventMarker() { }

        public SimulationEventMarker(double timestamp, EventPriority priority, string title, string subtitle, string description)
        {
            Timestamp = timestamp;
            Priority = priority;
            Title = title;
            DescriptionLabel = subtitle;
            Description = description;
        }
    }

    /// <summary>
    /// 동일 시간대에 발생한 이벤트들의 그룹 (시각적 마커 1개에 대응)
    /// </summary>
    public class SimulationMarkerGroup
    {
        public double Timestamp { get; }
        public System.Collections.Generic.List<SimulationEventMarker> Events { get; }

        /// <summary>
        /// 그룹 내 가장 높은 중요도 (마커 색상 결정용)
        /// </summary>
        public EventPriority MaxPriority { get; }

        public SimulationMarkerGroup(double timestamp, System.Collections.Generic.IEnumerable<SimulationEventMarker> events)
        {
            Timestamp = timestamp;
            Events = new System.Collections.Generic.List<SimulationEventMarker>(events);

            // 기본값 Low, 하나라도 High가 있으면 High, 그 외 Medium이 있으면 Medium
            MaxPriority = EventPriority.Low;
            if (Events.Exists(e => e.Priority == EventPriority.High))
            {
                MaxPriority = EventPriority.High;
            }
            else if (Events.Exists(e => e.Priority == EventPriority.Medium))
            {
                MaxPriority = EventPriority.Medium;
            }
        }
    }
}
