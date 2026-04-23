using Arction.Wpf.Charting;

using Arction.Wpf.Charting.Annotations;

using Arction.Wpf.Charting.EventMarkers;

using Arction.Wpf.Charting.SeriesXY;

using Arction.Wpf.Charting.Views.ViewXY;

using DevExpress.Xpf.Editors;

using DevExpress.XtraBars.Ribbon;

using DevExpress.XtraScheduler;

using OSTES.Common;

using OSTES.Data;

using OSTES.Dialog;

using OSTES.Interface;

using OSTES.Service;

using OSTES.Utils;

using OSTES.ViewModel.Dialog;

using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.Diagnostics;

using System.IO;

using System.Linq;

using System.Text;

using System.Threading.Tasks;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Data;

using System.Windows.Documents;

using System.Windows.Input;

using System.Windows.Media;

using System.Windows.Media.Imaging;

using System.Windows.Navigation;

using System.Windows.Shapes;



namespace OSTES.Chart

{

    /// <summary>

    /// UserCustomChart_2DSubplotGraph.xaml에 대한 상호 작용 논리

    /// </summary>

    public partial class UserCustomChart_2DSubplotGraph : UserControl, IUserCustomChart, IChartUIControl, IChartPlayback, IDisposable

    {

        private readonly OSTES.Dialog.IDialogService _dialogService = new DialogService();

        private readonly ChartColorService _chartColorService = new ChartColorService();



        /** @brief 반올림 자릿수 */

        private const int _roundingDigits = 6;



        /** @brief 해당 인스턴스에 설정된 차트 타입(Diff 계산에 사용됨). */

        private readonly ChartViewType _chartViewType;



        /** @brief 사용자 메모에서 현재 편집중인 Annotation */

        private AnnotationXY _editingAnnotationXY;



        /** @brief Cursor Tracking에 사용된 Annotaition 반환 */

        private IEnumerable<AnnotationXY> _trackingAnnotations => _chart.ViewXY.Annotations.Where(item => (item.Tag is AnnotationType type) && ((type == AnnotationType.CursorTooltip) || (type == AnnotationType.SelectionPin)));



        /** @brief 팬 여부 */

        private bool isPaend;



        /** @brief 첫번째 여부 */

        private bool isFirst;



        /** @brief YAxes별 차트 전시 범위 */

        private Dictionary<int, ChartRangeData> _chartRangeDataMap;



        /** @brief  마우스 클릭 위치 */

        private Point mouseDownPoint;



        /** @brief  마우스 우클릭 위치 */

        private Point _rightBtnDownPos;



        /** @brief 마우스 포인트 마커 */

        private SeriesEventMarker mouseMoveMarker;



        /** @brief 전시중인 차트 인스턴스 */

        private LightningChartUltimate _chart;



        /** @brief 한글 IME 정상 동작을 위한 Window. **/

        private EditorInputWindow _editorWindow;



        private LineSeriesCursor _trackingCurosr;

        /** @brief PlaybackCursor 마커 캐시 */

        private readonly Dictionary<int, List<SeriesEventMarker>> _playbackMarkerPool = new Dictionary<int, List<SeriesEventMarker>>();

        /** @brief PlaybackCursor 렌더 상태 캐시 */

        private readonly Dictionary<int, PlaybackCursorSnapshot[]> _playbackSnapshots = new Dictionary<int, PlaybackCursorSnapshot[]>();

        private struct PlaybackCursorSnapshot

        {

            public double X;

            public double Y;

            public bool Visible;

        }



        public UserCustomChart_2DSubplotGraph(ChartViewType chartViewType, string axisXTitle, string axisYTitle)

        {

            Debug.Assert(chartViewType == ChartViewType.TwoD_Time_Multi, "SubPlot 생성자 오류");



            _chartViewType = chartViewType;

            _chartRangeDataMap = new Dictionary<int, ChartRangeData>();



            _editorWindow = new EditorInputWindow();

            _editorWindow.SaveRequested += OnEditorSaved;

            _editorWindow.CancelRequested += OnEditorCancelled;



            InitializeComponent();

            CreateChart(axisXTitle, axisYTitle);

        }



        #region IDataManipulation Public API

        public void BeginUpdate() => _chart.BeginUpdate();



        public void EndUpdate() => _chart.EndUpdate();



        public void AddPoints(AddSeriesPointDTO addSeriesPointDTO)

        {

            var selectedFreeformPointLineSeries = _chart.ViewXY.FreeformPointLineSeries.ElementAtOrDefault(addSeriesPointDTO.SeriesIndex);

            if (selectedFreeformPointLineSeries == null) { return; }

            if (addSeriesPointDTO.SeriesPoint2D.Length <= 0) { return; }



            // todo(min) : 반복문 최소화 적용.

            var y_Min = addSeriesPointDTO.SeriesPoint2D.Min(p => p.Y);

            var y_Max = addSeriesPointDTO.SeriesPoint2D.Max(p => p.Y);

            var y_Avr = addSeriesPointDTO.SeriesPoint2D.Average(p => p.Y);



            selectedFreeformPointLineSeries.AddPoints(addSeriesPointDTO.SeriesPoint2D, false);

            selectedFreeformPointLineSeries.Tag = new SeriesMetrics(y_Min, y_Max, y_Avr);

        }



        /**

         * @brief 차트 범위 설정.

         *

        */

        public void SetRange(ChartRangeData rangeData, int yAxesIndex)

        {

            if (rangeData == default) { return; }



            _chartRangeDataMap[yAxesIndex] = rangeData; // 지역 변수 초기화.



            //_chart.ViewXY.ZoomToFit();

            _chart.ViewXY.XAxes[0].SetRange(rangeData.X_Min, rangeData.X_Max);

            _chart.ViewXY.YAxes[yAxesIndex].SetRange(rangeData.Y_Min, rangeData.Y_Max);

        }



        public void AddLineChartSeries(LineChartSeriesDTO lineChartSeriesDTO)

        {

            ChartCreate_2D.AddLineChart_Series(_chart, lineChartSeriesDTO.SeriesName, lineChartSeriesDTO.LineColor, lineChartSeriesDTO.LineSeriesType, lineChartSeriesDTO.SubplotIndex);

            var annot = CreateAnnotationXY(AnnotationType.CursorTooltip);

            _chart.ViewXY.Annotations.Add(annot);



            LightningChartThemeUtil.ChartSubplotThemeSelection(_chart, lineChartSeriesDTO.ChartThemeIndex, lineChartSeriesDTO.SubplotIndex, lineChartSeriesDTO.SubplotYAxisTitle, lineChartSeriesDTO.LineColor);



            Debug.Assert(_chart.ViewXY.Annotations[lineChartSeriesDTO.SubplotIndex] == annot, "2D Subplot AddLineSeries Index 오류");

        }



