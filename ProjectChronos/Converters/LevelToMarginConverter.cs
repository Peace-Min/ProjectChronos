using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ProjectChronos.Models;

namespace ProjectChronos.Converters
{
    public class LevelToMarginConverter : IValueConverter
    {
        // Parameter: "MarkerTickHeight,LabelHeight". 레이블이 패널 상단 밖으로 올라오도록 음수 Top Margin을 반환합니다.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                double markerH = 20; // 마커 틱 반경 (클릭 영역 포함)
                double labelH = 25; // 레이블 한 줄 높이 + 수직 패딩

                if (parameter is string paramStr)
                {
                    var parts = paramStr.Split(',');
                    if (parts.Length >= 1) double.TryParse(parts[0], out markerH);
                    if (parts.Length >= 2) double.TryParse(parts[1], out labelH);
                }

                // 음수 Top Margin: 패널 상단을 넘어 위쪽으로 레이블을 올림
                // Level 0: -markerH, Level 1: -(markerH + labelH), ...
                double topOffset = -(markerH + level * labelH);
                return new Thickness(0, topOffset, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LevelToHeightConverter : IValueConverter
    {
        // Parameter : "Base,Step" e.g. "10,40"
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int level)
            {
                double baseHeight = 10;
                double step = 40;

                if (parameter is string paramStr)
                {
                    var parts = paramStr.Split(',');
                    if (parts.Length >= 1) double.TryParse(parts[0], out baseHeight);
                    if (parts.Length >= 2) double.TryParse(parts[1], out step);
                }

                return baseHeight + (level * step);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// EventPriority → SolidColorBrush: 이벤트 개별 우선순위를 레이블 BorderBrush 색상으로 변환합니다.
    /// </summary>
    public class PriorityToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EventPriority priority)
            {
                if (priority == EventPriority.High)
                    return new SolidColorBrush(Color.FromRgb(0xff, 0x55, 0x55));   // 빨강
                if (priority == EventPriority.Medium)
                    return new SolidColorBrush(Color.FromRgb(0xff, 0xb8, 0x6c)); // 주황
                return new SolidColorBrush(Color.FromRgb(0x8b, 0xe9, 0xfd));      // 하늘
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
