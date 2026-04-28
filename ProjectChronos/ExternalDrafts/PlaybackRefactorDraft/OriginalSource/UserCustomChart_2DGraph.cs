using Arction.Wpf.Charting;

using Arction.Wpf.Charting.Annotations;

using Arction.Wpf.Charting.EventMarkers;

using Arction.Wpf.Charting.SeriesXY;

using Arction.Wpf.Charting.Views.ViewXY;

using DevExpress.CodeParser;

using DevExpress.Mvvm.Native;

using DevExpress.Utils.Filtering;

using DevExpress.XtraSpreadsheet.Model;

using OSTES.Common;

using OSTES.Data;

using OSTES.Interface;

using OSTES.Utils;

using OSTES.ViewModel.Dialog;

using System;

using System.Collections.Generic;

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

using System.Windows.Interop;

using System.Windows.Media;

using System.Windows.Media.Imaging;

using System.Windows.Navigation;

using System.Windows.Shapes;

using ChartViewType = OSTES.Common.ChartViewType;

using Geometry = OSTES.Utils.Geometry;

using SeriesPoint = Arction.Wpf.Charting.SeriesPoint;



namespace OSTES.Chart

{

    /// <summary>

    /// UserCustomChart_2DGraph.xaml에 대한 상호 작용 논리

    /// </summary>

    public partial class UserCustomChart_2DGraph : UserControl, IUserCustomChart, IChartUIControl, IChartPlayback, IDisposable

    {

        /** @brief 반올림 자릿수 */

        private const int _roundingDigits = 6;



        /** @brief 해당 인스턴스에 설정된 차트 타입(Diff 계산에 사용됨). */

        private readonly ChartViewType _chartViewType;



        /** @brief PlaybackCursor 마커 캐시 */

        private readonly Dictionary<int, List<SeriesEventMarker>> _playbackMarkerPool = new Dictionary<int, List<SeriesEventMarker>>();



        /** @brief PlaybackCursor 렌더 상태 캐시 */

        private readonly Dictionary<int, PlaybackCursorSnapshot[]> _playbackSnapshots = new Dictionary<int, PlaybackCursorSnapshot[]>();



        /** @brief 사용자 메모에서 현재 편집중인 Annotation */

        private AnnotationXY _editingAnnotationXY;



        /** @brief Cursor Tracking에 사용된 Annotaition 반환 */

        private IEnumerable<AnnotationXY> _trackingAnnotations => _chart.ViewXY.Annotations.Where(item => (item.Tag is AnnotationType type) && ((type == AnnotationType.CursorTooltip) || (type == AnnotationType.SelectionPin)));



        private IEnumerable<SeriesEventMarker> _mouseMoveMarkers => _chart.ViewXY.FreeformPointLineSeries.SelectMany(item => item.SeriesEventMarkers.Where(m => (m.Tag is MarkerType type) && type == MarkerType.CursorTooltip));



        /** @brief 팬 여부 */

        private bool isPaend;



        /** @brief 첫번째 여부 */

        private bool isFirst;

        /** @brief Delta 계산에 사용되는 최근 선택 포인트 */

        private readonly RecentDeltaPointBuffer _recentDeltaPoints = new RecentDeltaPointBuffer();



        /** @brief 차트 전시 범위 */

        private ChartRangeData _chartRangeData;



        /** @brief  마우스 클릭 위치 */

        private Point mouseDownPoint;



        /** @brief  마우스 우클릭 위치 */

        private Point _rightBtnDownPos;



        /** @brief 마우스 포인트 마커 */

        //private SeriesEventMarker mouseMoveMarker;



        /** @brief 전시중인 차트 인스턴스 */

        private LightningChartUltimate _chart;



        /** @brief 한글 IME 정상 동작을 위한 Window. **/

        private EditorInputWindow _editorWindow;



        public UserCustomChart_2DGraph(ChartViewType chartViewType, string axisXTitle = default, string axisYTitle = default)