        public void ClearChart()

        {

            _chart.BeginUpdate();



            foreach (var series in _chart.ViewXY.FreeformPointLineSeries)  //! 기존 선택된 Event Marker 삭제

            {

                series.Clear(); // 시리즈 초기화.



                for (var lastIndex = series.SeriesEventMarkers.Count() - 1; lastIndex >= 0; lastIndex--) // Marker 초기화.

                {

                    var marker = series.SeriesEventMarkers[lastIndex];

                    if ((marker.Tag is MarkerType type) && (type == MarkerType.CursorTooltip)) // CursorTooltip 이외 모든 마커 초기화.

                    {

                        marker.Visible = false;

                        continue;

                    }



                    marker.Dispose();

                    series.SeriesEventMarkers.Remove(marker);

                }

            }



            for (var lastIndex = _chart.ViewXY.Annotations.Count() - 1; lastIndex >= 0; lastIndex--)

            {

                var annotation = _chart.ViewXY.Annotations.ElementAt(lastIndex);

                if (annotation.Tag is AnnotationType type && type == AnnotationType.CursorTooltip) // CursorTooltip 이외 모든 어노테이션 초기화.

                {

                    annotation.Visible = false;

                    continue;

                }



                _chart.ViewXY.Annotations.Remove(annotation);

            }



            _chart.EndUpdate();

            ResetPlaybackState();

        }

        #endregion



        #region IChartExport Public API

        public bool IsValidChart => _chart.ViewXY.FreeformPointLineSeries.Count > 0;



        public string SaveChartToFile(string directiory)

        {

            var filePath = $"SnapShotChart_2DSubPlot.png";

            var fullPath = System.IO.Path.Combine(directiory, filePath);



            var graphBackgroundColor = _chart.ViewXY.GraphBackground.Color;

            var graphBackgroundGradientColor = _chart.ViewXY.GraphBackground.GradientColor;

            var chartBackgroundColor = _chart.ChartBackground.Color;

            var chartBackgroundGradientColor = _chart.ChartBackground.GradientColor;

            var chartXAxesLabelColor = _chart.ViewXY.XAxes[0].LabelsColor;



            // 차트 캡처를 위한 배경 변경.

            _chart.BeginUpdate();

            _chart.ViewXY.XAxes[0].LabelsColor = Colors.Black;

            _chart.ViewXY.YAxes.ForEach(item => item.LabelsColor = Colors.Black);

            _chart.ViewXY.GraphBackground.Color = _chart.ViewXY.GraphBackground.GradientColor = _chart.ChartBackground.Color = _chart.ChartBackground.GradientColor = Colors.White;

            _chart.EndUpdate();



            _chart.SaveToFile(fullPath, 546, 340);



            // 기존 차트 배경 롤백.

            _chart.BeginUpdate();

            _chart.ViewXY.GraphBackground.Color = graphBackgroundColor;

            _chart.ViewXY.GraphBackground.GradientColor = graphBackgroundGradientColor;

            _chart.ChartBackground.Color = chartBackgroundColor;

            _chart.ChartBackground.GradientColor = chartBackgroundGradientColor;

            _chart.ViewXY.XAxes[0].LabelsColor = chartXAxesLabelColor;



            // 260223(min) reference Type으로 인한 chartXAxesLabelColor 할당.

            for (var index = 0; index < _chart.ViewXY.YAxes.Count(); index++)

            {

                _chart.ViewXY.YAxes[index].LabelsColor = chartXAxesLabelColor;

            }

            _chart.EndUpdate();



            return fullPath;

        }



        public string GetChartMetaData()

        {

            var sb = new StringBuilder();



            foreach (var series in _chart.ViewXY.FreeformPointLineSeries)

            {

                if (series.Tag is SeriesMetrics seriesMetrics)

                {

                    var minMaxAvg = $"{series.Title.Text} : Y 통계 (Min/Max/Avg) = {Math.Round(seriesMetrics.Min, _roundingDigits)}/{Math.Round(seriesMetrics.Max, _roundingDigits)}/{Math.Round(seriesMetrics.Average, _roundingDigits)}";

                    sb.AppendLine(minMaxAvg);

                }

            }



            return sb.ToString();

        }

        #endregion



        #region IChartTheming Public API

        public void ChartThemeSelection(int index)

        {

            //throw new ArgumentException("Subplot에서 해당 API 미사용");

            LightningChartThemeUtil.ChartThemeSelection(_chart, index);

        }



        public void ChartThemeSelection(int index, int sharedIndex)

        {

            LightningChartThemeUtil.ChartThemeSelection(_chart, index);

        }



        public void ChartLegendTheme(int index)

        {

            LightningChartThemeUtil.ChartLegendTheme(_chart, index);

        }



        #endregion



        #region IChartUIControl Public API

        public void ZoomToFit() { }



        public void HideLegendBox() { }



        /// <summary>

        /// YAxes에 설정된 색상 기준으로 TrackingCurosr의 색상을 지정한다.

        /// </summary>

        public void UpdateTrackAnnotationColorFromResource()

        {

            var colors = new List<IColored>();

            foreach (var yAxes in _chart.ViewXY.YAxes)

            {

                colors.Add(new SimpleColored() { Color = yAxes.AxisColor });

            }



            var uniqueColor = _chartColorService.GenerateUniqueColor(colors);

            _trackingCurosr.LineStyle.Color = uniqueColor;

        }



        public ViewXY GetViewXY() { return null; }

        #endregion



        #region IChartPlayback Public API

        public void SetPlaybackCursor()

        {

            _chart.BeginUpdate();



            try

            {

                ResetPlaybackState();



                for (int seriesIndex = 0; seriesIndex < _chart.ViewXY.FreeformPointLineSeries.Count; seriesIndex++)

                {

                    var series = _chart.ViewXY.FreeformPointLineSeries[seriesIndex];

                    var playbackMarkers = series.SeriesEventMarkers.Where(m => (m.Tag is MarkerType type) && type == MarkerType.PlaybackCursor).ToList();



                    if (playbackMarkers.Count == 0)

                    {

                        var playbackCursorMarker = CreateEventMarker(MarkerType.PlaybackCursor);

                        playbackCursorMarker.Symbol.BorderColor = series.LineStyle.Color;

                        playbackCursorMarker.Symbol.Color1 = playbackCursorMarker.Symbol.Color2 = playbackCursorMarker.Symbol.Color3 = series.LineStyle.Color;

                        series.SeriesEventMarkers.Add(playbackCursorMarker);

                        playbackMarkers.Add(playbackCursorMarker);

                    }

                    else

                    {

                        foreach (var marker in playbackMarkers)

                        {

                            marker.Symbol.BorderColor = series.LineStyle.Color;

                            marker.Symbol.Color1 = marker.Symbol.Color2 = marker.Symbol.Color3 = series.LineStyle.Color;

                            marker.Visible = false;

                        }

                    }



                    _playbackMarkerPool[seriesIndex] = playbackMarkers;

                    _playbackSnapshots[seriesIndex] = CreateHiddenPlaybackSnapshots(playbackMarkers.Count);

                }

            }

            finally

            {

                _chart.EndUpdate();

            }

        }



