using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using ProjectChronos.Core;
using ProjectChronos.Models;

namespace ProjectChronos.ViewModels
{
    public class SimulationReplayViewModel : ViewModelBase
    {
        // -----------------------------------------------------------
        // [필드 영역 (Fields)] 
        // -----------------------------------------------------------

        // --- 시뮬레이션 상태 ---
        private double _totalDuration;
        private double _currentTime;
        private bool _isPlaying;
        private double _playbackSpeed = 1.0;
        private double _stepIntervalSeconds = 0.5;

        // --- 시간 계산 ---
        // 고정밀 시간 계산을 위한 Stopwatch
        // DispatcherTimer 대신 View의 CompositionTarget.Rendering에서 Tick()을 호출받아 사용합니다.
        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private double _lastElapsedSeconds;

        // --- 성능 최적화 ---
        // 메시지 전송 스로틀링: DB 데이터 간격(10ms)만큼만 메시지를 보냄
        private double _lastNotifiedTime = double.MinValue;

        // 이벤트 마커 정렬 캐시 (StepEvent 성능 최적화용)
        private System.Collections.Generic.List<SimulationMarkerGroup> _sortedGroups;



        // --- 렌더링 모드 ---
        private bool _isRealtimeRenderingEnabled = true;

        // -----------------------------------------------------------
        // [상수 (Constants)]
        // -----------------------------------------------------------

        /// <summary>메시지 전송 최소 간격 (초) - DB 데이터 간격과 동일</summary>
        private const double MinNotifyInterval = 0.01; // 10ms

        /// <summary>Tick에서 허용하는 최대 델타 타임 (초) - 시스템 렉 방지</summary>
        private const double MaxTickDeltaTime = 0.1;

        /// <summary>현재 시간과 이벤트 매칭 시 허용 오차 (초)</summary>
        private const double EventMatchEpsilon = 0.005; // 5ms

        /// <summary>시간 변경 감지 최소 오차 (초)</summary>
        private const double TimeChangeTolerance = 0.0001;

        /// <summary>이벤트 탐색 시 현재 위치 회피 오차 (초)</summary>
        private const double EventSearchEpsilon = 0.0001;


        // -----------------------------------------------------------
        // [생성자 (Constructor)]
        // -----------------------------------------------------------
        public SimulationReplayViewModel()
        {
            // Events: 각 SimulationEventMarker를 개별 마커로 EventMarkerPanel에 넣음
            // → EventMarkerPanel이 레이블 X너비 기반 충돌 감지 후 Level 자동 배정
            Events = new ObservableCollection<SimulationEventMarker>();

            // 커맨드 초기화
            PlayPauseCommand = new RelayCommand(_ => TogglePlayPause());
            StepCommand = new RelayCommand(param => Step(param));
            StepEventCommand = new RelayCommand(param => StepEvent(param));
            JumpToTimeCommand = new RelayCommand(param => JumpToTime(param));
        }

        /// <summary>
        /// 시뮬레이션 데이터로 뷰모델을 초기화합니다.
        /// </summary>
        /// <param name="totalDuration">전체 시뮬레이션 시간 (초)</param>
        /// <param name="events">타임라인에 표시할 이벤트 목록</param>
        public void Initialize(double totalDuration, System.Collections.Generic.IEnumerable<SimulationEventMarker> events)
        {
            TotalDuration = totalDuration;

            // 기존 이벤트 클리어 후 다시 추가
            Events.Clear();
            if (events != null)
            {
                // 1. StepEvent 기능을 위해 그룹 캐시 생성
                _sortedGroups = events
                    .GroupBy(e => Math.Round(e.Timestamp, 3))
                    .Select(g => new SimulationMarkerGroup(g.Key, g))
                    .OrderBy(g => g.Timestamp)
                    .ToList();

                // 2. 개별 이벤트 리스트(Events) 채우기 및 Primary 마커(Tick 용) 설정
                // EventMarkerPanel이 개별 이벤트마다 레이블 너비를 실측하여 X축 충돌을 감지합니다.
                foreach (var group in _sortedGroups)
                {
                    bool isFirst = true;
                    foreach (var ev in group.Events)
                    {
                        ev.IsPrimaryMarker = isFirst;
                        ev.MarkerPriority = group.MaxPriority; // 그룹 내 가장 높은 우선순위 색상을 틱에 적용
                        Events.Add(ev);
                        isFirst = false;
                    }
                }
            }
            else
            {
                _sortedGroups = null;
            }

            CurrentTime = 0.0;
            IsPlaying = false;
            CurrentEvents = null;

            // 스로틀링 타이머 초기화 (새 시뮬레이션 시작 시 즉시 알림 가능하도록)
            _lastNotifiedTime = double.MinValue;

            // [추가] 초기 상태(0초)에 이벤트가 있는지 확인하여 설정
            CheckEventAtZero();
        }