        {

            _chartViewType = chartViewType;



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

        public void SetRange(ChartRangeData rangeData, int seriesIndex)

        {

            if (rangeData == default) { return; }



            // 지역 변수 초기화.

            if (_chartRangeData == default)

            {

                _chartRangeData = rangeData;

            }

            else

            {

                _chartRangeData = new ChartRangeData()

                {

                    X_Min = Math.Min(_chartRangeData.X_Min, rangeData.X_Min),

                    Y_Min = Math.Min(_chartRangeData.Y_Min, rangeData.Y_Min),



                    X_Max = Math.Max(_chartRangeData.X_Max, rangeData.X_Max),

                    Y_Max = Math.Max(_chartRangeData.Y_Max, rangeData.Y_Max),

                };

            }

            _chart.ViewXY.XAxes[0].SetRange(_chartRangeData.X_Min, _chartRangeData.X_Max);

            _chart.ViewXY.YAxes[0].SetRange(_chartRangeData.Y_Min, _chartRangeData.Y_Max);

        }



        public void AddLineChartSeries(LineChartSeriesDTO lineChartSeriesDTO)

        {

            var addLineSeries = ChartCreate_2D.AddLineChart_Series(_chart, lineChartSeriesDTO.SeriesName, lineChartSeriesDTO.LineColor, lineChartSeriesDTO.LineSeriesType);

            var mouseMoveMarker = CreateEventMarker(MarkerType.CursorTooltip);

            addLineSeries.SeriesEventMarkers.Add(mouseMoveMarker);

        }





        public FreeformPointLineSeries AddLineChartSeries(string seriesName, Color color, LineSeriesType lineSeriesType)

        {

            var addLineSeries = ChartCreate_2D.AddLineChart_Series(_chart, seriesName, color, lineSeriesType);

            var mouseMoveMarker = CreateEventMarker(MarkerType.CursorTooltip);

            addLineSeries.SeriesEventMarkers.Add(mouseMoveMarker);



            return addLineSeries;

        }



        public void ClearChart()

        {

            txtXDelta.Visibility = Visibility.Collapsed;

            txtYDelta.Visibility = Visibility.Collapsed;

            txtDistance.Visibility = Visibility.Collapsed;



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



            ResetDeltaPointState();

            ResetPlaybackCursorState();

        }

        #endregion



        #region IChartExport Public API

        public bool IsValidChart => _chart.ViewXY.FreeformPointLineSeries.Count > 0;



        public string SaveChartToFile(string directiory)

        {

            var filePath = $"SnapShotChart_2D.png";

            var fullPath = System.IO.Path.Combine(directiory, filePath);



            var graphBackgroundColor = _chart.ViewXY.GraphBackground.Color;

            var graphBackgroundGradientColor = _chart.ViewXY.GraphBackground.GradientColor;

            var chartBackgroundColor = _chart.ChartBackground.Color;

            var chartBackgroundGradientColor = _chart.ChartBackground.GradientColor;

            var chartXAxesLabelColor = _chart.ViewXY.XAxes[0].LabelsColor;

            var chartYAxesLabelColor = _chart.ViewXY.YAxes[0].LabelsColor;

            var chartXAxesTitleColor = _chart.ViewXY.XAxes[0].Title.Color;

            var chartYAxesTitleColor = _chart.ViewXY.XAxes[0].Title.Color;

            var chartXAxisColor = _chart.ViewXY.XAxes[0].AxisColor;

            var chartYAxisColor = _chart.ViewXY.YAxes[0].AxisColor;



            // 차트 캡처를 위한 배경 변경.

            _chart.BeginUpdate();

            _chart.ViewXY.XAxes[0].LabelsColor = _chart.ViewXY.YAxes[0].LabelsColor = Colors.Black;

            _chart.ViewXY.XAxes[0].Title.Color = _chart.ViewXY.YAxes[0].Title.Color = Colors.Black;

            _chart.ViewXY.XAxes[0].AxisColor = _chart.ViewXY.YAxes[0].AxisColor = Colors.Black;

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

            _chart.ViewXY.YAxes[0].LabelsColor = chartYAxesLabelColor;

            _chart.ViewXY.XAxes[0].Title.Color = chartXAxesTitleColor;

            _chart.ViewXY.YAxes[0].Title.Color = chartYAxesTitleColor;

            _chart.ViewXY.XAxes[0].AxisColor = chartXAxisColor;

            _chart.ViewXY.YAxes[0].AxisColor = chartYAxisColor;

            _chart.EndUpdate();



            return fullPath;

        }



        public string GetChartMetaData()

        {

            var sb = new StringBuilder();



            // delta Text가 활성화 되었을때 AppendLine.

            if ((txtXDelta.Visibility == Visibility.Visible) && (txtYDelta.Visibility == Visibility.Visible))

            {

                var deltaXY = $"{txtXDelta.Text}, {txtYDelta.Text}, {txtDistance.Text}";

                sb.AppendLine(deltaXY);

            }



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

            LightningChartThemeUtil.ChartThemeSelection(_chart, index);

        }



        public void ChartLegendTheme(int index)

        {

            LightningChartThemeUtil.ChartLegendTheme(_chart, index);

        }

        #endregion



        #region IChartUIControl Public API

        public void ZoomToFit() => _chart.ViewXY.ZoomToFit();



        public void HideLegendBox()

        {

            if (_chart.ViewXY.LegendBox == null) { return; }



            _chart.ViewXY.LegendBox.Visible = false;

        }



        public void UpdateTrackAnnotationColorFromResource() { }



        public ViewXY GetViewXY() => _chart.ViewXY;

        #endregion



        #region IChartPlayback Public API

        public void SetPlaybackCursor()

        {

            ResetPlaybackCursorState();



            _chart.BeginUpdate();



            try

            {

                for (int seriesIndex = 0; seriesIndex < _chart.ViewXY.FreeformPointLineSeries.Count; seriesIndex++)

                {

                    var series = _chart.ViewXY.FreeformPointLineSeries[seriesIndex];

                    var existingMarkers = series.SeriesEventMarkers

                        .Where(m => (m.Tag is MarkerType type) && type == MarkerType.PlaybackCursor)

                        .ToList();



                    if (existingMarkers.Count == 0)

                    {

                        var marker = CreatePlaybackMarker(series.LineStyle.Color);

                        series.SeriesEventMarkers.Add(marker);

                        existingMarkers.Add(marker);

                    }

                    else

                    {

                        foreach (var marker in existingMarkers)

                        {

                            ApplyPlaybackMarkerStyle(marker, series.LineStyle.Color);

                            marker.Visible = false;

                        }

                    }



                    _playbackMarkerPool[seriesIndex] = existingMarkers;

                    _playbackSnapshots[seriesIndex] = CreateHiddenSnapshots(existingMarkers.Count);

                }

            }

            finally

            {

                _chart.EndUpdate();

            }

        }



        public void UpdatePlaybackCursorPosition(AddSeriesPointDTO addSeriesPointDTO)

        {

            if (addSeriesPointDTO == null)

            {

                return;

            }



            var series = _chart.ViewXY.FreeformPointLineSeries.ElementAtOrDefault(addSeriesPointDTO.SeriesIndex);

            if (series == null)

            {

                return;

            }



            var points = addSeriesPointDTO.SeriesPoint2D ?? Array.Empty<SeriesPoint>();

            EnsurePlaybackMarkerPool(addSeriesPointDTO.SeriesIndex, points.Length, series.LineStyle.Color);



            var markers = _playbackMarkerPool[addSeriesPointDTO.SeriesIndex];

            var snapshots = _playbackSnapshots[addSeriesPointDTO.SeriesIndex];



            bool hasVisualChange = false;



            for (int i = 0; i < markers.Count; i++)

            {

                bool shouldBeVisible = i < points.Length;

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



            if (!hasVisualChange)

            {

                return;

            }



            _chart.BeginUpdate();



            try

            {

                for (int i = 0; i < markers.Count; i++)

                {

                    bool shouldBeVisible = i < points.Length;

                    var marker = markers[i];



                    if (!shouldBeVisible)

                    {

                        marker.Visible = false;

                        snapshots[i] = new PlaybackCursorSnapshot { Visible = false };

                        continue;

                    }



                    var point = points[i];

                    marker.XValue = point.X;

                    marker.YValue = point.Y;

                    marker.Visible = true;



                    snapshots[i] = new PlaybackCursorSnapshot

                    {

                        X = point.X,

                        Y = point.Y,

                        Visible = true

                    };

                }

            }

            finally

            {

                _chart.EndUpdate();

            }

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



            ResetDeltaPointState();

            ResetPlaybackCursorState();

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

                _chart.BeginUpdate(); //! 차트 업데이트 시작



                var annot = default(AnnotationXY);

                var marker = default(SeriesEventMarker);



                var bestLineSeries = LightningChartMathUtils.FindSeriesUnderMouse(_chart, p, out SeriesPoint bestPoint);

                if (bestLineSeries == null)
                {
                    _chart.EndUpdate();
                    return;
                }



                if (bestLineSeries.SolveNearestDataPointByCoord((int)p.X, (int)p.Y, out double xValue, out double yValue, out int nearestIndex))

                {

                    var isDel = false;



                    annot = GetDeltaAnnotation();

                    isFirst = !isFirst;



                    SetAnnotation(annot, xValue, yValue);

                    annot.Text = $"{GetAxisDipslayValue(xValue, true)}\n{GetAxisDipslayValue(yValue, false)} ";

                    //annot.Visible = true;



                    if (!isDel)

                    {

                        marker = GetDeltaEventMarker(bestLineSeries);

                        marker.XValue = xValue;

                        marker.YValue = yValue;

                        marker.Visible = true;

                        _recentDeltaPoints.Push(xValue, yValue);

                    }

                }



                _chart.EndUpdate(); //! 차트 업데이트 종료

                SetDeltaText();  //! Delta Text 업데이트

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

            if (_mouseMoveMarkers.Count() > 0)

            {

                _chart.BeginUpdate(); //! 차트 업데이트 시작



                var annot = _trackingAnnotations.ElementAt(0);



                annot.Visible = false;



                _mouseMoveMarkers.ForEach(m => m.Visible = false);

                _chart.EndUpdate(); //! 차트 업데이트 종료

            }

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



            _mouseMoveMarkers.ForEach(m => m.Visible = false);



            var p = Mouse.GetPosition(_chart); // 마우스 좌표를 차트 좌표로 변환.



            var bestLineSeries = LightningChartMathUtils.FindSeriesUnderMouse(_chart, p, out SeriesPoint bestPoint);

            if (bestLineSeries == null) { return; }



            if (bestLineSeries.SolveNearestDataPointByCoord((int)p.X, (int)p.Y, out double xValue, out double yValue, out int nearestIndex))

            {

                _chart.BeginUpdate(); //! 차트 업데이트 시작



                var annot = _trackingAnnotations.ElementAt(0);

                SetAnnotation(annot, xValue, yValue);

                annot.Text = $"{GetAxisDipslayValue(xValue, true)}\n{GetAxisDipslayValue(yValue, false)}";   //! 마커 정보 전시

                annot.Visible = true;



                var marker = bestLineSeries.SeriesEventMarkers[0];

                marker.XValue = xValue;

                marker.YValue = yValue;

                marker.Visible = true;



                _chart.EndUpdate(); //! 차트 업데이트 종료

            }

        }



        /// <summary>

        /// RadialContextMenu - 기본크기 클릭 Action.

        /// </summary>

        private void ApplyDefaultSize_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            if (_chartRangeData == default) { return; }



            _chart.ViewXY.XAxes[0].SetRange(_chartRangeData.X_Min, _chartRangeData.X_Max);

            _chart.ViewXY.YAxes[0].SetRange(_chartRangeData.Y_Min, _chartRangeData.Y_Max);

        }



        /// <summary>

        /// RadialContextMenu - 마커 초기화 클릭 Action.

        /// </summary>

        private void ResetMarkers_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            txtXDelta.Visibility = Visibility.Collapsed;

            txtYDelta.Visibility = Visibility.Collapsed;

            txtDistance.Visibility = Visibility.Collapsed;



            _chart.BeginUpdate();



            foreach (var series in _chart.ViewXY.FreeformPointLineSeries)  //! 기존 선택된 Event Marker 삭제

            {

                for (var lastIndex = series.SeriesEventMarkers.Count() - 1; lastIndex >= 0; lastIndex--)

                {

                    var marker = series.SeriesEventMarkers[lastIndex];

                    if ((marker.Tag is MarkerType type) && (type == MarkerType.CursorTooltip)) // Cursor Marker 초기화 X.

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

                if ((annotation.Tag is AnnotationType type) && (type != AnnotationType.SelectionPin)) { continue; } // SelectionPin Annotation만 삭제.



                _chart.ViewXY.Annotations.Remove(annotation);

            }



            _chart.EndUpdate();



            ResetDeltaPointState();

            ResetPlaybackCursorState();

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



                _chart.ViewXY.Annotations.Remove(annotation);

            }



            _chart.EndUpdate();



        }

