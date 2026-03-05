using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using ProjectChronos.Core;
using ProjectChronos.Models;

namespace ProjectChronos.ViewModels
{
    /// <summary>
    /// 단일 차선(Lane)에 대한 뷰 모델
    /// </summary>
    public class LaneViewModel : ViewModelBase
    {
        public string LaneName { get; }
        public SimulationEventType EventType { get; }
        public ObservableCollection<SimulationEventMarker> LaneEvents { get; }

        public LaneViewModel(string laneName, SimulationEventType eventType)
        {
            LaneName = laneName;
            EventType = eventType;
            LaneEvents = new ObservableCollection<SimulationEventMarker>();
        }
    }

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
        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private double _lastElapsedSeconds;

        // --- 성능 최적화 ---
        private double _lastNotifiedTime = double.MinValue;

        // [리팩토링] 이벤트 단일 1차원 정렬 캐시 (시간 제어 로직 전용 - Group 사용 소멸)
        private System.Collections.Generic.List<SimulationEventMarker> _sortedEvents;

        // --- 렌더링 모드 ---
        private bool _isRealtimeRenderingEnabled = true;

        // -----------------------------------------------------------
        // [상수 (Constants)]
        // -----------------------------------------------------------

        private const double MinNotifyInterval = 0.01; // 10ms
        private const double MaxTickDeltaTime = 0.1;
        private const double EventMatchEpsilon = 0.005; // 5ms
        private const double TimeChangeTolerance = 0.0001;
        private const double EventSearchEpsilon = 0.0001;

        // -----------------------------------------------------------
        // [생성자 (Constructor)]
        // -----------------------------------------------------------
        public SimulationReplayViewModel()
        {
            // 5개의 차선(Lane) 초기화 (이름 및 EventType 맵핑)
            EventLanes = new ObservableCollection<LaneViewModel>
            {
                new LaneViewModel("탐색레이더", SimulationEventType.SearchRadar),
                new LaneViewModel("추적레이더", SimulationEventType.TrackRadar),
                new LaneViewModel("발사승인", SimulationEventType.LaunchApproval),
                new LaneViewModel("미사일발사", SimulationEventType.MissileLaunch),
                new LaneViewModel("요격", SimulationEventType.Intercept)
            };

            PlayPauseCommand = new RelayCommand(_ => TogglePlayPause());
            StepCommand = new RelayCommand(param => Step(param));
            StepEventCommand = new RelayCommand(param => StepEvent(param));
            JumpToTimeCommand = new RelayCommand(param => JumpToTime(param));
        }

        /// <summary>
        /// 시뮬레이션 데이터로 뷰모델을 초기화합니다.
        /// </summary>
        public void Initialize(double totalDuration, System.Collections.Generic.IEnumerable<SimulationEventMarker> events)
        {
            TotalDuration = totalDuration;

            // 각 차선 이벤트 클리어
            foreach (var lane in EventLanes)
            {
                lane.LaneEvents.Clear();
            }

            if (events != null)
            {
                // 1. 핵심 시간 제어 로직용 1차원 리스트 캐싱
                _sortedEvents = events.OrderBy(e => e.Timestamp).ToList();

                // 2. 시각적 UI 렌더링을 위해 각 차선(Lane)으로 데이터 분배
                foreach (var ev in _sortedEvents)
                {
                    switch (ev.EventType)
                    {
                        case SimulationEventType.SearchRadar:
                            EventLanes[0].LaneEvents.Add(ev);
                            break;
                        case SimulationEventType.TrackRadar:
                            EventLanes[1].LaneEvents.Add(ev);
                            break;
                        case SimulationEventType.LaunchApproval:
                            EventLanes[2].LaneEvents.Add(ev);
                            break;
                        case SimulationEventType.MissileLaunch:
                            EventLanes[3].LaneEvents.Add(ev);
                            break;
                        case SimulationEventType.Intercept:
                            EventLanes[4].LaneEvents.Add(ev);
                            break;
                    }
                }
            }
            else
            {
                _sortedEvents = null;
            }

            CurrentTime = 0.0;
            IsPlaying = false;
            CurrentEvents = null;

            _lastNotifiedTime = double.MinValue;

            CheckEventAtZero();
        }

