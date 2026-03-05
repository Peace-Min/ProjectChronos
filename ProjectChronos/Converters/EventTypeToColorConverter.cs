using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ProjectChronos.Models;

namespace ProjectChronos.Converters
{
    /// <summary>
    /// SimulationEventType을 고유한 색상(SolidColorBrush)으로 변환하는 컨버터
    /// </summary>
    public class EventTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SimulationEventType eventType)
            {
                switch (eventType)
                {
                    case SimulationEventType.SearchRadar:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700")); // 탐색레이더 (노란색)
                    case SimulationEventType.TrackRadar:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00BFFF")); // 추적레이더 (파란색)
                    case SimulationEventType.LaunchApproval:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5555")); // 발사승인 (빨간색)
                    case SimulationEventType.MissileLaunch:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#32CD32")); // 미사일발사 (녹색)
                    case SimulationEventType.Intercept:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9370DB")); // 요격 (보라색)
                    default:
                        return new SolidColorBrush(Colors.Gray);
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 단방향 바인딩 전용
            return Binding.DoNothing;
        }
    }
}