        /// <summary>

        /// RadialContextMenu - 사용자메모 추가 클릭 Action.

        /// </summary>

        private void AddUserMemo_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            if (_rightBtnDownPos == default) { return; }



            var bestLineSeries = LightningChartMathUtils.FindSeriesUnderMouse(_chart, _rightBtnDownPos, out SeriesPoint bestPoint);

            if (bestLineSeries == null) { return; }



            if (bestLineSeries.SolveNearestDataPointByCoord((int)_rightBtnDownPos.X, (int)_rightBtnDownPos.Y, out double xValue, out double yValue, out int nearestIndex))

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



            _chart.ViewXY.ZoomPanOptions.AxisMouseWheelAction = AxisMouseWheelAction.None;

            _chart.ViewXY.ZoomPanOptions.RightMouseButtonAction = MouseButtonAction.Pan;

            _chart.ViewXY.ZoomPanOptions.LeftMouseButtonAction = MouseButtonAction.None;

            _chart.ViewXY.ZoomPanOptions.MouseWheelZooming = MouseWheelZooming.HorizontalAndVertical;

            _chart.ViewXY.ZoomPanOptions.RightToLeftZoomAction = RightToLeftZoomActionXY.Off;



            _chart.ViewSmith.LegendBox.Tag = 10;

