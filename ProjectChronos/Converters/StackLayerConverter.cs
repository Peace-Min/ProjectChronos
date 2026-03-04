using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using ProjectChronos.Models;

namespace ProjectChronos.Converters
{
    /// <summary>
    /// 타임라인 마커들의 픽셀 기준 충돌 여부를 계산하여 Canvas.Top 오프셋을 반환합니다.
    ///
    /// [핵심 설계 원칙]
    /// - 겹침 판단 기준: 고정 시간차가 아닌 실제 렌더링 픽셀 거리 기반
    ///   → 동일한 0.1초 차이라도 전체 300초 구간이면 1px 안에 들어오지만,
    ///     10초 구간이면 충분히 떨어져서 겹칠 필요가 없음
    ///
    /// [엣지 케이스 대응]
    /// - 2~MaxVisibleLayers(기본3)개 겹침: 각각 layerStep(기본22px)씩 위로 스택
    /// - MaxVisibleLayers개 초과 겹침: 최대 높이(MaxVisibleLayers * layerStep)에서
    ///   더 이상 위로 쌓지 않고 같은 Top을 공유 → 무한 height 성장 원천 차단
    ///   (마커 자체가 원형이므로 겹쳐서 쌓여도 위치를 클릭하면 각 Popup이 열림)
    /// </summary>
    public class StackLayerConverter : IMultiValueConverter
    {
        /// <summary>마커 하나의 너비(px). 이보다 가까운 마커끼리는 겹친다고 판단.</summary>
        public double MarkerWidthPx { get; set; } = 22.0;

        /// <summary>레이어 간 세로 간격(px).</summary>
        public double LayerStepPx { get; set; } = 22.0;

        /// <summary>최대 허용 스택 레이어 수 - 이 수 초과 시 마지막 레이어에 클램프</summary>
        public int MaxVisibleLayers { get; set; } = 3;

        /// <summary>
        /// values[0]: 현재 마커 그룹의 Timestamp (double)
        /// values[1]: 전체 마커 그룹 컬렉션 (IEnumerable of SimulationMarkerGroup)
        /// values[2]: TotalDuration (double)
        /// values[3]: Canvas의 ActualWidth (double)
        /// 반환: TranslateTransform.Y에 적용할 음수 오프셋 (0 = 겹침 없음, -22 = 레이어 1, -44 = 레이어 2...)
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 4) return 0.0;

            if (!(values[0] is double)) return 0.0;
            double myTimestamp = (double)values[0];

            if (!(values[2] is double) || (double)values[2] <= 0) return 0.0;
            double totalDuration = (double)values[2];

            if (!(values[3] is double) || (double)values[3] <= 0) return 0.0;
            double canvasWidth = (double)values[3];

            // 전체 마커 그룹 목록 추출
            var allGroups = ExtractGroups(values[1]);
            if (allGroups.Count == 0) return 0.0;

            // 현재 마커의 픽셀 X 위치 계산
            double myPixelX = (myTimestamp / totalDuration) * canvasWidth;

            // 픽셀 기준으로 겹치는 마커 그룹을 수집하고 Timestamp 순서로 정렬
            var collisionGroup = allGroups
                .Where(g =>
                {
                    double gPixelX = (g.Timestamp / totalDuration) * canvasWidth;
                    return Math.Abs(gPixelX - myPixelX) < MarkerWidthPx;
                })
                .OrderBy(g => g.Timestamp)
                .ToList();

            // 충돌 그룹 내에서 현재 마커의 순서(레이어 인덱스) 결정
            int layerIndex = 0;
            for (int i = 0; i < collisionGroup.Count; i++)
            {
                if (Math.Abs(collisionGroup[i].Timestamp - myTimestamp) < 1e-9)
                {
                    layerIndex = i;
                    break;
                }
            }

            int clampedLayer = Math.Min(layerIndex, MaxVisibleLayers - 1);

            // 위로 쌓일수록 음수 오프셋 반환 (TranslateTransform.Y에 적용)
            // Layer 0: 0 (트랙 위치 유지), Layer 1: -22px, Layer 2: -44px
            return -(clampedLayer * LayerStepPx);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        private List<SimulationMarkerGroup> ExtractGroups(object rawValue)
        {
            var result = new List<SimulationMarkerGroup>();
            if (rawValue is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is SimulationMarkerGroup group)
                        result.Add(group);
                }
            }
            return result;
        }
    }
}
