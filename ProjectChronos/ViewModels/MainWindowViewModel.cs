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
                // [시나리오 1: 초기 이벤트] - 시작 직후
                new Models.SimulationEventMarker(5.0, Models.EventPriority.Medium, "시뮬레이션 시작", "초기 상태 설정 완료"),
                
                // [시나리오 2: 간격이 좁은 이벤트] - Range 기반 감지 테스트 (0.3초 간격)
                new Models.SimulationEventMarker(15.0, Models.EventPriority.High, "센서 활성화", "센서가 활성화되었습니다"),
                new Models.SimulationEventMarker(15.3, Models.EventPriority.Medium, "데이터 수신 시작", "센서로부터 데이터 수신 중"),
                
                // [시나리오 3: 중간 간격 이벤트] - 일반적인 이벤트 간격 (2초)
                new Models.SimulationEventMarker(25.0, Models.EventPriority.Medium, "1차 분석 완료", "초기 데이터 분석이 완료되었습니다"),
                new Models.SimulationEventMarker(27.0, Models.EventPriority.High, "이상 징후 감지", "임계값 초과 감지됨 (87%)"),
                
                // [시나리오 4: 넓은 간격 이벤트] - 고속 재생 시 건너뜀 테스트 (10초 간격)
                new Models.SimulationEventMarker(40.0, Models.EventPriority.High, "위험 상황 발생", "엔티티 A와 B가 충돌 위험 거리(5m) 이내 진입"),
                new Models.SimulationEventMarker(50.0, Models.EventPriority.High, "회피 기동 시작", "자동 회피 알고리즘 작동 중"),
                new Models.SimulationEventMarker(60.0, Models.EventPriority.Medium, "안전 거리 확보", "충돌 위험 해제됨"),
                
                // [시나리오 5: 연속 이벤트] - 매우 좁은 간격 (0.1초)
                new Models.SimulationEventMarker(80.0, Models.EventPriority.High, "시스템 부하 상승", "CPU 사용률 85%"),
                new Models.SimulationEventMarker(80.01, Models.EventPriority.High, "메모리 경고", "메모리 사용률 90%"),
                new Models.SimulationEventMarker(80.02, Models.EventPriority.High, "메모리 경고", "메모리 사용률 90%"),
                new Models.SimulationEventMarker(80.05, Models.EventPriority.High, "과부하 임계", "시스템 부하 위험 수준"),
                
                // [시나리오 6: 긴 간격 이벤트] - 20초 이상 간격
                new Models.SimulationEventMarker(100.0, Models.EventPriority.Medium, "정상화 완료", "시스템이 정상 상태로 복귀했습니다"),
                new Models.SimulationEventMarker(150.0, Models.EventPriority.Medium, "중간 체크포인트", "시뮬레이션 50% 진행"),
                
                // [시나리오 7: 종료 시퀀스]d
                new Models.SimulationEventMarker(200.0, Models.EventPriority.Medium, "최종 분석 시작", "누적 데이터 분석 중"),
                new Models.SimulationEventMarker(220.0, Models.EventPriority.High, "결과 생성 완료", "시뮬레이션 결과 리포트 생성됨"),
                new Models.SimulationEventMarker(240.0, Models.EventPriority.Medium, "시뮬레이션 종료", "모든 작업이 완료되었습니다"),
            };

            SimulationReplayViewModel.Initialize(250.0, exampleEvents);
        }
    }
}
