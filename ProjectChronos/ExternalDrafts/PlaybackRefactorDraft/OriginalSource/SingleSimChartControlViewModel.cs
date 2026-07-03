using Arction.Wpf.Charting;
using Arction.Wpf.Charting.Series3D;
using Arction.Wpf.Charting.SeriesXY;
using DevExpress.CodeParser;
using DevExpress.Mvvm;
using DevExpress.PivotGrid.PivotTable;
using DevExpress.Xpf.Charts;
using OSTES.Chart;
using OSTES.Common;
using OSTES.Data;
using OSTES.Dialog;
using OSTES.Interface;
using OSTES.Model;
using OSTES.Service;
using OSTES.Utils;
using OSTES.View.Demo;
using OSTES.ViewModel.Dialog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SeriesPoint = Arction.Wpf.Charting.SeriesPoint;

namespace OSTES.ViewModel.SIngleSim
{
    /// <summary>
    /// 사전 분석 - 그래프 분석 차트 뷰모델.
    /// </summary>
    public class SingleSimChartControlViewModel : ViewModelBase, IReplayMessageReceiver
    {
        private readonly OSTES.Dialog.IDialogService dialogService = new DialogService();
        private readonly GraphChartType _graphChartType;

        /// <summary>
        /// 수신 상태(_latestReplayTime/_isReplayRenderPending/_isForceRenderPending) 보호용 잠금.
        /// 메시지 수신은 송신부 Task.Run 발행으로 스레드풀에서, 렌더 콜백은 UI 스레드에서
        /// 실행되므로 동기화 없이는 stale 읽기(렌더 유실/이중 예약)가 가능하다.
        /// </summary>
        private readonly object _replayGate = new object();

        /// <summary>최신 재생 시간 (최신 상태 통합 렌더링용)</summary>
        private double _latestReplayTime = double.NaN;

        /// <summary>Dispatcher 렌더링 예약 중 여부 (최대 1개 pending)</summary>
        private bool _isReplayRenderPending;

        /// <summary>
        /// pending 중 도착한 강제 렌더(Seek/Stopped/StoppedByEvent) 의미 보존용 래치.
        /// 예약 당시 message.ChangeKind를 클로저로 소비하면 pending 중 도착한 Seek가
        /// Playback 취급되어 skip/스로틀로 유실될 수 있으므로, 콜백은 실행 시점의 이 값을 소비한다.
        /// </summary>
        private bool _isForceRenderPending;

        /// <summary>마지막 재생 렌더링 시각 (cadence 제한용)</summary>
        private DateTime _lastPlaybackRenderAt = DateTime.MinValue;

        /// <summary>재생 중 수신부 렌더링 최소 간격 (밀리초, 30Hz)</summary>
        private const int ReplayRenderIntervalMs = 33;

        /** @brief 렌더링 중첩 방지 플래그(투트랙, 2D/3D 공통). */
        private volatile bool _isUpdating;

        /** @brief 3D 차트 전용 재생 커서 스로틀링. */
        private DateTime _last3DPlaybackUpdateTime;

        /** @brief 3D 차트 재생 커서 스로틀링 주기(ms). App.config 에서 로드. */
        private readonly int _chart3DPlaybackThrottleMs;

        private SpatialSimulationModel _spatialSimulationModel;
        private ChartDefinition<SpatialData> _currentConfig;
        private CScenarioInfo _scenarioInfo;
        private CScenarioInfoSingle _scenarioInfoSingle;

        /** @brief 차트 범위 설정 타입(Default/Manual/Auto). */
        private RangeSettingType _rangeSettingType = RangeSettingType.Default;

        /** @brief 차트 전시 범위(Manual 모드 전용). */
        private ChartRangeData _charRangeData;

        /** @brief 차트 타입(SetRange 호출 분기용). */
        private ChartViewType _chartViewType;

        private Dictionary<string, int> _seriesIndexByPlayerKey;

        /// <summary>
        /// 재생 프레임 인덱스 (수신부 공용 ReplayFrameIndex).
        /// 해상도 그리드 정수 키로 저장/조회해 double 완전 일치 조회의 ULP 미스를 방지한다.
        /// </summary>
        private ReplayFrameIndex<List<AddSeriesPointDTO>> _playbackFrameIndex;

        /// <summary>
        /// 사전에 정의가 필요한 그래프 차트별 아군, 적군 정보.
        /// </summary>
        private Dictionary<GraphChartType, ChartDefinition<SpatialData>> _chartConfigs;

        /// <summary>
        /// 차트 축 종횡비 1:1 보정 기능 사용 여부.
        /// </summary>
        private bool IslockAspectRatio => _graphChartType == GraphChartType.XY || _graphChartType == GraphChartType.ThreeD;

        /// <summary>
        /// Manual 범위 설정 모드 여부.
        /// </summary>
        private bool IsManualRange => _rangeSettingType == RangeSettingType.Manual;