            _chart.ActiveView = ActiveView.ViewXY;



            _chart.ChartRenderOptions.InvokeRenderingInUIThread = false; // 렌더링 성능개선을 위한 BackGround Thread 사용 활성화 옵션.

            _chart.ChartRenderOptions.DeviceType = RendererDeviceType.HardwareOnlyD11; // 렌더링 성능개선을 위한 GPU 활성화 옵션.



            var themeIndex = ChartThemeComboItem.GetThemaIndex(AppConst.DEFAULT_CHART_THEME);

            LightningChartThemeUtil.ChartThemeSelection(_chart, themeIndex);

            LightningChartThemeUtil.ChartLegendTheme(_chart, themeIndex);



            var annot = CreateAnnotationXY(AnnotationType.CursorTooltip);

            _chart.ViewXY.Annotations.Add(annot);



            _chart.ViewXY.ZoomPanOptions.CtrlEnabled = true;

            _chart.ViewXY.ZoomPanOptions.ShiftEnabled = true;



            _chart.ViewXY.BeforePanning += ViewXY_Panned;

            _chart.MouseDown += Chart_MouseDown;

            _chart.MouseMove += new MouseEventHandler(Chart_MouseMove);

            _chart.MouseLeave += Chart_MouseLeave;

            _chart.MouseClick += Chart_MouseClick;



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

