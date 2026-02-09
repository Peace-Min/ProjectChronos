using System;
using System.Globalization;
using System.Windows.Data;
using ProjectChronos.Models;

namespace ProjectChronos.Converters
{
    /// <summary>
    /// EventPriority를 UI 스타일링을 위한 문자열 태그로 변환합니다.
    /// 예: High -> "Critical", Medium -> "Warning", Low -> "Info"
    /// </summary>
    public class PriorityToTagConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EventPriority priority)
            {
                switch (priority)
                {
                    case EventPriority.High:
                        return "Critical";
                    case EventPriority.Medium:
                        return "Warning";
                    case EventPriority.Low:
                        return "Info";
                    default:
                        return "Info";
                }
            }
            return "Info";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
