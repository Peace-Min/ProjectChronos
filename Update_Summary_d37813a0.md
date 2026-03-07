# 소스코드 변경 및 업데이트 요약 (Commit `d37813a0` 기준)

## 1. 개요
지정된 커밋 `d37813a0` 이후 UI 최신화 및 마커 렌더링 최적화를 위해 진행된 주요 변경 사항, 추가된 파일 및 수정 위치를 정리한 문서입니다.

## 2. 추가된 파일 (New Files)
타임라인 마커의 겹침을 X축 너비 기준으로 계산하고, 충돌 시 높이(Level)를 자동 배정하기 위해 새로운 사용자 정의 패널 클래스와 컨버터가 추가되었습니다.

*   **`ProjectChronos/Views/EventMarkerPanel.cs`**:
    *   기존 `Canvas` 대신 이벤트 마커의 배치를 전담하는 고도화된 커스텀 패널.
    *   각 마커의 실제 레이블 너비를 실측하여 X축 충돌을 연산하고 자동 단차(Level)를 부여합니다.
*   **`ProjectChronos/Converters/LevelToMarginConverter.cs`**:
    *   `EventMarkerPanel`이 계산한 Level(정수) 값을 바탕으로 UI 마커 레이블의 Top Margin 값을 변환해 주는 컨버터입니다.

## 3. 삭제된 파일 (Deleted Files)
*   **`ProjectChronos/Views/TimeToOffsetConverter.cs`**:
    *   이전 방식(비율 변환)에서 사용되던 컨버터로, `EventMarkerPanel` 도입에 따라 더 이상 필요하지 않아 삭제되었습니다.

## 4. 업데이트된 주요 파일 및 수정 위치 (Modified Files)

### 4.1. `ProjectChronos/Models/SimulationModels.cs`
*   **수정 위치:** `SimulationEventMarker` 클래스 내부
*   **변경 내용:**
    *   UI 하이라이트 처리를 위해 `ProjectChronos.Core.ViewModelBase` 상속 추가.
    *   `IsHighlighted` 상태 속성(Property) 추가 (현재 시간과 마커가 겹칠 때 UI 이펙트를 주기 위함).

### 4.2. `ProjectChronos/ViewModels/SimulationReplayViewModel.cs`
*   **수정 위치:** 생성자, 데이터 초기화 셋업(`Initialize`), 속성 정의부
*   **변경 내용:**
    *   `Events` 컬렉션의 제네릭 타입을 그룹 단위(`SimulationMarkerGroup`)에서 개별 이벤트 단위(`SimulationEventMarker`)로 변경. `EventMarkerPanel`에서 개별 배치를 수행하기 때문입니다.
    *   `CurrentEvents` 활성화 시 이전 활성화 이벤트들의 `IsHighlighted` 속성을 `false`로, 신규 맵핑된 이벤트들은 `true`로 설정하도록 최적화 로직 추가.

### 4.3. `ProjectChronos/ViewModels/MainWindowViewModel.cs`
*   **수정 위치:** `exampleEvents` 더미 데이터 선언부
*   **변경 내용:**
    *   너무 방대했던 시나리오별 더미 데이터를 간소화하여, 테스트에 필수적인 주요 이벤트(위험 상황, 네트워크 지연 등 중첩 케이스 포함) 5개로 축소했습니다.

### 4.4. `ProjectChronos/Views/SimulationReplayView.xaml`
*   **수정 위치:** `<ItemsControl>` 렌더링 영역 및 마커 데이터 템플릿
*   **변경 내용:**
    *   `ItemsControl.ItemsPanel`을 새로 만든 `local:EventMarkerPanel`로 교체.
    *   `TimeToOffsetConverter` 바인딩 제거 및 `local:EventMarkerPanel.Timestamp` Attached Property 바인딩 적용.
    *   `IsHighlighted` DataTrigger 추가: 현재 시간에 해당하는 마커 텍스트/보더 레이블에 더 밝은 다크 그레이색과 테두리 강조 효과(Glowing) 적용.
    *   **롤백 대비 주석화:** 기존 마우스 오버 시 뜨던 상세 팝업(`Popup`) 코드 및 단일 라인 요약 UI 제거 코드를 완전히 지우지 않고, 추후 재사용을 위해 `<!-- ... -->` XAML 주석으로 감싸 그대로 보존했습니다.

## 5. 요약
가장 큰 변화 구조는 ViewModel에서 마커 겹침을 제어하던 것(그룹화)을 **View의 커스텀 패널(`EventMarkerPanel`)에 역할을 위임**한 것입니다. 이에 따라 데이터 주입 구조가 개별 `Item` 단위로 슬림해졌으며, 재생 바에 따른 직관적인 이벤트 하이라이트 기능이 추가되었습니다.
