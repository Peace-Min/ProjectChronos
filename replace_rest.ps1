$content = Get-Content -Path 'ProjectChronos\ViewModels\ReportTimelineExportViewModel.cs' -Raw

# 1. MeasureRequiredDetailFieldWidth -> MeasureRequiredCardContentWidth description
$pattern1 = '(?s)requiredWidth = Math\.Max\(\s*requiredWidth,\s*MeasureRequiredDetailFieldWidth\(\s*simulationEvent\.DescriptionLabel,\s*simulationEvent\.Description,\s*labelFontSize,\s*valueFontSize\)\);'
$replacement1 = @"
            // [Description 누락] 높이 축소를 위해 계산에서 배제
            // requiredWidth = Math.Max(
            //     requiredWidth,
            //     MeasureRequiredDetailFieldWidth(
            //         simulationEvent.DescriptionLabel,
            //         simulationEvent.Description,
            //         labelFontSize,
            //         valueFontSize));
"@
$content = [regex]::Replace($content, $pattern1, $replacement1)

# 2. BuildDetailFields
$pattern2 = '(?s)AppendDetailField\(\s*fields,\s*simulationEvent\.DescriptionLabel,\s*simulationEvent\.Description,\s*fieldWidth,\s*labelFontSize,\s*valueFontSize,\s*ref totalHeight\);'
$replacement2 = @"
            // [Description 누락] 렌더링에서 배제하여 카드뷰 높이 축소
            // AppendDetailField(
            //     fields,
            //     simulationEvent.DescriptionLabel,
            //     simulationEvent.Description,
            //     fieldWidth,
            //     labelFontSize,
            //     valueFontSize,
            //     ref totalHeight);
"@
$content = [regex]::Replace($content, $pattern2, $replacement2)