            if (_recentDeltaPoints.HasTwoPoints)

            {

                var value1 = _recentDeltaPoints.OlderPoint;

                var value2 = _recentDeltaPoints.NewerPoint;



                switch (_chartViewType)

                {

                    case ChartViewType.TwoD_LL:

                        var value1ToVector2 = new SharpDX.Mathematics.Vector2((float)value1.X, (float)value1.Y);

                        var value2ToVector2 = new SharpDX.Mathematics.Vector2((float)value2.X, (float)value2.Y);

                        var distance = Geometry.GetDistanceWithLLA_2D(value1ToVector2, value2ToVector2, out double diffLat, out double diffLon);

                        var absLat = Math.Abs(diffLat);

                        var absLon = Math.Abs(diffLon);



                        txtXDelta.Text = $"∆Lon = {Math.Round(absLon, 6)}";

                        txtYDelta.Text = $"∆Lat = {Math.Round(absLat, 6)}";

                        txtDistance.Text = $"Dist = {distance}";



                        txtXDelta.Visibility = Visibility.Visible;

                        txtYDelta.Visibility = Visibility.Visible;

                        txtDistance.Visibility = Visibility.Visible;

                        break;



                    case ChartViewType.TwoD_XY:

                    case ChartViewType.TwoD_AxisSelectable:

                    case ChartViewType.TwoD_Time:

                    case ChartViewType.TwoD_Time_Multi:

                        var diffXValue = Math.Abs(value1.X - value2.X);

                        var diffYValue = Math.Abs(value1.Y - value2.Y);

                        var distValue = Math.Sqrt((diffXValue * diffXValue) + (diffYValue * diffYValue));



                        txtXDelta.Text = $"∆X = {Math.Round(diffXValue, 6)}";

                        txtYDelta.Text = $"∆Y = {Math.Round(diffYValue, 6)}";

                        txtDistance.Text = $"Dist = {distValue}";



                        txtXDelta.Visibility = Visibility.Visible;

                        txtYDelta.Visibility = Visibility.Visible;

                        txtDistance.Visibility = _chartViewType == ChartViewType.TwoD_XY ? Visibility.Visible : Visibility.Collapsed;

                        break;



                    default:

                        throw new ArgumentOutOfRangeException(nameof(ChartViewType));

                }

            }

