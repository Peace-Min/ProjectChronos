using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Threading;
using ProjectChronos.Core;
using ProjectChronos.Messages;
using ProjectChronos.Models;
using ProjectChronos.Services;

namespace ProjectChronos.ViewModels
{
    public class SimulationReplayViewModel : ViewModelBase
    {
        // -----------------------------------------------------------
        // [필드 영역 (Fields)] 
        // -----------------------------------------------------------

        // --- 시뮬레이션 상태 ---
        private dynamic _scenarioInfoSingle;
        private bool _isInitialize = false;

        public bool HasInteracted { get; private set; } = false;

        private double _totalDuration;
        private double _currentTime;
        private bool _isPlaying;
        private double _playbackSpeed = 1.0;
        private double _stepIntervalSeconds = 0.5;

        // --- 시간 계산 ---
        // 고정밀 시간 계산을 위한 Stopwatch
        // DispatcherTimer 대신 View의 CompositionTarget.Rendering에서 Tick()을 호출받아 사용합니다.
        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();
        private double _startWallTime;
        private double _startSimTime;
        private double _timeResolution = 0.01;
        private string _timeDisplayFormat = "mm\\:ss\\.ff";
        private string _numericTimeFormat = "0.00";
        private double _numericTimeMinWidth = 62;

        public double TimeResolution => _timeResolution;

        // --- 성능 최적화 ---
        // 메시지 전송 스로틀링: 렌더링 가능한 UI 갱신 cadence로 수신부 메시지를 제한합니다.
        private DateTime _lastNotifyUtc = DateTime.MinValue;

        // 이벤트 마커 정렬 캐시 (StepEvent 성능 최적화용)
        private System.Collections.Generic.List<SimulationMarkerGroup> _sortedGroups;
        private System.Collections.Generic.List<SimulationEventMarker> _sourceEvents;

        private bool _isUiTimeUpdatePending = false;
        private bool _forcePendingUiTimeDisplayUpdate = false;
        private DateTime _lastTimeDisplayUpdateUtc = DateTime.MinValue;



        // --- 렌더링 모드 ---
        private bool _isRealtimeRenderingEnabled = true;
        private readonly TimelineReportDefinitionService _timelineReportDefinitionService =
            new TimelineReportDefinitionService();

        // -----------------------------------------------------------
        // [상수 (Constants)]
        // -----------------------------------------------------------

        /// <summary>재생 중 수신부 메시지 전송 최소 간격 (밀리초)</summary>
        private const int PlaybackRenderIntervalMs = 100;

        /// <summary>재생 중 현재 시간 텍스트 표시 갱신 최소 간격 (밀리초)</summary>
        private const int CurrentTimeDisplayUpdateIntervalMs = 100;

        /// <summary>Tick에서 허용하는 최대 델타 타임 (초) - 시스템 렉 방지</summary>

        /// <summary>현재 시간과 이벤트 매칭 시 허용하는 최대 오차 (초)</summary>
        private const double MaxEventMatchEpsilon = 0.005; // 5ms

        /// <summary>시간 변경 감지에 사용하는 최대 오차 (초)</summary>
        private const double MaxTimeChangeTolerance = 0.0001;

        /// <summary>이벤트 탐색 시 현재 위치 회피에 사용하는 최대 오차 (초)</summary>
        private const double MaxEventSearchEpsilon = 0.0001;

        private const double MinTimeEpsilon = 0.000000001;


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
            JumpToEventTimeCommand = new RelayCommand(param => JumpToEventTime(param));
            ExportReportImageCommand = new RelayCommand(_ => ExportReportImage());
        }

        public event Action<SimulationTimeChangedMessage> SimulationTimeChanged;

        private void RaiseCurrentTimeDisplayChanged()
        {
            _lastTimeDisplayUpdateUtc = DateTime.UtcNow;
            OnPropertyChanged(nameof(CurrentTimeDisplay));
        }

