# ProjectChronos - Timeline Export Refactoring Handoff Specification

## 🤖 [For AI Agents] Overview
이 문서는 **"분석보고서(Report Timeline Export) 렌더링 방식의 UI/UX 전면 개편"** 에 대한 선행 기획(프로토타이핑) 작업 내용과 제약조건, 그리고 실제 구현(`Implementation`)을 진행할 때 변경해야 할 C# WPF 소스 코드의 위치와 구체적 로직을 기록한 에이전트 전용 명세서입니다.

사용자가 대화방에서 **"구현 시작"** 또는 **"본 개발 시작"** 이라고 지시하면, 즉시 이 문서를 읽고 아래의 가이드라인에 맞춰 `ReportTimelineExportViewModel.cs` 파일 수정을 시작하십시오.

---

## 🎨 1. Reference Prototypes (최종 시안)

구현 시 다음 HTML 프로토타입 소스를 분석하여 좌표 및 색상, 렌더링 규칙을 그대로 모방(Clone)해야 합니다.
- **최종 타겟 HTML (WPF 포팅 타겟):**
  - `C:\Users\minph\source\repos\ProjectChronos\ProjectChronos\output\시안\prototype_v5_high_fidelity_1.html` (오렌지 톤 기본 버전)
  - `C:\Users\minph\source\repos\ProjectChronos\ProjectChronos\output\playwright\prototype_v5_high_fidelity.html` (내부 보라색 강조 버전 로직 포함)
- **최종 시안 렌더링 이미지:** 
  - `C:\Users\minph\source\repos\ProjectChronos\ProjectChronos\output\시안\prototype_v7_matlab_color.png` (클라이언트 최종 보고용)

---

## 🎯 2. UI/UX Core Rules (반드시 지켜야 할 5대 원칙)

1. **시간 비례 X축 및 최소 간격(Minimum Gap) 보장**
   - 이벤트 간의 간격은 물리적인 시간(Timestamp 차이)에 비례(Proportional)하여야 합니다.
   - 단, `0.01` 같이 매우 짧은 구간은 글자가 겹치지 않게 '최소 픽셀 보장 거리(예: 30px)'를 강제로 부여하고, 남은 캔버스 Width를 나머지 긴 시간 구간들이 비율별로 나눠 갖도록 수학적 분배를 해야 합니다.
2. **타임라인 양 끝 꼬리선 절단 (Tail Clipping)**
   - 파란색 주축(Main Timeline Rail)은 1번 노드(시작 X)부터 마지막 노드(끝 X)까지만 정확히 그려야 하며, 앞뒤로 불필요하게 튀어나온 선(여분 - 구간)을 제거해야 합니다.
3. **마이크로 계층 분리 및 MATLAB 보라색 강조 (Micro Interval)**
   - 짧은 구간은 수직 점선을 일반 높이(`intervalY`)보다 높은 상위 계층(`microY`)까지 연장합니다.
   - 좁은 구간 안에 무리하게 가로 화살표(`> <`)를 넣지 않고 생략합니다.
   - 연장된 수직 점선과 `0.01` 텍스트 값은 시각적 차별화를 위해 `MATLAB Purple(#7e2f8e)` 색상으로 렌더링합니다.
4. **가로 중심축 일치 (Middle Baseline Alignment)**
   - 주황색 시간 구간 텍스트(`12.49` 등)는 점선 위로 붕 떠 있지 않고, 점선을 정 중앙으로 관통하듯 `Baseline`이 맞물려 배치되어야 합니다. (y축 좌표 직접 일치)
5. **카드 뷰 완벽 직렬 세로 스태킹 (Merged Column Stacking) [가장 중요]**
   - 모든 하단 카드뷰는 본인 노드(Event Marker)의 센터(X) 정렬을 원칙으로 합니다.
   - **X축 충돌 발생 시:** 겹치는 카드들을 가로로 밀어내어(흩뿌려) 어긋나게 만들면 안 됩니다! 겹치는 노드들의 그룹 **평균 센터 X (Group Average X)** 좌표를 하나 구한 뒤, 겹치는 모든 카드를 이 공통 X 좌표에 맞춰 완벽한 '일자 수직 기둥(Straight Stack)' 형태로 Y축만 늘려가며 직렬 배치해야 합니다.
   - 클라이언트 시안을 위해 카드의 높이(Height)는 `160px`에서 `120px`로 축소하며, `strEventDescription` (설명글) 라벨과 값을 렌더링에서 완전히 고의 누락시킵니다.

