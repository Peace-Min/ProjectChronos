using System;
using System.Globalization;
using System.Windows.Data;

namespace ProjectChronos.Views
{
    public class TimeToOffsetConverter : IMultiValueConverter
    {
        public double ThumbWidth { get; set; } = 0;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0]: Timestamp (double)
            // values[1]: TotalDuration (double)
            // values[2]: ActualWidth of Canvas (double)

            if (values.Length == 3 &&
                values[0] is double time &&
                values[1] is double duration &&
                values[2] is double width)
            {
                if (duration <= 0 || width <= 0) return 0.0;

                // 비율 계산
                double ratio = time / duration;
                
                // 캔버스 너비에 매핑
                double x = ratio * width;

                // (선택 사항) 썸의 중앙에 맞추기 위해 보정
                if (ThumbWidth > 0)
                {
                    x -= ThumbWidth / 2;
                }

                return x;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
