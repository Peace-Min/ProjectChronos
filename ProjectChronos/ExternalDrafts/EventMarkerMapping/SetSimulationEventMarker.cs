// =====================================================================================
//  SetSimulationEventMarker — EventCollector(DB) → SimulationEventMarker 매핑 (실 SW용)
// -------------------------------------------------------------------------------------
//  ※ 참조/이식용 파일. 이 리포(ProjectChronos)에서는 컴파일되지 않는다.
//    (CScenarioInfo / SimulationFrame / TableConfig / SingleSimAppConst 등은 실 SW 타입)
//    ExternalDrafts 하위라 메인 csproj·PlaybackRefactorDraft.csproj 어디에도 포함되지 않음.
//    실 SW의 해당 ViewModel 클래스에 이 메서드를 병합해서 사용할 것.
//
//  [무엇을 바꿨나 — 원본 source.txt 대비]
//    - 제거: AppConst.EventCollector_TimeAttribute / _DescriptionAttribute /
//            _RangeBtwPlatformAndTargetAttribute / _Source 4개 필드명 분기 + 관련 상수.
//    - 추가: s_time 도메인(모든 프레임 s_time 집합, 0 제외). 시간필드 자동판정용.
//    - 교체: 필드별 분기 → (1) s_time 도메인 매칭 = Timestamp 흡수(전시 드랍)
//                          (2) 나머지 저널링 필드 = Fields 에 라벨-값.
//    - 메서드 뼈대(EventCollector 조회·저널링 필터·최종 행·foreach·Add)는 원본 그대로.
//
//  [실제 DB 구조 반영 — 제공받은 3개 CSV 기준]
//    object_info : EventCollector → Object_Table_3
//    column_info : 3레벨 계층(평탄화)
//        Lv1 EventCollector(테이블)
//         └ Lv2 이벤트객체(mEventHit / mEventLaunchAuthorization / mEventMissileLaunch /
//                          mEventSearchRD / mEventTrackingRD)   ← filterdAttributes
//            └ Lv3 4필드(strEventDescription / dEventTime / dRangeBtwPlatformAndTarget /
//                        strSource)                              ← attribute.attributeInfo
//        attribute_name(예 "mEventHit.dEventTime") ↔ COLn 매핑은 SW 테이블 계층(GetTable)이 처리.
//        → 이 메서드는 속성경로로 접근하고 COLn 을 직접 만지지 않는다(원본과 동일).
//    table3      : s_time(식별자 키) + 값 누적. dEventTime == 발생 프레임의 s_time.
//                  ("200.64" 와 "200.63999999999999" 는 포맷만 다른 동일 double → 정확 비교 성립)
//
//  [검증] table3 실제 마지막 행 데이터로 재현 시 5개 마커가 정확히 생성됨(Timestamp/Fields/드랍).
//    Hit@215.38, LaunchAuth@204.39, MissileLaunch@204.39, SearchRD@200.64, TrackingRD@204.38
//
//  [실 SW쪽 전제]
//    - SimulationEventMarker 에 List<EventMarkerField> Fields 와 EventMarkerField(label,value) 존재
//      (이 리포에서 이미 적용한 제네릭 Fields 전환과 동일).
//    - row.Get<double>("s_time") 의 "s_time" 키는 실제 EventCollector 테이블 스키마명에 맞출 것.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExternalReference.EventMarkerMapping
{
    // ※ 실제로는 실 SW의 해당 ViewModel(SimulationEventMarkers 컬렉션을 가진 클래스)에 병합.
    //    아래는 병합 대상 시그니처를 보여주기 위한 placeholder 컨테이너일 뿐이다.
    internal partial class EventMarkerMappingReference
    {
        private void SetSimulationEventMarker(CScenarioInfo scenarioInfo, IEnumerable<SimulationFrame> simulationFrames)
        {
            // 1. 현재 시나리오에 설정된 EventCollector_COMPONENT 정보를 읽어온다.
            //    이벤트수집기는 구조체 단위 강제 저널링이라 ScenarioQueries 대신 직접 파싱.
            var eventCollectorPlayer = scenarioInfo.playerObjectMap.Values
                .FirstOrDefault(v => v.componentName == SingleSimAppConst.EventCollector_COMMPONENT)
                ?? throw new ArgumentNullException($"{SingleSimAppConst.EventCollector_COMMPONENT} 컴포넌트와 일치하는 시나리오 플레이어가 존재하지 않습니다.");

            var eventCollectorConfig = new TableConfig(eventCollectorPlayer.label);
            var filterdAttributes = eventCollectorPlayer.playerObjectAttributeValueMap.Values
                .Where(attribute => attribute.isJournaling == true)
                .ToList();
            var tableObjectName = eventCollectorPlayer.playerObjectName;

            // 이벤트가 누적된 최종 행(각 이벤트의 dEventTime이 이 행에 모두 존재).
            var eventCollectorRow = simulationFrames
                .Select(item => item.GetTable(tableObjectName))
                .Where(item => item != null)
                .LastOrDefault();

            // [추가] s_time 도메인 = 모든 프레임의 s_time 값 집합 (0=빈 baseline 프레임 제외).
            //   dEventTime은 자신이 발생한 프레임의 s_time과 값이 정확히 일치하므로,
            //   "값이 s_time 도메인에 속하는 숫자 필드 = 시간필드"로 식별한다(필드명 하드코딩 제거).
            //   0을 제외하지 않으면 최종 행의 값이 0인 숫자 필드가 시간필드로 오판정될 수 있다.
            var sTimeDomain = new HashSet<double>(
                simulationFrames
                    .Select(item => item.GetTable(tableObjectName))
                    .Where(item => item != null)
                    .Select(row => row.Get<double>("s_time"))   // ※ "s_time" 키는 실제 스키마에 맞게
                    .Where(t => t != 0.0));

            foreach (var attribute in filterdAttributes)
            {
                var addEvent = new SimulationEventMarker()
                {
                    Title = attribute.Label,
                    ObjectName = attribute.Name,
                };

                foreach (var child in attribute.attributeInfo)
                {
                    var isJournaling = child.Value.isJournaling;
                    var fieldName = $"{child.Value.ParentNode.Name}.{child.Value.attributeName}";
                    var label = child.Value.Label;

                    // 원시 값 조회 후 문화권 불변 문자열로 정규화.
                    var rawValue = eventCollectorRow[fieldName];
                    var text = Convert.ToString(rawValue, CultureInfo.InvariantCulture);

                    // (1) 시간필드 자동판정: 값이 s_time 도메인에 속하면 그 필드가 dEventTime.
                    //     → Timestamp(마커 위치)로 흡수하고 전시(Fields)에는 넣지 않는다.
                    if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands,
                                        CultureInfo.InvariantCulture, out var numeric)
                        && sTimeDomain.Contains(numeric))
                    {
                        addEvent.Timestamp = numeric;
                    }
                    // (2) 그 외 저널링 필드는 "어떤 필드인지 특정하지 않고" 라벨-값으로 전시.
                    else if (isJournaling && !string.IsNullOrWhiteSpace(text))
                    {
                        addEvent.Fields.Add(new EventMarkerField(label, text));
                    }
                }

                SimulationEventMarkers.Add(addEvent);
            }
        }
    }
}
