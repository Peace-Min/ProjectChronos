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
                // ── [A] 단독 이벤트 (Low Priority, 완전 고립) ──────────────────
                new Models.SimulationEventMarker(30.0, Models.EventPriority.Low, "초기화 완료", "System Init", "모든 서브시스템 초기화 완료")
                {
                    RangeBTWLabel = "고도",
                    RangeBTW = "500m"
                },

                // ── [B] High Priority 단독 이벤트 ────────────────────────────
                new Models.SimulationEventMarker(80.0, Models.EventPriority.High, "위협 신호 탐지", "Threat Signal", "미확인 RF 신호 수신 — 추가 분석 필요")
                {
                    RangeBTWLabel = "신호 거리",
                    RangeBTW = "2.1km"
                },

                // ── [C] 픽셀상 겹치는 스택: 80.3s (80.0과 픽셀 기준 22px 이내) ──
                new Models.SimulationEventMarker(80.3, Models.EventPriority.Medium, "신호 분석 시작", "Signal Analysis", "위협 신호 주파수 분석 중"),

                // ── [D] 모두 고립된 단독 구간 ────────────────────────────────
                new Models.SimulationEventMarker(130.0, Models.EventPriority.Low, "중간 체크포인트", "Checkpoint A", "전방 스캔 정상"),

                // ── [E] 픽셀 겹침 3연속 스택 (150.0 / 150.2 / 150.4) ─────────
                new Models.SimulationEventMarker(150.0, Models.EventPriority.High, "전방 장애물", "Obstacle Front", "정면 1.2km 비행체 감지")
                {
                    RangeBTWLabel = "충돌 여유",
                    RangeBTW = "1.2km"
                },
                new Models.SimulationEventMarker(150.2, Models.EventPriority.Medium, "회피 경로 계산", "Evasion Calc", "좌측 회피 경로 계산 완료"),
                new Models.SimulationEventMarker(150.4, Models.EventPriority.Low, "회피 기동 시작", "Evasion Start", "회피 기동 명령 전달됨"),

                // ── [F] 동일 타임스탬프 2개 (클러스터 배지 '2') ──────────────
                new Models.SimulationEventMarker(200.0, Models.EventPriority.High, "엔진 오버로드", "Engine Overload", "2번 엔진 과부하 감지")
                {
                    RangeBTWLabel = "온도",
                    RangeBTW = "1,250°C"
                },
                new Models.SimulationEventMarker(200.0, Models.EventPriority.Medium, "냉각 시스템 가동", "Cooling Active", "비상 냉각 시스템 자동 투입")
                {
                    RangeBTWLabel = "냉각 유량",
                    RangeBTW = "12 L/min"
                },

                // ── [G] 동일 타임스탬프 3개 (클러스터 배지 '3') ──────────────
                new Models.SimulationEventMarker(240.0, Models.EventPriority.High, "목표 포착", "Target Lock", "레이더 목표 포착 — 추적 개시")
                {
                    RangeBTWLabel = "목표 거리",
                    RangeBTW = "4.8km"
                },
                new Models.SimulationEventMarker(240.0, Models.EventPriority.Medium, "무장 시스템 준비", "Weapon Ready", "지정 무장 시스템 활성화 대기 중"),
                new Models.SimulationEventMarker(240.0, Models.EventPriority.Low, "교전 규칙 확인", "ROE Check", "교전 규칙 승인 요청 전송됨"),

                // ── [H] 종료 구간 단독 이벤트 ────────────────────────────────
                new Models.SimulationEventMarker(280.0, Models.EventPriority.Low, "복귀 경로 설정", "RTB Route", "기지 복귀 경로 계산 완료")
                {
                    RangeBTWLabel = "기지 거리",
                    RangeBTW = "38km"
                },
            };

            SimulationReplayViewModel.Initialize(300.0, exampleEvents);
        }
    }
}