        public void Clear()
        {
            if (IsPlaying)
            {
                IsPlaying = false;
            }

            _scenarioInfoSingle = null;
            _isInitialize = false;
            HasInteracted = false;

            _currentTime = 0.0;
            _totalDuration = 0.0;
            _playbackSpeed = 1.0;

            Events.Clear();
            _sourceEvents = null;
            _sortedGroups = null;
            CurrentEvents = null;

            if (_previousHighlightedEvents != null)
            {
                foreach (var ev in _previousHighlightedEvents)
                {
                    ev.IsHighlighted = false;
                }
                _previousHighlightedEvents = null;
            }

            PrimaryEvent = null;
            ExtraEventCount = 0;

            _lastNotifyUtc = DateTime.MinValue;
            _lastTimeDisplayUpdateUtc = DateTime.MinValue;
            _isUiTimeUpdatePending = false;
            _forcePendingUiTimeDisplayUpdate = false;
            _startWallTime = 0.0;
            _startSimTime = 0.0;

            RaiseCurrentTimeDisplayChanged();
            OnPropertyChanged(nameof(TotalTimeDisplay));
            OnPropertyChanged(nameof(CurrentTime));
            OnPropertyChanged(nameof(TotalDuration));
            OnPropertyChanged(nameof(PlaybackSpeed));
        }

        public void SetTimeResolution(double resolutionSeconds)
        {
            if (resolutionSeconds > 0)
            {
                _timeResolution = resolutionSeconds;
            }

            UpdateTimeDisplayFormat();
            RebuildEventGroups();
            SetCurrentTimeInternal(CurrentTime, forceNotify: true, SimulationTimeChangeKind.Seek);
            RaiseCurrentTimeDisplayChanged();
            OnPropertyChanged(nameof(TotalTimeDisplay));
            OnPropertyChanged(nameof(StepIntervalText));
            OnPropertyChanged(nameof(JumpTargetTimeText));
            OnPropertyChanged(nameof(NumericTimeMinWidth));
            OnPropertyChanged(nameof(TimeResolution));
        }

        private void UpdateTimeDisplayFormat()
        {
            int digits = GetResolutionDecimalDigits();
            _timeDisplayFormat = digits > 0
                ? "mm\\:ss\\." + new string('f', digits)
                : "mm\\:ss";
            _numericTimeFormat = digits > 0
                ? "0." + new string('0', digits)
                : "0";
            _numericTimeMinWidth = 62 + (digits - 2) * 8;
        }

        private int GetResolutionDecimalDigits()
        {
            if (_timeResolution >= 1.0)
            {
                return 0;
            }

            return Math.Min(9, Math.Max(0, (int)Math.Ceiling(-Math.Log10(_timeResolution))));
        }

        private double NormalizeReplayTime(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return _currentTime;
            }

            if (value <= 0.0)
            {
                return 0.0;
            }

            if (TotalDuration > 0.0 && value >= TotalDuration)
            {
                return TotalDuration;
            }

            double clamped = TotalDuration > 0.0 ? Math.Min(value, TotalDuration) : value;
            if (_timeResolution <= 0.0)
            {
                return clamped;
            }

            double normalized = Math.Round(clamped / _timeResolution, MidpointRounding.AwayFromZero) * _timeResolution;
            if (TotalDuration > 0.0)
            {
                normalized = Math.Min(normalized, TotalDuration);
            }

