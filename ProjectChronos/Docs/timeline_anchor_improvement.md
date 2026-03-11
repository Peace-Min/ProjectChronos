# 타임라인 레이블 앵커(Anchor) 처리 UI 개선방안

## 1. 문제 정의

### 현재 상태 (AS-IS)

타임라인 레이블은 `EventMarkerPanel.ArrangeOverride`에서 아래와 같이 X 좌표를 산정한다.

```csharp
// EventMarkerPanel.cs - ArrangeOverride
double finalX = item.X - (item.Element.DesiredSize.Width / 2);
```

레이블의 **중심점**을 Timestamp에 해당하는 픽셀 X에 배치하는 방식이다.

| 구간 | 현상 |
|------|------|
| **시작점 근처** (Timestamp ≈ 0) | `finalX` 가 음수(−) → 레이블이 좌측 밖으로 잘림 |
| **끝점 근처** (Timestamp ≈ TotalDuration) | `finalX + Width` 가 패널 폭 초과 → 레이블이 우측 밖으로 잘림 |
| **중간 구간** | 문제 없음 (중앙 정렬이 자연스럽게 동작) |

또한 `Simon ulationReplayView.xaml`에서 타임라인 트랙의 좌우 여백(ThumbWidth/2 = 9px)만큼만 보정하고 있어, 레이블이 그 공간을 활용하지 못하고 있다.

---

## 2. 핵심 개념: **엣지 앵커(Edge Anchor)**

```
┌─────────────────────────────────────────────────────────┐
│  시작점 앵커 영역          중간 자유 배치        끝점 앵커 영역  │
│  [0.00s ██████]           █ █  █   █           [██████ 299.00s] │
│◄──── 레이블 우측 정렬 ────►│             │◄── 레이블 좌측 정렬 ──►│
│   (시작점을 기준으로 우측)  │    중앙 정렬 │  (끝점을 기준으로 좌측) │
└─────────────────────────────────────────────────────────┘
```

- **시작점(Left Edge) 앵커**: Timestamp가 충분히 좌측에 있을 때, 레이블을 **Timestamp의 좌변 기준** 또는 **패널 좌측 경계에 고정**
- **끝점(Right Edge) 앵커**: Timestamp가 충분히 우측에 있을 때, 레이블을 **Timestamp의 우변 기준** 또는 **패널 우측 경계에 고정**

---

## 3. 앵커 판단 조건

레이블의 절반 너비(`halfW`)가 해당 방향으로 패널 경계를 침범하는지 여부로 판단한다.

```
시작점 앵커 적용 조건:  finalX_중앙정렬 < 0
                       즉,  itemX < halfW

끝점 앵커 적용 조건:   finalX_중앙정렬 + labelWidth > panelWidth
                       즉,  itemX + halfW > panelWidth
```

---

## 4. 개선 방안: `EventMarkerPanel` 수정

### 4-1. `ArrangeOverride` 내 X 좌표 결정 로직 개선

현재 코드(`EventMarkerPanel.cs` ~260번째 줄)를 아래와 같이 교체한다.

```csharp
// [AS-IS] 단순 중앙 정렬
double finalX = item.X - (item.Element.DesiredSize.Width / 2.0);

// [TO-BE] 엣지 앵커 적용 (Clamp with Anchor)
double labelW   = item.Element.DesiredSize.Width;
double halfW    = labelW / 2.0;
double rawLeft  = item.X - halfW;           // 중앙 정렬 기준 좌측 X

double finalX;
if (rawLeft < 0)
{
    // 시작점 앵커: 레이블 좌변을 Timestamp 픽셀(item.X)에 고정
    // → 레이블이 우측 영역을 활용하여 완전히 표시됨
    finalX = item.X;
}
else if (rawLeft + labelW > width)
{
    // 끝점 앵커: 레이블 우변을 Timestamp 픽셀(item.X)에 고정
    // → 레이블이 좌측 영역을 활용하여 완전히 표시됨
    finalX = item.X - labelW;
}
else
{
    // 중간 구간: 기존 중앙 정렬 유지
    finalX = rawLeft;
}
// 최종 패널 경계 보정 (안전장치)
finalX = Math.Max(0, Math.Min(finalX, width - labelW));
```

### 4-2. `MeasureOverride` 내 충돌 감지 범위도 동일하게 처리

`MeasureOverride`에서 `myStart / myEnd` 계산 시에도 동일한 앵커 로직 적용이 필요하다. 그렇지 않으면 배치된 실제 위치와 충돌 계산 위치가 불일치하여 레이아웃 depth(Level)가 잘못 계산될 수 있다.

