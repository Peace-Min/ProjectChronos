using System;

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
    public class SimulationEventMarker
    {
        /// <summary>
        /// 이벤트 발생 시점 (초 단위)
        /// </summary>
        public double Timestamp { get; set; }

        /// <summary>
        /// 이벤트 중요도 (마커 색상 및 모양 결정)
        /// </summary>
        public EventPriority Priority { get; set; }

        /// <summary>
        /// 이벤트 제목 (간단한 요약)
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 이벤트 보조 제목 (타이틀 하위 개념)
        /// </summary>
        public string Subtitle { get; set; }

        /// <summary>
        /// 이벤트 상세 설명
        /// </summary>
        public string Description { get; set; }

        public SimulationEventMarker(double timestamp, EventPriority priority, string title, string subtitle, string description)
        {
            Timestamp = timestamp;
            Priority = priority;
            Title = title;
            Subtitle = subtitle;
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
