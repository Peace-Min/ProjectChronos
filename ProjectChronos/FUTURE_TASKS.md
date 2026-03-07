# FUTURE_TASKS: 이벤트 마커 동적 스태킹 보완 항목

본 문서는 현재까지 진행된 이벤트 마커 가시성 개선 작업의 미비점과 향후 반드시 보완해야 할 핵심 요구사항을 정리한 문서입니다. 차기 에이전트 혹은 개발자는 아래 내용을 바탕으로 구현을 고도화해야 합니다.

## 1. 핵심 요구사항: 물리적 중첩 방지 (Dynamic Neighborhood Stacking)
현재 구현체는 동일 시각(또는 근접 시각) 이벤트를 수직으로 쌓고 있으나, **레이블의 실제 가로 길이(Physical Width)**를 고려한 동적 스태킹이 완벽하지 않습니다.

### 보완 목표
- **EventMarkerPanel의 역할 강화**: WPF의 `Panel.MeasureOverride`와 `ArrangeOverride`를 100% 활용하여, 자식 요소들의 `DesiredSize.Width`를 실시간으로 측정해야 합니다.
- **X축 기준 중첩 판정**: 
  - 시간값(`Timestamp`)이 다르더라도 레이블의 텍스트가 길어서 가로 영역이 겹친다면, 무조건 Y축 레벨을 높여서 배치해야 합니다.
  - 현재는 단순히 시간차로만 계산하는 경향이 있으나, **창 크기를 줄이거나 폰트가 커져서 발생하는 물리적 겹침**을 감지하는 것이 핵심입니다.

## 2. 세부 보완 항목
- [ ] **중첩 판정 로직 정교화**: `EventMarkerPanel.cs` 내의 `ArrangeOverride`에서 이전 아이템의 `(Center_X + Half_Width)`와 현재 아이템의 `(Center_X - Half_Width)`를 비교하여 `Layer` 레벨을 결정할 것.
- [ ] **DataTemplate 구조 최적화**: 현재 `ItemsControl` 내부의 `StackPanel`로 묶인 구조는 `EventMarkerPanel`이 개별 레이블의 너비를 개별적으로 측정하기 어렵게 만듭니다. 동일 시각 이벤트도 `EventMarkerPanel`의 직접적인 자식으로 평탄화(Flatten)하여 배치하는 것을 검토하십시오.
- [ ] **프로토타입과의 일치성**: `timeline_prototype_v2.html`의 자바스크립트 로직은 이미 물리적 너비를 예측하여 `level`을 계산하고 있습니다. 이 로직을 C# `EventMarkerPanel`에서 `DesiredSize.Width` 기반으로 정확히 재현해야 합니다.

## 3. 참조 파일
- `docs_technical_analysis.md`: 패널 방식의 우월성 분석
- `timeline_prototype_v2.html`: 이상적인 스태킹 알고리즘 구현 예시 (JS)
- `SimulationReplayView.xaml`: 현재 적용된 UI 구조
- `EventMarkerPanel.cs`: 실제 레이아웃 로직이 포함된 클래스
