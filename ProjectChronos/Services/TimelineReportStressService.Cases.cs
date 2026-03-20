using System;
using System.Collections.Generic;
using System.Globalization;
using ProjectChronos.Models;

namespace ProjectChronos.Services
{
    public sealed partial class TimelineReportStressService
    {
        private IEnumerable<StressCaseSpec> BuildDeterministicCases(TimelineReportStressOptions options)
        {
            foreach (double width in options.DeterministicWidths)
            {
                foreach (ProfileDefinition profile in GetProfiles())
                {
                    foreach (int uniqueGroupCount in options.DeterministicUniqueGroupCounts)
                    {
                        foreach (int duplicateDepth in options.DeterministicDuplicateDepths)
                        {
                            yield return CreateDeterministicCase(width, uniqueGroupCount, duplicateDepth, profile);
                        }
                    }
                }
            }
        }

        private IEnumerable<StressCaseSpec> BuildRandomCases(TimelineReportStressOptions options)
        {
            var profiles = new List<ProfileDefinition>(GetProfiles());

            int seedStart = options.RandomSeedStart <= 0 ? 1 : options.RandomSeedStart;
            int seedEnd = seedStart + Math.Max(0, options.RandomSeedCount - 1);

            for (int seed = seedStart; seed <= seedEnd; seed++)
            {
                foreach (double width in options.RandomWidths)
                {
                    foreach (int uniqueGroupCount in options.RandomUniqueGroupCounts)
                    {
                        int randomSeed = unchecked((seed * 7919) + ((int)Math.Round(width) * 397) + (uniqueGroupCount * 53));
                        var random = new Random(randomSeed);
                        ProfileDefinition profile = profiles[random.Next(profiles.Count)];
                        List<double> timestamps = BuildRandomTimestamps(profile.Token, uniqueGroupCount, random);
                        var events = new List<SimulationEventMarker>();
                        int maximumStack = 1;

                        for (int groupIndex = 0; groupIndex < timestamps.Count; groupIndex++)
                        {
                            int duplicateDepth = random.Next(1, 7);
                            maximumStack = Math.Max(maximumStack, duplicateDepth);

                            for (int stackIndex = 0; stackIndex < duplicateDepth; stackIndex++)
                            {
                                events.Add(CreateRandomEvent(profile, groupIndex, stackIndex, timestamps[groupIndex], random));
                            }
                        }

                        yield return new StressCaseSpec
                        {
                            CaseId = string.Format(
                                CultureInfo.InvariantCulture,
                                "rand_s{0:00}_{1}_w{2}_g{3}_m{4}",
                                seed,
                                profile.Token,
                                FormatWidthToken(width),
                                uniqueGroupCount,
                                maximumStack),
                            Suite = "random",
                            Seed = seed,
                            ProfileName = "랜덤-" + profile.DisplayName,
                            Width = width,
                            UniqueGroupCount = uniqueGroupCount,
                            MaximumTimestampStack = maximumStack,
                            Events = events
                        };
                    }
                }
            }
        }

        private static IEnumerable<ProfileDefinition> GetProfiles()
        {
            yield return new ProfileDefinition("uniform", "균등 분산");
            yield return new ProfileDefinition("micro", "마이크로 간격");
            yield return new ProfileDefinition("cluster", "동일 시각 군집");
            yield return new ProfileDefinition("longfields", "장문 필드");
        }

        private static StressCaseSpec CreateDeterministicCase(double width, int uniqueGroupCount, int duplicateDepth, ProfileDefinition profile)
        {
            List<double> timestamps = BuildDeterministicTimestamps(profile.Token, uniqueGroupCount);
            var events = new List<SimulationEventMarker>(uniqueGroupCount * duplicateDepth);

            for (int groupIndex = 0; groupIndex < timestamps.Count; groupIndex++)
            {
                for (int stackIndex = 0; stackIndex < duplicateDepth; stackIndex++)
                {
                    events.Add(CreateDeterministicEvent(profile, groupIndex, stackIndex, timestamps[groupIndex], uniqueGroupCount));
                }
            }

            return new StressCaseSpec
            {
                CaseId = string.Format(
                    CultureInfo.InvariantCulture,
                    "det_{0}_w{1}_g{2}_d{3}",
                    profile.Token,
                    FormatWidthToken(width),
                    uniqueGroupCount,
                    duplicateDepth),
                Suite = "deterministic",
                Seed = 0,
                ProfileName = profile.DisplayName,
                Width = width,
                UniqueGroupCount = uniqueGroupCount,
                MaximumTimestampStack = duplicateDepth,
                Events = events
            };
        }