            return Math.Max(0.0, normalized);
        }

        private double NormalizeReplayDuration(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                return _timeResolution > 0.0 ? _timeResolution : 0.0;
            }

            if (_timeResolution <= 0.0)
            {
                return value;
            }

            return Math.Max(
                _timeResolution,
                Math.Round(value / _timeResolution, MidpointRounding.AwayFromZero) * _timeResolution);
        }

        private double GetResolutionHalfStep()
        {
            return _timeResolution > 0.0
                ? Math.Max(_timeResolution * 0.5, MinTimeEpsilon)
                : MinTimeEpsilon;
        }

        private double GetTimeChangeTolerance()
        {
            double resolutionNoiseTolerance = _timeResolution > 0.0
                ? Math.Max(_timeResolution * 0.000001, MinTimeEpsilon)
                : MinTimeEpsilon;
            return Math.Min(MaxTimeChangeTolerance, resolutionNoiseTolerance);
        }

        private double GetEventMatchEpsilon()
        {
            return Math.Min(MaxEventMatchEpsilon, GetResolutionHalfStep());
        }

        private double GetEventSearchEpsilon()
        {
            return Math.Min(MaxEventSearchEpsilon, GetResolutionHalfStep());
        }

        private double GetEventGroupKey(double timestamp)
        {
            return NormalizeReplayTime(timestamp);
        }

        private string FormatNumericTime(double seconds)
        {
            return seconds.ToString(_numericTimeFormat, CultureInfo.InvariantCulture);
        }

        public double NumericTimeMinWidth
        {
            get => _numericTimeMinWidth;
        }

        public void InitializeSpatialDbSource(dynamic scenarioInfoSingle, double simulationMaxTime)
        {
            _scenarioInfoSingle = scenarioInfoSingle;

            if (_scenarioInfoSingle == null || _scenarioInfoSingle.SimulationEventMarkers == null)
            {
                Initialize(simulationMaxTime, null);
                return;
            }

            Initialize(simulationMaxTime, _scenarioInfoSingle.SimulationEventMarkers);
        }

        /// <summary>
        /// 시뮬레이션 데이터로 뷰모델을 초기화합니다.
        /// </summary>
        /// <param name="totalDuration">전체 시뮬레이션 시간 (초)</param>
        /// <param name="events">타임라인에 표시할 이벤트 목록</param>
        public void Initialize(double totalDuration, System.Collections.Generic.IEnumerable<SimulationEventMarker> events)
        {
            TotalDuration = totalDuration;

            _sourceEvents = events?.ToList();
            RebuildEventGroups();

            CurrentTime = 0.0;
            IsPlaying = false;
            CurrentEvents = null;

            // 스로틀링 타이머 초기화 (새 시뮬레이션 시작 시 즉시 알림 가능하도록)
            _lastNotifyUtc = DateTime.MinValue;

            // [추가] 초기 상태(0초)에 이벤트가 있는지 확인하여 설정
            CheckEventAtZero();

            _isInitialize = true;
        }

        /// <summary>
        /// 시뮬레이션 시작 시점(0초)에 이벤트가 있는지 확인하고, 있다면 CurrentEvents에 설정합니다.
        /// </summary>
        private void CheckEventAtZero()
        {
            if (_sortedGroups == null || _sortedGroups.Count == 0) return;

            // 0초 근처(EventMatchEpsilon 이내)에 있는 그룹 찾기
            var zeroGroup = _sortedGroups.FirstOrDefault(g => Math.Abs(g.Timestamp) <= GetEventMatchEpsilon());

            if (zeroGroup != null)
            {
                CurrentEvents = zeroGroup.Events;
            }
        }

        private void RebuildEventGroups()
        {
            Events.Clear();
            CurrentEvents = null;

            if (_sourceEvents == null || _sourceEvents.Count == 0)
            {
                _sortedGroups = null;
                return;
            }

            _sortedGroups = _sourceEvents
                .GroupBy(e => GetEventGroupKey(e.Timestamp))
                .Select(g => new SimulationMarkerGroup(g.Key, g))
                .OrderBy(g => g.Timestamp)
                .ToList();

            // EventMarkerPanel이 개별 이벤트마다 레이블 너비를 실측하여 X축 충돌을 감지합니다.
            foreach (var group in _sortedGroups)
            {
                bool isFirst = true;
                foreach (var ev in group.Events)
                {
                    ev.DisplayTimestamp = FormatNumericTime(group.Timestamp);
                    ev.IsPrimaryMarker = isFirst;
                    ev.MarkerPriority = group.MaxPriority;
                    Events.Add(ev);
                    isFirst = false;
                }
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
            SetCurrentTimeInternal(value, forceNotify, SimulationTimeChangeKind.Seek);
        }

        private void SetCurrentTimeInternal(double value, bool forceNotify, SimulationTimeChangeKind changeKind)
        {
            SetCurrentTimeInternal(value, forceNotify, changeKind, asyncNotify: false, resetWallClock: changeKind == SimulationTimeChangeKind.Seek);
        }

        private void SetCurrentTimeInternal(double value, bool forceNotify, SimulationTimeChangeKind changeKind, bool asyncNotify)
        {
            SetCurrentTimeInternal(value, forceNotify, changeKind, asyncNotify, resetWallClock: false);
        }

        private void SetCurrentTimeInternal(double value, bool forceNotify, SimulationTimeChangeKind changeKind, bool asyncNotify, bool resetWallClock)
        {
            value = NormalizeReplayTime(value);

            if (IsPlaying && resetWallClock)
            {
                _startWallTime = _stopwatch.Elapsed.TotalSeconds;
                _startSimTime = value;
            }

            if (Math.Abs(_currentTime - value) > GetTimeChangeTolerance())
            {
                _currentTime = value;

                if (asyncNotify)
                {
                    PublishTimeChangedAsyncIfDue(_currentTime, changeKind, forceNotify);
                    QueueUiTimeUpdate(forceDisplayUpdate: forceNotify || changeKind != SimulationTimeChangeKind.Playback);
                }
                else
                {
                    RaiseCurrentTimeDisplayChanged();
                    OnPropertyChanged(nameof(CurrentTime));
                    PublishTimeChangedIfDue(_currentTime, changeKind, forceNotify);
                }
            }
            else if (forceNotify)
            {
                if (asyncNotify)
                {
                    PublishTimeChangedAsyncIfDue(_currentTime, changeKind, forceNotify: true);
                    QueueUiTimeUpdate(forceDisplayUpdate: true);
                }
                else
                {
                    RaiseCurrentTimeDisplayChanged();
                    OnPropertyChanged(nameof(CurrentTime));
                    PublishTimeChangedIfDue(_currentTime, changeKind, forceNotify: true);
                }
            }
        }

        private string FormatReplayTime(double seconds)
        {
            long ticks = (long)Math.Round(seconds * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero);
            return TimeSpan.FromTicks(ticks).ToString(_timeDisplayFormat);
        }

        public string CurrentTimeDisplay => FormatReplayTime(CurrentTime);
        public string TotalTimeDisplay => FormatReplayTime(TotalDuration);

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
            set
            {
                if (IsPlaying && _playbackSpeed != value)
                {
                    double elapsedWall = _stopwatch.Elapsed.TotalSeconds - _startWallTime;
                    _startSimTime = CurrentTime - elapsedWall * value;
                }

                SetProperty(ref _playbackSpeed, value);
            }
        }

        private static readonly ObservableCollection<double> _speedCollection =
            new ObservableCollection<double>() { 0.5, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        public ObservableCollection<double> SpeedCollection
        {
            get { return _speedCollection; }
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
            get => _stepIntervalSeconds.ToString(_numericTimeFormat);
            set
            {
                if (double.TryParse(value, out double result) && result > 0)
                {
                    StepIntervalSeconds = NormalizeReplayDuration(result);
                }
                OnPropertyChanged(nameof(StepIntervalText));
            }
        }

        /// <summary>
        /// [UI 바인딩용] 수동 이동할 시간 텍스트
        /// </summary>
        private double _jumpTargetTime = 0.0;
        public string JumpTargetTimeText
        {
            get => _jumpTargetTime.ToString(_numericTimeFormat);
            set
            {
                if (double.TryParse(value, out double result))
                {
                    _jumpTargetTime = NormalizeReplayTime(result);
                }
                OnPropertyChanged(nameof(JumpTargetTimeText));
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
        public ICommand JumpToEventTimeCommand { get; }
        public ICommand ExportReportImageCommand { get; }

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
            PublishTimeChangedIfDue(newTime, SimulationTimeChangeKind.Seek, forceNotify);
        }

        private void NotifyTimeChanged(double newTime, SimulationTimeChangeKind changeKind, bool forceNotify = false)
        {
            PublishTimeChangedIfDue(newTime, changeKind, forceNotify);
        }

        private void PublishTimeChangedAsyncIfDue(double newTime, SimulationTimeChangeKind changeKind, bool forceNotify)
        {
            var message = CreateTimeChangedMessageIfDue(newTime, changeKind, forceNotify);
            if (message == null)
            {
                return;
            }

            Task.Run(() => SimulationTimeChanged?.Invoke(message));
        }

        private void PublishTimeChangedIfDue(double newTime, SimulationTimeChangeKind changeKind, bool forceNotify)
        {
            var message = CreateTimeChangedMessageIfDue(newTime, changeKind, forceNotify);
            if (message == null)
            {
                return;
            }

            SimulationTimeChanged?.Invoke(message);
        }

        private SimulationTimeChangedMessage CreateTimeChangedMessageIfDue(double newTime, SimulationTimeChangeKind changeKind, bool forceNotify)
        {
            if (!_isRealtimeRenderingEnabled && !forceNotify) return null;

            bool shouldNotify = forceNotify || (DateTime.UtcNow - _lastNotifyUtc) >= TimeSpan.FromMilliseconds(PlaybackRenderIntervalMs);

            if (!shouldNotify)
            {
                return null;
            }

            _lastNotifyUtc = DateTime.UtcNow;

            if (!IsPlaying || forceNotify)
            {
                double eventMatchEpsilon = GetEventMatchEpsilon();
                var matchedGroup = _sortedGroups?
                    .FirstOrDefault(g => Math.Abs(g.Timestamp - newTime) <= eventMatchEpsilon);

                CurrentEvents = matchedGroup?.Events;
            }

            if (!_isInitialize)
            {
                return null;
            }

            if (!HasInteracted && (changeKind == SimulationTimeChangeKind.Seek || changeKind == SimulationTimeChangeKind.Playback))
            {
                HasInteracted = true;
            }

            var currentEvent = CurrentEvents?.FirstOrDefault();
            return new SimulationTimeChangedMessage(newTime, currentEvent, changeKind);
        }

        private void QueueUiTimeUpdate(bool forceDisplayUpdate)
        {
            if (forceDisplayUpdate)
            {
                _forcePendingUiTimeDisplayUpdate = true;
            }

            if (_isUiTimeUpdatePending)
            {
                return;
            }

            _isUiTimeUpdatePending = true;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _isUiTimeUpdatePending = false;
                bool updateDisplay = _forcePendingUiTimeDisplayUpdate || ShouldUpdateCurrentTimeDisplay();
                _forcePendingUiTimeDisplayUpdate = false;

                if (updateDisplay)
                {
                    RaiseCurrentTimeDisplayChanged();
                }

                OnPropertyChanged(nameof(CurrentTime));
            }), DispatcherPriority.DataBind);
        }

        private bool ShouldUpdateCurrentTimeDisplay()
        {
            return (DateTime.UtcNow - _lastTimeDisplayUpdateUtc) >=
                   TimeSpan.FromMilliseconds(CurrentTimeDisplayUpdateIntervalMs);
        }

        private void TogglePlayPause()
        {
            if (IsPlaying)
            {
                IsPlaying = false;
                NotifyTimeChanged(CurrentTime, SimulationTimeChangeKind.Stopped, forceNotify: true);
                return;
            }

            IsPlaying = true;
        }

        private void StartPlayback()
        {
            if (CurrentTime >= TotalDuration)
            {
                SetCurrentTimeInternal(0.0, forceNotify: true, SimulationTimeChangeKind.Seek);
            }

            _stopwatch.Restart();
            _startWallTime = _stopwatch.Elapsed.TotalSeconds;
            _startSimTime = CurrentTime;

            _isUiTimeUpdatePending = false;
            _forcePendingUiTimeDisplayUpdate = false;
            _lastTimeDisplayUpdateUtc = DateTime.MinValue;
        }

        private void StopPlayback()
        {
            _stopwatch.Stop();
        }

        public void Tick(object sender, EventArgs e)
        {
            if (!IsPlaying) return;

            double currentWallTime = _stopwatch.Elapsed.TotalSeconds;
            double elapsedWallSeconds = currentWallTime - _startWallTime;
            double nextTime = _startSimTime + elapsedWallSeconds * PlaybackSpeed;

            nextTime = NormalizeReplayTime(nextTime);

            if (nextTime >= TotalDuration)
            {
                SetCurrentTimeInternal(TotalDuration, forceNotify: true, SimulationTimeChangeKind.Stopped);
                IsPlaying = false;
                return;
            }

            var matchedGroup = _sortedGroups?.FirstOrDefault(g =>
                g.Timestamp > CurrentTime && g.Timestamp <= nextTime
            );

            if (matchedGroup != null)
            {
                CurrentEvents = matchedGroup.Events;

                if (IsAutoPauseEnabled)
                {
                    SetCurrentTimeInternal(matchedGroup.Timestamp, forceNotify: true, SimulationTimeChangeKind.StoppedByEvent);
                    IsPlaying = false;

                }
                else
                {
                    SetCurrentTimeInternal(matchedGroup.Timestamp, forceNotify: true, SimulationTimeChangeKind.Playback, asyncNotify: true);
                    SetCurrentTimeInternal(nextTime, forceNotify: false, SimulationTimeChangeKind.Playback, asyncNotify: true);
                }
            }
            else
            {
                CurrentEvents = null;
                SetCurrentTimeInternal(nextTime, forceNotify: false, SimulationTimeChangeKind.Playback, asyncNotify: true);
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
            SetCurrentTimeInternal(newTime, forceNotify: true, SimulationTimeChangeKind.Seek);
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
            double eventSearchEpsilon = GetEventSearchEpsilon();
            if (direction > 0)
            {
                // 다음 이벤트 찾기
                targetGroup = _sortedGroups.FirstOrDefault(g => g.Timestamp > CurrentTime + eventSearchEpsilon);

                // 더 이상 이벤트가 없으면 끝으로 이동
                if (targetGroup == null)
                {
                    SetCurrentTimeInternal(TotalDuration, forceNotify: true, SimulationTimeChangeKind.Seek);
                    return;
                }
            }
            else
            {
                // 이전 이벤트 찾기
                targetGroup = _sortedGroups.LastOrDefault(g => g.Timestamp < CurrentTime - eventSearchEpsilon);

                // 더 이상 이벤트가 없으면 처음으로 이동
                if (targetGroup == null)
                {
                    SetCurrentTimeInternal(0.0, forceNotify: true, SimulationTimeChangeKind.Seek);
                    return;
                }
            }

            // 찾은 이벤트 위치로 이동
            if (targetGroup != null)
            {
                // SetCurrentTimeInternal을 사용하여 이중 호출 방지
                // forceNotify=true로 이벤트 이동 시 즉시 알림 전송
                SetCurrentTimeInternal(targetGroup.Timestamp, forceNotify: true, SimulationTimeChangeKind.Seek);
            }
        }

        /// <summary>
        /// 입력된 시간(JumpTargetTimeText)으로 즉시 이동합니다.
        /// </summary>
        private void JumpToTime(object param)
        {
            IsPlaying = false; // 이동 시 일시 정지
            SetCurrentTimeInternal(_jumpTargetTime, forceNotify: true, SimulationTimeChangeKind.Seek);
        }

        /// <summary>
        /// 특정 이벤트의 시간으로 즉시 이동합니다.
        /// </summary>
        private void JumpToEventTime(object param)
        {
            if (param is SimulationEventMarker marker)
            {
                IsPlaying = false;
                SetCurrentTimeInternal(marker.Timestamp, forceNotify: true, SimulationTimeChangeKind.Seek);
            }
            else if (param is double timestamp)
            {
                IsPlaying = false;
                SetCurrentTimeInternal(timestamp, forceNotify: true, SimulationTimeChangeKind.Seek);
            }
        }

        private void ExportReportImage()
        {
            try
            {
                var eventSnapshot = CreateReportEventsSnapshot();
                if (eventSnapshot.Count == 0)
                {
                    MessageBox.Show(
                        "생성할 이벤트 데이터가 없습니다.",
                        "보고서 이미지 생성",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                string imagePath = _timelineReportDefinitionService.GetDefaultPrototypeExportPath();
                var exportInput = _timelineReportDefinitionService.CreateReportExportInput(eventSnapshot, imagePath, _timeResolution);

                var imageService = new TimelineReportExportService();
                imageService.Export(exportInput);

                var dialogResult = MessageBox.Show(
                    "이미지가 저장되었습니다.\n저장된 폴더를 여시겠습니까?",
                    "보고서 이미지 생성",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    string outputDirectory = Path.GetDirectoryName(imagePath);
                    if (!string.IsNullOrWhiteSpace(outputDirectory) && Directory.Exists(outputDirectory))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = "\"" + outputDirectory + "\"",
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "보고서 이미지 생성 실패",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private IReadOnlyList<SimulationEventMarker> CreateReportEventsSnapshot()
        {
            return Events
                .Select(item => new SimulationEventMarker
                {
                    Timestamp = item.Timestamp,
                    Priority = item.Priority,
                    Title = item.Title,
                    DescriptionLabel = item.DescriptionLabel,
                    Description = item.Description,
                    RangeBTWLabel = item.RangeBTWLabel,
                    RangeBTW = item.RangeBTW,
                    SourceLabel = item.SourceLabel,
                    Source = item.Source,
                    IsPrimaryMarker = item.IsPrimaryMarker,
                    MarkerPriority = item.MarkerPriority
                })
                .ToList();
        }

        #endregion
    }
}
