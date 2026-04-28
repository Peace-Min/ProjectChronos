using Arction.Wpf.Charting;

using Arction.Wpf.Charting.Annotations;

using Arction.Wpf.Charting.EventMarkers;

using Arction.Wpf.Charting.Series3D;

using Arction.Wpf.Charting.Views.View3D;

using DevExpress.Mvvm.Native;

using DevExpress.UnitConversion;

using DevExpress.Utils.Filtering;

using DevExpress.Xpf.CodeView;

using OSTES.Common;

using OSTES.Data;

using OSTES.Interface;

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

using System.Windows.Media;

using System.Windows.Media.Imaging;

using System.Windows.Navigation;

using System.Windows.Shapes;

using SeriesPoint = Arction.Wpf.Charting.SeriesPoint;

using Geometry = OSTES.Utils.Geometry;



namespace OSTES.Chart

{

    /// <summary>

    /// UserCustomChart_3DGraph.xaml에 대한 상호 작용 논리

    /// </summary>

    public partial class UserCustomChart_3DGraph : UserControl, IUserCustomChart, IChartPlayback, IDisposable

    {

        /** @brief 반올림 자릿수 */

        private const int _roundingDigits = 6;



        /** @brief 초기 카메라 줌 위치 */

        private const int _initViewDistance = 180;



        /** @brief 첫번째 여부 */

        private bool isFirst;

        /** @brief 해당 인스턴스에 설정된 차트 타입(Diff 계산에 사용됨). */

        private readonly ChartViewType _chartViewType;



        /** @brief 사용자 메모에서 현재 편집중인 Annotation */

        private Annotation3D _editingAnnotation3D;



        /** @brief 마우스 커서를 따라가는 마커 역할 수행하는 PointLineSeries3D */

        private PointLineSeries3D _trackingMarkerSeries;



        /** @brief 마우스 클릭 마커 역할 수행하는 PointLineSeries3D */

        private PointLineSeries3D _selectionPinMarkerSeries;



        /** @brief 차트 전시 범위 */

        private ChartRangeData _chartRangeData;



        /** @brief  마우스 좌클릭 위치 */

        private Point _mouseLeftDownPoint;



        /** @brief  마우스 우클릭 위치 */

        private Point _mouseRightDownPoint;



        /** @brief 전시중인 차트 인스턴스 */

        private LightningChartUltimate _chart;



        /** @brief 카메라 초기 설정 정보 */

        private Camera3DInfo _initailCamera3DInfo;



        /** @brief 한글 IME 정상 동작을 위한 Window. **/

        private EditorInputWindow _editorWindow;



        /** @brief Cursor Tracking에 사용된 Annotaition 반환 */

        private IEnumerable<Annotation3D> TrackingAnnotations => _chart.View3D.Annotations.Where(item => (item.Tag is AnnotationType type) && ((type == AnnotationType.CursorTooltip) || (type == AnnotationType.SelectionPin)));



        /** @brief Cursor Tracking에 사용된 PointLineSeries3D 반환 */

        private IEnumerable<PointLineSeries3D> TrackingPointLineSeries3D => _chart.View3D.PointLineSeries3D.Where(item => (item.Tag is AnnotationType type) && ((type == AnnotationType.CursorTooltip) || (type == AnnotationType.SelectionPin)));



        /** @brief 차트의 데이터 전시 목적으로만 사용된 PointLineSeries3D 반환 */

        private IEnumerable<PointLineSeries3D> ChartDataPointLineSeries3D => _chart.View3D.PointLineSeries3D.Where(p => !(p.Tag is AnnotationType));



        public UserCustomChart_3DGraph(ChartViewType chartViewType, string axisXTitle, string axisYTitle, string axisZTitle)

        {

            Debug.Assert(chartViewType == ChartViewType.ThreeD_LLA || chartViewType == ChartViewType.ThreeD_XYZ, "UserCustomChart_3DGraph 생성자 오류");



            _chartViewType = chartViewType;



            _editorWindow = new EditorInputWindow();

            _editorWindow.SaveRequested += OnEditorSaved;

            _editorWindow.CancelRequested += OnEditorCancelled;



            InitializeComponent();

            CreateChart(axisXTitle, axisYTitle, axisZTitle);

        }



        #region IUserCustomChart Public API

        public void BeginUpdate() => _chart.BeginUpdate();



        public void EndUpdate() => _chart.EndUpdate();



        public void AddPoints(AddSeriesPointDTO addSeriesPointDTO)