            else

            {

                txtXDelta.Visibility = Visibility.Collapsed;

                txtYDelta.Visibility = Visibility.Collapsed;

                txtDistance.Visibility = Visibility.Collapsed;

            }

        }



        /**

         * @brief delta event marker 반환

         *

         * @returns delta event marker 객체

        */

        private SeriesEventMarker GetDeltaEventMarker(FreeformPointLineSeries series)

        {

            var marker = default(SeriesEventMarker);



            var diffMarkers = _chart.ViewXY.FreeformPointLineSeries.SelectMany(item => item.SeriesEventMarkers.Where(m => (m.Visible) && (m.Tag is MarkerType type) && (type == MarkerType.SelectionPin))).ToList();

            if (diffMarkers.Count() < 2) //! delta event marker가 3개 미만이면 생성

            {

                marker = CreateEventMarker(MarkerType.SelectionPin);

                series.SeriesEventMarkers.Add(marker);

            }

            else

            {

                if (isFirst)  //! delta aevent marker가 3개이상이면 순차적으로 반환

                {

                    diffMarkers.ElementAt(0).Visible = false;



                    marker = CreateEventMarker(MarkerType.SelectionPin);

                    series.SeriesEventMarkers.Add(marker);

                }

                else

                {

                    diffMarkers.ElementAt(1).Visible = false;



                    marker = CreateEventMarker(MarkerType.SelectionPin);

                    series.SeriesEventMarkers.Add(marker);

                }

            }



            return marker;

        }



        /**

           * @brief Annotation 생성.

           *

           * @returns 생성된 Annotation.

        */

        private AnnotationXY CreateAnnotationXY(AnnotationType annotationType)

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

                    annot = new AnnotationXY(_chart.ViewXY, _chart.ViewXY.XAxes[0], _chart.ViewXY.YAxes[0]) //! Annotation 객체 생성

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

            switch (markerType)

            {

                case MarkerType.CursorTooltip:

                case MarkerType.SelectionPin:

                    marker.Tag = markerType;

                    marker.Symbol.BorderColor = Colors.DarkGreen; //! Event Marker 객체 초기값 설정

                    marker.Symbol.Shape = Arction.Wpf.Charting.Shape.Rectangle;

                    marker.Symbol.BorderWidth = 3F;

                    marker.Symbol.GradientFill = GradientFillPoint.Solid;

                    marker.Symbol.Color1 = marker.Symbol.Color2 = marker.Symbol.Color3 = Colors.Transparent;

                    marker.MouseInteraction = false;

                    marker.Label.Visible = false;

                    marker.Visible = false;

                    break;



                case MarkerType.PlaybackCursor:

                    marker.Tag = markerType;

                    marker.Symbol.BorderColor = Colors.DarkGreen; //! Event Marker 객체 초기값 설정

                    marker.Symbol.Shape = Arction.Wpf.Charting.Shape.Circle;

                    marker.Symbol.BorderWidth = 5F;

                    marker.Symbol.GradientFill = GradientFillPoint.Solid;

                    marker.Symbol.Color1 = marker.Symbol.Color2 = marker.Symbol.Color3 = Colors.Transparent;

                    marker.MouseInteraction = false;

                    marker.Label.Visible = false;

                    marker.Visible = false;

                    break;



                default:

                    throw new ArgumentOutOfRangeException(nameof(markerType));

            }

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



            var middle = (_chart.ViewXY.YAxes[0].Maximum + _chart.ViewXY.YAxes[0].Minimum) / 2; //! Y축 중앙값 계산

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