        private void CheckEventAtZero()
        {
            if (_sortedEvents == null || _sortedEvents.Count == 0) return;

            // 0초 근처(EventMatchEpsilon 이내)에 있는 이벤트들 모두 긁어오기
            var zeroEvents = _sortedEvents.Where(e => Math.Abs(e.Timestamp) <= EventMatchEpsilon).ToList();
            
            if (zeroEvents.Any())
            {
                CurrentEvents = zeroEvents;
                System.Diagnostics.Debug.WriteLine($"[Init] Events found at 0s - Count: {zeroEvents.Count}");
            }
        }

        // -----------------------------------------------------------
        // [속성 (Properties)]
        // -----------------------------------------------------------
        #region Properties

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

        public double CurrentTime
        {
            get => _currentTime;
            set => SetCurrentTimeInternal(value, forceNotify: false);
        }

        private void SetCurrentTimeInternal(double value, bool forceNotify)
        {
            if (value < 0.0) value = 0.0;
            if (value > TotalDuration) value = TotalDuration;

            if (SetProperty(ref _currentTime, value))
            {
                OnPropertyChanged(nameof(CurrentTimeDisplay));
                OnPropertyChanged(nameof(CurrentTime)); 

                NotifyTimeChanged(_currentTime, forceNotify);
            }
        }

        public string CurrentTimeDisplay => TimeSpan.FromSeconds(CurrentTime).ToString(@"mm\:ss\.ff");
        public string TotalTimeDisplay => TimeSpan.FromSeconds(TotalDuration).ToString(@"mm\:ss\.ff");

        // [리팩토링] XAML UI 렌더링용 5개의 차선 콜렉션
        public ObservableCollection<LaneViewModel> EventLanes { get; }

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

        public double PlaybackSpeed
        {
            get => _playbackSpeed;
            private set => SetProperty(ref _playbackSpeed, value);
        }

        public string PlaybackSpeedText
        {
            get => _playbackSpeed.ToString("0.##");
            set
            {
                if (double.TryParse(value, out double result) && result > 0)
                {
                    PlaybackSpeed = result;
                }
                OnPropertyChanged(nameof(PlaybackSpeedText));
            }
        }

        public double StepIntervalSeconds
        {
            get => _stepIntervalSeconds;
            private set => SetProperty(ref _stepIntervalSeconds, value);
        }

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

        private string _jumpTargetTimeText = "0.0";
        public string JumpTargetTimeText
        {
            get => _jumpTargetTimeText;
            set => SetProperty(ref _jumpTargetTimeText, value);
        }

        public bool IsRealtimeRenderingEnabled
        {
            get => _isRealtimeRenderingEnabled;
            set => SetProperty(ref _isRealtimeRenderingEnabled, value);
        }