        {

            //var trackingPointSeries3D = TrackingPointLineSeries3D.Count();

            //var selectedIndex = addSeriesPointDTO.SeriesIndex + trackingPointSeries3D;



            var selectedFreeformPointLineSeries = ChartDataPointLineSeries3D.ElementAt(addSeriesPointDTO.SeriesIndex);



            if (selectedFreeformPointLineSeries == null) { return; }

            if (addSeriesPointDTO.SeriesPoint3D.Length <= 0) { return; }



            selectedFreeformPointLineSeries.AddPoints(addSeriesPointDTO.SeriesPoint3D, false);

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

                    Z_Min = Math.Min(_chartRangeData.Z_Min, rangeData.Z_Min),



                    X_Max = Math.Max(_chartRangeData.X_Max, rangeData.X_Max),

                    Y_Max = Math.Max(_chartRangeData.Y_Max, rangeData.Y_Max),

                    Z_Max = Math.Max(_chartRangeData.Z_Max, rangeData.Z_Max),

                };

            }



            _chart.View3D.XAxisPrimary3D.SetRange(_chartRangeData.X_Min, _chartRangeData.X_Max);

            _chart.View3D.YAxisPrimary3D.SetRange(_chartRangeData.Y_Min, _chartRangeData.Y_Max);

