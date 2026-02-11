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
        private System.Collections.Generic.List<SimulationEventMarker> _sortedEvents;



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
        private const double EventMatchEpsilon = 0.1; // 100ms

        /// <summary>시간 변경 감지 최소 오차 (초)</summary>
        private const double TimeChangeTolerance = 0.0001;

        /// <summary>이벤트 탐색 시 현재 위치 회피 오차 (초)</summary>
        private const double EventSearchEpsilon = 0.001;


        // -----------------------------------------------------------
        // [생성자 (Constructor)]
        // -----------------------------------------------------------
        public SimulationReplayViewModel()
        {
            Events = new ObservableCollection<SimulationEventMarker>();

            // 커맨드 초기화
            PlayPauseCommand = new RelayCommand(_ => TogglePlayPause());
            StepCommand = new RelayCommand(param => Step(param));
            StepEventCommand = new RelayCommand(param => StepEvent(param));
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
                // 성능 최적화: 이벤트를 미리 정렬하여 추가 및 캐싱
                // StepEvent에서 반복적인 정렬을 피하기 위함
                var sortedList = events.OrderBy(e => e.Timestamp).ToList();

                foreach (var evt in sortedList)
                {
                    Events.Add(evt);
                }

                // 정렬된 리스트를 캐싱 (StepEvent에서 사용)
                _sortedEvents = sortedList;
            }
            else
            {
                _sortedEvents = null;
            }

            CurrentTime = 0.0;
            IsPlaying = false;
            CurrentEvent = null;

            // 스로틀링 타이머 초기화 (새 시뮬레이션 시작 시 즉시 알림 가능하도록)
            _lastNotifiedTime = double.MinValue;

        }

        // -----------------------------------------------------------
        // [속성 (Properties)]
        // -----------------------------------------------------------
        #region Properties

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

        // 타임라인 이벤트 목록 Collection
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
        /// 현재 시점에 활성화된 이벤트 (없으면 null)
        /// NotifyTimeChanged에서 갱신됩니다.
        /// </summary>
        private SimulationEventMarker _currentEvent;
        public SimulationEventMarker CurrentEvent
        {
            get => _currentEvent;
            set
            {
                if (SetProperty(ref _currentEvent, value))
                {
                    // 이벤트 감지 시 디버그 출력 (필요 시 로깅 연동)
                    if (_currentEvent != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Event Detected] {_currentEvent.Description} ({_currentEvent.Timestamp:F2}s)");
                    }
                }
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
                    CurrentEvent = _sortedEvents?.FirstOrDefault(evt =>
                       Math.Abs(evt.Timestamp - newTime) <= EventMatchEpsilon
                   );
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
            // Start는 초과(>) End는 이하(<=)로 하여 중복 방지 (무한 일시정지 방지)
            var matchedEvent = _sortedEvents?.FirstOrDefault(evt =>
                evt.Timestamp > CurrentTime && evt.Timestamp <= nextTime
            );

            if (matchedEvent != null)
            {
                CurrentEvent = matchedEvent; // UI 알림 (먼저 설정하여 일관성 유지)

                // 🎯 [SNAP] 이벤트가 있다면, 목표 시간(nextTime)을 무시하고 이벤트 시간으로 강제 착륙
                // 스로틀링 무시하고 즉시 알림 전송 (forceNotify: true)
                SetCurrentTimeInternal(matchedEvent.Timestamp, forceNotify: true);

                IsPlaying = false; // 일시 정지

                System.Diagnostics.Debug.WriteLine($"[Auto Pause] Event at {matchedEvent.Timestamp:F2}s - {matchedEvent.Title ?? matchedEvent.Description}");
            }
            else
            {
                // 이벤트가 없으면 원래 목표대로 이동하고, CurrentEvent 초기화

                // [Range Check 결과 이벤트 없음]
                // 단순히 다음 시간으로 이동하며, 기존에 표시되던 이벤트가 있다면 제거
                CurrentEvent = null;
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
            if (_sortedEvents == null || _sortedEvents.Count == 0) return;

            SimulationEventMarker targetEvent = null;

            // EventSearchEpsilon을 두어 현재 위치와 같은 이벤트에 갇히지 않도록 함
            if (direction > 0)
            {
                // 다음 이벤트 찾기
                targetEvent = _sortedEvents.FirstOrDefault(e => e.Timestamp > CurrentTime + EventSearchEpsilon);

                // 더 이상 이벤트가 없으면 끝으로 이동
                if (targetEvent == null)
                {
                    SetCurrentTimeInternal(TotalDuration, forceNotify: true);
                    return;
                }
            }
            else
            {
                // 이전 이벤트 찾기
                targetEvent = _sortedEvents.LastOrDefault(e => e.Timestamp < CurrentTime - EventSearchEpsilon);

                // 더 이상 이벤트가 없으면 처음으로 이동
                if (targetEvent == null)
                {
                    SetCurrentTimeInternal(0.0, forceNotify: true);
                    return;
                }
            }

            // 찾은 이벤트 위치로 이동
            if (targetEvent != null)
            {
                // SetCurrentTimeInternal을 사용하여 이중 호출 방지
                // forceNotify=true로 이벤트 이동 시 즉시 알림 전송
                SetCurrentTimeInternal(targetEvent.Timestamp, forceNotify: true);
            }
        }

        #endregion
    }
}
