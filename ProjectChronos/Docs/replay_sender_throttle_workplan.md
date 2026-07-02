# Replay Sender Throttle Work Plan

## Base

- Base commit: `0939dfa626d0c36550c55a909e77037fe1bc8374`
- Related docs:
  - `Docs/replay_slider_throttle_followup.md` (설계 논의 원본)
  - `Docs/replay_architecture_time_resolution_improvement.md`

이 문서는 followup.md 이후의 논의로 확정된 **최소 수정안 + 재현/검증 환경**의 작업 계획이다.

## 확정된 진단

### 재생 중 송신부 UI 스레드 파이프라인 (현재)

```text
CompositionTarget.Rendering (~60Hz, View Loaded 동안 상시)
 └─ Tick()
     └─ SetCurrentTimeInternal(asyncNotify: true)   ← Playback 경로 전용
         ├─ PublishTimeChangedAsyncIfDue  → 100ms 스로틀 (10Hz) [적용됨]
         └─ QueueUiTimeUpdate → BeginInvoke(DataBind)  ← 매 프레임
             ├─ CurrentTimeDisplay 알림: 100ms 스로틀 [적용됨]
             └─ OnPropertyChanged(CurrentTime): 스로틀 없음  ← 잔여 압박
                 └─ Slider.Value 바인딩 → Track/Thumb/RepeatButton 레이아웃 → 렌더
```

### 핵심 사실

1. `CurrentTime` PropertyChanged의 소비자는 XAML 전체에서 Slider.Value 바인딩
   **단 하나** (`SimulationReplayView.xaml:415`).
   내부 로직(수신부 메시지, 이벤트 매칭, 시크)은 `_currentTime` 필드를 직접 읽는다.
2. Tick이 프레임 기반이므로 `CurrentTime` 변경 빈도는 프레임레이트로 캡된다.
   - 1x: 10ms/10us 모두 ~60회/초 (차이 없음)
   - 0.1x: 10ms는 그리드 양자화로 ~10회/초 자연 스로틀, 10us는 ~60회/초
   - **해상도 의존 증상은 저배속 재생에서 발생** — 10us 구간 분석 시 감속 사용과 일치.
3. WPF 우선순위: DataBind(8) > Render(7) > Input(5) > **Background(4)**.
   재생 중 매 프레임 DataBind+Render 작업이 큐를 점유하면 Background 수신부는
   idle 슬롯을 얻지 못한다. 저성능 PC에서 프레임 예산 소진 시 슬롯이 0이 된다.
4. 수신부(메인 SW)는 이미 latest-state 코얼레싱(단일 pending + 33ms cadence)으로
   개선됨. 남은 병목은 송신부가 idle 슬롯을 만들어주지 못하는 것.

### 왜 SliderDisplayTime 신설이 아닌 알림 스로틀인가

followup.md의 `SliderDisplayTime` 분리와 동일한 재생 중 효과를,
바인딩/시크 경로/드래그 동작 변경 없이 얻을 수 있다:

- `QueueUiTimeUpdate`는 Playback 경로에서만 호출되므로 스로틀 범위가
  자동으로 "재생 중"으로 한정됨.
- 수동 조작(드래그 시크/스텝/점프/일시정지/이벤트정지)은 동기 경로로
  즉시 알림 — 조작 응답성 변화 없음.
- 유일한 비용: 재생 중 Thumb 자동 이동이 60Hz → ~10Hz.
  (1000px/200s/1x 기준 스텝당 0.5px — 체감 불가. 필요 시 상수로 33~50ms 조정)
- `SliderDisplayTime` 분리는 픽셀 단위 스로틀링이 실측으로 필요해질 때
  승격 (followup.md 8단계의 원래 위치).

## 수정 내용

### F1. Playback 한정 CurrentTime 알림 스로틀 (SimulationReplayViewModel)

- `PlaybackSliderUpdateIntervalMs = 100` 상수 추가.
- `QueueUiTimeUpdate`:
  - display/slider 둘 다 due가 아니고 force도 아니면 **BeginInvoke 포스트 자체를 생략**
    (DataBind 포스트도 ~10-20Hz로 감소).
  - 콜백에서 slider due(또는 force)일 때만 `OnPropertyChanged(CurrentTime)`.
- A/B 검증용 토글 `IsPlaybackSliderThrottleEnabled` (기본 true).
  false = 기존(베이스라인) 동작 재현.
- force 경로(시크/일시정지/이벤트정지/재생종료)는 기존대로 즉시 flush.

