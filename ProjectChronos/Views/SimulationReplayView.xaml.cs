using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace ProjectChronos.Views
{
    public partial class SimulationReplayView : UserControl
    {
        public SimulationReplayView()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            System.Windows.Media.CompositionTarget.Rendering += OnRendering;
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            System.Windows.Media.CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (DataContext is ProjectChronos.ViewModels.SimulationReplayViewModel vm)
            {
                vm.Tick(sender, e);
            }
        }
    }

    /// <summary>
    /// TotalDuration과 ContainerWidth를 기반으로 특정 타임스탬프를 수평 오프셋(Canvas.Left)으로 변환합니다.
    /// 슬라이더의 Thumb 너비를 고려하여 중심을 맞춥니다.
    /// </summary>
    public class TimeToOffsetConverter : IMultiValueConverter
    {
        /// <summary>
        /// 슬라이더 Thumb의 너비입니다. XAML에서 설정 가능합니다. 기본값 18.0
        /// </summary>
        public double ThumbWidth { get; set; } = 18.0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // 예상 바인딩 값:
            // values[0]: CurrentTime (double) - 현재 시간
            // values[1]: TotalDuration (double) - 전체 시간
            // values[2]: ContainerWidth (double) - 컨테이너 너비

            if (values.Length < 3 ||
                !(values[0] is double current) ||
                !(values[1] is double total) ||
                !(values[2] is double width))
            {
                return 0.0;
            }

            if (total <= 0 || width <= 0)
                return 0.0;

            double ratio = current / total;

            // 비율 제한 0.0 ~ 1.0 (Clamping)
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;

            // 슬라이더 썸(Thumb)의 너비를 고려한 오프셋 보정
            // Slider의 Track은 전체 너비를 사용하지만, Thumb의 중심점은 [ThumbWidth/2, Width - ThumbWidth/2] 범위에서 움직입니다.
            // XAML에서 Thumb Width="18"로 설정되어 있으므로 이를 반영합니다.
            double availableWidth = width - ThumbWidth;

            // 시작점 보정: Thumb의 중심이 0일 때, 실제로는 ThumbWidth/2 위치에 있습니다.
            double offset = (ThumbWidth / 2) + (ratio * availableWidth);

            return offset;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
