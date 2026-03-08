using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace ProjectChronos.Views
{
    public class EventMarkerPanel : Panel
    {
        public static readonly DependencyProperty TotalDurationProperty =
            DependencyProperty.Register("TotalDuration", typeof(double), typeof(EventMarkerPanel),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public double TotalDuration
        {
            get { return (double)GetValue(TotalDurationProperty); }
            set { SetValue(TotalDurationProperty, value); }
        }

        public static readonly DependencyProperty ThumbWidthProperty =
            DependencyProperty.Register("ThumbWidth", typeof(double), typeof(EventMarkerPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        /// <summary>
        /// 슬라이더 썸(Thumb)의 너비. 좌표 계산 시 썸의 중심과 일치시키기 위해 사용합니다. (기본값 0)
        /// </summary>
        public double ThumbWidth
        {
            get { return (double)GetValue(ThumbWidthProperty); }
            set { SetValue(ThumbWidthProperty, value); }
        }

        public static readonly DependencyProperty IsLabelPanelProperty =
            DependencyProperty.Register("IsLabelPanel", typeof(bool), typeof(EventMarkerPanel),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public bool IsLabelPanel
        {
            get { return (bool)GetValue(IsLabelPanelProperty); }
            set { SetValue(IsLabelPanelProperty, value); }
        }

        public static readonly DependencyProperty LevelSpacingProperty =
            DependencyProperty.Register("LevelSpacing", typeof(double), typeof(EventMarkerPanel),
                new FrameworkPropertyMetadata(25.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

        public double LevelSpacing
        {
            get { return (double)GetValue(LevelSpacingProperty); }
            set { SetValue(LevelSpacingProperty, value); }
        }

        // 마커나 레이블에서 시간값을 가져오기 위한 첨부 속성 (Attached Property)
        public static readonly DependencyProperty TimestampProperty =
            DependencyProperty.RegisterAttached("Timestamp", typeof(double), typeof(EventMarkerPanel),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

        public static double GetTimestamp(UIElement element)
        {
            return (double)element.GetValue(TimestampProperty);
        }

        public static void SetTimestamp(UIElement element, double value)
        {
            element.SetValue(TimestampProperty, value);
        }

        // 마커 레벨 (Neighborhood Stacking)을 뷰에 전달하기 위한 첨부 속성
        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.RegisterAttached("Level", typeof(int), typeof(EventMarkerPanel),
                new FrameworkPropertyMetadata(0));

        public static int GetLevel(UIElement element)
        {
            return (int)element.GetValue(LevelProperty);
        }

        public static void SetLevel(UIElement element, int value)
        {
            element.SetValue(LevelProperty, value);
        }

        private const double BaseY = 0; // 하단 마커 기본 Y 좌표
        private const double LabelPadding = 10; // 레이블 간 최소 간격

        // 자식 요소별 계산 결과를 임시 저장하는 구조체
        private class ChildItem
        {
            public UIElement Element { get; set; }
            public double Timestamp { get; set; }
            public double X { get; set; }
            public double Width { get; set; }
            public int Level { get; set; }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = availableSize.Width;
            if (double.IsInfinity(width)) width = SystemParameters.PrimaryScreenWidth;

            double maxLevelHeight = 0;
            int maxLevel = 0;

            var items = new List<ChildItem>();

            // 1. 모든 자식 요소의 원하는 크기 측정
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double timestamp = GetTimestamp(child);

                double ratio = TotalDuration > 0 ? timestamp / TotalDuration : 0;

                // Slider의 Thumb 이동 범위와 일치하도록 계산 (Thumb의 중심 기준)
                double availableWidth = width - ThumbWidth;
                double xCenter = (ThumbWidth / 2.0) + (ratio * availableWidth);

                items.Add(new ChildItem
                {
                    Element = child,
                    Timestamp = timestamp,
                    X = xCenter,
                    Width = child.DesiredSize.Width
                });
            }

            // 2. 레벨 계산 (Neighborhood Stacking 방지 알고리즘)
            // 시간순 정렬 (먼저 발생한 이벤트부터 배치)
            items = items.OrderBy(i => i.Timestamp).ToList();
            List<List<ChildItem>> levels = new List<List<ChildItem>>();

            foreach (var item in items)
            {
                double myStart = item.X - (item.Width / 2) - LabelPadding;
                double myEnd = item.X + (item.Width / 2) + LabelPadding;

                int targetLevel = 0;
                while (true)
                {
                    if (levels.Count <= targetLevel)
                    {
                        levels.Add(new List<ChildItem>());
                        levels[targetLevel].Add(item);
                        item.Level = targetLevel;
                        break;
                    }

                    // 현재 레벨에 겹치는 요소가 있는지 확인
                    bool conflict = false;
                    foreach (var existing in levels[targetLevel])
                    {
                        double exStart = existing.X - (existing.Width / 2) - LabelPadding;
                        double exEnd = existing.X + (existing.Width / 2) + LabelPadding;

                        // 범위가 겹치면 충돌
                        if (!(myEnd < exStart || myStart > exEnd))
                        {
                            conflict = true;
                            break;
                        }
                    }

                    if (!conflict)
                    {
                        levels[targetLevel].Add(item);
                        item.Level = targetLevel;
                        break;
                    }

                    targetLevel++;
                }

                maxLevelHeight = Math.Max(maxLevelHeight, item.Element.DesiredSize.Height);
                maxLevel = Math.Max(maxLevel, item.Level);
            }

            // 필요한 최소 높이 반환
            double requiredHeight = maxLevelHeight;
            if (IsLabelPanel)
            {
                if (items.Count > 0)
                    requiredHeight = (maxLevel + 1) * LevelSpacing + 5; // 약간의 여백 추가
                else
                    requiredHeight = 0;
            }

            return new Size(width, requiredHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double width = finalSize.Width;

            var items = new List<ChildItem>();

            foreach (UIElement child in InternalChildren)
            {
                double timestamp = GetTimestamp(child);
                double ratio = TotalDuration > 0 ? timestamp / TotalDuration : 0;

                // Slider의 Thumb 이동 범위와 일치하도록 계산 (Thumb의 중심 기준)
                double availableWidth = width - ThumbWidth;
                double xCenter = (ThumbWidth / 2.0) + (ratio * availableWidth);

                items.Add(new ChildItem
                {
                    Element = child,
                    Timestamp = timestamp,
                    X = xCenter,
                    Width = child.DesiredSize.Width
                });
            }

            // 안정적인 정렬을 위해 Timestamp 오름차순
            items = items.OrderBy(i => i.Timestamp).ToList();
            List<List<ChildItem>> levels = new List<List<ChildItem>>();

            foreach (var item in items)
            {
                double myStart = item.X - (item.Width / 2) - LabelPadding;
                double myEnd = item.X + (item.Width / 2) + LabelPadding;

                int targetLevel = 0;
                while (true)
                {
                    if (levels.Count <= targetLevel)
                    {
                        levels.Add(new List<ChildItem>());
                        levels[targetLevel].Add(item);
                        item.Level = targetLevel;
                        break;
                    }

                    bool conflict = false;
                    foreach (var existing in levels[targetLevel])
                    {
                        double exStart = existing.X - (existing.Width / 2) - LabelPadding;
                        double exEnd = existing.X + (existing.Width / 2) + LabelPadding;

                        // 겹침 여부 (교집합)
                        if (Math.Max(myStart, exStart) < Math.Min(myEnd, exEnd))
                        {
                            conflict = true;
                            break;
                        }
                    }

                    if (!conflict)
                    {
                        levels[targetLevel].Add(item);
                        item.Level = targetLevel;
                        break;
                    }
                    targetLevel++;
                }

                // 좌표 결정
                double finalX = item.X - (item.Element.DesiredSize.Width / 2);

                // Y: 패널 내에선 최하단에 정렬 (실제 수직 스태킹은 자식 템플릿의 Level 바인딩이 처리)
                double finalY = finalSize.Height - item.Element.DesiredSize.Height;

                if (IsLabelPanel)
                {
                    // 레벨 0이 가장 아래(타임라인에 가깝게), 레벨이 높아질수록 위로 올라감
                    finalY = finalSize.Height - ((item.Level + 1) * LevelSpacing) + (LevelSpacing - item.Element.DesiredSize.Height) / 2;
                }

                item.Element.Arrange(new Rect(finalX, finalY, item.Element.DesiredSize.Width, item.Element.DesiredSize.Height));

                SetLevel(item.Element, item.Level);
            }

            return finalSize;
        }
    }

    public static class ScrollViewerBehavior
    {
        public static readonly DependencyProperty AutoScrollToBottomProperty =
            DependencyProperty.RegisterAttached("AutoScrollToBottom", typeof(bool), typeof(ScrollViewerBehavior), new PropertyMetadata(false, AutoScrollToBottomChanged));

        public static bool GetAutoScrollToBottom(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoScrollToBottomProperty);
        }

        public static void SetAutoScrollToBottom(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoScrollToBottomProperty, value);
        }

        private static void AutoScrollToBottomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                if ((bool)e.NewValue)
                {
                    scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                    // Initial scroll to bottom
                    scrollViewer.ScrollToBottom();
                }
                else
                {
                    scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
                }
            }
        }

        private static void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange != 0)
            {
                var scrollViewer = sender as ScrollViewer;
                scrollViewer?.ScrollToBottom();
            }
        }
    }
}