        public void UpdatePlaybackCursorPosition(AddSeriesPointDTO addSeriesPointDTO)

        {
            if (addSeriesPointDTO == null) { return; }

            var selectedFreeformPointLineSeries = _chart.ViewXY.FreeformPointLineSeries.ElementAtOrDefault(addSeriesPointDTO.SeriesIndex);

            if (selectedFreeformPointLineSeries == null) { return; }

            var points = addSeriesPointDTO.SeriesPoint2D ?? Array.Empty<SeriesPoint>();

            EnsurePlaybackMarkerPool(addSeriesPointDTO.SeriesIndex, points.Length, selectedFreeformPointLineSeries.LineStyle.Color);

            var pooledMarkers = _playbackMarkerPool[addSeriesPointDTO.SeriesIndex];

            var snapshots = _playbackSnapshots[addSeriesPointDTO.SeriesIndex];

            var hasVisualChange = false;

            for (var i = 0; i < pooledMarkers.Count; i++)

            {

                var shouldBeVisible = i < points.Length;

                if (!shouldBeVisible)

                {

                    if (snapshots[i].Visible)

                    {

                        hasVisualChange = true;

                    }

                    continue;

                }



                var point = points[i];

                if (!snapshots[i].Visible || snapshots[i].X != point.X || snapshots[i].Y != point.Y)

                {

                    hasVisualChange = true;

                }

            }

            if (!hasVisualChange) { return; }



            _chart.BeginUpdate();



            try

            {

                for (var i = 0; i < pooledMarkers.Count; i++)

                {

                    var currentMarker = pooledMarkers[i];

                    if (i < points.Length)

                    {

                        currentMarker.XValue = points[i].X;

                        currentMarker.YValue = points[i].Y;

                        currentMarker.Visible = true;

                        snapshots[i] = new PlaybackCursorSnapshot()

                        {

                            X = points[i].X,

                            Y = points[i].Y,

                            Visible = true

                        };

                    }

                    else

                    {

                        currentMarker.Visible = false;

                        snapshots[i] = new PlaybackCursorSnapshot() { Visible = false };

                    }

                }

            }

            finally

            {

                _chart.EndUpdate();

            }

            return;



#if false
            try

            {

                // PlaybackCursor의 마커만 판단.

                var markers = selectedFreeformPointLineSeries.SeriesEventMarkers.Where(m => (m.Tag is MarkerType type) && type == MarkerType.PlaybackCursor).ToList();



                if (addSeriesPointDTO.SeriesPoint2D.Length <= 0) // SeriesPoint가 존재하지않으면 PlaybackCursor 비활성화 처리 후 return.

                {

                    markers.ForEach(m => m.Visible = false);

                    return;

                }



                var dataCount = addSeriesPointDTO.SeriesPoint2D.Count();



                // 현재 시리지의 마커 개수가 부족한 경우 신규 생성.

                while (markers.Count() < dataCount)

                {

                    var playbackCursorMarker = CreateEventMarker(MarkerType.PlaybackCursor);

                    playbackCursorMarker.Symbol.BorderColor = selectedFreeformPointLineSeries.LineStyle.Color;

                    playbackCursorMarker.Symbol.Color1 = playbackCursorMarker.Symbol.Color2 = playbackCursorMarker.Symbol.Color3 = selectedFreeformPointLineSeries.LineStyle.Color;

                    selectedFreeformPointLineSeries.SeriesEventMarkers.Add(playbackCursorMarker);

                    markers.Add(playbackCursorMarker);

                }



                // 모든 마커를 순회하며 업데이트.

                for (var i = 0; i < markers.Count(); i++)

                {

                    var currentMarker = markers.ElementAt(i);

                    if (i < dataCount)

                    {

                        currentMarker.XValue = addSeriesPointDTO.SeriesPoint2D[i].X;

                        currentMarker.YValue = addSeriesPointDTO.SeriesPoint2D[i].Y;

                        currentMarker.Visible = true;

                    }

                    else

                    {

                        currentMarker.Visible = false;

                    }

                }

            }

            finally

            {

                _chart.EndUpdate();

            }
#endif

        }

        #endregion



        #region IDisposable Public API

        public void Dispose()

        {

            if (_chart != null)

            {

                _chart.ViewXY.LegendBox.SeriesTitleMouseMoveOverOn -= LegendBox_SeriesTitleMouseMoveOverOn;

                _chart.ViewXY.LegendBox.SeriesTitleMouseMoveOverOff -= LegendBox_SeriesTitleMouseMoveOverOff;

                _chart.ViewXY.BeforePanning -= ViewXY_Panned;

                _chart.MouseDown -= Chart_MouseDown;

                _chart.MouseMove -= new MouseEventHandler(Chart_MouseMove);

                _chart.MouseLeave -= Chart_MouseLeave;

                _chart.MouseClick -= Chart_MouseClick;



                gridChart.Children.Clear(); //! 차트 삭제

                _chart.Dispose();

                _chart = null;

            }

            ResetPlaybackState();

        }

        #endregion



        #region Mouse Interfaction Handlers

        /**

         * @brief LegenBox Mouse Over 이벤트 처리.

         *

         * @param   sender 이벤트 출저.

         * @param   e      선택된 시리즈 정보.

        */

        private void LegendBox_SeriesTitleMouseMoveOverOn(object sender, SeriesTitleMouseMovedEventArgs e)

        {

            var selectedSeries = e.Series;

            if (!(selectedSeries.Tag is SeriesMetrics seriesMetrics)) { return; }



            txtLegenBoxTooltip.Text = $"최소 = {Math.Round(seriesMetrics.Min, _roundingDigits)}\n최대 = {Math.Round(seriesMetrics.Max, _roundingDigits)}\n평균 = {Math.Round(seriesMetrics.Average, _roundingDigits)}";

            legenBoxTooltip.Visibility = Visibility.Visible;

        }



        /**

         * @brief LegenBox Mouse Leave 이벤트 처리.

         *

         * @param   sender 이벤트 출저.

         * @param   e      선택된 시리즈 정보.

        */

