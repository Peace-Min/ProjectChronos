# 이벤트 중첩 해결 방식 비교 분석

이벤트 마커와 레이블의 중첩을 해결하기 위한 두 가지 접근 방식(Converter vs Custom Panel)을 비교합니다.

## 1. 개요
현재 문제는 **"시간 간격이 좁을 때"**뿐만 아니라 **"시간은 다르지만 레이블 제목이 길어서 서로 겹칠 때"**도 해결해야 한다는 점입니다.

---

## 2. 방식별 상세 비교

| 비교 항목 | **IValueConverter 방식** | **Custom Panel (추천)** |
| :--- | :--- | :--- |
| **중첩 판단 기준** | 단순히 **Timestamp 값**만 비교 가능 (`values[0] - values[1] < 0.1` 등) | **실제 렌더링된 너비(Actual Width)** 기반으로 정밀 판단 가능 |
| **이웃 데이터 가시성** | 컨버터는 개별 아이템만 처리하므로, 다른 아이템의 위치를 알기 위해 복잡한 `MultiBinding`이 필요함 | 패널은 모든 자식(Children)을 한꺼번에 측정하므로 **이웃 간의 간격 계산이 기본 제공됨** |
| **동적 레이아웃** | 창 크기가 변해도 컨버터는 다시 계산되지 않음 (수동 트리거 필요) | 창 크기가 변하거나 폰트 크기가 바뀌면 **자동으로 재배치(Layout Pass)**가 일어남 |
| **MVVM 준수** | 뷰모델에서 '레벨' 정보를 미리 계산해서 줘야 할 수도 있음 (UI 로직이 VM으로 침범) | 뷰모델은 데이터만 관리하고, **UI 배치는 100% 뷰(Panel)가 전담**하여 분리가 완벽함 |
| **구현 난이도** | 낮음 (단순 수학 계산) | 중간 (Measure/Arrange 오버라이드 구현 필요) |

---

## 3. 왜 Custom Panel인가? (결정적 이유)

**"레이블의 너비는 렌더링 전에는 알 수 없습니다."**

1.  사용자가 창을 줄이면 레이블들이 더 쉽게 겹칩니다. 컨버터는 이 "물리적 중첩"을 알 방법이 없습니다.
2.  `EventMarkerPanel`은 WPF의 표준 레이아웃 엔진(`MeasureOverride`)을 사용하여:
    -   A 레이블에게 "너 얼마나 크니?"라고 물어보고 (`Measure`)
    -   그 크기를 바탕으로 "그럼 B 옆에 있으면 겹치니까 위로 한 칸 가자!"라고 결정(`Arrange`)합니다.
3.  이것이 가장 WPF답고, 뷰모델을 깨끗하게 유지하면서도 가장 정밀한 결과를 낼 수 있는 방식입니다.

---

## 4. 최종 구현 계획 (XAML 예시)

뷰모델 수정 없이 XAML에서 다음과 같이 단순히 패널만 갈아끼우면 됩니다.

```xml
<ItemsControl ItemsSource="{Binding Events}">
    <!-- 기존 Canvas 대신 커스텀 패널 사용 -->
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <local:EventMarkerPanel TotalDuration="{Binding DataContext.TotalDuration, ...}" />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
</ItemsControl>
```
