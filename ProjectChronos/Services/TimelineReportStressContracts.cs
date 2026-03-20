using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;

namespace ProjectChronos.Services
{
    [DataContract]
    public sealed class TimelineReportStressOptions
    {
        [DataMember(Order = 1)]
        public List<double> DeterministicWidths { get; set; }

        [DataMember(Order = 2)]
        public List<int> DeterministicUniqueGroupCounts { get; set; }

        [DataMember(Order = 3)]
        public List<int> DeterministicDuplicateDepths { get; set; }

        [DataMember(Order = 4)]
        public int RandomSeedCount { get; set; }

        [DataMember(Order = 5)]
        public List<double> RandomWidths { get; set; }

        [DataMember(Order = 6)]
        public List<int> RandomUniqueGroupCounts { get; set; }

        [DataMember(Order = 7)]
        public string ArtifactRoot { get; set; }

        [DataMember(Order = 8)]
        public bool RenderProblemCases { get; set; }

        [DataMember(Order = 9)]
        public bool IncludeDeterministic { get; set; }

        [DataMember(Order = 10)]
        public bool IncludeRandom { get; set; }

        [DataMember(Order = 11)]
        public int RandomSeedStart { get; set; }

        [DataMember(Order = 12, EmitDefaultValue = false)]
        public string ProgressJsonPath { get; set; }

        [DataMember(Order = 13, EmitDefaultValue = false)]
        public string ChunkId { get; set; }

        public static TimelineReportStressOptions CreateDefault(string artifactRoot = null)
        {
            string resolvedArtifactRoot = string.IsNullOrWhiteSpace(artifactRoot)
                ? Path.Combine(
                    Path.GetTempPath(),
                    "ProjectChronos",
                    "ReportExportStress",
                    System.DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture))
                : Path.GetFullPath(artifactRoot);

