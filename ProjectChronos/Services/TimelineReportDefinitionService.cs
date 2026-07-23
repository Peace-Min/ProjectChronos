using System;
using System.Collections.Generic;
using System.IO;
using ProjectChronos.Models;
using ProjectChronos.ViewModels;

namespace ProjectChronos.Services
{
    public class TimelineReportDefinitionService
    {
        public const double DefaultScenarioDurationSeconds = 300.0;
        public const double DefaultReportCanvasWidth = 1920.0;

        public string GetDefaultPrototypeExportPath()
        {
            return Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "output",
                "report_timeline_prototype.png"));
        }

        public string GetDefaultRealDataExportPath()
        {
            return Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..",
                "..",
                "output",
                "report_timeline_realdata_visibility.png"));
        }

        public TimelineReportExportInput CreatePrototypeReportExportInput(string outputPath)
        {
            return CreateReportExportInput(CreateScenarioEvents(), outputPath);
        }

        public TimelineReportExportInput CreateRealDataReportExportInput(string outputPath)
        {
            return CreateReportExportInput(CreateRealDataVisibilityEvents(), outputPath);
        }

        public TimelineReportExportInput CreateReportExportInput(IReadOnlyList<SimulationEventMarker> events, string outputPath, double timeResolution = 0.01)
        {
            return new TimelineReportExportInput(
                "\uADF8\uB798\uD504 \uC608\uC2DC 3",
                "Time-Event",
                Array.Empty<string>(),
                DefaultReportCanvasWidth,
                0,
                events ?? Array.Empty<SimulationEventMarker>(),
                outputPath,
                timeResolution);
        }

        public IReadOnlyList<SimulationEventMarker> CreateRealDataVisibilityEvents()
        {
            return new List<SimulationEventMarker>
            {
                CreateReportEvent(
                    200.64,
                    EventPriority.Low,
                    "mEventSearchRD",
                    "strEventDescription",
                    null,
                    "dRangeBtwPlatformAndTarget",
                    "10396.433261655855",
                    "strSource",
                    "HG001"),
                CreateReportEvent(
                    213.13,
                    EventPriority.Medium,
                    "mEventTrackingRD",
                    "strEventDescription",
                    null,
                    "dRangeBtwPlatformAndTarget",
                    "6969.8308505605364",
                    "strSource",
                    "HG001"),
                CreateReportEvent(
                    213.14,
                    EventPriority.Medium,
                    "mEventLaunchAuthorization",
                    "strEventDescription",
                    null,
                    "dRangeBtwPlatformAndTarget",
                    "6975.3416944586925",
                    "strSource",
                    "HG001"),
                CreateReportEvent(
                    213.14,
                    EventPriority.High,
                    "mEventMissileLaunch",
                    "strEventDescription",
                    null,
                    "dRangeBtwPlatformAndTarget",
                    "6975.3416944586925",
                    "strSource",
                    "HG001"),
                CreateReportEvent(
                    221.71,
                    EventPriority.High,
                    "mEventHit",
                    "strEventDescription",
                    null,
                    "dRangeBtwPlatformAndTarget",
                    "204.46276181477023",
                    "strSource",
                    "HG001")
            };
        }

        public IReadOnlyList<SimulationEventMarker> CreateScenarioEvents()
        {
            return new List<SimulationEventMarker>
            {
                CreateScenarioEvent(
                    255.80,
                    EventPriority.Low,
                    "탐색레이더",
                    "탐지 결과",
                    "탐색레이더가 원거리 표적을 최초 탐지하고 위협 트랙 생성을 시작했습니다.",
                    "148.0 km",
                    "탐색레이더 -> 위협 표적"),
                CreateScenarioEvent(
                    256.35,
                    EventPriority.Low,
                    "EO 감시",
                    "센서 보강",
                    "EO 센서가 동일 표적 방향에서 열원을 확보해 탐지 신뢰도를 보강했습니다.",
                    "146.5 km",
                    "EO 센서 -> 위협 표적"),
                CreateScenarioEvent(
                    257.10,
                    EventPriority.Low,
                    "IFF 조회",
                    "식별 상태",
                    "식별장비 질의 결과 우군 응답이 없어 미식별 표적으로 분류했습니다.",
                    "145.9 km",
                    "IFF 시스템 -> 미식별 표적"),
                CreateScenarioEvent(
                    258.20,
                    EventPriority.Medium,
                    "위협평가",
                    "평가 상태",
                    "전술판단기가 접근 속도와 진입 방향을 기준으로 고위험 교전 후보로 승격했습니다.",
                    "143.2 km",
                    "전술판단기 -> 교전 후보"),

                CreateScenarioEvent(
                    262.40,
                    EventPriority.Medium,
                    "추적레이더",
                    "추적 상태",
                    "추적레이더가 표적을 정밀 추적 모드로 전환하고 항적 갱신 주기를 높였습니다.",
                    "109.7 km",
                    "추적레이더 -> 위협 표적"),
                CreateScenarioEvent(
                    263.10,
                    EventPriority.Medium,
                    "항적융합",
                    "융합 결과",
                    "센서융합기가 탐색레이더와 EO 데이터를 결합해 단일 항적 번호를 확정했습니다.",
                    "108.4 km",
                    "센서융합기 -> 항적 A-17"),
                CreateScenarioEvent(
                    264.05,
                    EventPriority.Medium,
                    "교전계획",
                    "할당 상태",
                    "교전관리기가 대응 자산을 선정하고 단일 요격체 투입 계획을 수립했습니다.",
                    "106.2 km",
                    "교전관리기 -> 요격체 1번"),
                CreateScenarioEvent(
                    265.00,
                    EventPriority.Medium,
                    "사격통제준비",
                    "준비 상태",
                    "사격통제기가 초기 발사 해와 비행 경로 제약 조건을 계산 완료했습니다.",
                    "103.9 km",
                    "사격통제기 -> 요격체 1번"),

                CreateScenarioEvent(
                    267.17,
                    EventPriority.Medium,
                    "발사승인",
                    "승인 상태",
                    "교전통제기가 안전 구역과 위협도를 재확인한 뒤 발사를 승인했습니다.",
                    "41.2 km",
                    "교전통제기 -> 요격체 1번"),
                CreateScenarioEvent(
                    268.05,
                    EventPriority.High,
                    "발사",
                    "발사 상태",
                    "요격체가 발사돼 초기 가속 구간에 진입했고 비행 제어가 정상 시작됐습니다.",
                    "41.2 km",
                    "요격체 1번 -> 위협 표적"),
                CreateScenarioEvent(
                    269.10,
                    EventPriority.High,
                    "데이터링크 연동",
                    "연동 상태",
                    "지휘통제 링크가 연결돼 비행 중 표적 갱신 정보를 수신하기 시작했습니다.",
                    "39.7 km",
                    "데이터링크 -> 요격체 1번"),
                CreateScenarioEvent(
                    270.15,
                    EventPriority.High,
                    "중간유도 개시",
                    "유도 상태",
                    "유도컴퓨터가 예측 충돌점을 기준으로 중간유도 명령을 송신했습니다.",
                    "34.8 km",
                    "유도컴퓨터 -> 위협 표적"),

                CreateScenarioEvent(
                    277.90,
                    EventPriority.High,
                    "궤적보정",
                    "보정 상태",
                    "말기 구간 진입 직전 마지막 경로 보정이 수행돼 오차 범위를 축소했습니다.",
                    "12.6 km",
                    "유도컴퓨터 -> 위협 표적"),
                CreateScenarioEvent(
                    279.05,
                    EventPriority.High,
                    "종말유도전환",
                    "전환 상태",
                    "시커헤드가 표적을 재획득하고 종말유도 모드로 전환했습니다.",
                    "5.4 km",
                    "시커헤드 -> 위협 표적"),
                CreateScenarioEvent(
                    280.20,
                    EventPriority.High,
                    "요격",
                    "교전 결과",
                    "요격체가 표적에 도달해 최종 요격을 완료했고 후속 추적에서도 파편군만 확인됐습니다.",
                    "0.6 km",
                    "요격체 1번 -> 위협 표적")
            };
        }

        private static SimulationEventMarker CreateReportEvent(
            double timestamp,
            EventPriority priority,
            string title,
            string descriptionLabel,
            string description,
            string rangeLabel,
            string rangeValue,
            string sourceLabel,
            string source)
        {
            // ─── [ROLLBACK] 구 고정필드 생성 (Fields 전환으로 폐기) ───
            /*
            return new SimulationEventMarker
            {
                Timestamp = timestamp,
                Priority = priority,
                Title = title,
                DescriptionLabel = descriptionLabel,
                Description = description,
                RangeBTWLabel = rangeLabel,
                RangeBTW = rangeValue,
                SourceLabel = sourceLabel,
                Source = source
            };
            */
            // ─── [ROLLBACK] 끝 ───
            var fields = new List<EventMarkerField>();
            AddField(fields, descriptionLabel, description);
            AddField(fields, rangeLabel, rangeValue);
            AddField(fields, sourceLabel, source);
            return new SimulationEventMarker
            {
                Timestamp = timestamp,
                Priority = priority,
                Title = title,
                Fields = fields
            };
        }

        private static void AddField(List<EventMarkerField> fields, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(label) && !string.IsNullOrWhiteSpace(value))
            {
                fields.Add(new EventMarkerField(label, value));
            }
        }

        private static SimulationEventMarker CreateScenarioEvent(
            double timestamp,
            EventPriority priority,
            string title,
            string descriptionLabel,
            string description,
            string rangeBetween,
            string source)
        {
            // ─── [ROLLBACK] 구 고정필드 생성 (Fields 전환으로 폐기) ───
            /*
            return new SimulationEventMarker
            {
                Timestamp = timestamp,
                Priority = priority,
                Title = title,
                DescriptionLabel = descriptionLabel,
                Description = description,
                RangeBTWLabel = "타겟간 거리",
                RangeBTW = rangeBetween,
                SourceLabel = "소스 타겟",
                Source = source
            };
            */
            // ─── [ROLLBACK] 끝 ───
            var fields = new List<EventMarkerField>();
            AddField(fields, descriptionLabel, description);
            AddField(fields, "타겟간 거리", rangeBetween);
            AddField(fields, "소스 타겟", source);
            return new SimulationEventMarker
            {
                Timestamp = timestamp,
                Priority = priority,
                Title = title,
                Fields = fields
            };
        }
    }
}