        private void LegendBox_SeriesTitleMouseMoveOverOff(object sender, SeriesTitleMouseMovedEventArgs e)

        {

            legenBoxTooltip.Visibility = Visibility.Collapsed;

        }



        /**

        * @brief 팬 이벤트 처리.

        *

        * @param   sender 이벤트 출저.

        * @param   e      팬 이벤트 정보.

       */

        private void ViewXY_Panned(object sender, BeforePanningXYEventArgs e)

        {

            isPaend = true; //! 팬 여부 설정.

        }



        /**

         * @brief 차트 마우스 다운 이벤트 처리.

         *

         * @param   sender 차트.

         * @param   e      이벤트 Args.

        */



        private void Chart_MouseDown(object sender, MouseButtonEventArgs e)

        {

            if (e.LeftButton == MouseButtonState.Pressed)

            {

                mouseDownPoint = e.GetPosition(_chart);

            }



        }



        /**

      * @brief 차트 마우스 클릭 이벤트 처리.

      *

      * @param   sender 차트.

      * @param   e      이벤트 Args.

     */

        private void Chart_MouseClick(object sender, MouseButtonEventArgs e)

        {

            var p = Mouse.GetPosition(_chart); //! 마우스 좌표를 차트 좌표로 변환.

            if (e.ChangedButton == MouseButton.Right)

            {

                if (!isPaend)

                {

                    _rightBtnDownPos = p;

                    var transForm = new TranslateTransform

                    {

                        X = p.X,

                        Y = p.Y

                    };

                    mousePosLabel.RenderTransform = transForm;



                    radialMenu.ShowPopup(mousePosLabel);

                    return;

                }

                isPaend = false;

            }





            if (e.ChangedButton != MouseButton.Left) { return; }

            var distance = Math.Sqrt(Math.Pow(p.X - mouseDownPoint.X, 2) + Math.Pow(p.Y - mouseDownPoint.Y, 2));

            if (distance < 5)

            {

                var cursor = _trackingCurosr;

                if (cursor == null) { return; }



                var bestLineSeries = LightningChartMathUtils.FindSeriesUnderMouse(_chart, p, out SeriesPoint bestPoint);

                if (bestLineSeries == null) { return; }



                var annot = CreateAnnotationXY(AnnotationType.UserMemo, bestLineSeries.AssignYAxisIndex);

                var sb = new StringBuilder();



                _chart.BeginUpdate(); //! 차트 업데이트 시작



                for (var index = 0; index < _chart.ViewXY.FreeformPointLineSeries.Count(); index++)

                {

                    // Annotation StringBuilder 할당.

                    var series = _chart.ViewXY.FreeformPointLineSeries.ElementAtOrDefault(index);

                    var targetPoint = LightningChartMathUtils.FindNearestPointByX_Binary(series.Points, series.PointCount, cursor.ValueAtXAxis); // X값 기반 이진탐색 진행.



                    sb.AppendLine($"{series.Title.Text}: {GetAxisDipslayValue(targetPoint.Y, false)}");



                    // EventMarker 할당.

                    var addMarker = CreateEventMarker(MarkerType.CursorTooltip);

                    addMarker.XValue = targetPoint.X;

                    addMarker.YValue = targetPoint.Y;

                    addMarker.Tag = annot;

                    addMarker.Visible = true;

                    series.SeriesEventMarkers.Add(addMarker);

                }

                sb.Append($"\n시간 : {cursor.ValueAtXAxis}");

                SetAnnotation(annot, bestPoint.X, bestPoint.Y);

                annot.Text = sb.ToString();

                annot.AssignYAxisIndex = annot.AssignYAxisIndex;

                annot.Visible = true;

                _chart.ViewXY.Annotations.Add(annot);



                //Add cursor.

                var lineCursor = new LineSeriesCursor(_chart.ViewXY, _chart.ViewXY.XAxes[0])

                {

                    Style = CursorStyle.PointTracking,

                    Tag = annot,

                };

                lineCursor.LineStyle.Pattern = LinePattern.Dash;

                lineCursor.LineStyle.Width = 1;

                lineCursor.LineStyle.Color = _trackingCurosr.LineStyle.Color;

                lineCursor.MouseInteraction = false;

                lineCursor.SnapToPoints = true;

                lineCursor.ValueAtXAxis = bestPoint.X;

                _chart.ViewXY.LineSeriesCursors.Add(lineCursor);



                _chart.EndUpdate(); //! 차트 업데이트 종료

            }

            else

            {

                ;

            }

        }



        /**

         * @brief 차트 마우스 Leave 이벤트 처리.

         *

         * @param   sender 차트.

         * @param   e      이벤트 Args.

      */

        private void Chart_MouseLeave(object sender, MouseEventArgs e)

        {

            _chart.BeginUpdate(); //! 차트 업데이트 시작



            // 추적 어노테이션 비활성화.

            foreach (var annotation in _trackingAnnotations)

            {

                annotation.Visible = false;

            }



            // 추적 라인커서 비활성화.

            _trackingCurosr.Visible = false;



            // 추적 마커 비활성화.

            foreach (var series in _chart.ViewXY.FreeformPointLineSeries)

            {

                var filteredMarkers = series.SeriesEventMarkers.Where(m => (m.Tag is MarkerType type) && (type != MarkerType.PlaybackCursor));

                foreach (var marker in filteredMarkers)

                {

                    marker.Visible = false;

                }

            }

            _chart.EndUpdate(); //! 차트 업데이트 종료

        }



        /**

           * @brief 차트 마우스 이동 이벤트 처리.

           *

           * @param   sender 차트.

           * @param   e      이벤트 Args.

        */

        private void Chart_MouseMove(object sender, MouseEventArgs e)

        {

            if (_chart.ViewXY.FreeformPointLineSeries.Count == 0) { return; }



            var p = Mouse.GetPosition(_chart); // 마우스 좌표를 차트 좌표로 변환.



            var bestLineSeries = LightningChartMathUtils.FindSeriesUnderMouse(_chart, p, out SeriesPoint bestPoint);

            if (bestLineSeries == null) { return; }



            _chart.BeginUpdate(); //! 차트 업데이트 시작

            _trackingCurosr.Visible = true;

            _trackingCurosr.ValueAtXAxis = bestPoint.X;

            _chart.EndUpdate(); //! 차트 업데이트 종료

        }



        /// <summary>

        /// RadialContextMenu - 기본크기 클릭 Action.

        /// </summary>

        private void ApplyDefaultSize_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            if (_chartRangeDataMap.Count == 0) { return; }