        /// <summary>
        /// 시뮬레이션 시작 시점(0초)에 이벤트가 있는지 확인하고, 있다면 CurrentEvents에 설정합니다.
        /// </summary>
        private void CheckEventAtZero()
        {
            if (_sortedGroups == null || _sortedGroups.Count == 0) return;

            // 0초 근처(EventMatchEpsilon 이내)에 있는 그룹 찾기
            var zeroGroup = _sortedGroups.FirstOrDefault(g => Math.Abs(g.Timestamp) <= EventMatchEpsilon);

            if (zeroGroup != null)
            {
                CurrentEvents = zeroGroup.Events;
                System.Diagnostics.Debug.WriteLine($"[Init] Event found at 0s - Count: {zeroGroup.Events.Count}");
            }
        }

        // -----------------------------------------------------------
        // [속성 (Properties)]
        // -----------------------------------------------------------
        #region Properties

        private bool _isAutoPauseEnabled = true;
        /// <summary>
        /// 이벤트 도달 시 자동 멈춤 기능 활성화 여부
        /// </summary>
        public bool IsAutoPauseEnabled
        {
            get => _isAutoPauseEnabled;
            set => SetProperty(ref _isAutoPauseEnabled, value);
        }

        private double _lastHighlightTime = -1;

        /// <summary>
        /// 전체 시뮬레이션 길이 (초 단위)
        /// </summary>
        public double TotalDuration
        {
            get => _totalDuration;
            set
            {
                if (SetProperty(ref _totalDuration, value))
                {
                    OnPropertyChanged(nameof(TotalTimeDisplay));
                }
            }
        }

        /// <summary>
        /// 현재 시뮬레이션 시간 (초 단위 Double)
        /// 변경 시 유효성 검사(Clamp) 및 외부 알림(NotifyTimeChanged)을 수행합니다.
        /// </summary>
        public double CurrentTime
        {
            get => _currentTime;
            set => SetCurrentTimeInternal(value, forceNotify: false);
        }

        /// <summary>
        /// 내부적으로 CurrentTime을 설정하며, forceNotify 옵션을 제공합니다.
        /// Slider 바인딩 업데이트와 외부 알림을 일원화하여 이중 호출을 방지합니다.
        /// </summary>
        /// <param name="value">설정할 시간 값</param>
        /// <param name="forceNotify">true일 경우 스로틀링을 무시하고 강제로 알림 전송</param>
        private void SetCurrentTimeInternal(double value, bool forceNotify)
        {
            // 범위 제한 (Clamp)
            if (value < 0.0) value = 0.0;
            if (value > TotalDuration) value = TotalDuration;

            if (SetProperty(ref _currentTime, value))
            {
                OnPropertyChanged(nameof(CurrentTimeDisplay));
                OnPropertyChanged(nameof(CurrentTime)); // Slider 바인딩 명시적 업데이트

                // 중요: 시간이 변경될 때마다 외부 연동 로직 호출
                NotifyTimeChanged(_currentTime, forceNotify);
            }
        }

        // 화면 표시용 문자열 (mm:ss.ff 형식 - 10ms 단위 표시)
        public string CurrentTimeDisplay => TimeSpan.FromSeconds(CurrentTime).ToString(@"mm\:ss\.ff");
        public string TotalTimeDisplay => TimeSpan.FromSeconds(TotalDuration).ToString(@"mm\:ss\.ff");

        // 타임라인 이벤트 목록 Collection (개별 이벤트 단위)
        public ObservableCollection<SimulationEventMarker> Events { get; }