# 3. BuildIntervalItems
$pattern3 = '(?s)double top = absoluteLineY - labelSize\.Height - \(16\.0 \* layoutScale\);.*?Width = width\s*\}\);'
$replacement3 = @"
                // 텍스트가 점선을 밟고 넘어가는 듯한 Middle Baseline Alignment
                double textCenteringYOffset = labelSize.Height / 2.0;
                double top = absoluteLineY - textCenteringYOffset;
                
                double baselineLocal = Math.Max(0.0, anchorBottomAbsolute - top);
                double lineYLocal = absoluteLineY - top;
                double labelLeftAbsolute = ((visualStart + visualEnd) / 2.0) - (labelSize.Width / 2.0);
                double left = Math.Min(visualStart, labelLeftAbsolute) - (18.0 * layoutScale);
                double right = Math.Max(visualEnd, labelLeftAbsolute + labelSize.Width) + (18.0 * layoutScale);
                double width = Math.Max(1.0, right - left);
                
                double lineStartLocal = visualStart - left;
                double lineEndLocal = visualEnd - left;
                double arrowStartLocal = lineStartLocal + anchorGap;
                double arrowEndLocal = lineEndLocal - anchorGap;
                
                Geometry generatedArrowGeo;
                Geometry generatedFrameGeo;
                Brush targetStrokeBrush = GetIntervalBrush(index);
                bool showChevronHeads = false;

                if (laneKind == SlotIntervalLaneKind.Micro)
                {
                    // [Micro Account] 마이크로 계층 표시
                    // 펜 색상은 보라색, 가로 화살표 생략, 대신 수직 점선으로 길게 뻗음
                    targetStrokeBrush = CreateBrush(0x7E, 0x2F, 0x8E); // MATLAB Purple
                    generatedArrowGeo = Geometry.Empty;
                    
                    // 더 높은 곳으로 수직선 연장.
                    double vertExtend = 60.0 * layoutScale;
                    top -= vertExtend;
                    lineYLocal += vertExtend; // relative to new top
                    baselineLocal += vertExtend;

                    double centerLocal = ((visualStart + visualEnd) / 2.0) - left;
                    
                    // 수직 점선 
                    string vertPathStr = string.Format(System.Globalization.CultureInfo.InvariantCulture, `"M {0:0.##},{1:0.##} L {0:0.##},{2:0.##}`", centerLocal, lineYLocal, lineYLocal - vertExtend);
                    generatedFrameGeo = Geometry.Parse(vertPathStr);
                }
                else
                {
                    double availableArrowSpan = Math.Max(0.0, arrowEndLocal - arrowStartLocal);
                    showChevronHeads = availableArrowSpan > BaseIntervalChevronThreshold;
                    generatedArrowGeo = showChevronHeads ? BuildArrowGeometry(arrowStartLocal, arrowEndLocal, lineYLocal) : Geometry.Empty;
                    generatedFrameGeo = BuildIntervalFrameGeometry(arrowStartLocal, arrowEndLocal, lineYLocal);
                }

                IntervalItems.Add(new ReportTimelineSlotIntervalItem
                {
                    ArrowGeometry = generatedArrowGeo,
                    FontSize = fontSize,
                    FrameGeometry = generatedFrameGeo,
                    Height = Math.Max(1.0, baselineLocal + (8.0 * layoutScale)),
                    Label = label,
                    LabelLeft = labelLeftAbsolute - left,
                    Left = left,
                    ShowChevronHeads = showChevronHeads,
                    Stroke = targetStrokeBrush,
                    Top = top,
                    Width = width
                });
"@
$content = [regex]::Replace($content, $pattern3, $replacement3)

# 4. BuildDetailColumnLefts
$pattern4 = '(?s)private List<double> BuildDetailColumnLefts\(.*?double maximumLeft = Math\.Max\(minimumLeft, TimelineWidth - cardWidth - outerMargin\);.*?return lefts;\s*\}'
$replacement4 = @"
private List<double> BuildDetailColumnLefts(
            IReadOnlyList<SlotGroup> groups,
            double cardWidth,
            double minimumColumnGap,
            double outerMargin)
        {
            var mergedLefts = new List<double>(groups.Count);
            double searchToleranceArea = cardWidth + minimumColumnGap;
            
            int index = 0;
            while(index < groups.Count)
            {
                int endClusterIdx = index;
                // 군집 판별: 노드간 센터 차이가 겹침허용범위 내에 있으면 동일 스택 덩어리로 간주
                while(endClusterIdx + 1 < groups.Count && (groups[endClusterIdx + 1].CenterX - groups[endClusterIdx].CenterX) < searchToleranceArea)
                {
                    endClusterIdx++;
                }

                double sumOfCenters = 0.0;
                for(int j = index; j <= endClusterIdx; j++) 
                {
                    sumOfCenters += groups[j].CenterX;
                }
                
                double clusterMergeAverageX = sumOfCenters / (endClusterIdx - index + 1);
                double clusteredCardLeft = clusterMergeAverageX - (cardWidth / 2.0);
                
                // 스택 내의 모든 이벤트를 완벽하게 세로 1열로 떨어뜨림
                for(int j = index; j <= endClusterIdx; j++) 
                {
                    mergedLefts.Add(clusteredCardLeft);
                }

                index = endClusterIdx + 1;
            }

            // 양 옆 Margin 범위를 벗어나지 않게 클리핑 가드
            double boundaryMinLeft = outerMargin;
            double boundaryMaxLeft = Math.Max(boundaryMinLeft, TimelineWidth - cardWidth - outerMargin);
            
            for(int j = 0; j < mergedLefts.Count; j++) 
            {
                mergedLefts[j] = Clamp(mergedLefts[j], boundaryMinLeft, boundaryMaxLeft);
            }

            return mergedLefts;
        }
"@
$content = [regex]::Replace($content, $pattern4, $replacement4)

Set-Content -Path 'ProjectChronos\ViewModels\ReportTimelineExportViewModel.cs' -Value $content -Encoding UTF8
Write-Output "Done replacing rest"
