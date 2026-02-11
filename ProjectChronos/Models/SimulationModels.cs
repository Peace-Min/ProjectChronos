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
        /// 이벤트 상세 설명
        /// </summary>
        public string Description { get; set; }

        public SimulationEventMarker(double timestamp, EventPriority priority, string title, string description)
        {
            Timestamp = timestamp;
            Priority = priority;
            Title = title;
            Description = description;
        }
    }
}