            return new TimelineReportStressOptions
            {
                DeterministicWidths = new List<double> { 560, 600, 650, 700, 750, 850, 900, 932, 1000, 1100, 1200, 1500, 1900 },
                DeterministicUniqueGroupCounts = new List<int> { 1, 4, 8, 12, 16, 20, 24 },
                DeterministicDuplicateDepths = new List<int> { 1, 2, 3, 4 },
                RandomSeedCount = 20,
                RandomWidths = new List<double> { 650, 850, 932, 1100, 1500 },
                RandomUniqueGroupCounts = new List<int> { 4, 8, 12, 16 },
                ArtifactRoot = resolvedArtifactRoot,
                RenderProblemCases = true,
                IncludeDeterministic = true,
                IncludeRandom = true,
                RandomSeedStart = 1,
                ProgressJsonPath = Path.Combine(resolvedArtifactRoot, "progress.json"),
                ChunkId = null
            };
        }
    }

    [DataContract]
    public sealed class TimelineReportStressSummary
    {
        [DataMember(Order = 1)]
        public string ArtifactRoot { get; set; }

        [DataMember(Order = 2)]
        public string FlaggedDirectory { get; set; }

        [DataMember(Order = 3)]
        public string WarningsDirectory { get; set; }

        [DataMember(Order = 4)]
        public string SummaryJsonPath { get; set; }

        [DataMember(Order = 5)]
        public string SummaryMarkdownPath { get; set; }

        [DataMember(Order = 6, EmitDefaultValue = false)]
        public string ReviewMarkdownPath { get; set; }

        [DataMember(Order = 7)]
        public string GeneratedAtLocal { get; set; }

        [DataMember(Order = 8)]
        public int ExitCode { get; set; }

        [DataMember(Order = 9)]
        public int TotalCaseCount { get; set; }

        [DataMember(Order = 10)]
        public int DeterministicCaseCount { get; set; }

        [DataMember(Order = 11)]
        public int RandomCaseCount { get; set; }

        [DataMember(Order = 12)]
        public int HardFailureCount { get; set; }

        [DataMember(Order = 13)]
        public int WarningCount { get; set; }

        [DataMember(Order = 14, EmitDefaultValue = false)]
        public double? MinimumSafeWidth { get; set; }

        [DataMember(Order = 15, EmitDefaultValue = false)]
        public double? RecommendedWidth { get; set; }

        [DataMember(Order = 16)]
        public List<double> MinimumSafeWidthCandidates { get; set; }

        [DataMember(Order = 17)]
        public List<double> RecommendedWidthCandidates { get; set; }

        [DataMember(Order = 18)]
        public Dictionary<string, int> MaxUniqueGroupsByWidth { get; set; }

        [DataMember(Order = 19)]
        public Dictionary<string, int> MaxTotalEventsByWidth { get; set; }

        [DataMember(Order = 20)]
        public List<TimelineReportStressRepresentativeIssue> RepresentativeIssues { get; set; }

        [DataMember(Order = 21)]
        public List<TimelineReportStressCaseResult> Cases { get; set; }
    }

    [DataContract]
    public sealed class TimelineReportStressCaseResult
    {
        [DataMember(Order = 1)]
        public string CaseId { get; set; }

        [DataMember(Order = 2)]
        public string Suite { get; set; }

        [DataMember(Order = 3)]
        public int Seed { get; set; }

        [DataMember(Order = 4)]
        public string Profile { get; set; }

        [DataMember(Order = 5)]
        public double Width { get; set; }

        [DataMember(Order = 6)]
        public int UniqueGroupCount { get; set; }

        [DataMember(Order = 7)]
        public int DuplicateDepth { get; set; }

        [DataMember(Order = 8)]
        public int TotalEventCount { get; set; }

        [DataMember(Order = 9)]
        public bool HardFail { get; set; }

        [DataMember(Order = 10)]
        public bool Warning { get; set; }

        [DataMember(Order = 11, EmitDefaultValue = false)]
        public string FailureReason { get; set; }

        [DataMember(Order = 12, EmitDefaultValue = false)]
        public string WarningReason { get; set; }

        [DataMember(Order = 13)]
        public List<string> FailureReasons { get; set; }

        [DataMember(Order = 14)]
        public List<string> WarningReasons { get; set; }

        [DataMember(Order = 15, EmitDefaultValue = false)]
        public string GeneratedPngPath { get; set; }

        [DataMember(Order = 16)]
        public double TimelineWidth { get; set; }

        [DataMember(Order = 17)]
        public double RenderedHeight { get; set; }

        [DataMember(Order = 18)]
        public int MaximumTimestampStack { get; set; }
    }

    [DataContract]
    public sealed class TimelineReportStressRepresentativeIssue
    {
        [DataMember(Order = 1)]
        public string Severity { get; set; }

        [DataMember(Order = 2)]
        public string ReasonKey { get; set; }

        [DataMember(Order = 3)]
        public string CaseId { get; set; }

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public string GeneratedPngPath { get; set; }
    }

    [DataContract]
    public sealed class TimelineReportStressProgress
    {
        [DataMember(Order = 1)]
        public string ArtifactRoot { get; set; }

        [DataMember(Order = 2, EmitDefaultValue = false)]
        public string ChunkId { get; set; }

        [DataMember(Order = 3)]
        public string State { get; set; }

        [DataMember(Order = 4)]
        public string StartedAtLocal { get; set; }

        [DataMember(Order = 5)]
        public string UpdatedAtLocal { get; set; }

        [DataMember(Order = 6)]
        public int TotalCaseCount { get; set; }

        [DataMember(Order = 7)]
        public int CompletedCaseCount { get; set; }

        [DataMember(Order = 8)]
        public int HardFailureCount { get; set; }

        [DataMember(Order = 9)]
        public int WarningCount { get; set; }

        [DataMember(Order = 10, EmitDefaultValue = false)]
        public string CurrentCaseId { get; set; }

        [DataMember(Order = 11, EmitDefaultValue = false)]
        public string CurrentSuite { get; set; }

        [DataMember(Order = 12, EmitDefaultValue = false)]
        public string CurrentProfile { get; set; }

        [DataMember(Order = 13)]
        public double CurrentWidth { get; set; }

        [DataMember(Order = 14)]
        public int CurrentSeed { get; set; }

        [DataMember(Order = 15, EmitDefaultValue = false)]
        public string SummaryJsonPath { get; set; }

        [DataMember(Order = 16, EmitDefaultValue = false)]
        public string Note { get; set; }
    }
}