        private static List<double> BuildDeterministicTimestamps(string profileToken, int uniqueGroupCount)
        {
            switch (profileToken)
            {
                case "micro":
                    return BuildMicroTimestamps(uniqueGroupCount, 120.0);
                case "cluster":
                    return BuildClusterTimestamps(uniqueGroupCount, 48.0);
                case "longfields":
                    return BuildUniformTimestamps(uniqueGroupCount, 56.0, 190.0);
                default:
                    return BuildUniformTimestamps(uniqueGroupCount, 24.0, 252.0);
            }
        }

        private static List<double> BuildRandomTimestamps(string profileToken, int uniqueGroupCount, Random random)
        {
            switch (profileToken)
            {
                case "micro":
                    return BuildRandomMicroTimestamps(uniqueGroupCount, 90.0 + (random.NextDouble() * 40.0), random);
                case "cluster":
                    return BuildRandomClusterTimestamps(uniqueGroupCount, 28.0 + (random.NextDouble() * 24.0), random);
                case "longfields":
                    return BuildRandomUniformTimestamps(uniqueGroupCount, 36.0 + (random.NextDouble() * 20.0), 160.0 + (random.NextDouble() * 60.0), random);
                default:
                    return BuildRandomUniformTimestamps(uniqueGroupCount, 18.0 + (random.NextDouble() * 20.0), 210.0 + (random.NextDouble() * 50.0), random);
            }
        }

        private static List<double> BuildUniformTimestamps(int count, double start, double span)
        {
            var timestamps = new List<double>(count);
            if (count <= 1)
            {
                timestamps.Add(Math.Round(start + (span / 2.0), 3));
                return timestamps;
            }

            double gap = span / (count - 1);
            for (int index = 0; index < count; index++)
            {
                timestamps.Add(Math.Round(start + (gap * index), 3));
            }

            return timestamps;
        }

        private static List<double> BuildRandomUniformTimestamps(int count, double start, double span, Random random)
        {
            var timestamps = new List<double>(count);
            if (count <= 1)
            {
                timestamps.Add(Math.Round(start + (span / 2.0), 3));
                return timestamps;
            }

            double minimumGap = span / (count * 1.5);
            double current = start;

            for (int index = 0; index < count; index++)
            {
                timestamps.Add(RoundAscendingTimestamp(timestamps, current));
                if (index < count - 1)
                {
                    current += minimumGap + (random.NextDouble() * minimumGap * 0.8);
                }
            }

            return timestamps;
        }

        private static List<double> BuildMicroTimestamps(int count, double start)
        {
            double[] gaps = { 0.08, 0.12, 0.18, 0.27, 0.34, 0.42, 0.56, 0.71, 0.85 };
            var timestamps = new List<double>(count);
            double current = start;

            for (int index = 0; index < count; index++)
            {
                timestamps.Add(Math.Round(current, 3));
                if (index < count - 1)
                {
                    current += gaps[index % gaps.Length];
                }
            }

            return timestamps;
        }

        private static List<double> BuildRandomMicroTimestamps(int count, double start, Random random)
        {
            var timestamps = new List<double>(count);
            double current = start;

            for (int index = 0; index < count; index++)
            {
                timestamps.Add(RoundAscendingTimestamp(timestamps, current));
                if (index < count - 1)
                {
                    current += 0.03 + (random.NextDouble() * 0.92);
                }
            }

            return timestamps;
        }

        private static List<double> BuildClusterTimestamps(int count, double start)
        {
            double[] gaps = { 5.2, 0.18, 6.4, 0.22, 4.7, 0.16, 7.1, 0.24 };
            var timestamps = new List<double>(count);
            double current = start;

            for (int index = 0; index < count; index++)
            {
                timestamps.Add(Math.Round(current, 3));
                if (index < count - 1)
                {
                    current += gaps[index % gaps.Length];
                }
            }

            return timestamps;
        }

        private static List<double> BuildRandomClusterTimestamps(int count, double start, Random random)
        {
            var timestamps = new List<double>(count);
            double current = start;

            for (int index = 0; index < count; index++)
            {
                timestamps.Add(RoundAscendingTimestamp(timestamps, current));
                if (index < count - 1)
                {
                    bool narrowGap = (index % 2 == 1) || random.NextDouble() < 0.35;
                    current += narrowGap
                        ? 0.02 + (random.NextDouble() * 0.35)
                        : 3.5 + (random.NextDouble() * 5.5);
                }
            }

            return timestamps;
        }