        public SingleSimChartControlViewModel(GraphChartType graphChartType)
        {
            _graphChartType = graphChartType;

            // App.config 에서 3D 스로틀링 주기 로드(기본값 100ms).
            var throttleConfig = System.Configuration.ConfigurationManager.AppSettings["Chart3DPlaybackThrottleMs"];
            if (int.TryParse(throttleConfig, out int throttleMs))
            {
                _chart3DPlaybackThrottleMs = throttleMs;
            }
            else
            {
                _chart3DPlaybackThrottleMs = 100;
            }

            Initialize();
        }

        public IUserCustomChart ChartControl { get => GetValue<IUserCustomChart>(); set => SetValue(value); }

        /// <summary>
        /// 차트 설정 다이얼로그용 ViewModel 인스턴스.
        /// </summary>
        public UserAnalSetViewModel UserAnalSetViewModel { get; set; }

        private void Initialize()
        {
            UpdateConfig();
            CreateChartControl();
            SubscribeEvents();
        }

        private void UpdateConfig()
        {
            _chartConfigs = new Dictionary<GraphChartType, ChartDefinition<SpatialData>>()
            {
                [GraphChartType.XY] = new ChartDefinition<SpatialData>
                {
                    AllyName = SingleSimAppConst.MISSIL_COMMPONENT,
                    EnemyName = SingleSimAppConst.Target_COMMPONENT,
                    AllyLegendBoxTitle = Resources.AllyMissile,
                    EnemyLegendBoxTitle = Resources.TargetLabel,
                    AxisXTitle = Resources.LongitudeDeg,
                    AxisYTitle = Resources.LatitudeDeg,
                    XSelector = d => d.LonPos,
                    YSelector = d => d.LatPos,
                    RangeMode = DefaultRangeMode.Coordinate,
                },
                [GraphChartType.Yaw] = new ChartDefinition<SpatialData>
                {
                    AllyName = SingleSimAppConst.MISSIL_COMMPONENT,
                    EnemyName = SingleSimAppConst.Target_COMMPONENT,
                    AllyLegendBoxTitle = Resources.AllyYaw,
                    EnemyLegendBoxTitle = Resources.TargetYaw,
                    AxisXTitle = Resources.TimeSec,
                    AxisYTitle = "Yaw(deg)",
                    XSelector = d => d.STime,
                    YSelector = d => d.Yaw,
                    RangeMode = DefaultRangeMode.TimeFixed,
                },
                [GraphChartType.Pitch] = new ChartDefinition<SpatialData>
                {
                    AllyName = SingleSimAppConst.MISSIL_COMMPONENT,
                    EnemyName = SingleSimAppConst.Target_COMMPONENT,
                    AllyLegendBoxTitle = Resources.AllyPitch,
                    EnemyLegendBoxTitle = Resources.TargetPitch,
                    AxisXTitle = Resources.TimeSec,
                    AxisYTitle = "Pitch(deg)",
                    XSelector = d => d.STime,
                    YSelector = d => d.Pitch,
                    RangeMode = DefaultRangeMode.TimeFixed,
                },
                [GraphChartType.Roll] = new ChartDefinition<SpatialData>
                {
                    AllyName = SingleSimAppConst.MISSIL_COMMPONENT,
                    EnemyName = SingleSimAppConst.Target_COMMPONENT,
                    AllyLegendBoxTitle = Resources.AllyRoll,
                    EnemyLegendBoxTitle = Resources.TargetRoll,
                    AxisXTitle = Resources.TimeSec,
                    AxisYTitle = "Roll(deg)",
                    XSelector = d => d.STime,
                    YSelector = d => d.Roll,
                    RangeMode = DefaultRangeMode.TimeFixed,
                },
                [GraphChartType.ThreeD] = new ChartDefinition<SpatialData>
                {
                    AllyName = SingleSimAppConst.MISSIL_COMMPONENT,
                    EnemyName = SingleSimAppConst.Target_COMMPONENT,
                    AllyLegendBoxTitle = Resources.AllyMissile,
                    EnemyLegendBoxTitle = Resources.TargetLabel,
                    AxisXTitle = Resources.LongitudeDeg,
                    AxisYTitle = Resources.LatitudeDeg,
                    AxisZTitle = Resources.AltitudeM,
                    XSelector = d => d.LonPos,
                    YSelector = d => d.Alt,
                    ZSelector = d => d.Lat,
                    RangeMode = DefaultRangeMode.Coordinate,
                },

                [GraphChartType.Alt] = new ChartDefinition<SpatialData>
                {
                    AllyName = SingleSimAppConst.MISSIL_COMMPONENT,
                    EnemyName = SingleSimAppConst.Target_COMMPONENT,
                    AllyLegendBoxTitle = Resources.AllyAlt,
                    EnemyLegendBoxTitle = Resources.TargetAlt,
                    AxisXTitle = Resources.TimeSec,
                    AxisYTitle = Resources.AltitudeM,
                    XSelector = d => d.STime,
                    YSelector = d => d.AltPos,
                    RangeMode = DefaultRangeMode.TimeAuto,
                },
                [GraphChartType.Lon_N] = new ChartDefinition<SpatialData>
                {
                    AllyName = SingleSimAppConst.MISSIL_COMMPONENT,
                    EnemyName = SingleSimAppConst.Target_COMMPONENT,
                    AllyLegendBoxTitle = Resources.AllyLonSpeed,
                    EnemyLegendBoxTitle = Resources.TargetLonSpeed,
                    AxisXTitle = Resources.TimeSec,
                    AxisYTitle = Resources.LonSpeedDegS,
                    XSelector = d => d.STime,
                    YSelector = d => d.LonVel,
                    RangeMode = DefaultRangeMode.TimeFixed,
                },
                [GraphChartType.Lat_E] = new ChartDefinition<SpatialData>
                {
                    AllyName = SingleSimAppConst.MISSIL_COMMPONENT,
                    EnemyName = SingleSimAppConst.Target_COMMPONENT,
                    AllyLegendBoxTitle = Resources.AllyLatSpeed,
                    EnemyLegendBoxTitle = Resources.TargetLatSpeed,
                    AxisXTitle = Resources.TimeSec,
                    AxisYTitle = Resources.LatSpeedDegS,
                    XSelector = d => d.STime,
                    YSelector = d => d.LatVel,
                    RangeMode = DefaultRangeMode.TimeFixed,
                },
                [GraphChartType.Alt_D] = new ChartDefinition<SpatialData>
                {
                    AllyName = SingleSimAppConst.MISSIL_COMMPONENT,
                    EnemyName = SingleSimAppConst.Target_COMMPONENT,
                    AllyLegendBoxTitle = Resources.AllyAltSpeed,
                    EnemyLegendBoxTitle = Resources.TargetAltSpeed,
                    AxisXTitle = Resources.TimeSec,
                    AxisYTitle = Resources.AltSpeedMS,
                    XSelector = d => d.STime,
                    YSelector = d => d.AltVel,
                    RangeMode = DefaultRangeMode.TimeAuto,
                },
            };

            if (!_chartConfigs.TryGetValue(_graphChartType, out var config)) { return; }
            _currentConfig = config;
        }