            _chart.View3D.ZAxisPrimary3D.SetRange(_chartRangeData.Z_Min, _chartRangeData.Z_Max);

        }



        public void AddLineChartSeries(LineChartSeriesDTO lineChartSeriesDTO)

        {

            if (_trackingMarkerSeries == null)

            {

                _trackingMarkerSeries = CreateBoxMarkerSeries(AnnotationType.CursorTooltip);

                _chart.View3D.PointLineSeries3D.Add(_trackingMarkerSeries);

                _trackingMarkerSeries.Points = new SeriesPoint3D[1] { new SeriesPoint3D(double.NaN, double.NaN, double.NaN) };

            }



            if (_selectionPinMarkerSeries == null)

            {

                _selectionPinMarkerSeries = CreateBoxMarkerSeries(AnnotationType.SelectionPin);

                _chart.View3D.PointLineSeries3D.Add(_selectionPinMarkerSeries);

                _selectionPinMarkerSeries.Points = new SeriesPoint3D[2] { new SeriesPoint3D(double.NaN, double.NaN, double.NaN), new SeriesPoint3D(double.NaN, double.NaN, double.NaN) };

            }



            var addSeries = ChartCreate_3D.AddLineChart_Series(_chart, lineChartSeriesDTO.SeriesName);

            ChartCreate_3D.EditPointSeries(addSeries, 1, LinePattern.Solid, lineChartSeriesDTO.SeriesName, lineChartSeriesDTO.LineColor, 1, lineChartSeriesDTO.LineColor, lineChartSeriesDTO.LineSeriesType);

        }



        public void ClearChart()

        {

            txtXDelta.Visibility = Visibility.Collapsed;

            txtYDelta.Visibility = Visibility.Collapsed;

            txtZDelta.Visibility = Visibility.Collapsed;

            txtDistance.Visibility = Visibility.Collapsed;



            _chart.BeginUpdate();



            _chart.View3D.Camera.ViewDistance = _initViewDistance;

            _chart.View3D.XAxisPrimary3D.Title.DistanceToAxis = 20;

            _chart.View3D.YAxisPrimary3D.Title.DistanceToAxis = 20;

            _chart.View3D.ZAxisPrimary3D.Title.DistanceToAxis = 20;



            _chart.View3D.PointLineSeries3D.ForEach(s => s.Clear());



            _trackingMarkerSeries.Clear();

            _trackingMarkerSeries.Points = new SeriesPoint3D[1] { new SeriesPoint3D(double.NaN, double.NaN, double.NaN) };



            _selectionPinMarkerSeries.Clear();

            _selectionPinMarkerSeries.Points = new SeriesPoint3D[2] { new SeriesPoint3D(double.NaN, double.NaN, double.NaN), new SeriesPoint3D(double.NaN, double.NaN, double.NaN) };



            for (var lastIndex = _chart.View3D.Annotations.Count() - 1; lastIndex >= 0; lastIndex--)

            {

                var annotation = _chart.View3D.Annotations.ElementAt(lastIndex);

                if ((annotation.Tag is AnnotationType type) && (type != AnnotationType.SelectionPin)) { continue; } // SelectionPin Annotation만 삭제.



                _chart.View3D.Annotations.Remove(annotation);

            }



            for (var lastIndex = _chart.View3D.Annotations.Count() - 1; lastIndex >= 0; lastIndex--)

            {

                var annotation = _chart.View3D.Annotations.ElementAt(lastIndex);

                if ((annotation.Tag is AnnotationType type) && (type != AnnotationType.UserMemo)) { continue; } // SelectionPin UserMemo만 삭제.



                _chart.View3D.Annotations.Remove(annotation);

            }



            _chart.EndUpdate();

        }

        #endregion



        #region IChartExport Public API

        public bool IsValidChart => _chart.View3D.PointLineSeries3D.Count > 0;



        public string SaveChartToFile(string directiory)

        {

            var filePath = $"SnapShotChart_3D.png";

            var fullPath = System.IO.Path.Combine(directiory, filePath);



            var view3D = _chart.View3D;

            var xAxisPrimary3DLabelsColor = view3D.XAxisPrimary3D.LabelsColor;

            var yAxisPrimary3DLabelsColor = view3D.YAxisPrimary3D.LabelsColor;

            var zAxisPrimary3DLabelsColor = view3D.ZAxisPrimary3D.LabelsColor;



            var chartBackgroundColor = _chart.ChartBackground.Color;

            var chartBackgroundGradientColor = _chart.ChartBackground.GradientColor;



            var wallOnBottomColor = _chart.View3D.WallOnFront.GridStripColorX;

            var wallOnFrontColor = _chart.View3D.WallOnFront.GridStripColorX;

            var wallOnLeftColor = _chart.View3D.WallOnFront.GridStripColorX;

            var wallOnRightColor = _chart.View3D.WallOnFront.GridStripColorX;

            var wallOnBackColor = _chart.View3D.WallOnFront.GridStripColorX;



            // 차트 캡처를 위한 배경 변경.

            _chart.BeginUpdate();

            _chart.View3D.XAxisPrimary3D.LabelsColor = _chart.View3D.YAxisPrimary3D.LabelsColor = _chart.View3D.ZAxisPrimary3D.LabelsColor = Colors.Black;

            _chart.ChartBackground.Color = _chart.ChartBackground.GradientColor = Colors.White;

            _chart.View3D.WallOnBottom.GridStripColorX = _chart.View3D.WallOnFront.GridStripColorX = _chart.View3D.WallOnLeft.GridStripColorY = _chart.View3D.WallOnRight.GridStripColorY = _chart.View3D.WallOnBack.GridStripColorY = Colors.White;

            _chart.EndUpdate();



            _chart.SaveToFile(fullPath, 546, 340);



            // 기존 차트 배경 롤백.

            _chart.BeginUpdate();

            _chart.View3D.XAxisPrimary3D.LabelsColor = xAxisPrimary3DLabelsColor;

            _chart.View3D.YAxisPrimary3D.LabelsColor = yAxisPrimary3DLabelsColor;

            _chart.View3D.ZAxisPrimary3D.LabelsColor = zAxisPrimary3DLabelsColor;

            _chart.ChartBackground.Color = chartBackgroundColor;

            _chart.ChartBackground.GradientColor = chartBackgroundGradientColor;

            _chart.View3D.WallOnBottom.GridStripColorX = wallOnBottomColor;

            _chart.View3D.WallOnFront.GridStripColorX = wallOnFrontColor;

            _chart.View3D.WallOnLeft.GridStripColorY = wallOnLeftColor;

            _chart.View3D.WallOnRight.GridStripColorY = wallOnRightColor;

            _chart.View3D.WallOnBack.GridStripColorY = wallOnBackColor;

            _chart.EndUpdate();



            return fullPath;

        }



        public string GetChartMetaData()

        {

            var sb = new StringBuilder();



            // delta Text가 활성화 되었을때 AppendLine.

            if ((txtXDelta.Visibility == Visibility.Visible) && (txtYDelta.Visibility == Visibility.Visible) && (txtZDelta.Visibility == Visibility.Visible))

            {

                var deltaXY = $"{txtXDelta.Text}, {txtYDelta.Text}, {txtZDelta.Text}, {txtDistance.Text}";

                sb.AppendLine(deltaXY);

            }



            return sb.ToString();

        }

        #endregion



        #region IChartTheming Public API

        public void ChartThemeSelection(int index)

        {

            LightningChartThemeUtil.Chart3D_Theme(_chart, index);

        }



        public void ChartLegendTheme(int index)

        {

            LightningChartThemeUtil.Chart3D_LegendTheme(_chart, index);

        }



        #endregion



        #region IChartPlayback Public API

        public void SetPlaybackCursor()

        {

            _chart.BeginUpdate();



            var chartDataPointLineSeries3DList = _chart.View3D.PointLineSeries3D.Where(p => !(p.Tag is AnnotationType)).ToList();



            // PointLineSeries3D 전용 PlaybackCursor 포인트시리즈 추가

            foreach (var pointLineSeries3D in chartDataPointLineSeries3DList)

            {

                var playbackCursorSeries3D = CreateBoxMarkerSeries(AnnotationType.PlaybackCursor);

                playbackCursorSeries3D.PointStyle.Shape3D = PointShape3D.Torus;

                playbackCursorSeries3D.PointStyle.Size3D.SetValues(4, 4, 4);

                playbackCursorSeries3D.Material.DiffuseColor = pointLineSeries3D.Material.DiffuseColor;

                playbackCursorSeries3D.Material.EmissiveColor = pointLineSeries3D.Material.EmissiveColor;

                _chart.View3D.PointLineSeries3D.Add(playbackCursorSeries3D);



                pointLineSeries3D.Tag = playbackCursorSeries3D;

            }



            _chart.EndUpdate();

        }



        public void UpdatePlaybackCursorPosition(AddSeriesPointDTO addSeriesPointDTO)

        {

            var selectedFreeformPointLineSeries = ChartDataPointLineSeries3D.ElementAtOrDefault(addSeriesPointDTO.SeriesIndex);

            if (selectedFreeformPointLineSeries == null) { return; }



            _chart.BeginUpdate();



            try

            {

                // 선택된 시리즈의 Tag값이 PlaybackCursor 역할을 수행하는 PointLineSeries3D.

                if (!(selectedFreeformPointLineSeries.Tag is PointLineSeries3D playbackCursorSeries)) { return; }



                if (addSeriesPointDTO.SeriesPoint3D.Length <= 0) // SeriesPoint가 존재하지않으면 PlaybackCursor 비활성화 처리 후 return.

                {

                    playbackCursorSeries.Visible = false;

                    return;

                }



                // 현재 시리지의 마커 개수가 부족한 경우 신규 생성.

                playbackCursorSeries.Visible = true;

                playbackCursorSeries.Points = addSeriesPointDTO.SeriesPoint3D;

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

                _chart.MouseMove -= new MouseEventHandler(Chart_MouseMove);

                _chart.MouseDown -= Chart_MouseDown;

                _chart.MouseClick -= Chart_MouseClick;

                _chart.MouseLeave -= Chart_MouseLeave;



                gridChart.Children.Clear(); //! 차트 삭제

                _chart.Dispose();

                _chart = null;

            }

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

         * @brief 차트 마우스 다운 이벤트 처리.

         *

         * @param   sender 차트.

         * @param   e      이벤트 Args.

        */



        private void Chart_MouseDown(object sender, MouseButtonEventArgs e)

        {

            if (e.LeftButton == MouseButtonState.Pressed)

            {

                _mouseLeftDownPoint = e.GetPosition(_chart);

            }

            else if (e.RightButton == MouseButtonState.Pressed)

            {

                _mouseRightDownPoint = e.GetPosition(_chart);

            }

        }



        private PointLineSeries3D _activePointLineSeries3D;



        /**

         * @brief 차트 마우스 클릭 이벤트 처리.

         *

         * @param   sender 차트.

         * @param   e      이벤트 Args.

        */

        private void Chart_MouseClick(object sender, MouseButtonEventArgs e)

        {

            var currentPos = Mouse.GetPosition(_chart); //! 마우스 좌표를 차트 좌표로 변환.

            var distance = double.NaN;



            // 마우스 우클릭 조건문.

            if (e.ChangedButton == MouseButton.Right)

            {

                distance = Math.Sqrt(Math.Pow(currentPos.X - _mouseRightDownPoint.X, 2) + Math.Pow(currentPos.Y - _mouseRightDownPoint.Y, 2));



                // 5 pixel 이상 움직인 경우 패닝 or 회전으로 간주하여 클릭 이벤트 무시.

                if (distance < 5)

                {

                    _activePointLineSeries3D = GetActivePointLineSeries3D(out SeriesPoint3D activeSeriesPoint3D);



                    var transForm = new TranslateTransform

                    {

                        X = currentPos.X,

                        Y = currentPos.Y

                    };

                    mousePosLabel.RenderTransform = transForm;



                    radialMenu.ShowPopup(mousePosLabel);

                    return;

                }

            }



            // 마우스 좌클릭 조건문.

            if (e.ChangedButton != MouseButton.Left) { return; }

            distance = Math.Sqrt(Math.Pow(currentPos.X - _mouseLeftDownPoint.X, 2) + Math.Pow(currentPos.Y - _mouseLeftDownPoint.Y, 2));

            if (distance < 5)

            {

                var annot = default(Annotation3D);



                if (GetActivePointLineSeries3D(out SeriesPoint3D activeSeriesPoint3D) == null) { return; }



                _chart.BeginUpdate(); //! 차트 업데이트 시작



                annot = GetDeltaAnnotation3D();

                isFirst = !isFirst;



                var updatSeriesPoint3D = new SeriesPoint3D[2] { new SeriesPoint3D(double.NaN, double.NaN, double.NaN), new SeriesPoint3D(double.NaN, double.NaN, double.NaN) };

                if (TrackingAnnotations.ElementAt(1) == annot)

                {

                    updatSeriesPoint3D[0] = activeSeriesPoint3D;

                    updatSeriesPoint3D[1] = _selectionPinMarkerSeries.Points[1];

                }

                else if (TrackingAnnotations.ElementAt(2) == annot)

                {

                    updatSeriesPoint3D[0] = _selectionPinMarkerSeries.Points[0];

                    updatSeriesPoint3D[1] = activeSeriesPoint3D;

                }

                _selectionPinMarkerSeries.Clear();

                _selectionPinMarkerSeries.AddPoints(updatSeriesPoint3D, false);

                SetAnnotation3D(annot, activeSeriesPoint3D);

                annot.Text = GetAxisDipslayValue(activeSeriesPoint3D);

                annot.Visible = false;



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

            _chart.BeginUpdate(); //! 차트 업데이트 시작



            var annot = TrackingAnnotations.ElementAt(0);

            annot.Visible = false;



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

            var annot = TrackingAnnotations.ElementAt(0);

            annot.Visible = false;

            _trackingMarkerSeries.Visible = false;



            if (_chart.View3D.PointLineSeries3D.Count == 0) { return; }

            if (GetActivePointLineSeries3D(out SeriesPoint3D activeSeriesPoint3D) == null) { return; }



            _chart.BeginUpdate();



            _trackingMarkerSeries.Clear();

            _trackingMarkerSeries.AddPoints(new SeriesPoint3D[] { activeSeriesPoint3D }, false);

            _trackingMarkerSeries.Visible = true;



            //! 마커 정보 전시

            SetAnnotation3D(annot, activeSeriesPoint3D);

            annot.Text = GetAxisDipslayValue(activeSeriesPoint3D);

            annot.Visible = true;



            _chart.EndUpdate();

        }



        private PointLineSeries3D GetActivePointLineSeries3D(out SeriesPoint3D activeSeriesPoint3D)

        {

            var mouseInteractionObject = _chart.GetActiveMouseOverObject(); // GetActiveMouseOverObject() 메소드를 통해 마우스와 충돌된 객체를 검출한다.

            if ((mouseInteractionObject == null) || !(mouseInteractionObject is PointLineSeries3D mouseHitPointLineSeries3D) || (!mouseHitPointLineSeries3D.Visible))

            {

                activeSeriesPoint3D = default;

                return default;

            }



            var activeIndex = mouseHitPointLineSeries3D.LastMouseHitTestIndex;

            activeSeriesPoint3D = mouseHitPointLineSeries3D.Points[activeIndex];



            return mouseHitPointLineSeries3D;

        }

        /// <summary>

        /// RadialContextMenu - 기본크기 클릭 Action.

        /// </summary>

        private void ApplyDefaultSize_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            if (_chartRangeData == default) { return; }



            _chart.BeginUpdate();



            _chart.View3D.XAxisPrimary3D.SetRange(_chartRangeData.X_Min, _chartRangeData.X_Max);

            _chart.View3D.YAxisPrimary3D.SetRange(_chartRangeData.Y_Min, _chartRangeData.Y_Max);

            _chart.View3D.ZAxisPrimary3D.SetRange(_chartRangeData.Z_Min, _chartRangeData.Z_Max);



            _chart.View3D.Camera.RotationX = _initailCamera3DInfo.RotationX;

            _chart.View3D.Camera.RotationY = _initailCamera3DInfo.RotationY;

            _chart.View3D.Camera.RotationZ = _initailCamera3DInfo.RotationZ;

            _chart.View3D.Camera.ViewDistance = _initailCamera3DInfo.ViewDistance;



            _chart.EndUpdate();

        }



        /// <summary>

        /// RadialContextMenu - 마커 초기화 클릭 Action.

        /// </summary>

        private void ResetMarkers_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            txtXDelta.Visibility = Visibility.Collapsed;

            txtYDelta.Visibility = Visibility.Collapsed;

            txtZDelta.Visibility = Visibility.Collapsed;

            txtDistance.Visibility = Visibility.Collapsed;



            _chart.BeginUpdate();



            _trackingMarkerSeries.Clear();

            _trackingMarkerSeries.Points = new SeriesPoint3D[1] { new SeriesPoint3D(double.NaN, double.NaN, double.NaN) };



            _selectionPinMarkerSeries.Clear();

            _selectionPinMarkerSeries.Points = new SeriesPoint3D[2] { new SeriesPoint3D(double.NaN, double.NaN, double.NaN), new SeriesPoint3D(double.NaN, double.NaN, double.NaN) };



            for (var lastIndex = _chart.View3D.Annotations.Count() - 1; lastIndex >= 0; lastIndex--)

            {

                var annotation = _chart.View3D.Annotations.ElementAt(lastIndex);

                if ((annotation.Tag is AnnotationType type) && (type != AnnotationType.SelectionPin)) { continue; } // SelectionPin Annotation만 삭제.



                _chart.View3D.Annotations.Remove(annotation);

            }



            _chart.EndUpdate();

        }



        /// <summary>

        /// RadialContextMenu - 사용자메모 초기화 클릭 Action.

        /// </summary>

        private void ResetUserMemo_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            _chart.BeginUpdate();



            for (var lastIndex = _chart.View3D.Annotations.Count() - 1; lastIndex >= 0; lastIndex--)

            {

                var annotation = _chart.View3D.Annotations.ElementAt(lastIndex);

                if ((annotation.Tag is AnnotationType type) && (type != AnnotationType.UserMemo)) { continue; } // SelectionPin UserMemo만 삭제.



                _chart.View3D.Annotations.Remove(annotation);

            }



            _chart.EndUpdate();



        }

        /// <summary>

        /// RadialContextMenu - 사용자메모 추가 클릭 Action.

        /// </summary>

        private void AddUserMemo_Click(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)

        {

            if (_mouseRightDownPoint == default) { return; }

            if (_activePointLineSeries3D == null) { return; }



            var activeSeriesPoint3D = _activePointLineSeries3D.Points[_activePointLineSeries3D.LastMouseHitTestIndex];

            _chart.BeginUpdate();



            var usermemoAnnotation = CreateAnnotation3D(AnnotationType.UserMemo);

            usermemoAnnotation.Text = GetAxisDipslayValue(activeSeriesPoint3D);



            SetAnnotation3D(usermemoAnnotation, activeSeriesPoint3D);

            _chart.View3D.Annotations.Add(usermemoAnnotation);



            _chart.EndUpdate();

        }



        /// <summary>

        /// 사용자 메모 마우스 클릭 Action.

        /// </summary>

        private void Memo_MouseDown(object sender, MouseEventArgs e)

        {

            if (!(sender is Annotation3D memoAnnotation)) { return; }

            if (e.RightButton != MouseButtonState.Pressed) { return; }



            var currentPos = Mouse.GetPosition(_chart);

            var distance = Math.Sqrt(Math.Pow(currentPos.X - _mouseRightDownPoint.X, 2) + Math.Pow(currentPos.Y - _mouseRightDownPoint.Y, 2));



            // 5 pixel 이상 움직인 경우 패닝 or 회전으로 간주하여 클릭 이벤트 무시.

            if (distance < 5)

            {

                if (this.Resources["MyCustomMenu"] is ContextMenu menu)

                {

                    if (!(menu.Items[0] is MenuItem itemEdit)) { return; }

                    if (!(menu.Items[2] is MenuItem itemDelete)) { return; }

                    itemEdit.Click += (s, args) =>

                    {

                        _editingAnnotation3D = memoAnnotation;



                        // 1.Close Menu.

                        menu.IsOpen = false;



                        var originalText = _editingAnnotation3D.Text;

                        var screenPoint = menu.PointToScreen(new Point(0, 0));



                        _editorWindow.OpenAt(originalText, screenPoint.X + 10, screenPoint.Y + 10);

                    };



                    itemDelete.Click += (s, args) =>

                    {

                        _chart.View3D.Annotations.Remove(memoAnnotation);

                    };



                    menu.IsOpen = true;

                }

            }



            e.Handled = true; // 차트의 기본 우클릭 동작 차단.

        }

        #endregion



        private void CreateChart(string axisXTitle, string axisYTitle, string axisZTitle)

        {

            _chart = new LightningChartUltimate("Network Customizing Technologies Inc/Eom Yong-Seob-Renewed/LightningChartUltimate/DQ3CJZYJFYJFYS422FNEWBU2EV2ZMH4Y65AU");

            _chart.BeginUpdate();



            _chart.Title.MouseInteraction = false;

            _chart.Title.Font.Size = 32;

            _chart.Title.Border.Style = BorderType.None;

            _chart.Title.Visible = false;



            //Set active view

            _chart.ActiveView = ActiveView.View3D;



            //Set second light location

            _chart.View3D.Lights[1].Location.SetValues(-40, 60, -40);



            _chart.View3D.XAxisPrimary3D.Title.Text = axisXTitle;

            _chart.View3D.YAxisPrimary3D.Title.Text = axisZTitle;

            _chart.View3D.ZAxisPrimary3D.Title.Text = axisYTitle;



            _chart.View3D.XAxisPrimary3D.Title.DistanceToAxis = 20;

            _chart.View3D.YAxisPrimary3D.Title.DistanceToAxis = 20;

            _chart.View3D.ZAxisPrimary3D.Title.DistanceToAxis = 20;



            _chart.View3D.Camera.ViewDistance = _initViewDistance;

            _chart.View3D.Camera.MinimumViewDistance = 0; // 줌 최대 거리 옵션.



            _chart.View3D.ZoomPanOptions.AxisMouseWheelAction = AxisMouseWheelAction.ZoomAll;



            _chart.ChartRenderOptions.InvokeRenderingInUIThread = true;

            _chart.ChartRenderOptions.DeviceType = RendererDeviceType.HardwareOnlyD9;



            _chart.View3D.LegendBox.Visible = true;

            _chart.View3D.LegendBox.Position = LegendBoxPosition.TopRight;

            _chart.View3D.LegendBox.Layout = LegendBoxLayout.Vertical;

            _chart.View3D.LegendBox.Position = LegendBoxPosition.TopRight;

            _chart.View3D.LegendBox.Offset.SetValues(-45, 10);

            _chart.View3D.LegendBox.AllowMouseResize = false;



            // 260408(min) 전역테마 적용.

            var themeIndex = ChartThemeComboItem.GetThemaIndex(AppConst.DEFAULT_CHART_THEME);



            LightningChartThemeUtil.Chart3D_Theme(_chart, themeIndex);

            LightningChartThemeUtil.Chart3D_LegendTheme(_chart, themeIndex);



            var annot = CreateAnnotation3D(AnnotationType.CursorTooltip);

            _chart.View3D.Annotations.Add(annot);



            _chart.MouseMove += new MouseEventHandler(Chart_MouseMove);

            _chart.MouseDown += Chart_MouseDown;

            _chart.MouseClick += Chart_MouseClick;

            _chart.MouseLeave += Chart_MouseLeave;



            _chart.EndUpdate();



            gridChart.Children.Add(_chart);



            _initailCamera3DInfo = new Camera3DInfo(_chart.View3D.Camera.RotationX, _chart.View3D.Camera.RotationY, _chart.View3D.Camera.RotationZ, _chart.View3D.Camera.ViewDistance);

        }



        private PointLineSeries3D CreateBoxMarkerSeries(AnnotationType annotationType)

        {

            var markerSeries = new PointLineSeries3D(_chart.View3D, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary);

            markerSeries.PointStyle.Shape3D = PointShape3D.Box;

            markerSeries.PointStyle.Size3D.SetValues(2, 2, 2);

            markerSeries.Material.DiffuseColor = Colors.Green;

            markerSeries.LineVisible = false;

            markerSeries.ShowInLegendBox = false;

            markerSeries.Tag = annotationType;



            return markerSeries;

        }

        /**

         * @brief delta annotation 반환

         *

         * @returns delta annotation 객체

        */

        private Annotation3D GetDeltaAnnotation3D()

        {

            var annot = default(Annotation3D);

            if (TrackingAnnotations.Count() < 3)  //! delta annotation이 3개 미만이면 생성

            {

                annot = CreateAnnotation3D(AnnotationType.SelectionPin);

                _chart.View3D.Annotations.Add(annot);

            }

            else

            {

                if (isFirst)  //! delta annotation이 3개이상이면 순차적으로 반환

                {

                    annot = TrackingAnnotations.ElementAt(1);

                }

                else

                {

                    annot = TrackingAnnotations.ElementAt(2);

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

            if ((_selectionPinMarkerSeries?.Points?.Length ?? 0) >= 2 &&
                HasValidDeltaPoint(_selectionPinMarkerSeries.Points[0]) &&
                HasValidDeltaPoint(_selectionPinMarkerSeries.Points[1]))

            {

                var value1 = _selectionPinMarkerSeries.Points[0];

                var value2 = _selectionPinMarkerSeries.Points[1];



                switch (_chartViewType)

                {

                    case ChartViewType.ThreeD_XYZ:

                        var diffXValue = Math.Abs(value1.X - value2.X);

                        var diffYValue = Math.Abs(value1.Y - value2.Y);

                        var diffZValue = Math.Abs(value1.Z - value2.Z);

                        var distValue = Math.Sqrt(diffXValue * diffXValue + diffYValue * diffYValue + diffZValue * diffZValue);



                        txtXDelta.Text = $"∆X = {Math.Round(diffXValue, 6)}";

                        txtYDelta.Text = $"∆Y = {Math.Round(diffYValue, 6)}";

                        txtZDelta.Text = $"∆Z = {Math.Round(diffZValue, 6)}";

                        txtDistance.Text = $"Dist = {Math.Round(distValue, 6)}";



                        txtXDelta.Visibility = Visibility.Visible;

                        txtYDelta.Visibility = Visibility.Visible;

                        txtZDelta.Visibility = Visibility.Visible;

                        txtDistance.Visibility = Visibility.Visible;

                        break;



                    case ChartViewType.ThreeD_LLA:

                        var value1ToVector3 = new SharpDX.Mathematics.Vector3((float)value1.X, (float)value1.Y, (float)value1.Z);

                        var value2ToVector3 = new SharpDX.Mathematics.Vector3((float)value2.X, (float)value2.Y, (float)value2.Z);

                        var distance = Geometry.GetDistanceWithLLA(value1ToVector3, value2ToVector3, out double deltaLon, out double deltaLat, out double deltaAlt);

                        var absLon = Math.Abs(deltaLon);

                        var absLat = Math.Abs(deltaLat);

                        var absAlt = Math.Abs(deltaAlt);



                        txtXDelta.Text = $"∆Lon = {Math.Round(absLon, 6)}";

                        txtYDelta.Text = $"∆Lat = {Math.Round(absLat, 6)}";

                        txtZDelta.Text = $"∆Alt = {Math.Round(absAlt, 6)}";

                        txtDistance.Text = $"Dist = {Math.Round(distance, 6)}";



                        txtXDelta.Visibility = Visibility.Visible;

                        txtYDelta.Visibility = Visibility.Visible;

                        txtZDelta.Visibility = Visibility.Visible;

                        txtDistance.Visibility = Visibility.Visible;

                        break;



                    default:

                        throw new ArgumentOutOfRangeException(nameof(ChartViewType));

                }

            }

            else

            {

                txtXDelta.Visibility = Visibility.Collapsed;

                txtYDelta.Visibility = Visibility.Collapsed;

                txtZDelta.Visibility = Visibility.Collapsed;

                txtDistance.Visibility = Visibility.Collapsed;

            }

        }





        /**

           * @brief Annotation 생성.

           *

           * @returns 생성된 Annotation.

        */

        private Annotation3D CreateAnnotation3D(AnnotationType annotationType)

        {

            var annot = (Annotation3D)default;

            switch (annotationType)

            {

                case AnnotationType.CursorTooltip:

                    annot = new Annotation3D(_chart.View3D, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary)

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



                case AnnotationType.SelectionPin:

                    annot = new Annotation3D(_chart.View3D, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary)

                    {

                        TargetCoordinateSystem = AnnotationTargetCoordinates.AxisValues,

                        LocationCoordinateSystem = CoordinateSystem.AxisValues,

                        Visible = false,

                        MouseInteraction = false,

                        Tag = annotationType,

                        //Style = AnnotationStyle.Callout,

                        Sizing = Annotation3DSizing.ScreenCoordinates,

                    };



                    annot.SizeScreenCoords.Width = 15;

                    annot.SizeScreenCoords.Height = 15;

                    annot.Fill.Color = Colors.Transparent;

                    annot.BorderLineStyle.Color = Colors.DarkGreen;

                    //annot.BorderLineStyle.Width = 3F;



                    //annot.Style = AnnotationStyle.Rectangle;

                    //annot.Fill.GradientFill = GradientFill.Solid;

                    //annot.Fill.Style = RectFillStyle.ColorOnly;

                    annot.Shadow.Visible = false;

                    //annot.LocationRelativeOffset.X = 0;

                    //annot.LocationRelativeOffset.Y = -40;

                    return annot;



                case AnnotationType.UserMemo:

                    annot = new Annotation3D(_chart.View3D, Axis3DBinding.Primary, Axis3DBinding.Primary, Axis3DBinding.Primary)

                    {

                        Tag = annotationType,

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

         * @brief Annotation 설정

         *

         * @param   annot annotation 객체.

         * @param   xVal  x 값.

         * @param   yVal  y 값.

        */

        private void SetAnnotation3D(Annotation3D annot, SeriesPoint3D seriesPoint3D)

        {

            annot.TargetAxisValues.X = seriesPoint3D.X; //! Annotation X값 설정.

            annot.TargetAxisValues.Y = seriesPoint3D.Y; //! Annotation Y값 설정.

            annot.TargetAxisValues.Z = seriesPoint3D.Z; //! Annotation Z값 설정.



            var middle = (_chart.View3D.YAxisPrimary3D.Maximum + _chart.View3D.YAxisPrimary3D.Minimum) / 2; //! Y축 중앙값 계산

            if (seriesPoint3D.Y > middle)  //! 중앙값 여부에 따른 전시 위치 설정

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

         * @param   AxisDimension3D 값.

         *

         * @returns 전시 값

        */

        private string GetAxisDipslayValue(double value, AxisDimension3D axisDimension3D)

        {

            switch (axisDimension3D)

            {

                case AxisDimension3D.X:

                    return $"X={value}";  //! X축 전시 값 생성



                case AxisDimension3D.Y:

                    return $"Y={value}"; //! Y축 전시 값 생성



                case AxisDimension3D.Z:

                    return $"Z={value}"; //! Y축 전시 값 생성



                default:

                    throw new ArgumentOutOfRangeException(nameof(axisDimension3D));

            }

        }



        /**

        * @brief 축 전시 값 반환

        *

        * @param   value   값.

        * @param   AxisDimension3D 값.

        *

        * @returns 전시 값

       */

        private string GetAxisDipslayValue(SeriesPoint3D value)

        {

            return $"{GetAxisDipslayValue(value.X, AxisDimension3D.X)}\n{GetAxisDipslayValue(value.Y, AxisDimension3D.Y)}\n{GetAxisDipslayValue(value.Z, AxisDimension3D.Z)}";

        }

        private bool HasValidDeltaPoint(SeriesPoint3D point)

        {

            return !double.IsNaN(point.X) && !double.IsNaN(point.Y) && !double.IsNaN(point.Z);

        }



        private void EditorBox_PreviewKeyDown(object sender, KeyEventArgs e)

        {

            if (e.Key == Key.Enter)

            {

                if (Keyboard.Modifiers != ModifierKeys.Shift)

                {

                    e.Handled = true;



                    // 저장 동작.

                    if (_editingAnnotation3D != null)

                    {

                        _editingAnnotation3D.Text = EditorTextBox.Text;

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

            if (_editingAnnotation3D != null)

            {

                _editingAnnotation3D.Text = newText;

            }

        }

    }

}
