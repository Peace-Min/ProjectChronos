# 이벤트 마커 가시성 개선 작업

- [x] 이벤트 마커 중첩 문제 분석 및 해결 방안 설계
- [x] HTML 프로토타입 제작 및 사용자 검토
    - [x] 수평 근접 레이블 중첩 방지 로직 추가 (Neighborhood Stacking)
- [x] WPF EventMarkerPanel 커스텀 패널 구현 (Measure/Arrange 알고리즘)
- [x] WPF SimulationReplayView.xaml 패널 교체 적용
- [x] 🔄 **[Self-Building Loop]** 자동화 UI 렌더링 검증 완성 및 성공
    1. [x] 통합 테스트 파이프라인 (`run_test.ps1`) 구축
    2. [x] RawViewWalker 기반 세부 요소 좌표 추출 (`dump_ui_tree.ps1`) 
    3. [x] JSON 좌표 검증 결과: 겹치는 이벤트(80.0s, 80.1s) 간 `Top` 좌표가 레이아웃 알고리즘에 의해 각각 다르게 Stacking 할당(`426` vs `391`)됨을 수학적으로 검증 완료.

