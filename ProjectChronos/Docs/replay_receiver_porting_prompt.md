# [작업지시] 리플레이 수신부 개선 이식 (메인 SW / 폐쇄망 에이전트용)

이 문서는 폐쇄망 환경의 코딩 에이전트에게 그대로 전달하는 작업지시 프롬프트다.
외부 검색 없이 이 문서와 아래 참조 커밋의 diff만으로 작업을 완료할 수 있도록 작성되었다.

---

## 역할

너는 WPF(.NET Framework) 메인 SW(OSTES)의 리플레이 수신부를 수정하는 코딩 에이전트다.
아래 참조 커밋에 검증 완료된 최종 형태가 있으니, 그 패턴을 메인 SW의 실제 수신부
클래스들에 적용하라. 창의적 변형 없이 참조본의 패턴을 그대로 따르고,
모호한 지점은 임의 판단하지 말고 질문으로 보고하라.

## 배경 (요약)

- 리플레이 시간해상도가 10ms 고정에서 최소 10us까지 확장되었다.
- 송신부(SimulationReplayViewModel) 스로틀 개선은 이미 적용 완료. 수신부는
  Background Dispatcher 우선순위로 전환되어 기능 확인까지 끝난 상태다.
- 남은 작업: 수신부의 잠재 결함 4종 수정. 모두 프로토타입에서 검증된 패턴이다.
  1. pending 중 도착한 Seek/Stopped의 "즉시 렌더" 의미가 옛 클로저에 삼켜져 유실
  2. 스레드풀(메시지 수신) ↔ UI 스레드(렌더 콜백)의 공유 상태 비동기화
  3. 프레임 인덱스의 double 완전 일치 조회가 ULP(마지막 비트) 차이로 빗나가
     10us 구간에서 조용히 10ms 폴백으로 강하
  4. ClearChart 시 수신 상태 미해제 → 다음 시나리오에서 stale 렌더 + 해제 지연

## 참조 커밋 (이 저장소 = ProjectChronos 프로토타입)

| 커밋 | 내용 | 이식 대상 |
|---|---|---|
| `95c06b5` | force 래치(_isForceRenderPending) + _replayGate lock + cadence 타임스탬프를 실제 렌더 후 기록 | ReceiveSimulationTimeChangedMessage |
| `07c3266` | 공용 클래스 ReplayFrameIndex<TFrame> 신설 + 정수 키 전환 | 신규 파일 + 인덱스 사용부 |
| `378ae55` | ClearChart 시 인덱스 명시적 Clear + 수신 상태 리셋 | ClearChart |

완성 참조본 파일 (이 저장소 기준):
- `ExternalDrafts/PlaybackRefactorDraft/OriginalSource/SingleSimChartControlViewModel.cs`
  — 수신부 1개 클래스에 전체 패턴이 적용된 최종 상태
- `ExternalDrafts/PlaybackRefactorDraft/OriginalSource/ReplayFrameIndex.cs`
  — 그대로 복사해 쓰는 공용 클래스

## 작업 범위

메인 SW에서 `SimulationTimeChangedMessage`를 수신해 재생 커서를 렌더링하는
**모든 수신부 클래스**에 적용한다. (예: SingleSimChartControlViewModel,
SingleSimUserAnalyChartViewModel, 지도 수신부 등 — 실제 목록은
IReplayMessageReceiver 구현체 또는 ReplayDockingHub 등록부를 검색해 확정하고,
확정한 목록을 작업 시작 전에 보고하라.)

---

## 단계별 지시

각 단계 완료 시 빌드가 성공해야 다음 단계로 진행한다.

### 1단계: ReplayFrameIndex 공용 클래스 추가

`ReplayFrameIndex.cs`를 메인 SW의 공용 위치(기존 공용 유틸과 같은 프로젝트/폴더)에
추가한다. 참조본을 그대로 복사하되 namespace만 메인 SW 규칙에 맞춘다.
핵심 계약 (변경 금지):