            _chart.ViewXY.XAxes[0].SetRange(_chartRangeDataMap.FirstOrDefault().Value.X_Min, _chartRangeDataMap.FirstOrDefault().Value.X_Max);

            // SubPlot은 YAxes 갯수만큼 SetRange 진행.

            foreach (var kvp in _chartRangeDataMap)

            {

                _chart.ViewXY.YAxes[kvp.Key].SetRange(kvp.Value.Y_Min, kvp.Value.Y_Max);

            }

        }



        /// <summary>

        /// RadialContextMenu - 마커 초기화 클릭 Action.

        /// </summary>

        private void ResetMarkers_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            //txtXDelta.Visibility = Visibility.Collapsed;

            //txtYDelta.Visibility = Visibility.Collapsed;



            _chart.BeginUpdate();



            foreach (var item in _chart.ViewXY.FreeformPointLineSeries[0].SeriesEventMarkers)  //! 기존 선택된 Event Marker 삭제

            {

                item.Dispose();

            }



            for (var lastIndex = _chart.ViewXY.Annotations.Count() - 1; lastIndex >= 0; lastIndex--)

            {

                var annotation = _chart.ViewXY.Annotations.ElementAt(lastIndex);

                if ((annotation.Tag is AnnotationType type) && (type != AnnotationType.SelectionPin)) { continue; } // SelectionPin Annotation만 삭제.



                _chart.ViewXY.Annotations.Remove(annotation);

            }



            _chart.ViewXY.FreeformPointLineSeries[0].SeriesEventMarkers.Clear();

            mouseMoveMarker?.Dispose();

            mouseMoveMarker = null;



            _chart.EndUpdate();