        private void EditorBox_PreviewKeyDown(object sender, KeyEventArgs e)

        {

            if (e.Key == Key.Enter)

            {

                if (Keyboard.Modifiers != ModifierKeys.Shift)

                {

                    e.Handled = true;



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

        private void ResetDeltaPointState()

        {

            _recentDeltaPoints.Clear();

            isFirst = false;

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

                var marker = CreatePlaybackMarker(color);

                _chart.ViewXY.FreeformPointLineSeries[seriesIndex].SeriesEventMarkers.Add(marker);

                markers.Add(marker);

            }



            if (!_playbackSnapshots.TryGetValue(seriesIndex, out var snapshots))

            {

                snapshots = Array.Empty<PlaybackCursorSnapshot>();

            }



            if (snapshots.Length >= markers.Count)

            {

                return;

            }



            var expanded = new PlaybackCursorSnapshot[markers.Count];

            Array.Copy(snapshots, expanded, snapshots.Length);

            _playbackSnapshots[seriesIndex] = expanded;

        }



        private SeriesEventMarker CreatePlaybackMarker(Color color)

        {

            var marker = CreateEventMarker(MarkerType.PlaybackCursor);

            ApplyPlaybackMarkerStyle(marker, color);

            marker.Visible = false;

            return marker;

        }



        private void ApplyPlaybackMarkerStyle(SeriesEventMarker marker, Color color)

        {

            marker.Symbol.BorderColor = color;

            marker.Symbol.Color1 = color;

            marker.Symbol.Color2 = color;

            marker.Symbol.Color3 = color;

        }



        private PlaybackCursorSnapshot[] CreateHiddenSnapshots(int count)

        {

            var snapshots = new PlaybackCursorSnapshot[count];

            for (int i = 0; i < count; i++)

            {

                snapshots[i] = new PlaybackCursorSnapshot { Visible = false };

            }



            return snapshots;

        }



        private void ResetPlaybackCursorState()

        {

            _playbackMarkerPool.Clear();

            _playbackSnapshots.Clear();

        }

        /// <summary>
        /// 마우스로 선택한 최근 두 포인트를 보관한다.
        /// SelectionPin Marker는 기존 UI 로직이 관리하고, Delta Text는 이 버퍼의 좌표를 기준으로 계산한다.
        /// </summary>
        private sealed class RecentDeltaPointBuffer

        {

            /** @brief 최근 선택 포인트 2개를 저장하는 고정 버퍼 */

            private readonly DeltaPointSnapshot[] _points = new DeltaPointSnapshot[2];

            /** @brief 다음 클릭 좌표가 저장될 위치 */

            private int _nextWriteIndex;

            /** @brief 현재 버퍼에 저장된 유효 포인트 개수 */

            private int _count;

            /** @brief 기존 SelectionPin 교체 순서와 맞추기 위한 다음 저장 위치 */

            /** @brief Delta 계산이 가능한 상태인지 여부 */

            public bool HasTwoPoints => _count == _points.Length;

            /** @brief 현재 Delta 계산 기준 중 오래된 포인트 */

            public DeltaPointSnapshot OlderPoint => _points[_nextWriteIndex];

            /** @brief 현재 Delta 계산 기준 중 최신 포인트 */

            public DeltaPointSnapshot NewerPoint => _points[(_nextWriteIndex + 1) % _points.Length];

            /// <summary>
            /// 새 선택 좌표를 추가한다.
            /// 2개를 초과하면 가장 오래된 좌표를 덮어써서 항상 최신 두 점만 유지한다.
            /// </summary>
            public void Push(double x, double y)

            {

                _points[_nextWriteIndex] = new DeltaPointSnapshot

                {

                    X = x,

                    Y = y,

                    Visible = true

                };

                _nextWriteIndex = (_nextWriteIndex + 1) % _points.Length;

                if (_count < _points.Length)

                {

                    _count++;

                }

            }

            /** @brief 선택 포인트 기록을 초기화한다. */

            public void Clear()

            {

                for (var i = 0; i < _points.Length; i++)

                {

                    _points[i] = default;

                }

                _nextWriteIndex = 0;

                _count = 0;

            }

        }

        private struct DeltaPointSnapshot

        {

            public double X;

            public double Y;

            public bool Visible;

        }

    }

}