### F2. CompositionTarget.Rendering 구독 게이팅 — 보류로 이동

- 검토 결과: 구독 자체가 연속 렌더 루프를 강제하는 것은 사실이나,
  절감 대상이 "일시정지/유휴 상태"의 CPU이고 그 시점에는 수신부 기아 문제가 없음.
  재생 중에는 구독이 어차피 필요하므로 **핵심 증상(재생 중 Background 기아)에 기여도 없음**.
- F1 적용 후 계측에서 유휴 부하가 문제로 나타나면 그때 도입 (보류 목록 참조).

## 검증 환경 (Diagnostics)

### 구성 요소

```text
Diagnostics/ReplaySenderMetrics.cs     송신부 누적 카운터 (Interlocked)
Diagnostics/SyntheticReplayReceiver.cs 합성 수신부 — 메인 SW 패턴 미러
                                       (latest-state, 단일 pending, 33ms cadence)
                                       우선순위(Background/Normal), 렌더비용(0/30/60/120ms),
                                       post→run 레이턴시 p50/p95/max
Diagnostics/UiLoadGenerator.cs         프레임당 busy-wait N ms — 저성능 PC 모사
Diagnostics/ReplayDiagnosticsViewModel.cs  하네스 패널 VM + 1초 샘플링
Diagnostics/ReplayDiagnosticsRunner.cs 자동 시나리오 러너 (CSV 출력)
MainWindow Row1                        진단 패널 UI
```

### 측정 지표 (초당)

```text
[송신부] Tick, CurrentTime changed, Dispatcher post, Slider notify(CurrentTime PC),
         Display notify, Message sent, Message throttled
[수신부] received, render scheduled, render completed, render coalesced,
         dispatcher latency p50/p95/max (ms)
```

### 자동 시나리오 (--replay-diag <preset>)

| preset | 스로틀 | 해상도 | 배속 | 수신부 우선순위 | 렌더비용 | UI부하 | 목적 |
|---|---|---|---|---|---|---|---|
| repro  | OFF | 10us | 0.1x | Background | 30ms | 8ms/frame | 현상 재현 |
| fixed  | ON  | 10us | 0.1x | Background | 30ms | 8ms/frame | 보완 검증 |
| coarse | OFF | 10ms | 0.1x | Background | 30ms | 8ms/frame | 10ms 대조(자연 스로틀 입증) |

각 프리셋: 자동 재생 → 12초간 1초 간격 샘플링 → `output/replay_diag_<preset>.csv` → 종료.

### 성공 기준

```text
repro : Slider notify/sec ≈ 60,  수신부 render completed/sec ≈ 0   (기아 재현)
fixed : Slider notify/sec ≤ ~10, 수신부 render completed/sec > 0 안정
coarse: Slider notify/sec ≈ ~10 (0.1x 자연 스로틀 — "10ms는 괜찮았다" 설명)
공통  : CurrentTime 내부 정밀도 불변, 시크/일시정지/이벤트정지 시 Thumb 정확 안착
수동  : 스로틀 ON 상태에서 드래그/스텝/점프 즉시 반응
```

## 작업 순서

1. 이 문서 작성.
2. Diagnostics 인프라 (카운터/합성 수신부/부하 생성기).
3. 송신부 계측 + F1 스로틀(+토글).
4. 자동 러너 (`--replay-diag` → CSV) + csproj 등록.   ← 회귀 검증용, UI 패널보다 우선
5. 빌드 → repro/fixed/coarse 실행 → CSV로 성공 기준 검증.
6. (결과 판단 후) UI 패널 필요성 재평가.

## 보류 목록 (이번 범위 아님)

- F2 CompositionTarget.Rendering 구독 IsPlaying 게이팅: 유휴 CPU 절감용.
  재생 중 기아와 무관하므로 F1 계측 후 필요 시 도입.
- 하네스 UI 패널 (MainWindow Row1): 탐색적 디버깅 편의용. 러너 결과 확인 후 판단.
- 수신부(메인 SW) changeKind 래치: pending 중 도착한 Seek/Stopped의 force 의미가
  옛 메시지의 ChangeKind로 소실되는 문제. 별도 건.
- 수신부 `_playbackFrameIndex` 정수 키 전환 (double ULP 미스 → 10ms 폴백 강하).
  로드 시점 해상도 전달 방식으로 확정, 별도 건.
- `SliderDisplayTime` 분리 + 드래그 Behavior: 고정 케이던스가 부족할 때 승격.
- 픽셀 단위 스로틀링, 디지트 단위 텍스트 스로틀링.