```csharp
public sealed class ReplayFrameIndex<TFrame>
{
    // 시나리오 시간해상도(초)로 키 단위 확정. 예: 10us → 0.00001, 1ms → 0.001
    // 내부: 단위/초 = Math.Round(1.0 / 해상도), 10ms 폴백 단위 = 0.01 × 단위/초
    public void Configure(double timeResolutionSeconds);

    // 시간 → (long)Math.Round(seconds * 단위/초) 정수 키로 저장
    public void Add(double seconds, TFrame frame);

    // 정밀 키 조회 → 미스 시 10ms 그리드 키로 폴백 → 그래도 없으면 false
    public bool TryGetFrame(double seconds, out TFrame frame);

    public void Clear();                    // 내부 딕셔너리 비움 + 단위 초기화
    public int Count { get; }
    public IEnumerable<TFrame> Frames { get; }   // 전수 순회용 (범위 계산 등)
}
```

이유(맥락): double을 Dictionary 키로 완전 일치 조회하면, 같은 십진 시간이라도
송신부 양자화 값과 DB 원본 값의 마지막 비트가 달라 조회가 빗나간다.
반올림 정수 키는 이 노이즈를 흡수한다.

### 2단계: 수신부 클래스별 — 메시지 수신/렌더 콜백 교체

각 수신부의 `ReceiveSimulationTimeChangedMessage`를 참조본(커밋 `95c06b5` 이후 형태)
패턴으로 수정한다. 필드 3개 추가:

```csharp
private readonly object _replayGate = new object();
private bool _isForceRenderPending;
// (기존) private bool _isReplayRenderPending; / private double _latestReplayTime;
```

수신 측 패턴 (수신은 스레드풀에서 호출될 수 있음):

```csharp
bool forceRender = message.ChangeKind != SimulationTimeChangeKind.Playback;

lock (_replayGate)
{
    _latestReplayTime = message.NewTime;
    if (forceRender) { _isForceRenderPending = true; }

    // cadence 제한: 강제 렌더가 래치된 경우 우회
    if (!forceRender && !_isForceRenderPending)
    {
        if ((DateTime.UtcNow - _lastPlaybackRenderAt).TotalMilliseconds < ReplayRenderIntervalMs)
        { return; }
    }

    if (_isReplayRenderPending) { return; }   // 코얼레싱 (최대 1개 pending)
    _isReplayRenderPending = true;
}
// BeginInvoke 예약은 lock 밖에서
```

콜백 측 패턴 (반드시 지킬 것 3가지):

```csharp
// (1) 예약 당시 message를 클로저로 소비하지 말 것 — 실행 시점 상태를 lock 하에 소비
double renderTime; bool isForceRender;
lock (_replayGate)
{
    _isReplayRenderPending = false;
    isForceRender = _isForceRenderPending;
    _isForceRenderPending = false;
    renderTime = _latestReplayTime;
}

// (2) 강제 렌더는 중첩 방지 skip / 3D 스로틀을 모두 우회
if (!isForceRender && _isUpdating) { return; }
// 3D 스로틀 분기도 message.ChangeKind 대신 !isForceRender 조건으로

// (3) cadence 타임스탬프는 콜백 진입 시가 아니라 "실제 렌더 완료 후" 기록
//     (조회 미스/skip이 cadence를 소모해 다음 렌더를 억제하지 않도록)
_lastPlaybackRenderAt = DateTime.UtcNow;   // ← ChartControl.EndUpdate() 직후
```

Dispatcher 우선순위는 각 클래스의 **현재 설정값을 그대로 유지**한다 (변경 금지).

### 3단계: 수신부 클래스별 — 프레임 인덱스 교체

