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
            // 검증 시나리오를 위한 테스트 이벤트
            var exampleEvents = new List<Models.SimulationEventMarker>
            {
                new Models.SimulationEventMarker(40.0, Models.EventPriority.Medium, "위험 상황 발생", "Proximity Alert", "엔티티 A와 B가 충돌 위험 거리(5m) 이내 진입"),

                new Models.SimulationEventMarker(80.0, Models.EventPriority.Medium, "시스템 부하 상승", "CPU Load High", "CPU 사용률 85%"),
                new Models.SimulationEventMarker(80.1, Models.EventPriority.Medium, "네트워크 지연1", "Network Lag", "핑 150ms 초과"), // 동시 발생 이벤트 추가
                new Models.SimulationEventMarker(80.1, Models.EventPriority.Medium, "네트워크 지연2", "Network Lag", "핑 150ms 초과"), // 동시 발생 이벤트 추가
                
                new Models.SimulationEventMarker(220.0, Models.EventPriority.Medium, "시뮬레이션 종료", "Job Done", "모든 작업이 완료되었습니다"),
            };

            SimulationReplayViewModel.Initialize(250.0, exampleEvents);
        }
    }
}