```csharp
// MeasureOverride 내 - 앵커 적용한 실제 시작 X 계산
double labelW   = item.Width;
double halfW    = labelW / 2.0;
double rawLeft  = item.X - halfW;
double anchoredX;

if (rawLeft < 0)
    anchoredX = item.X;                         // 시작점 앵커
else if (rawLeft + labelW > width)
    anchoredX = item.X - labelW;                // 끝점 앵커
else
    anchoredX = rawLeft;                        // 중앙

anchoredX = Math.Max(0, Math.Min(anchoredX, width - labelW));

double myStart = anchoredX - LabelPadding;
double myEnd   = anchoredX + labelW + LabelPadding;
```

---

## 5. XAML 측 보조 개선 (`SimulationReplayView.xaml`)

### 5-1. 타임라인 영역 좌우 Padding 명시

시작점/끝점 레이블이 컨트롤 전체를 침범하지 않도록 레이블 패널과 트랙에 동일한 좌우 여백을 설정한다.

```xml
<!-- 현재: Labels Area ScrollViewer -->
<ScrollViewer Margin="0,0,0,30" ...>

<!-- 개선: 레이블도 트랙의 Thumb 반경과 동일한 여백 맞춤 -->
<!-- ThumbWidth=18 → 양쪽 9px 이미 보정됨(xCenter 계산에 포함)  -->
<!-- 추가로 레이블 최대 너비를 고려할 경우 아래 ClipToBounds를 활용 -->
<ScrollViewer Margin="0,0,0,30" ClipToBounds="True" ...>
```

### 5-2. 트랙 Border의 Margin과 앵커 영역 일치

```xml
<!-- 현재 트랙 배경 -->
<Border Height="4" Background="#555555" CornerRadius="2"
        VerticalAlignment="Center" Margin="9,0"/>

<!-- Margin="9,0" → ThumbWidth(18)/2 = 9 이미 반영됨 (유지) -->
<!-- 레이블 앵커 영역과 트랙이 시각적으로 이어지도록 CornerRadius 유지 -->
```

---

## 6. 시각적 동작 비교

### 시작점 마커 (Timestamp = 0~5s, 패널 너비 1200px 예시)

| 방식 | finalX | 표시 결과 |
|------|--------|-----------|
| **AS-IS** (중앙 정렬) | -40 ~ -10px | 레이블 60~70% 잘림 |
| **TO-BE** (시작점 앵커) | item.X = 9px | 레이블 100% 표시, Tick 좌측에 붙어 우측으로 전개 |

### 끝점 마커 (Timestamp = 295~299s, 패널 너비 1200px 예시)

| 방식 | finalX + Width | 표시 결과 |
|------|----------------|-----------|
| **AS-IS** (중앙 정렬) | 1210~1230px | 레이블 10~30px 잘림 |
| **TO-BE** (끝점 앵커) | item.X = 1191px | 레이블 100% 표시, Tick 우측에 붙어 좌측으로 전개 |

---

## 7. 구현 우선순위 및 체크리스트

- [ ] **`EventMarkerPanel.cs` - `ArrangeOverride`**: 앵커 로직 적용 (핵심)
- [ ] **`EventMarkerPanel.cs` - `MeasureOverride`**: 충돌 감지에도 앵커 위치 반영
- [ ] **레이아웃 검증**: TotalDuration = 300s 기준, Timestamp = 0, 1, 2, 298, 299, 300 마커 배치 테스트
- [ ] **레벨 스태킹 검증**: 시작점/끝점에 동시에 여러 이벤트가 있을 때 Level 할당이 올바른지 확인
- [ ] **스크롤뷰 클리핑**: `ScrollViewer`의 `ClipToBounds`로 패널 외부 레이아웃 렌더링 방지 여부 검토

---

## 8. 참고: 앵커 전환 '경계선' 계산

레이블이 앵커를 적용하는 Timestamp 임계치는 아래와 같다.

```
시작점 앵커 임계 Timestamp = (labelWidth / 2) / availableWidth * TotalDuration
끝점 앵커 임계 Timestamp  = TotalDuration - (labelWidth / 2) / availableWidth * TotalDuration
```

예시 (레이블 너비 90px, 패널 유효 너비 1182px, TotalDuration 300s):

```
시작점 앵커 적용 구간: 0 ~ 약 11.4s
끝점 앵커 적용 구간:  약 288.6s ~ 300s
```

이 범위를 벗어난 마커는 기존 중앙 정렬을 그대로 사용하므로 기존 동작에 영향이 없다.