            ResetPlaybackState();

        }



        /// <summary>

        /// RadialContextMenu - 사용자메모 초기화 클릭 Action.

        /// </summary>

        private void ResetUserMemo_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            _chart.BeginUpdate();



            for (var lastIndex = _chart.ViewXY.Annotations.Count() - 1; lastIndex >= 0; lastIndex--)

            {

                var annotation = _chart.ViewXY.Annotations.ElementAt(lastIndex);

                if ((annotation.Tag is AnnotationType type) && (type != AnnotationType.UserMemo)) { continue; } // SelectionPin UserMemo만 삭제.



                RemoveTaggedDependencies(annotation);

                _chart.ViewXY.Annotations.Remove(annotation);

            }



            _chart.EndUpdate();

        }



        /// <summary>

        /// RadialContextMenu - Y축 범위 설정 클릭 Action.

        /// </summary>

        private void SetAxisYRange_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            var yAxisInfos = new List<YAxisInfo>();

            foreach (var lineSeries in _chart.ViewXY.FreeformPointLineSeries.ToList())

            {

                var yaxisIndex = lineSeries.AssignYAxisIndex;

                var mappedYAxis = _chart.ViewXY.YAxes[yaxisIndex];

                var yAxisInfo = new YAxisInfo(yaxisIndex, lineSeries.Title.Text, mappedYAxis.Minimum, mappedYAxis.Maximum);

                yAxisInfos.Add(yAxisInfo);

            }



            if (yAxisInfos.Count == 0) { return; }



            var viewModel = new YAxisRangeSettingWindowViewModel(yAxisInfos);

            if (_dialogService.ShowDialog("Y축 전시 범위 설정", viewModel) == false) { return; }



            _chart.BeginUpdate();



            if (viewModel.Rows == null) { return; }

            foreach (var row in viewModel.Rows)

            {

                var mappedYAxis = _chart.ViewXY.YAxes[row.Index];

                mappedYAxis.SetRange(row.YMin, row.YMax);

            }

            viewModel.Dispose(); // 명시적 Dispose 호출.



            _chart.EndUpdate();

        }



        /// <summary>

        /// RadialContextMenu - 사용자메모 추가 클릭 Action.

        /// </summary>

        private void AddUserMemo_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            if (_rightBtnDownPos == default) { return; }

            if (_chart.ViewXY.FreeformPointLineSeries[0].SolveNearestDataPointByCoord((int)_rightBtnDownPos.X, (int)_rightBtnDownPos.Y, out double xValue, out double yValue, out int nearestIndex))

            {

                _chart.BeginUpdate();



                var usermemoAnnotation = CreateAnnotationXY(AnnotationType.UserMemo);

                usermemoAnnotation.Text = $"X={xValue}\nY={Math.Round(yValue, 6)}";



                SetAnnotation(usermemoAnnotation, xValue, yValue);

                _chart.ViewXY.Annotations.Add(usermemoAnnotation);



                _chart.EndUpdate();

            }

        }



        /// <summary>

        /// 사용자 메모 마우스 클릭 Action.

        /// </summary>

        private void Memo_MouseDown(object sender, MouseEventArgs e)

        {

            if (!(sender is AnnotationXY memoAnnotation)) { return; }



            if (e.RightButton != MouseButtonState.Pressed) { return; }

            if (!isPaend)

            {

                if (this.Resources["MyCustomMenu"] is ContextMenu menu)

                {

                    if (!(menu.Items[0] is MenuItem itemEdit)) { return; }

                    if (!(menu.Items[2] is MenuItem itemDelete)) { return; }

                    itemEdit.Click += (s, args) =>

                    {

                        _editingAnnotationXY = memoAnnotation;



                        // 1.Close Menu.

                        menu.IsOpen = false;



                        var originalText = _editingAnnotationXY.Text;

                        var screenPoint = menu.PointToScreen(new Point(0, 0));



                        _editorWindow.OpenAt(originalText, screenPoint.X + 10, screenPoint.Y + 10);

                    };



                    itemDelete.Click += (s, args) =>

                    {

                        RemoveTaggedDependencies(memoAnnotation);

                        _chart.ViewXY.Annotations.Remove(memoAnnotation);

                    };



                    menu.IsOpen = true;

                }

            }

            isPaend = false;



            e.Handled = true; // 차트의 기본 우클릭 동작 차단.

        }

        #endregion



        private void CreateChart(string axisXTitle, string axisYTitle)

        {

            _chart = new LightningChartUltimate("Network Customizing Technologies Inc/Eom Yong-Seob-Renewed/LightningChartUltimate/DQ3CJZYJFYJFYS422FNEWBU2EV2ZMH4Y65AU");

            _chart.BeginUpdate();



            _chart.Title.MouseInteraction = false;

            _chart.Title.Font.Size = 32;

            _chart.Title.Border.Style = BorderType.None;

            _chart.Title.Visible = false;

            _chart.ViewXY.DropOldSeriesData = false; //! 확대시 데이터 사라짐 방지.



            // Y축 영역 배경색 띠 활성화.

            _chart.ViewXY.AxisLayout.AxisGridStrips = XYAxisGridStrips.Y;



            _chart.ViewXY.XAxes[0].LabelsColor = Colors.Black;

            _chart.ViewXY.XAxes[0].MouseInteraction = false;

            _chart.ViewXY.XAxes[0].Title.MouseInteraction = false;

            _chart.ViewXY.XAxes[0].Title.Text = axisXTitle;

            _chart.ViewXY.XAxes[0].LabelsNumberFormat = "G";

            _chart.ViewXY.XAxes[0].AutoFormatLabels = false;

            _chart.ViewXY.XAxes[0].ScrollMode = XAxisScrollMode.None;

            _chart.ViewXY.XAxes[0].MouseScaling = false;

            _chart.ViewXY.XAxes[0].ScaleType = ScaleType.Linear;

            _chart.ViewXY.XAxes[0].ValueType = AxisValueType.Number;



            _chart.ViewXY.YAxes[0].LabelsColor = Colors.Black;

            _chart.ViewXY.YAxes[0].MouseInteraction = false;

            _chart.ViewXY.YAxes[0].Title.MouseInteraction = false;

            _chart.ViewXY.YAxes[0].Title.Text = axisYTitle;

            _chart.ViewXY.YAxes[0].LabelsNumberFormat = "G";

            _chart.ViewXY.YAxes[0].AutoFormatLabels = false;

            _chart.ViewXY.YAxes[0].MouseScaling = false;

            _chart.ViewXY.YAxes[0].ScaleType = ScaleType.Linear;

            _chart.ViewXY.YAxes[0].LabelsNumberFormat = "0.00";



            _chart.ViewXY.LegendBox.Visible = true;

            _chart.ViewXY.LegendBox.SeriesTitleMouseMoveOverOn += LegendBox_SeriesTitleMouseMoveOverOn;

            _chart.ViewXY.LegendBox.SeriesTitleMouseMoveOverOff += LegendBox_SeriesTitleMouseMoveOverOff;

            _chart.ViewXY.LegendBox.Position = LegendBoxPosition.TopRight;

            _chart.ViewXY.LegendBox.Layout = LegendBoxLayout.Vertical;

            _chart.ViewXY.LegendBox.Position = LegendBoxPosition.TopRight;

            _chart.ViewXY.LegendBox.Offset.SetValues(-45, 10);

            _chart.ViewXY.LegendBox.AllowMouseResize = false;

            _chart.ViewXY.LegendBox.MoveFromSeriesTitle = false;



            _chart.ViewXY.ZoomPanOptions.AxisMouseWheelAction = AxisMouseWheelAction.None;

            _chart.ViewXY.ZoomPanOptions.RightMouseButtonAction = MouseButtonAction.Pan;

            _chart.ViewXY.ZoomPanOptions.LeftMouseButtonAction = MouseButtonAction.None;

            _chart.ViewXY.ZoomPanOptions.MouseWheelZooming = MouseWheelZooming.Horizontal;

            _chart.ViewXY.ZoomPanOptions.RightToLeftZoomAction = RightToLeftZoomActionXY.Off;

            _chart.ViewXY.ZoomPanOptions.PanDirection = PanDirection.Horizontal; // Pan 이동시 가로축만 허용.



            // X-Shared SubPlot 설정.

            //_chart.ViewXY.AxisLayout.SegmentsGap = 30;

            //_chart.ViewXY.AxisLayout.AutoShrinkSegmentsGap = true;

            //_chart.ViewXY.AxisLayout.AxisGridStrips = XYAxisGridStrips.Y;



            _chart.ViewXY.AxisLayout.YAxesLayout = YAxesLayout.Stacked;

            _chart.ViewXY.AxisLayout.YAxisAutoPlacement = YAxisAutoPlacement.AllLeft;

            _chart.ViewXY.AxisLayout.AutoAdjustAxisGap = 0;



            _chart.ViewSmith.LegendBox.Tag = 10;

            _chart.ActiveView = ActiveView.ViewXY;



            _chart.ChartRenderOptions.InvokeRenderingInUIThread = false; // 렌더링 성능개선을 위한 BackGround Thread 사용 활성화 옵션.

            _chart.ChartRenderOptions.DeviceType = RendererDeviceType.HardwareOnlyD11; // 렌더링 성능개선을 위한 GPU 활성화 옵션.



            var themeIndex = ChartThemeComboItem.GetThemaIndex(AppConst.DEFAULT_CHART_THEME);



            LightningChartThemeUtil.ChartThemeSelection(_chart, themeIndex);

            LightningChartThemeUtil.ChartLegendTheme(_chart, themeIndex);



            //var annot = CreateAnnotationXY(AnnotationType.CursorTooltip);

            //_chart.ViewXY.Annotations.Add(annot);



            _chart.ViewXY.ZoomPanOptions.CtrlEnabled = true;

            _chart.ViewXY.ZoomPanOptions.ShiftEnabled = true;



            _chart.ViewXY.BeforePanning += ViewXY_Panned;

            _chart.MouseDown += Chart_MouseDown;

            _chart.MouseMove += new MouseEventHandler(Chart_MouseMove);

            _chart.MouseLeave += Chart_MouseLeave;

            _chart.MouseClick += Chart_MouseClick;



            //Add cursor.

            _trackingCurosr = new LineSeriesCursor(_chart.ViewXY, _chart.ViewXY.XAxes[0])

            {

                Style = CursorStyle.VerticalNoTracking

            };

            _trackingCurosr.LineStyle.Color = Color.FromArgb(150, 255, 0, 0);

            _trackingCurosr.MouseInteraction = false;

            _trackingCurosr.SnapToPoints = true;

            _trackingCurosr.PositionChanged += Cursor_PositionChanged;



            _chart.ViewXY.LineSeriesCursors.Add(_trackingCurosr);

            _chart.EndUpdate();



            gridChart.Children.Add(_chart);

        }



        /**

         * @brief delta annotation 반환

         *

         * @returns delta annotation 객체

        */

        private AnnotationXY GetDeltaAnnotation()

        {

            var annot = default(AnnotationXY);

            if (_trackingAnnotations.Count() < 3)  //! delta annotation이 3개 미만이면 생성

            {

                annot = CreateAnnotationXY(AnnotationType.SelectionPin);

                _chart.ViewXY.Annotations.Add(annot);

            }

            else

            {

                if (isFirst)  //! delta annotation이 3개이상이면 순차적으로 반환

                {

                    annot = _trackingAnnotations.ElementAt(1);

                }

                else

                {

                    annot = _trackingAnnotations.ElementAt(2);

                }

            }

            return annot;

        }



        /**

        * @brief Delta Text 전시.

        *

        * @param   전시 축.

       */

        private void SetDeltaText()

        {

            //if (_trackingAnnotations.Count() == 3)

            //{

            //    // todo(min) : 차트 종류별 차이값 전시 로직 적용.

            //    switch (_chartViewType)

            //    {

            //        case ChartViewType.TwoD_LL:

            //            break;



            //        case ChartViewType.TwoD_XY:

            //            break;



            //        case ChartViewType.TwoD_Time:

            //            break;



            //        case ChartViewType.TwoD_Time_Multi:

            //            break;



            //        default:

            //            throw new ArgumentOutOfRangeException(nameof(ChartViewType));

            //    }



            //    var value1 = _trackingAnnotations.ElementAt(1).TargetAxisValues;

            //    var values2 = _trackingAnnotations.ElementAt(2).TargetAxisValues;

            //    var diffXValue = Math.Abs(value1.X - values2.X);

            //    var diffYValue = Math.Abs(value1.Y - values2.Y);



            //    txtXDelta.Text = $"∆X = {Math.Round(diffXValue, 6)}";

            //    txtYDelta.Text = $"∆Y = {Math.Round(diffYValue, 6)}";



            //    txtXDelta.Visibility = Visibility.Visible;

            //    txtYDelta.Visibility = Visibility.Visible;

            //}

            //else

            //{

            //    txtXDelta.Visibility = Visibility.Collapsed;

            //    txtYDelta.Visibility = Visibility.Collapsed;

            //}

        }



        /**

         * @brief delta event marker 반환

         *

         * @returns delta event marker 객체

        */

        private SeriesEventMarker GetDeltaEventMarker()

        {

            var marker = default(SeriesEventMarker);



            if (_chart.ViewXY.FreeformPointLineSeries[0].SeriesEventMarkers.Count < 3) //! delta event marker가 3개 미만이면 생성

            {

                marker = CreateEventMarker(MarkerType.SelectionPin);

                _chart.ViewXY.FreeformPointLineSeries[0].SeriesEventMarkers.Add(marker);

            }

            else

            {

                if (isFirst)  //! delta aevent marker가 3개이상이면 순차적으로 반환

                {

                    marker = _chart.ViewXY.FreeformPointLineSeries[0].SeriesEventMarkers[1];

                }

                else

                {

                    marker = _chart.ViewXY.FreeformPointLineSeries[0].SeriesEventMarkers[2];

                }

            }



            return marker;

        }



        /**

           * @brief Annotation 생성.

           *

           * @returns 생성된 Annotation.

        */

        private AnnotationXY CreateAnnotationXY(AnnotationType annotationType, int yAxesIndex = 0)

        {

            var annot = (AnnotationXY)default;

            switch (annotationType)

            {

                case AnnotationType.CursorTooltip:

                case AnnotationType.SelectionPin:

                    annot = new AnnotationXY(_chart.ViewXY, _chart.ViewXY.XAxes[0], _chart.ViewXY.YAxes[0]) //! Annotation 객체 생성

                    {

                        TargetCoordinateSystem = AnnotationTargetCoordinates.AxisValues,

                        Visible = false,

                        MouseInteraction = false,

                        LocationCoordinateSystem = CoordinateSystem.RelativeCoordinatesToTarget,

                        Tag = annotationType,

                    };



                    annot.Fill.Color = Color.FromArgb(180, 30, 30, 30); //! 생성된 Annotation 객체 초기값 설정

                    annot.Fill.GradientFill = GradientFill.Solid;

                    annot.BorderLineStyle.Color = Color.FromArgb(50, 255, 255, 255);

                    annot.Shadow.Visible = false;

                    annot.LocationRelativeOffset.X = 0;

                    annot.LocationRelativeOffset.Y = -40;

                    annot.TextStyle.Color = Colors.White;

                    annot.Style = AnnotationStyle.Rectangle;



                    return annot;



                case AnnotationType.UserMemo:

                    annot = new AnnotationXY(_chart.ViewXY, _chart.ViewXY.XAxes[0], _chart.ViewXY.YAxes[yAxesIndex]) //! Annotation 객체 생성

                    {

                        Tag = AnnotationType.UserMemo,

                        Style = AnnotationStyle.Callout,

                        LocationCoordinateSystem = CoordinateSystem.RelativeCoordinatesToTarget,

                        TargetCoordinateSystem = AnnotationTargetCoordinates.AxisValues,

                        MouseInteraction = true,

                        MoveByMouse = true,

                    };



                    annot.Fill.Color = Color.FromArgb(200, 255, 255, 200);

                    annot.Fill.Style = RectFillStyle.ColorOnly;

                    annot.Shadow.Visible = false;

                    annot.LocationRelativeOffset.X = 0;

                    annot.LocationRelativeOffset.Y = -40;

                    annot.MouseDown += Memo_MouseDown;

                    annot.MovedByMouse += (s, _) => { _.Handled = true; };

                    return annot;



                default:

                    throw new ArgumentOutOfRangeException(nameof(annotationType));

            }



        }



        /**

          * @brief Event Marker 생성.

          *

          * @returns 생성된 Event Marker.

         */

        private SeriesEventMarker CreateEventMarker(MarkerType markerType)

        {

            var marker = new SeriesEventMarker(); //! Event Marker 객체 생성

            marker.Tag = markerType;

            marker.Symbol.BorderColor = Colors.DarkGreen; //! Event Marker 객체 초기값 설정

            marker.Symbol.Shape = Arction.Wpf.Charting.Shape.Rectangle;

            marker.Symbol.BorderWidth = 3F;

            marker.Symbol.GradientFill = GradientFillPoint.Solid;

            marker.Symbol.Color1 = marker.Symbol.Color2 = marker.Symbol.Color3 = Colors.Transparent;

            marker.MouseInteraction = false;

            marker.Label.Visible = false;

            marker.Visible = false;

            return marker;

        }



        /**

         * @brief Annotation 설정

         *

         * @param   annot annotation 객체.

         * @param   xVal  x 값.

         * @param   yVal  y 값.

        */

        private void SetAnnotation(AnnotationXY annot, double xVal, double yVal)

        {

            annot.TargetAxisValues.X = xVal; //! Annotation X값 설정

            annot.TargetAxisValues.Y = yVal; //! Annotation Y값 설정



            var middle = (_chart.ViewXY.YAxes[annot.AssignYAxisIndex].Maximum + _chart.ViewXY.YAxes[annot.AssignYAxisIndex].Minimum) / 2; //! Y축 중앙값 계산

            if (yVal > middle)  //! 중앙값 여부에 따른 전시 위치 설정

            {

                annot.Anchor.X = 0.5f;

                annot.Anchor.Y = -1.0f;

            }

            else

            {

                annot.Anchor.X = 0.5f;

                annot.Anchor.Y = 0.5f;

            }

        }



        /**

         * @brief 축 전시 값 반환

         *

         * @param   value   값.

         * @param   isXAxis X축 여부

         *

         * @returns 전시 값

        */

        private string GetAxisDipslayValue(double value, bool isXAxis)

        {

            string result;

            if (isXAxis)

            {

                result = $"X={value}";  //! X축 전시 값 생성

            }

            else

            {

                result = $"Y={value}"; //! Y축 전시 값 생성

            }

            return result;



        }



        private void Cursor_PositionChanged(object sender, Arction.Wpf.Charting.Views.ViewXY.PositionChangedEventArgs e)

        {

            e.CancelRendering = true;



            if (!(sender is LineSeriesCursor cursor)) { return; }



            _chart.BeginUpdate();



            for (var index = 0; index < _chart.ViewXY.FreeformPointLineSeries.Count(); index++)

            {

                var series = _chart.ViewXY.FreeformPointLineSeries.ElementAtOrDefault(index);

                var filterdMarkers = series.SeriesEventMarkers.Where(m => (m.Tag is MarkerType type) && (type != MarkerType.PlaybackCursor)).ToList();

                var marker = filterdMarkers.FirstOrDefault();

                var annot = _trackingAnnotations.ElementAt(index);

                if (marker == null)

                {

                    marker = CreateEventMarker(MarkerType.CursorTooltip);

                    series.SeriesEventMarkers.Add(marker);

                }



                if ((series.PointCount == 0) || (!series.Visible))

                {

                    annot.Visible = false;

                    marker.Visible = false;

                    continue;

                }



                var targetPoint = LightningChartMathUtils.FindNearestPointByX_Binary(series.Points, series.PointCount, cursor.ValueAtXAxis); // X값 기반 이진탐색 진행.



                marker.XValue = targetPoint.X;

                marker.YValue = targetPoint.Y;

                marker.Visible = true;



                annot.AssignYAxisIndex = index;

                SetAnnotation(annot, targetPoint.X, targetPoint.Y);

                annot.Text = $"{GetAxisDipslayValue(targetPoint.X, true)}\n{GetAxisDipslayValue(targetPoint.Y, false)}";   //! 마커 정보 전시

                annot.Visible = true;

            }



            _chart.EndUpdate();

        }



        private void EditorBox_PreviewKeyDown(object sender, KeyEventArgs e)

        {

            if (e.Key == Key.Enter)

            {

                if (Keyboard.Modifiers != ModifierKeys.Shift)

                {

                    e.Handled = true;



                    // 저장 동작.

                    if (_editingAnnotationXY != null)

                    {

                        _editingAnnotationXY.Text = EditorTextBox.Text;

                    }



                    EditorPopup.IsOpen = false; // 팝업 닫기;

                }

            }

            else if (e.Key == Key.Escape)

            {

                // Cancel

                EditorPopup.IsOpen = false;

                e.Handled = true;

            }

        }



        private void OnEditorCancelled()

        {

            // 필요 시 원본 텍스트로 복원

            // _editingAnnotationXY.Text = _originalText;

        }



        private void OnEditorSaved(string newText)

        {

            // 저장 동작.

            if (_editingAnnotationXY != null)

            {

                _editingAnnotationXY.Text = newText;

            }

        }



        /// <summary>

        /// 어노테이션에 Tag로 설정된 마커, 시리즈커서를 삭제한다.

        /// </summary>

        /// <param name="annotation"></param>

        private void RemoveTaggedDependencies(AnnotationXY annotation)

        {

            bool matchCondition(object tag) => tag != null && tag.Equals(annotation);



            // 어노테이션에 종속된 마커 삭제.

            for (var lastSeriesIndex = _chart.ViewXY.FreeformPointLineSeries.Count() - 1; lastSeriesIndex >= 0; lastSeriesIndex--)

            {

                var lineSeries = _chart.ViewXY.FreeformPointLineSeries.ElementAt(lastSeriesIndex);

                for (var lastMarkerIndex = lineSeries.SeriesEventMarkers.Count() - 1; lastMarkerIndex >= 0; lastMarkerIndex--)

                {

                    var marker = lineSeries.SeriesEventMarkers.ElementAt(lastMarkerIndex);

                    if (!matchCondition(marker.Tag)) { continue; }



                    lineSeries.SeriesEventMarkers.Remove(marker);

                }

            }



            // 어노테이션에 종속된 시리즈 커서 삭제.

            for (var lastCursorIndex = _chart.ViewXY.LineSeriesCursors.Count() - 1; lastCursorIndex >= 0; lastCursorIndex--)

            {

                var cursorSeries = _chart.ViewXY.LineSeriesCursors.ElementAt(lastCursorIndex);

                if (!matchCondition(cursorSeries.Tag)) { continue; }



                _chart.ViewXY.LineSeriesCursors.Remove(cursorSeries);

            }

        }

        private void EnsurePlaybackMarkerPool(int seriesIndex, int requiredCount, Color color)

        {

            if (!_playbackMarkerPool.TryGetValue(seriesIndex, out var markers))

            {

                markers = new List<SeriesEventMarker>();

                _playbackMarkerPool[seriesIndex] = markers;

            }



            while (markers.Count < requiredCount)

            {

                var marker = CreateEventMarker(MarkerType.PlaybackCursor);

                marker.Symbol.BorderColor = color;

                marker.Symbol.Color1 = marker.Symbol.Color2 = marker.Symbol.Color3 = color;

                _chart.ViewXY.FreeformPointLineSeries[seriesIndex].SeriesEventMarkers.Add(marker);

                markers.Add(marker);

            }



            if (!_playbackSnapshots.TryGetValue(seriesIndex, out var snapshots))

            {

                snapshots = Array.Empty<PlaybackCursorSnapshot>();

            }



            if (snapshots.Length >= markers.Count) { return; }



            var expanded = new PlaybackCursorSnapshot[markers.Count];

            Array.Copy(snapshots, expanded, snapshots.Length);

            _playbackSnapshots[seriesIndex] = expanded;

        }

        private PlaybackCursorSnapshot[] CreateHiddenPlaybackSnapshots(int count)

        {

            var snapshots = new PlaybackCursorSnapshot[count];

            for (var i = 0; i < count; i++)

            {

                snapshots[i] = new PlaybackCursorSnapshot() { Visible = false };

            }



            return snapshots;

        }

        private void ResetPlaybackState()

        {

            _playbackMarkerPool.Clear();

            _playbackSnapshots.Clear();

        }

    }

}
