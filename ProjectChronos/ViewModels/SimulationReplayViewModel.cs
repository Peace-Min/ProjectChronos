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
        private double _totalDuration;
        private double _currentTime;
        private bool _isPlaying;
        private double _playbackSpeed = 1.0;
        private double _stepIntervalSeconds = 0.5;

        // 고정밀 시간 계산을 위한 Stopwatch
        // DispatcherTimer 대신 View의 CompositionTarget.Rendering에서 Tick()을 호출받아 사용합니다.
        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private double _lastElapsedSeconds;

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
                foreach (var evt in events)
                {
                    Events.Add(evt);
                }
            }

            CurrentTime = 0.0;
            IsPlaying = false;
            CurrentEvent = null;
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
            set
            {
                // 범위 제한 (Clamp)
                if (value < 0.0) value = 0.0;
                if (value > TotalDuration) value = TotalDuration;

                if (SetProperty(ref _currentTime, value))
                {
                    OnPropertyChanged(nameof(CurrentTimeDisplay));

                    // 중요: 시간이 변경될 때마다 외부 연동 로직 호출
                    NotifyTimeChanged(_currentTime);
                }
            }
        }

        // 화면 표시용 문자열 (mm:ss.f 형식)
        public string CurrentTimeDisplay => TimeSpan.FromSeconds(CurrentTime).ToString(@"mm\:ss\.f");
        public string TotalTimeDisplay => TimeSpan.FromSeconds(TotalDuration).ToString(@"mm\:ss\.f");

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

        public string PlayButtonText => IsPlaying ? "일시정지" : "재생";

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

        private void NotifyTimeChanged(double newTime)
        {
            // CRITICAL: Hook for external services
            // 이 시점에 설정된 이벤트에 해당하는 시간인지 알 수 있음

            // 0.1초(100ms) 이내의 오차 범위 내에서 이벤트가 있는지 확인
            // 재생 배속이나 프레임 속도에 따라 이 값은 조절될 수 있습니다.
            double epsilon = 0.1;

            // LINQ를 사용하여 현재 시간과 일치하는 첫 번째 이벤트를 찾습니다.
            var matchedEvent = Events.FirstOrDefault(e => Math.Abs(e.Timestamp - newTime) <= epsilon);

            // 찾은 이벤트를 CurrentEvent 속성에 설정 (UI 바인딩 가능)
            CurrentEvent = matchedEvent;

            // TODO: 필요한 경우 여기서 외부 메시지를 보낼 수 있습니다.
            // Messenger.Default.Send(new SimulationTimeChangedMessage(newTime, matchedEvent));
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
        /// </summary>
        public void Tick()
        {
            if (!IsPlaying) return;

            // Stopwatch를 사용한 델타 타임(dt) 계산
            double currentElapsed = _stopwatch.Elapsed.TotalSeconds;
            double dt = currentElapsed - _lastElapsedSeconds;
            _lastElapsedSeconds = currentElapsed;

            if (dt <= 0) return; // 방어 코드

            // [안전 장치] 
            // 시스템 렉 등으로 dt가 너무 클 경우 시간 점프를 방지 (최대 0.1초 제한)
            if (dt > 0.1) dt = 0.1;

            // 배속 적용
            var addedTime = dt * PlaybackSpeed;
            var nextTime = CurrentTime + addedTime;

            // 종료 조건 체크
            if (nextTime >= TotalDuration)
            {
                CurrentTime = TotalDuration;
                IsPlaying = false;
            }
            else
            {
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
            CurrentTime += step;
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

            var sortedEvents = Events.OrderBy(x => x.Timestamp).ToList();
            SimulationEventMarker targetEvent = null;

            // 아주 작은 오차(0.001)를 두어 현재 위치와 같은 이벤트에 갇히지 않도록 함
            if (direction > 0)
            {
                // 다음 이벤트 찾기
                targetEvent = sortedEvents.FirstOrDefault(e => e.Timestamp > CurrentTime + 0.001);

                // 더 이상 이벤트가 없으면 끝으로 이동
                if (targetEvent == null)
                {
                    CurrentTime = TotalDuration;
                    return;
                }
            }
            else
            {
                // 이전 이벤트 찾기
                targetEvent = sortedEvents.LastOrDefault(e => e.Timestamp < CurrentTime - 0.001);

                // 더 이상 이벤트가 없으면 처음으로 이동
                if (targetEvent == null)
                {
                    CurrentTime = 0.0;
                    return;
                }
            }

            // 찾은 이벤트 위치로 이동
            if (targetEvent != null)
            {
                CurrentTime = targetEvent.Timestamp;
            }
        }

        #endregion
    }
}
