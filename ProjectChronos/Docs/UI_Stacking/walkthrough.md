# 이벤트 마커 스태킹 레이아웃 최종 검증 보고서

## 수행한 작업 요약

사용자의 스크린샷 피드백을 바탕으로 마커와 레이블이 타임라인 선에 올바르게 정렬되지 않는 원인을 분석하고, `DataTemplate` 구조를 전면 개편했습니다.

### 핵심 변경 사항

| 파일 | 변경 내용 |
|---|---|
| `SimulationReplayView.xaml` | `MarkerRoot` Grid를 **3행 구조**(상단 레이블공간 `*` / 마커 틱 `Auto` / 하단 여백 `*`)로 전면 개편. 레이블과 연결선은 Row 0 (Bottom), 마커 틱은 Row 1 (Center), HitArea는 RowSpan 3으로 배치 |
| `SimulationReplayView.xaml` | `EventMarkerPanel`에서 고정 `Height="200"` 및 `VerticalAlignment="Bottom"` 제거, 패널이 부모 공간을 `Stretch`로 온전히 차지하도록 수정 |
| `EventMarkerPanel.cs` | Stacking 알고리즘 (Level 배정)은 변경 없음 — 기존 로직이 정확히 동작했음을 좌표 검증으로 확인 |

---

## Self-Building Loop 수학적 검증 결과

`run_test.ps1` → `dump_ui_tree.ps1` 파이프라인을 통해 실제 렌더링 좌표를 추출했습니다.

```
타임라인 컨트롤 영역: Top ≈ 408~434px
이벤트 레이블 기준선: Top ≈ 322px (단독 이벤트)

[Level 0] 40.0s  "위험 상황 발생"  Top: 322  ← 기준 레벨
[Level 0] 220.0s "시뮬레이션 종료" Top: 322  ← 기준 레벨

[Level 1] 80.0s  "시스템 부하 상승" Top: 287  ← 322 - 35px = Level +1
[Level 2] 80.1s  "네트워크 지연"    Top: 252  ← 287 - 35px = Level +2
```

**검증 통과**: 겹치는 80.0s ~ 80.1s 이벤트들이 35px 간격으로 위쪽(Bottom → Top)으로 정확히 쌓이며, 서로 겹치지 않는 것을 수학적으로 확인했습니다.

---

## 캡처 결과물

![UI 자동화 캡처](file:///C:/Users/minph/source/repos/ProjectChronos/ProjectChronos/ui_capture.png)