        /// <summary>
        /// [리팩토링] 현재 시점에 활성화된 '개별 이벤트들' 컬렉션 (Group 대체)
        /// 동일 시간에 1개 이상 발생할 수 있으므로 리스트 형태 유지 (UI 요약 전시용)
        /// </summary>
        private System.Collections.Generic.List<SimulationEventMarker> _currentEvents;
        public System.Collections.Generic.List<SimulationEventMarker> CurrentEvents
        {
            get => _currentEvents;
            set
            {
                if (SetProperty(ref _currentEvents, value))
                {
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

        private void NotifyTimeChanged(double newTime, bool forceNotify = false)
        {
            if (!_isRealtimeRenderingEnabled && !forceNotify) return;

            bool shouldNotify = forceNotify || (Math.Abs(newTime - _lastNotifiedTime) >= MinNotifyInterval);

            if (shouldNotify)
            {
                _lastNotifiedTime = newTime;

                // [리팩토링: UI 모던화] 모든 마커를 순회하며 현재 시간 스캐닝 선(Playhead)과 닿아있는지(IsActive) 동기화
                if (_sortedEvents != null)
                {
                    foreach (var ev in _sortedEvents)
                    {
                        bool isActive = Math.Abs(ev.Timestamp - newTime) <= EventMatchEpsilon;
                        if (ev.IsActive != isActive)
                        {
                            ev.IsActive = isActive;
                        }
                    }
                }

                if (!IsPlaying || forceNotify)
                {
                    // [리팩토링] 가장 가까운 이벤트 검색 후, 그 동일 시간에 터진 이벤트 모두 끌어오기
                    var closestEvent = _sortedEvents?
                        .OrderBy(e => Math.Abs(e.Timestamp - newTime))
                        .FirstOrDefault();

                    if (closestEvent != null && Math.Abs(closestEvent.Timestamp - newTime) <= EventMatchEpsilon)
                    {
                        CurrentEvents = _sortedEvents.Where(e => Math.Abs(e.Timestamp - closestEvent.Timestamp) <= TimeChangeTolerance).ToList();
                    }
                    else
                    {
                        CurrentEvents = null;
                    }
                }

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
        }

        private void StopPlayback()
        {
            _stopwatch.Stop();
        }

        public void Tick(object sender, EventArgs e)
        {
            if (!IsPlaying) return;

            double currentElapsed = _stopwatch.Elapsed.TotalSeconds;
            double dt = currentElapsed - _lastElapsedSeconds;
            _lastElapsedSeconds = currentElapsed;

            if (dt <= 0) return;
            if (dt > MaxTickDeltaTime) dt = MaxTickDeltaTime;

            var addedTime = dt * PlaybackSpeed;
            var nextTime = CurrentTime + addedTime;

            if (nextTime >= TotalDuration)
            {
                CurrentTime = TotalDuration;
                IsPlaying = false;
                return;
            }

            // [리팩토링 엄격 보존] Range Check: 이동 경로 내 첫 번째 도달 이벤트 찾기
            var firstEventInRange = _sortedEvents?.FirstOrDefault(ev =>
                ev.Timestamp > CurrentTime && ev.Timestamp <= nextTime
            );

            if (firstEventInRange != null)
            {
                // 착륙 지점(도달 이벤트 시간)에 있는 모든 이벤트들(다중 차선) 취합 (UI 요약/알림용)
                var simultaneousEvents = _sortedEvents.Where(ev => Math.Abs(ev.Timestamp - firstEventInRange.Timestamp) <= TimeChangeTolerance).ToList();
                CurrentEvents = simultaneousEvents;

                // 🎯 [SNAP] 타겟 이벤트 시간으로 정확히 강제 이동
                SetCurrentTimeInternal(firstEventInRange.Timestamp, forceNotify: true);
                IsPlaying = false;

                System.Diagnostics.Debug.WriteLine($"[Auto Pause] Event at {firstEventInRange.Timestamp:F2}s - Concurrent: {simultaneousEvents.Count}");
            }
            else
            {
                CurrentEvents = null;
                CurrentTime = nextTime;
            }
        }

        private void Step(object param)
        {
            IsPlaying = false;

            int direction = 1;
            if (param is string s && int.TryParse(s, out int parsed)) direction = parsed;
            else if (param is int i) direction = i;

            var step = StepIntervalSeconds * direction;
            var newTime = CurrentTime + step;

            SetCurrentTimeInternal(newTime, forceNotify: true);
        }

        private void StepEvent(object param)
        {
            IsPlaying = false;

            int direction = 1;
            if (param is string s && int.TryParse(s, out int parsed)) direction = parsed;
            else if (param is int i) direction = i;

            if (_sortedEvents == null || _sortedEvents.Count == 0) return;

            SimulationEventMarker targetEvent = null;

            if (direction > 0)
            {
                // [리팩토링 보존] 다음 이벤트 탐색
                targetEvent = _sortedEvents.FirstOrDefault(e => e.Timestamp > CurrentTime + EventSearchEpsilon);

                if (targetEvent == null)
                {
                    SetCurrentTimeInternal(TotalDuration, forceNotify: true);
                    return;
                }
            }
            else
            {
                // [리팩토링 보존] 이전 이벤트 탐색
                targetEvent = _sortedEvents.LastOrDefault(e => e.Timestamp < CurrentTime - EventSearchEpsilon);

                if (targetEvent == null)
                {
                    SetCurrentTimeInternal(0.0, forceNotify: true);
                    return;
                }
            }

            if (targetEvent != null)
            {
                SetCurrentTimeInternal(targetEvent.Timestamp, forceNotify: true);
            }
        }

        private void JumpToTime(object param)
        {
            if (double.TryParse(JumpTargetTimeText, out double targetTime))
            {
                IsPlaying = false;
                SetCurrentTimeInternal(targetTime, forceNotify: true);
            }
        }

        #endregion
    }
}