        private static double RoundAscendingTimestamp(IList<double> timestamps, double current)
        {
            double rounded = Math.Round(current, 3);
            if (timestamps.Count == 0)
            {
                return rounded;
            }

            double minimumNext = timestamps[timestamps.Count - 1] + 0.002;
            return rounded <= timestamps[timestamps.Count - 1]
                ? Math.Round(minimumNext, 3)
                : rounded;
        }

        private static SimulationEventMarker CreateDeterministicEvent(ProfileDefinition profile, int groupIndex, int stackIndex, double timestamp, int uniqueGroupCount)
        {
            bool isLong = string.Equals(profile.Token, "longfields", StringComparison.OrdinalIgnoreCase);

            return new SimulationEventMarker
            {
                Timestamp = timestamp,
                Priority = ResolvePriority(groupIndex, stackIndex),
                Title = BuildTitle(groupIndex, stackIndex, isLong),
                DescriptionLabel = "탐지 결과",
                Description = BuildDescription(profile.Token, groupIndex, stackIndex, isLong, uniqueGroupCount),
                RangeBTWLabel = "타깃간 거리",
                RangeBTW = string.Format(CultureInfo.InvariantCulture, "{0:0.0} km", Math.Max(0.6, 180.0 - (groupIndex * 5.3) - (stackIndex * 1.2))),
                SourceTargetLabel = "소스 타깃",
                SourceTarget = BuildSourceTarget(profile.Token, groupIndex, stackIndex, isLong)
            };
        }

        private static SimulationEventMarker CreateRandomEvent(ProfileDefinition profile, int groupIndex, int stackIndex, double timestamp, Random random)
        {
            bool forceLong = string.Equals(profile.Token, "longfields", StringComparison.OrdinalIgnoreCase) || random.NextDouble() < 0.25;
            bool includeDescription = random.NextDouble() < 0.8;
            bool includeRange = random.NextDouble() < 0.7;
            bool includeSourceTarget = random.NextDouble() < 0.7;

            if (!includeDescription && !includeRange && !includeSourceTarget)
            {
                switch (random.Next(3))
                {
                    case 0:
                        includeDescription = true;
                        break;
                    case 1:
                        includeRange = true;
                        break;
                    default:
                        includeSourceTarget = true;
                        break;
                }
            }

            return new SimulationEventMarker
            {
                Timestamp = timestamp,
                Priority = ResolvePriority(groupIndex + random.Next(3), stackIndex + random.Next(3)),
                Title = BuildRandomTitle(groupIndex, stackIndex, forceLong, random),
                DescriptionLabel = includeDescription ? PickDescriptionLabel(random) : null,
                Description = includeDescription ? BuildRandomDescription(profile.Token, groupIndex, stackIndex, forceLong, random) : null,
                RangeBTWLabel = includeRange ? "타깃간 거리" : null,
                RangeBTW = includeRange ? string.Format(CultureInfo.InvariantCulture, "{0:0.0} km", 0.5 + (random.NextDouble() * 180.0)) : null,
                SourceTargetLabel = includeSourceTarget ? "소스 타깃" : null,
                SourceTarget = includeSourceTarget ? BuildRandomSourceTarget(profile.Token, groupIndex, stackIndex, forceLong, random) : null
            };
        }

        private static EventPriority ResolvePriority(int groupIndex, int stackIndex)
        {
            int value = Math.Abs(groupIndex + stackIndex) % 3;
            switch (value)
            {
                case 0:
                    return EventPriority.High;
                case 1:
                    return EventPriority.Medium;
                default:
                    return EventPriority.Low;
            }
        }

        private static string BuildTitle(int groupIndex, int stackIndex, bool isLong)
        {
            string[] titles =
            {
                "탐색레이더",
                "추적레이더",
                "위협분류",
                "교전판단",
                "발사승인",
                "발사",
                "요격"
            };

            string title = titles[(groupIndex + stackIndex) % titles.Length];
            return isLong
                ? title + " 세부 상황 분석 시나리오 " + string.Format(CultureInfo.InvariantCulture, "{0:00}", groupIndex + 1)
                : title;
        }

        private static string BuildRandomTitle(int groupIndex, int stackIndex, bool isLong, Random random)
        {
            string[] prefixes =
            {
                "탐색레이더",
                "추적레이더",
                "위협분류",
                "교전판단",
                "발사승인",
                "발사",
                "요격",
                "데이터링크"
            };

            string title = prefixes[(groupIndex + stackIndex + random.Next(prefixes.Length)) % prefixes.Length];
            return isLong
                ? string.Format(CultureInfo.InvariantCulture, "{0} 상세 전개 케이스 {1:00} 단계 {2:00}", title, groupIndex + 1, stackIndex + 1)
                : title;
        }