```csharp
// 필드
private ReplayFrameIndex<List<AddSeriesPointDTO>> _playbackFrameIndex;

// 데이터 로드 시 (InitializeSpatialDbSourceAsync 등)
_playbackFrameIndex = new ReplayFrameIndex<List<AddSeriesPointDTO>>();
_playbackFrameIndex.Configure(/* 시나리오의 시간해상도(초) */);
// ⚠ 확인 필요: 시나리오 정보(CScenarioInfoSingle 등)에서 시간해상도를 읽는
//    실제 속성명을 찾아 연결할 것. 송신부 SetTimeResolution에 전달되는 값과
//    동일한 소스여야 한다. 못 찾으면 임의로 정하지 말고 질문으로 보고.

// 인덱스 구축 루프
_playbackFrameIndex.Add(timeEntry.Key, frames);

// 렌더 콜백 조회 (기존 TryGetValue + Math.Round(t,2) 폴백 2단 로직을 통째로 대체)
var frameIndex = _playbackFrameIndex;          // 지역변수 고정 (해제 경합 방어)
if (frameIndex == null) { return; }
if (!frameIndex.TryGetFrame(renderTime, out var targetFrames)) { return; }

// 전수 순회하던 곳 (예: CalculateAutoFitRange)
_playbackFrameIndex.Frames.SelectMany(frames => frames)...
```

### 4단계: 수신부 클래스별 — ClearChart 보강

```csharp
lock (_replayGate)
{
    _playbackFrameIndex?.Clear();     // 명시적으로 비움 (대용량 프레임 즉시 GC 대상)
    _playbackFrameIndex = null;
    _latestReplayTime = double.NaN;
    _isForceRenderPending = false;
    _lastPlaybackRenderAt = DateTime.MinValue;
    // _isReplayRenderPending은 건드리지 않는다 — 대기 중인 콜백이 스스로 해제.
    //  여기서 false로 덮으면 이중 예약 창이 생긴다.
}
```

### 5단계: 점검 항목 (수정 아님, 조사 후 보고)

- ReplayDockingHub(또는 Messenger)의 수신자 등록이 ClearChart/Dispose 시
  해제되는지 확인. 허브가 수신자를 강참조로 보관하면 VM+차트가 GC되지 않는
  누수가 된다. 해제 코드가 없으면 위치와 함께 보고만 하라 (수정은 별도 승인).

---

## 검증 체크리스트 (전 클래스 적용 후)

1. 전체 빌드 성공, 경고 증가 없음.
2. 재생 중 각 수신부(차트/지도)가 주기적으로 갱신됨 (Background에서도).
3. **시크 즉시 반영**: 재생 중 슬라이더 점프 → 모든 수신부가 곧바로 해당 시각 렌더
   (force 래치 검증 지점 — 특히 3D 차트에서 스로틀에 안 걸리는지).
4. **10us 구간 정밀도**: 시간해상도 확장 구간에서 커서가 10ms가 아닌
   해상도 단위로 이동 (정수 키 검증 지점).
5. **시나리오 교체**: A 재생 중 정지 → 초기화 → B 로드 직후, A의 시각으로
   렌더되는 프레임이 없어야 함 (ClearChart 리셋 검증 지점).
6. **반복 로드 메모리**: 시나리오 로드→해제를 5회 이상 반복해도 메모리가
   누적 증가하지 않아야 함.
7. 일시정지/이벤트정지/재생종료 시 최종 시각이 정확히 렌더됨.

## 제약

- 이 문서에 없는 리팩토링/스타일 변경/최적화를 하지 말 것.
- 각 수신부 클래스는 개별 커밋으로 분리할 것 (문제 시 클래스 단위 롤백 가능하게).
- 기존 코드 스타일(한국어 주석, 명시적 중괄호, 번호 주석)을 따를 것.
- 판단이 필요한 모든 지점(해상도 속성명, 수신부 목록, 허브 해제 등)은
  임의 결정 대신 질문 목록으로 정리해 보고할 것.
