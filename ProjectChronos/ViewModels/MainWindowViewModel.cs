using ProjectChronos.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectChronos.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public SimulationReplayViewModel SimulationReplayViewModel { get; } = new SimulationReplayViewModel();

        public MainWindowViewModel()
        {
            // [리팩토링] 각 이벤트 종류당 발생 가능한 이벤트는 오직 1개 뿐입니다. (총 5개 이벤트)
            var exampleEvents = new List<Models.SimulationEventMarker>
            {
                new Models.SimulationEventMarker { Timestamp = 5.0,  EventType = Models.SimulationEventType.SearchRadar,    Title = "탐색레이더 감지", DescriptionLabel = "방위각", Description = "135도",   RangeBTWLabel = "표적거리", RangeBTW = "150km" },
                new Models.SimulationEventMarker { Timestamp = 14.9, EventType = Models.SimulationEventType.TrackRadar,     Title = "추적레이더 할당", DescriptionLabel = "추적오차", Description = "±5.2m", RangeBTWLabel = "표적거리", RangeBTW = "120km" },
                new Models.SimulationEventMarker { Timestamp = 15.0, EventType = Models.SimulationEventType.LaunchApproval, Title = "교전 권고 및 승인", DescriptionLabel = "위협등급", Description = "심각",    RangeBTWLabel = "예상타격", RangeBTW = "45초 후" },
                new Models.SimulationEventMarker { Timestamp = 15.0, EventType = Models.SimulationEventType.MissileLaunch,  Title = "유도탄 발사",     DescriptionLabel = "발사대",   Description = "1번 런처", RangeBTWLabel = "상대거리", RangeBTW = "88km" },
                new Models.SimulationEventMarker { Timestamp = 30.0, EventType = Models.SimulationEventType.Intercept,      Title = "표적 요격 완료",   DescriptionLabel = "교전결과", Description = "파괴 확인", RangeBTWLabel = "잔해상태", RangeBTW = "해상 낙하" }
            };

            // 전체 시뮬레이션 시간 40초를 기준으로 초기화
            SimulationReplayViewModel.Initialize(40, exampleEvents);
        }
    }
}