        private static string BuildDescription(string profileToken, int groupIndex, int stackIndex, bool isLong, int uniqueGroupCount)
        {
            string baseText = string.Format(CultureInfo.InvariantCulture, "표적군 {0:00}에 대한 센서/교전 상태 전환을 기록했습니다.", groupIndex + 1);

            if (string.Equals(profileToken, "micro", StringComparison.OrdinalIgnoreCase))
            {
                baseText = string.Format(CultureInfo.InvariantCulture, "직전 이벤트와 1초 미만 간격으로 이어지는 빠른 전개를 기록했습니다. stack {0:00}.", stackIndex + 1);
            }
            else if (string.Equals(profileToken, "cluster", StringComparison.OrdinalIgnoreCase))
            {
                baseText = string.Format(CultureInfo.InvariantCulture, "동일 시각 묶음 안에서 발생한 중첩 이벤트 {0:00}를 표시합니다.", stackIndex + 1);
            }

            return isLong
                ? baseText + " MATLAB 사용자가 읽을 때 카드 폭이 줄어들어도 핵심 의미가 유지되는지 확인하기 위해 설명 문자열을 의도적으로 길게 구성했습니다. 전체 그룹 수는 " + uniqueGroupCount.ToString(CultureInfo.InvariantCulture) + "개입니다."
                : baseText;
        }

        private static string BuildRandomDescription(string profileToken, int groupIndex, int stackIndex, bool isLong, Random random)
        {
            string[] fragments =
            {
                "센서 상태를 갱신했습니다.",
                "표적 추적 품질이 상승했습니다.",
                "교전 가능 판단이 갱신되었습니다.",
                "유도 명령이 재할당되었습니다.",
                "운용자 확인 단계가 완료되었습니다."
            };

            string description = string.Format(
                CultureInfo.InvariantCulture,
                "그룹 {0:00} / stack {1:00}에서 {2}",
                groupIndex + 1,
                stackIndex + 1,
                fragments[random.Next(fragments.Length)]);

            return isLong
                ? description + " 레이아웃 스트레스 테스트를 위해 설명 길이를 확장하고 줄바꿈이 여러 번 발생하도록 추가 부연 문장을 연결했습니다."
                : description;
        }

        private static string BuildSourceTarget(string profileToken, int groupIndex, int stackIndex, bool isLong)
        {
            string[] sources = { "탐색레이더", "추적레이더", "교전통제기", "요격체", "데이터링크" };
            string[] targets = { "위협 표적", "교전 표적", "유도 구간", "식별 결과", "위협군" };
            string value = sources[(groupIndex + stackIndex) % sources.Length] + " -> " + targets[(groupIndex + 2) % targets.Length];
            return isLong
                ? value + " / 교전 시나리오 " + string.Format(CultureInfo.InvariantCulture, "{0:00}", groupIndex + 1)
                : value;
        }

        private static string BuildRandomSourceTarget(string profileToken, int groupIndex, int stackIndex, bool isLong, Random random)
        {
            string[] sources = { "탐색레이더", "추적레이더", "교전통제기", "요격체", "데이터링크", "전술컴퓨터" };
            string[] targets = { "위협 표적", "교전 표적", "초기 유도 구간", "종말 유도 구간", "상태 벡터", "식별 결과" };
            string value = sources[random.Next(sources.Length)] + " -> " + targets[random.Next(targets.Length)];
            return isLong
                ? value + " / seed profile " + profileToken + " / group " + string.Format(CultureInfo.InvariantCulture, "{0:00}", groupIndex + 1) + " / stack " + string.Format(CultureInfo.InvariantCulture, "{0:00}", stackIndex + 1)
                : value;
        }

        private static string PickDescriptionLabel(Random random)
        {
            string[] labels = { "탐지 결과", "추적 상태", "교전 판단", "발사 상태", "요격 결과" };
            return labels[random.Next(labels.Length)];
        }

        private static string FormatWidthToken(double width)
        {
            return Math.Round(width).ToString("0", CultureInfo.InvariantCulture);
        }

        private sealed class StressCaseSpec
        {
            public string CaseId { get; set; }
            public string Suite { get; set; }
            public int Seed { get; set; }
            public string ProfileName { get; set; }
            public double Width { get; set; }
            public int UniqueGroupCount { get; set; }
            public int MaximumTimestampStack { get; set; }
            public List<SimulationEventMarker> Events { get; set; }
        }

        private sealed class ProfileDefinition
        {
            public ProfileDefinition(string token, string displayName)
            {
                Token = token;
                DisplayName = displayName;
            }

            public string Token { get; private set; }
            public string DisplayName { get; private set; }
        }
    }
}