---

## 🛠 3. Code Modification Targets (WPF C# 구현 위치)

다음 파일의 메서드들을 리팩토링합니다. 원본의 그리기 알고리즘 구조를 크게 변경하게 됩니다.
**대상 파일:** `c:\Users\minph\source\repos\ProjectChronos\ProjectChronos\ViewModels\ReportTimelineExportViewModel.cs`

### A. 선행 데이터 및 X축 렌더링 설계 (`BuildLayout` 등)
- 현재 균등 피치(Equal pitch)를 생성하는 등분할 로직을 폐기하십시오.
- `SimulationEventContext` 그룹 리스트를 순회하며 `(마지막시간 - 처음시간)` 전체 범위를 구한 뒤 비율에 맞추되, 간격이 너무 좁은 요소들을 1차로 식별하여 `Minimum Gap`을 할당하고, 잔여 픽셀 폭(`usableWidth - 예약된 Gap 합계`)을 바탕으로 큰 간격들을 비례 배분하여 `CenterX` 배열을 계산하는 로직을 신규 작성하십시오.

### B. 선/텍스트 요소 수정 (`BuildIntervalItems`)
- 상단 간격(Interval) 그리기 시 이전 노드와 현 노드의 시간차가 마이크로(예: < 1.0s) 구역인지를 확인합니다.
- 마이크로일 경우, 가로 점선과 양끝 꺾쇠 화살표를 생성하는 파트를 패스하고, 대신 특정 높이(Y축 음수/더 높은 곳)까지 수직 점선을 높이며 펜 컬러 색상을 `#7E2F8E` 브러시로 변경하는 전용 `DrawingContext` 제어 로직을 추가하십시오.
- `FormattedText` 출력 시, 텍스트가 줄 위를 걷는 것이 아니라 관통하도록 `y`축 값을 보정하십시오.

### C. 카드 정렬/충돌 알고리즘 (`BuildDetailColumnLefts` 등)
- 기존에는 카드의 Width 때문에 X축 Left 값을 산출할 때 겹치면 단순히 `lefts[index] = Math.Min...` 으로 서로 밀어냈습니다.
- **새로운 로직:** 카드 간의 충돌 검사를 먼저 수행하여 "겹치는 노드 덩어리(Collision Cluster)"를 그룹화합니다. 이 클러스터에 속한 이벤트 마커들의 `CenterX` 평균값을 구하여 통일된 `MergeCenterX` 하나로 묶습니다. 해당 클러스터에 속한 모든 카드는 무조건 `MergeCenterX - (CardWidth / 2)` 라는 단일 Left 값을 가지도록 강제되며, 렌더링 로직(`BuildSlotItems` 등)에서는 이 클러스터 내 카드들이 각자 `Y = baseCardY + (height + spacing) * 순번` 으로 무조건 '세로'로 떨어지도록 그리기 Y축을 할당해야 합니다.

### D. 카드 렌더링 상세 (`BuildDetailedInfos` 또는 연관 그리기 메서드)
- 카드 드로잉 시 `Height`를 줄입니다.
- `strEventDescription` 렌더링 텍스트 블록 두 줄(`Title`, `Value`)을 생략(Remove)합니다.
- Main Timeline Rail(주축 굵은 파란 선)을 그릴 때 앞뒤 하드코딩된 X 시작/끝 점이 아니라 배열의 첫 `CenterX`와 마지막 `CenterX` 포인트까지만 딱 맞춰 선을 절단(Line Clip)하여 그립니다.

---
> **[Agent Note]**
> 위 내용을 완벽히 파악했다면, 구현 시작 시 파일을 스캔하며 이 문서에서 요구한 수학적 정렬, CSS(Brush) 대응, 레이아웃 변경 사항들을 단번에 적용하고 컴파일 가능한 코드로 제공하십시오. 절대 기존의 UI 코드를 그대로 남겨 둔 채 텍스트만 얹지 마십시오.