        public void ClearChart()
        {
            _spatialSimulationModel = null;
            _currentConfig = null;
            _scenarioInfo = null;
            _scenarioInfoSingle = null;
            _rangeSettingType = RangeSettingType.Default;
            _charRangeData = null;
            _chartViewType = default(ChartViewType);
            _seriesIndexByPlayerKey = null;
            _playbackFrameIndex = null;

            // UserAnalSetViewModel 내부 참조 초기화 후 해제.
            if (UserAnalSetViewModel != null)
            {
                UserAnalSetViewModel.ClearReferences();
                UserAnalSetViewModel = null;
            }

            // 차트 설정 딕셔너리 초기화 (새 시나리오에서 UpdateConfig()로 재생성).
            _chartConfigs = null;

            // 기존 차트 초기화 (차트 인스턴스 유지, 시리즈/이벤트만 해제).
            // 재생성은 InitializeSpatialDbSourceAsync에서 RebindChartEvents로 수행.
            //ChartControl?.ClearChart();

            ChartControl?.Dispose();
            ChartControl = null;
        }

        public async Task InitializeSpatialDbSourceAsync(SpatialSimulationModel source, CScenarioInfoSingle scenarioInfoSingle, ChartComponentConfig chartConfig = null)
        {
            _seriesIndexByPlayerKey = new Dictionary<string, int>();
            _playbackFrameIndex = new ReplayFrameIndex<List<AddSeriesPointDTO>>();
            _spatialSimulationModel = source;
            _scenarioInfo = scenarioInfoSingle.CScenarioInfo;
            _scenarioInfoSingle = scenarioInfoSingle;

            // 시나리오가 보유한 시간해상도로 키 단위 확정 (송신부 SetTimeResolution과 동일 소스).
            // NOTE: 이식 시 실제 해상도 속성명으로 연결할 것.
            _playbackFrameIndex.Configure(scenarioInfoSingle.TimeResolution);

            UpdateConfig();

            // 현재 타입에 맞는 config 정보.
            if (!_chartConfigs.TryGetValue(_graphChartType, out var config)) { return; }
            _currentConfig = config;

            // ClearChart()에서 초기화된 차트 재생성 또는 재바인딩.
            if (ChartControl == null)
            {
                CreateChartControl();
            }
            else
            {
                // 기존 차트 인스턴스 재사용: 이벤트/타이머/어노테이션 재바인딩.
                ChartControl.RebindChartEvents();
            }

            // 차트 초기화.
            // 1. UserAnalSetViewModel 초기화 (chartConfig 에서 저장된 색상 정보 로드).
            UserAnalSetViewModel = new UserAnalSetViewModel(_graphChartType.ToString(), new ObservableCollection<UserAxisData>(), GetChartViewType(), chartConfig);

            // 2. 시리즈 인덱스 매핑 생성 (BuildSeriesAxisData 및 CreateSeries 에서 사용).
            BuildSeriesIndexMap(source, scenarioInfoSingle.CScenarioInfo);

            // 3. chartConfig 가 없는 경우에만 UserAxisDatas 채움 (CreateSeries 에서 색상 읽음).
            if (chartConfig == null)
            {
                UserAnalSetViewModel.UserAxisDatas = BuildSeriesAxisData();
            }

            // 4. 시리즈 생성 (UserAxisDatas 에서 색상 읽음).
            CreateSeries();

            var renderPointMap = new Dictionary<int, List<ChartPoint3D>>();
            var is3DViewer = _graphChartType == GraphChartType.ThreeD;

            // 데이터 추출.
            await Task.Run(() =>
            {
                foreach (var timeEntry in source.SpatialDataCache.ByTimeAndObjectName)
                {
                    // 현재 시간에 데이터가 없는 시리즈의 커서 업데이트를 위해,
                    // 등록된 모든 시리즈에 대해 빈 DTO를 먼저 채운 뒤 실제 데이터로 덮어쓴다.
                    var frames = _seriesIndexByPlayerKey
                    .OrderBy(kvp => kvp.Value)
                    .Select(kvp => new AddSeriesPointDTO(kvp.Value, null, null))
                    .ToList();

                    foreach (var objectEntry in timeEntry.Value)
                    {
                        var objectName = objectEntry.Key;

                        if (!_seriesIndexByPlayerKey.TryGetValue(objectName, out var seriesIndex)) { continue; }

                        var sourcePoints = objectEntry.Value;
                        var convertPoints = new List<ChartPoint3D>(objectEntry.Value.Count);

                        foreach (var point in sourcePoints)
                        {
                            var chartPoint = is3DViewer
                            ? new ChartPoint3D(config.XSelector(point), config.YSelector(point), config.ZSelector(point), point.STime)
                            : new ChartPoint3D(config.XSelector(point), config.YSelector(point), default, point.STime);

                            convertPoints.Add(chartPoint);

                            if ((point.PlayerName == config.AllyName) || (point.PlayerName == config.EnemyName))
                            {
                                if (!renderPointMap.TryGetValue(seriesIndex, out var points))
                                {
                                    points = new List<ChartPoint3D>();
                                    renderPointMap[seriesIndex] = points;
                                }

                                points.Add(chartPoint);
                            }

                        }

                        var dto = is3DViewer
                        ? new AddSeriesPointDTO(seriesIndex, null, ChartPointMapper.ToSeriesPoints3D(convertPoints))
                        : new AddSeriesPointDTO(seriesIndex, ChartPointMapper.ToSeriesPoints2D(convertPoints), null);

                        frames[seriesIndex] = dto;
                    }

                    _playbackFrameIndex.Add(timeEntry.Key, frames);
                }
            });

            // 렌더링 수행.
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    ChartControl.BeginUpdate();
                    // 2D/3D 분기처리.
                    if (is3DViewer)
                    {
                        foreach (var renderPoint in renderPointMap)
                        {
                            var seriesIndex = renderPoint.Key;
                            var points = renderPoint.Value;

                            if (points.Any())
                            {
                                var allyRangeData = ChartPointMapper.ComputeRange3D(points);
                                var seriesPoints = ChartPointMapper.ToSeriesPoints3D(points);
                                var seriesPointDTO = new AddSeriesPointDTO(seriesIndex, null, seriesPoints);

                                ChartControl.AddPoints(seriesPointDTO);
                                //ChartControl.SetRange(allyRangeData, seriesIndex);
                            }
                        }
                    }
                    else
                    {
                        foreach (var renderPoint in renderPointMap)
                        {
                            var seriesIndex = renderPoint.Key;
                            var points = renderPoint.Value;

                            if (points.Any())
                            {
                                var allyRangeData = ChartPointMapper.ComputeRange2D(points);
                                var seriesPoints = ChartPointMapper.ToSeriesPoints2D(points);
                                var seriesPointDTO = new AddSeriesPointDTO(seriesIndex, seriesPoints, null);

                                ChartControl.AddPoints(seriesPointDTO);
                                //ChartControl.SetRange(allyRangeData, seriesIndex);
                            }
                        }
                    }
                }
                finally
                {
                    ChartControl.EndUpdate();

                    // 렌더링 완료 후 _playbackFrameIndex 데이터 기반 범위 적용.
                    ApplyChartRange();

                    // 차트 인터페이스 호출.
                    if (ChartControl is IChartUIControl chartUIControl)
                    {
                        chartUIControl.UpdateTrackAnnotationColorFromResource();
                    }
                    if (ChartControl is IChartPlayback chartPlayback)
                    {
                        chartPlayback.SetPlaybackCursor();
                    }
                }
            }, DispatcherPriority.Background);
        }

        #region 차트 설정 (차트 설정 다이얼로그 및 시각적 속성 적용 관련)
        private void CreateChartControl()
        {
            var chartThemaIndex = ChartThemeComboItem.GetThemaIndex(AppConst.DEFAULT_CHART_THEME);

            switch (_graphChartType)
            {
                case GraphChartType.XY:
                    ChartControl = new UserCustomChart_2DGraph(ChartViewType.TwoD_LL, _currentConfig.AxisXTitle, _currentConfig.AxisYTitle, IslockAspectRatio);
                    break;

                case GraphChartType.Yaw:
                case GraphChartType.Pitch:
                case GraphChartType.Roll:
                case GraphChartType.Alt:
                case GraphChartType.Lon_N:
                case GraphChartType.Lat_E:
                case GraphChartType.Alt_D:
                    ChartControl = new UserCustomChart_2DGraph(ChartViewType.TwoD_Time, _currentConfig.AxisXTitle, _currentConfig.AxisYTitle, IslockAspectRatio);
                    break;

                case GraphChartType.ThreeD:
                    ChartControl = new UserCustomChart_3DGraph(ChartViewType.ThreeD_LLA, _currentConfig.AxisXTitle, _currentConfig.AxisYTitle, _currentConfig.AxisZTitle, IslockAspectRatio);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(_graphChartType));
            }

            ChartControl.ChartThemeSelection(chartThemaIndex);
            ChartControl.ChartLegendTheme(chartThemaIndex);
        }

        /// <summary>
        /// UserAnalSetViewModel 에서 차트 범위 설정 정보를 현재 ViewModel 필드로 로드합니다.
        /// </summary>
        private void LoadUserAnalSetConfig()
        {
            _rangeSettingType = UserAnalSetViewModel.SelectedRangeSettingType;
            _chartViewType = UserAnalSetViewModel.SelectedChartViewType;

            if (_rangeSettingType == RangeSettingType.Manual)
            {
                _charRangeData = UserAnalSetViewModel.ChartRangeModelItem.Clone();
            }
        }

        /// <summary>
        /// 시나리오의 플레이어 정보를 기준으로 시리즈 인덱스 매핑을 생성합니다.
        /// </summary>
        private void BuildSeriesIndexMap(SpatialSimulationModel source, CScenarioInfo scenarioInfo)
        {
            _seriesIndexByPlayerKey.Clear();

            var seriesIndex = 0;
            var spatialObjects = source.SpatialDataCache.ByObjectName;

            foreach (var player in scenarioInfo.playerObjectMap.Values)
            {
                var coponentName = player.componentName;
                var playerUniqueKey = player.playerObjectName;
                var isAllyPlayer = coponentName == SingleSimAppConst.MISSIL_COMMPONENT;
                var isEnemyPlayer = coponentName == SingleSimAppConst.Target_COMMPONENT;

                // DB에 데이터가 없는 시나리오의 플레이어는 전시하지 않는다.
                if (!spatialObjects.ContainsKey(playerUniqueKey)) { continue; }

                // 아군 플레이어 or 적군 플레이어가 아닌 플레이어는 전시하지 않는다.
                if (!(isAllyPlayer || isEnemyPlayer)) { continue; }

                _seriesIndexByPlayerKey.Add(playerUniqueKey, seriesIndex);
                seriesIndex++;
            }
        }

        /// <summary>
        /// UserAxisDatas의 색상 정보를 바탕으로 시리즈를 생성하고 차트 설정을 로드합니다.
        /// </summary>
        private void CreateSeries()
        {
            var chartThemaIndex = ChartThemeComboItem.GetThemaIndex(AppConst.DEFAULT_CHART_THEME);

            // 0. _seriesIndexByPlayerKey 순서(시리즈 인덱스 순)로 정렬 (BuildSeriesAxisData와 순서 일치).
            var sortedEntries = _seriesIndexByPlayerKey.OrderBy(kvp => kvp.Value).ToList();

            foreach (var entry in sortedEntries)
            {
                var playerUniqueKey = entry.Key;
                var seriesIndex = entry.Value;

                // 1. 플레이어 정보 조회.
                var mappedPlayer = _scenarioInfo.playerObjectMap.Values.FirstOrDefault(v => v.playerObjectName == playerUniqueKey);
                if (mappedPlayer == null) { continue; }

                var coponentName = mappedPlayer.componentName;
                var isAllyPlayer = coponentName == SingleSimAppConst.MISSIL_COMMPONENT;
                var isEnemyPlayer = coponentName == SingleSimAppConst.Target_COMMPONENT;
                if (!(isAllyPlayer || isEnemyPlayer)) { continue; }

                // 2. 색상 및 선 종류 할당 (UserAxisDatas 에서 AttributeLabel = player.Label 기준 읽음).
                var existingAxisData = UserAnalSetViewModel.UserAxisDatas.FirstOrDefault(d => d.AttributeLabel == mappedPlayer.Label);
                var color = existingAxisData.Color;
                var lineType = existingAxisData.SelectedLineSeriesType;

                // 3. 시리즈 생성.
                var seriesTitle = isAllyPlayer
                    ? $"{_currentConfig.AllyLegendBoxTitle}({mappedPlayer.Label})"
                    : $"{_currentConfig.EnemyLegendBoxTitle}({mappedPlayer.Label})";

                var lineChartSeriesDTO = new LineChartSeriesDTO(seriesIndex, chartThemaIndex, seriesTitle, color, lineType);

                ChartControl.AddLineChartSeries(lineChartSeriesDTO);
            }

            // 4. 차트 설정 로드.
            LoadUserAnalSetConfig();
        }

        /// <summary>
        /// 차트 설정 다이얼로그를 열고 시각적 속성(테마, 범위, 시리즈)을 적용합니다.
        /// </summary>
        public void ChartSetting()
        {
            // 0. 차트 컨트롤이 초기화되지 않았으면 반환.
            if (ChartControl == null) { return; }

            // 0-1. 차트 세팅 컨트롤이 초기화되지 않았으면 반환.
            if (UserAnalSetViewModel == null) { return; }
            if (_scenarioInfoSingle == null) { return; }

            // 1. 차트 타입 매핑(GraphChartType → ChartViewType).
            UserAnalSetViewModel.SelectedChartViewType = GetChartViewType();

            // 2. 다이얼로그 표시.
            if (dialogService.ShowDialog(Resources.AnalysisSettings, UserAnalSetViewModel, typeof(OSTES.View.Dialog.GraphAnalysisChartSettingView)) != true) { return; }

            // 3. 설정 적용.
            ApplyChartSettings();
        }

        /// <summary>
        /// 차트 범위 및 시각적 속성을 적용합니다.
        /// 다이얼로그 확인 시점에서 호출됩니다.
        /// </summary>
        private void ApplyChartSettings()
        {
            // 1. 전역테마 강제 적용.
            //UserAnalSetViewModel.ChartThemeIndex = ChartThemeComboItem.GetThemaIndex(AppConst.DEFAULT_CHART_THEME);

            // 2. 차트 설정 로드.
            LoadUserAnalSetConfig();

            // 3. 범위 변경 적용(타입별 범위 계산 + 차트 적용).
            ApplyChartRange();

            // 4. 시리즈 시각적 속성 변경 적용.
            ApplySeriesVisual();
        }

        /// <summary>
        /// GraphChartType에 해당하는 ChartViewType을 반환합니다.
        /// </summary>
        private ChartViewType GetChartViewType()
        {
            switch (_graphChartType)
            {
                case GraphChartType.XY:
                    return ChartViewType.TwoD_LL;
                case GraphChartType.ThreeD:
                    return ChartViewType.ThreeD_LLA;
                default:
                    return ChartViewType.TwoD_Time;
            }
        }

        /// <summary>
        /// 범위 설정 타입에 따라 범위를 계산하고 차트에 적용합니다.
        /// </summary>
        private void ApplyChartRange()
        {
            // 1. 범위 설정 타입별 범위 계산.
            var range = (ChartRangeData)null;

            switch (_rangeSettingType)
            {
                case RangeSettingType.Default: // Default 모드: RangeMode별 처리.
                    range = CalculateDefaultRange();
                    break;

                case RangeSettingType.Manual: // Manual 모드: 사용자가 설정한 범위 정보 사용.
                    range = _charRangeData;
                    break;

                case RangeSettingType.Auto: // Auto 모드: 전체 데이터 자동 맞춤.
                    range = CalculateAutoFitRange();
                    break;

                default:
                    break;
            }

            if (range == null) { return; }

            ChartControl.BeginUpdate();
            try
            {
                ChartControl.SetRange(range, 0, IsManualRange);
            }
            finally
            {
                ChartControl.EndUpdate();
            }
        }

        private ChartRangeData CalculateDefaultRange()
        {
            var range = (ChartRangeData)null;

            switch (_currentConfig.RangeMode)
            {
                case DefaultRangeMode.TimeFixed: // X축 0~종료시간 고정, Y축 -180~180 고정.
                    range = _scenarioInfoSingle.GetDefaultRange(_chartViewType);
                    if (range != null)
                    {
                        range.Y_Min = -180;
                        range.Y_Max = 180;
                    }
                    break;

                case DefaultRangeMode.TimeAuto: // X축 0~종료시간 고정, Y축 데이터 기반 자동 조정.
                    var timeRange = _scenarioInfoSingle.GetDefaultRange(_chartViewType);
                    var autoRange = CalculateAutoFitRange();
                    if (timeRange != null && autoRange != null)
                    {
                        range = new ChartRangeData
                        {
                            X_Min = timeRange.X_Min,
                            X_Max = timeRange.X_Max,
                            Y_Min = autoRange.Y_Min,
                            Y_Max = autoRange.Y_Max,
                        };
                    }
                    break;

                case DefaultRangeMode.Coordinate: // X축/Y축 모두 차트 시리즈 객체 좌표 범위.
                    range = _scenarioInfoSingle.GetDefaultRange(_chartViewType, _seriesIndexByPlayerKey.Keys);
                    break;

                default:
                    break;
            }

            if (range == null)
            {
                range = CalculateAutoFitRange();
            }

            return range;
        }

        /// <summary>
        /// _playbackFrameIndex의 전체 데이터에서 자동 맞춤 범위를 계산합니다.
        /// </summary>
        private ChartRangeData CalculateAutoFitRange()
        {
            if (_playbackFrameIndex == null || _playbackFrameIndex.Count == 0) { return null; }

            if (_graphChartType == GraphChartType.ThreeD)
            {
                var allPoints = _playbackFrameIndex.Frames
                    .SelectMany(frames => frames)
                    .SelectMany(dto => dto.SeriesPoint3D ?? Array.Empty<SeriesPoint3D>())
                    .ToList();

                if (allPoints.Count == 0) { return null; }

                return ChartPointMapper.ComputeRange3D(allPoints);
            }
            else
            {
                var allPoints = _playbackFrameIndex.Frames
                    .SelectMany(frames => frames)
                    .SelectMany(dto => dto.SeriesPoint2D ?? Array.Empty<SeriesPoint>())
                    .ToList();

                if (allPoints.Count == 0) { return null; }

                return ChartPointMapper.ComputeRange2D(allPoints);
            }
        }

        /// <summary>
        /// 시리즈 정보를 기반으로 UserAxisData 컬렉션을 빌드합니다.
        /// chartConfig 가 없는 초기화 시점에 1회 호출되며 고유 색상을 할당합니다.
        /// </summary>
        private ObservableCollection<UserAxisData> BuildSeriesAxisData()
        {
            if (_scenarioInfo == null || _currentConfig == null) { return new ObservableCollection<UserAxisData>(); }

            var result = new ObservableCollection<UserAxisData>();
            var chartColorService = new OSTES.Service.ChartColorService();

            // 0. _seriesIndexByPlayerKey 순서(시리즈 인덱스 순)로 정렬.
            var sortedEntries = _seriesIndexByPlayerKey.OrderBy(kvp => kvp.Value).ToList();

            foreach (var entry in sortedEntries)
            {
                var playerUniqueKey = entry.Key;

                // 1. 플레이어 정보 조회.
                var mappedPlayer = _scenarioInfo.playerObjectMap.Values.FirstOrDefault(v => v.playerObjectName == playerUniqueKey);
                if (mappedPlayer == null) { continue; }

                var legendTitle = (mappedPlayer.componentName == SingleSimAppConst.MISSIL_COMMPONENT)
                    ? _currentConfig.AllyLegendBoxTitle
                    : _currentConfig.EnemyLegendBoxTitle;

                // 2. 고유 색상 생성 및 UserAxisData 추가 (AttributeLabel = player.Label).
                var color = chartColorService.GenerateUniqueColor(result);

                var userAxisData = new UserAxisData()
                {
                    ObjectLabel = legendTitle,
                    AttributeLabel = mappedPlayer.Label,
                    Unit = _currentConfig.AxisYTitle,
                    Color = color,
                    SelectedLineSeriesType = LineSeriesType.LineOnly,
                };

                result.Add(userAxisData);
            }

            return result;
        }

        /// <summary>
        /// UserAnalSetViewModel.UserAxisDatas의 변경된 시각적 속성을 ChartControl 시리즈에 적용합니다.
        /// </summary>
        private void ApplySeriesVisual()
        {
            if (UserAnalSetViewModel.UserAxisDatas == null) { return; }
            if (_seriesIndexByPlayerKey == null) { return; }

            // 0. UserAxisDatas와 ChartControl 시리즈를 인덱스로 매핑.
            var axisDatasCount = UserAnalSetViewModel.UserAxisDatas.Count;
            var sortedSeriesIndexes = _seriesIndexByPlayerKey.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Value).ToList();

            ChartControl.BeginUpdate();
            try
            {
                for (var i = 0; i < axisDatasCount; i++)
                {
                    if (i >= sortedSeriesIndexes.Count) { break; }

                    // 1. 변경된 속성 조회.
                    var userAxisData = UserAnalSetViewModel.UserAxisDatas[i];

                    // 2. LightningChart의 시리즈 속성 업데이트.
                    ChartControl.UpdateSeriesVisual(sortedSeriesIndexes[i], userAxisData.Color, userAxisData.SelectedLineSeriesType, userAxisData.AttributeLabel);
                }

                // 3. 시리즈 색상 변경 후 마커 색상 동기화.
                if (ChartControl is IChartUIControl chartUIControl)
                {
                    chartUIControl.UpdateTrackAnnotationColorFromResource();
                }
                if (ChartControl is IChartPlayback chartPlayback)
                {
                    chartPlayback.SetPlaybackCursor();
                }
            }
            finally
            {
                ChartControl.EndUpdate();
            }
        }

        /// <summary>
        /// 현재 차트 설정을 ChartComponentConfig로 반환합니다.
        /// </summary>
        public ChartComponentConfig GetChartConfig()
        {
            if (UserAnalSetViewModel == null) { return null; }

            return new ChartComponentConfig(UserAnalSetViewModel);
        }
        #endregion

        #region EventHandlers & Subscriptions (이벤트 핸들러, 메시지 구독자, 통신 응답 처리 관련)
        private void SubscribeEvents()
        {
            Messenger.Default.Register<ChartThemeComboItem>(this, ReceiveUpdateChartTheme);
            // SimulationTimeChangedMessage는 ReplayDockingHub를 통해 라우팅됨.
        }

        /// <summary>
        /// 시뮬레이션 재생 시간 변경 메시지 수신 (허브용 명시적 구현).
        /// </summary>
        /// <param name="message">시간 변경 메시지</param>
        void IReplayMessageReceiver.ReceiveSimulationTimeChangedMessage(SimulationTimeChangedMessage message)
        {
            ReceiveSimulationTimeChangedMessage(message);
        }

        private void ReceiveUpdateChartTheme(ChartThemeComboItem param)
        {
            var themeIndex = ChartThemeComboItem.GetThemaIndex(param.Name);

            ChartControl.ChartThemeSelection(themeIndex);
            ChartControl.ChartLegendTheme(themeIndex);
        }

        private void ReceiveSimulationTimeChangedMessage(SimulationTimeChangedMessage message)
        {
            if (!(ChartControl is IChartPlayback)) { return; }
            if (_spatialSimulationModel == null) { return; }

            // 0. 강제 렌더링 여부 판단 (Seek, Stopped, StoppedByEvent는 즉시 렌더링).
            bool forceRender = message.ChangeKind != SimulationTimeChangeKind.Playback;

            lock (_replayGate)
            {
                // 1. 최신 시간 갱신 + 강제 렌더 의미 래치.
                //    pending 중 도착한 Seek/Stopped도 여기서 래치되어 콜백에서 유실되지 않는다.
                _latestReplayTime = message.NewTime;
                if (forceRender) { _isForceRenderPending = true; }

                // 2. 재생 중이면 렌더링 cadence 제한 (강제 렌더가 래치된 경우 우회).
                if (!forceRender && !_isForceRenderPending)
                {
                    var now = DateTime.UtcNow;
                    if ((now - _lastPlaybackRenderAt).TotalMilliseconds < ReplayRenderIntervalMs)
                    {
                        return;
                    }
                }

                // 3. pending 렌더링이 있으면 최신 시간/강제 여부만 갱신된 상태이므로 추가 예약 불필요.
                if (_isReplayRenderPending)
                {
                    return;
                }

                // 4. Dispatcher에 렌더링 1개만 예약.
                _isReplayRenderPending = true;
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                // 5. 실행 시점의 최신 상태를 lock 하에 소비.
                //    예약 당시 message의 시간/ChangeKind는 사용하지 않는다 (stale 클로저 방지).
                double renderTime;
                bool isForceRender;
                lock (_replayGate)
                {
                    _isReplayRenderPending = false;
                    isForceRender = _isForceRenderPending;
                    _isForceRenderPending = false;
                    renderTime = _latestReplayTime;
                }

                // 5-0. Background 대기 중 ClearChart로 인덱스가 해제될 수 있으므로 로컬 참조로 방어.
                var frameIndex = _playbackFrameIndex;
                if (frameIndex == null) { return; }

                // 5-1. 해상도 그리드 정수 키 조회 (정밀 → 10ms 폴백은 ReplayFrameIndex가 수행).
                List<AddSeriesPointDTO> targetFrames = null;
                if (!frameIndex.TryGetFrame(renderTime, out targetFrames))
                {
                    // 해당 시간에 데이터 없음 → 렌더하지 않음 (기존 의도 유지).
                    return;
                }

                // 6. 렌더링 수행 (중첩 방지). 강제 렌더는 건너뛰지 않는다.
                if (!isForceRender && _isUpdating)
                {
                    return;
                }

                // 7. 3D 차트 전용 스로틀링 (App.config). 강제 렌더가 아닌 경우만 적용.
                if (_graphChartType == GraphChartType.ThreeD && !isForceRender)
                {
                    var now = DateTime.UtcNow;
                    if ((now - _last3DPlaybackUpdateTime).TotalMilliseconds < _chart3DPlaybackThrottleMs)
                    {
                        return;
                    }
                    _last3DPlaybackUpdateTime = now;
                }

                _isUpdating = true;
                try
                {
                    ChartControl.BeginUpdate();
                    foreach (var frame in targetFrames)
                    {
                        ((IChartPlayback)ChartControl).UpdatePlaybackCursorPosition(frame);
                    }
                    ChartControl.EndUpdate();

                    // 8. cadence 기준 시각은 실제 렌더 완료 시점에 갱신.
                    //    조회 미스/중첩 skip이 cadence를 소모해 다음 렌더를 억제하지 않도록
                    //    콜백 진입 시점이 아닌 여기서 기록한다.
                    _lastPlaybackRenderAt = DateTime.UtcNow;
                }
                finally
                {
                    _isUpdating = false;
                }
            }), DispatcherPriority.Normal);
        }

        #endregion

    }
}
