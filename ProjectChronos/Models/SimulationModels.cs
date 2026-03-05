using System;
using System.ComponentModel;

namespace ProjectChronos.Models
{
    /// <summary>
    /// 시뮬레이션 이벤트 종류 (다중 차선 분배용)
    /// </summary>
    public enum SimulationEventType
    {
        SearchRadar,    // 탐색레이더
        TrackRadar,     // 추적레이더
        LaunchApproval, // 발사승인
        MissileLaunch,  // 미사일발사
        Intercept       // 요격
    }

    /// <summary>
    /// 시뮬레이션 타임라인에 표시될 이벤트 마커 데이터 모델
    /// </summary>
    public class SimulationEventMarker : INotifyPropertyChanged
    {
        private bool _isActive;

        /// <summary>
        /// (UI 트랜지션용) 현재 재생 시간이 마커 위치와 일치하는지 여부
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// 이벤트 발생 시점 (초 단위)
        /// </summary>
        public double Timestamp { get; set; }

        /// <summary>
        /// 이벤트 종류 (차선 매핑 용도)
        /// </summary>
        public SimulationEventType EventType { get; set; }

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

        public SimulationEventMarker() { }

        public SimulationEventMarker(double timestamp, SimulationEventType eventType, string title, string subtitle, string description)
        {
            Timestamp = timestamp;
            EventType = eventType;
            Title = title;
            DescriptionLabel = subtitle;
            Description = description;
        }
    }
}