        /// <summary>
        /// 재생 상태 (True=재생 중, False=일시 정지)
        /// </summary>
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (SetProperty(ref _isPlaying, value))
                {
                    if (_isPlaying) StartPlayback();
                    else StopPlayback();

                    OnPropertyChanged(nameof(PlayButtonText));
                }
            }
        }

        public string PlayButtonText => IsPlaying ? "일시 중지" : "재생";

        /// <summary>
        /// 재생 속도 (배속)
        /// </summary>
        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            private set => SetProperty(ref _playbackSpeed, value);
        }

        /// <summary>
        /// [UI 바인딩용] 재생 속도 텍스트 (유효성 검사 포함)
        /// 0보다 큰 숫자만 허용하며, 잘못된 입력 시 이전 값으로 되돌립니다.
        /// </summary>
        public string PlaybackSpeedText
        {
            get => _playbackSpeed.ToString("0.##");
            set
            {
                if (double.TryParse(value, out double result) && result > 0)
                {
                    PlaybackSpeed = result;
                }
                OnPropertyChanged(nameof(PlaybackSpeedText)); // 잘못된 입력 시 UI 갱신
            }
        }

        /// <summary>
        /// 스텝 이동 간격 (초 단위)
        /// </summary>
        public double StepIntervalSeconds
        {
            get => _stepIntervalSeconds;
            private set => SetProperty(ref _stepIntervalSeconds, value);
        }

        /// <summary>
        /// [UI 바인딩용] 스텝 간격 텍스트 (유효성 검사 포함)
        /// </summary>
        public string StepIntervalText
        {
            get => _stepIntervalSeconds.ToString("0.##");
            set
            {
                if (double.TryParse(value, out double result) && result > 0)
                {
                    StepIntervalSeconds = result;
                }
                OnPropertyChanged(nameof(StepIntervalText));
            }
        }

        /// <summary>
        /// [UI 바인딩용] 수동 이동할 시간 텍스트
        /// </summary>
        private string _jumpTargetTimeText = "0.0";
        public string JumpTargetTimeText
        {
            get => _jumpTargetTimeText;
            set => SetProperty(ref _jumpTargetTimeText, value);
        }

        /// <summary>
        /// 실시간 렌더링 활성화 여부
        /// True: 모든 thumb 움직임에 대해 Sub ViewModel 렌더링 수행 (부드러운 미리보기)
        /// False: 이벤트 발생 시점 또는 수동 조작 시에만 렌더링 (성능 우선)
        /// </summary>
        public bool IsRealtimeRenderingEnabled
        {
            get => _isRealtimeRenderingEnabled;
            set => SetProperty(ref _isRealtimeRenderingEnabled, value);
        }

        /// <summary>
        /// 현재 시점에 활성화된 이벤트 그룹 (없으면 null)
        /// NotifyTimeChanged에서 갱신됩니다.
        /// </summary>
        private System.Collections.Generic.List<SimulationEventMarker> _currentEvents;
        private System.Collections.Generic.List<SimulationEventMarker> _previousHighlightedEvents;

        public System.Collections.Generic.List<SimulationEventMarker> CurrentEvents
        {
            get => _currentEvents;
            set
            {
                if (SetProperty(ref _currentEvents, value))
                {
                    // 기존 하이라이트 해제
                    if (_previousHighlightedEvents != null)
                    {
                        foreach (var ev in _previousHighlightedEvents) ev.IsHighlighted = false;
                    }

                    // 새 이벤트 하이라이트 활성화
                    if (_currentEvents != null)
                    {
                        foreach (var ev in _currentEvents) ev.IsHighlighted = true;
                    }
                    _previousHighlightedEvents = _currentEvents;

                    if (_currentEvents != null && _currentEvents.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Events Detected] Count: {_currentEvents.Count} at {_currentEvents[0].Timestamp:F2}s");
                    }
                    UpdateEventSummary();
                }
            }
        }


        private SimulationEventMarker _primaryEvent;
        public SimulationEventMarker PrimaryEvent
        {
            get => _primaryEvent;
            set => SetProperty(ref _primaryEvent, value);
        }

        private int _extraEventCount;
        public int ExtraEventCount
        {
            get => _extraEventCount;
            set => SetProperty(ref _extraEventCount, value);
        }

        private void UpdateEventSummary()
        {
            if (CurrentEvents != null && CurrentEvents.Count > 0)
            {
                PrimaryEvent = CurrentEvents[0];
                ExtraEventCount = CurrentEvents.Count - 1;
            }
            else
            {
                PrimaryEvent = null;
                ExtraEventCount = 0;
            }
        }

        #endregion

        // -----------------------------------------------------------
        // [커맨드 (Commands)]
        // -----------------------------------------------------------
        #region Commands

        public ICommand PlayPauseCommand { get; }
        public ICommand StepCommand { get; }
        public ICommand StepEventCommand { get; }
        public ICommand JumpToTimeCommand { get; }

        #endregion

        #region Logic

        /// <summary>
        /// 시간 변경을 외부(UI 등)에 알리고 필요한 처리를 수행합니다.
        /// [역할] 순수 알림 담당. 재생 로직은 Tick으로 이관되었으며, 여기서는 수동 조작 시의 피드백과 외부 메시지 전송만 수행합니다.
        /// </summary>
        /// <param name="newTime">변경된 시간</param>
        /// <param name="forceNotify">true일 경우 스로틀링을 무시하고 강제로 메시지 전송 (이벤트 스냅, 수동 탐색 등)</param>
        private void NotifyTimeChanged(double newTime, bool forceNotify = false)
        {
            // 렌더링 모드 체크
            if (!_isRealtimeRenderingEnabled && !forceNotify) return;

            // 스로틀링 체크
            bool shouldNotify = forceNotify || (Math.Abs(newTime - _lastNotifiedTime) >= MinNotifyInterval);

            if (shouldNotify)
            {
                _lastNotifiedTime = newTime;

                // [수정] 재생 로직(Range Check)은 Tick으로 이동하여 재귀 호출 위험 제거

                // [수동 조작 시 UI 반응용 단순 매칭]
                // 재생 중이 아닐 때(수동 스크럽) 현재 위치의 이벤트를 표시
                if (!IsPlaying || forceNotify)
                {
                    // [수정] 가장 가까운 그룹 찾기 (근접 매칭)
                    // 기존 FirstOrDefault는 범위 내 첫 요소를 반환하므로, 두 그룹이 겹칠 때 항상 앞쪽을 선택하는 문제 방지
                    var matchedGroup = _sortedGroups?
                        .Where(g => Math.Abs(g.Timestamp - newTime) <= EventMatchEpsilon)
                        .OrderBy(g => Math.Abs(g.Timestamp - newTime))
                        .FirstOrDefault();

                    CurrentEvents = matchedGroup?.Events;
                }

                // 외부 메시지 전송 등
                // Messenger.Default.Send(new SimulationTimeChangedMessage(newTime, CurrentEvent));

                System.Diagnostics.Debug.WriteLine($"[Time Notify] {newTime:F3}s");
            }
        }

        private void TogglePlayPause()
        {
            IsPlaying = !IsPlaying;
        }

        private void StartPlayback()
        {
            if (CurrentTime >= TotalDuration)
            {
                CurrentTime = 0.0;
            }

            _lastElapsedSeconds = 0;
            _stopwatch.Restart();
            // _playbackTimer.Start(); // 제거됨
        }

        private void StopPlayback()
        {
            // _playbackTimer.Stop(); // 제거됨
            _stopwatch.Stop();
        }

        /// <summary>
        /// View의 CompositionTarget.Rendering 이벤트에서 매 프레임 호출됩니다.
        /// [핵심 역할] 시뮬레이션 시간 진행, 이벤트 감지(Range Check), 스냅(Snap), 상태 초기화를 총괄하는 사령탑 메서드입니다.
        /// </summary>
        public void Tick(object sender, EventArgs e)
        {
            if (!IsPlaying) return;

            // 1. 델타 타임 계산
            double currentElapsed = _stopwatch.Elapsed.TotalSeconds;
            double dt = currentElapsed - _lastElapsedSeconds;
            _lastElapsedSeconds = currentElapsed;

            if (dt <= 0) return;
            if (dt > MaxTickDeltaTime) dt = MaxTickDeltaTime;

            // 2. 가려고 하는 목표 시간 계산
            var addedTime = dt * PlaybackSpeed;
            var nextTime = CurrentTime + addedTime;

            // 3. 종료 조건 체크
            if (nextTime >= TotalDuration)
            {
                CurrentTime = TotalDuration;
                IsPlaying = false;
                return;
            }

            // 4. [핵심] 이동 경로상의 이벤트 감지 (Range Check)
            // CurrentTime(현재) ~ nextTime(미래) 사이에 이벤트가 있는지 미리 확인
            var matchedGroup = _sortedGroups?.FirstOrDefault(g =>
                g.Timestamp > CurrentTime && g.Timestamp <= nextTime
            );

            if (matchedGroup != null)
            {
                CurrentEvents = matchedGroup.Events; // UI 알림 (먼저 설정하여 일관성 유지)

                if (IsAutoPauseEnabled)
                {
                    // 🎯 [SNAP] 이벤트가 있다면, 목표 시간(nextTime)을 무시하고 이벤트 시간으로 강제 착륙
                    // 스로틀링 무시하고 즉시 알림 전송 (forceNotify: true)
                    SetCurrentTimeInternal(matchedGroup.Timestamp, forceNotify: true);

                    IsPlaying = false; // 일시 정지

                    System.Diagnostics.Debug.WriteLine($"[Auto Pause] Event Group at {matchedGroup.Timestamp:F2}s - Count: {matchedGroup.Events.Count}");
                }
                else
                {
                    // 자동 멈춤 OFF: 멈추지 않고 흘러가지만 잔상(Highlight)을 남김
                    SetCurrentTimeInternal(matchedGroup.Timestamp, forceNotify: true);
                    CurrentTime = nextTime;
                    _lastHighlightTime = nextTime;
                }
            }
            else
            {
                // 이벤트가 없으면 원래 목표대로 이동하고, CurrentEvents 초기화

                // [Range Check 결과 이벤트 없음]
                if (!IsAutoPauseEnabled && _lastHighlightTime >= 0)
                {
                    // 1초 뒤에 잔상 해제
                    if (nextTime - _lastHighlightTime > 1.0 * PlaybackSpeed)
                    {
                        CurrentEvents = null;
                        _lastHighlightTime = -1;
                    }
                }
                else
                {
                    CurrentEvents = null;
                }

                CurrentTime = nextTime;
            }
        }

        /// <summary>
        /// 지정된 간격(StepInterval)만큼 시간을 앞/뒤로 이동합니다.
        /// param: "1" (앞으로), "-1" (뒤로)
        /// </summary>
        private void Step(object param)
        {
            IsPlaying = false; // 수동 조작 시 일시 정지

            int direction = 1;
            // 파라미터 파싱 (문자열 또는 정수)
            if (param is string s && int.TryParse(s, out int parsed)) direction = parsed;
            else if (param is int i) direction = i;

            var step = StepIntervalSeconds * direction;
            var newTime = CurrentTime + step;

            // SetCurrentTimeInternal을 사용하여 이중 호출 방지
            // forceNotify=true로 수동 조작 시 즉시 알림 전송
            SetCurrentTimeInternal(newTime, forceNotify: true);
        }

        /// <summary>
        /// 다음 또는 이전 이벤트로 즉시 이동합니다.
        /// param: "1" (다음), "-1" (이전)
        /// </summary>
        private void StepEvent(object param)
        {
            IsPlaying = false;

            int direction = 1;
            if (param is string s && int.TryParse(s, out int parsed)) direction = parsed;
            else if (param is int i) direction = i;

            // 성능 최적화: 미리 정렬된 리스트 사용 (Initialize에서 캐싱됨)
            if (_sortedGroups == null || _sortedGroups.Count == 0) return;

            SimulationMarkerGroup targetGroup = null;

            // EventSearchEpsilon을 두어 현재 위치와 같은 이벤트에 갇히지 않도록 함
            if (direction > 0)
            {
                // 다음 이벤트 찾기
                targetGroup = _sortedGroups.FirstOrDefault(g => g.Timestamp > CurrentTime + EventSearchEpsilon);

                // 더 이상 이벤트가 없으면 끝으로 이동
                if (targetGroup == null)
                {
                    SetCurrentTimeInternal(TotalDuration, forceNotify: true);
                    return;
                }
            }
            else
            {
                // 이전 이벤트 찾기
                targetGroup = _sortedGroups.LastOrDefault(g => g.Timestamp < CurrentTime - EventSearchEpsilon);

                // 더 이상 이벤트가 없으면 처음으로 이동
                if (targetGroup == null)
                {
                    SetCurrentTimeInternal(0.0, forceNotify: true);
                    return;
                }
            }

            // 찾은 이벤트 위치로 이동
            if (targetGroup != null)
            {
                // SetCurrentTimeInternal을 사용하여 이중 호출 방지
                // forceNotify=true로 이벤트 이동 시 즉시 알림 전송
                SetCurrentTimeInternal(targetGroup.Timestamp, forceNotify: true);
            }
        }

        /// <summary>
        /// 입력된 시간(JumpTargetTimeText)으로 즉시 이동합니다.
        /// </summary>
        private void JumpToTime(object param)
        {
            if (double.TryParse(JumpTargetTimeText, out double targetTime))
            {
                IsPlaying = false; // 이동 시 일시 정지
                SetCurrentTimeInternal(targetTime, forceNotify: true);
            }
        }

        #endregion
    }
}
