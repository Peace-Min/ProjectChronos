param(
    [string]$ArtifactRoot = $(Join-Path $env:TEMP ('ProjectChronos\ReportExportStress\agent-run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [switch]$Resume
)

function New-ChunkPlan {
    $chunks = New-Object System.Collections.Generic.List[object]

    foreach ($width in @(560, 600, 650, 700, 750, 850, 900, 932, 1000, 1100, 1200, 1500, 1900))
    {
        [void]$chunks.Add([pscustomobject]@{
            Id = ('det-' + $width)
            IncludeDeterministic = $true
            IncludeRandom = $false
            DeterministicWidths = $width.ToString()
            RandomWidths = ''
            RandomSeedStart = 1
            RandomSeedCount = 0
        })
    }

    for ($seedStart = 1; $seedStart -le 20; $seedStart += 2)
    {
        $seedEnd = [Math]::Min($seedStart + 1, 20)
        [void]$chunks.Add([pscustomobject]@{
            Id = ('rand-' + ('{0:D2}' -f $seedStart) + '-' + ('{0:D2}' -f $seedEnd))
            IncludeDeterministic = $false
            IncludeRandom = $true
            DeterministicWidths = ''
            RandomWidths = '650,850,932,1100,1500'
            RandomSeedStart = $seedStart
            RandomSeedCount = ($seedEnd - $seedStart + 1)
        })
    }

    return $chunks
}

function New-ChunkStatus {
    param(
        [string]$ChunkId,
        [string]$ProgressJsonPath
    )

    return [pscustomobject]([ordered]@{
        id = $ChunkId
        status = 'pending'
        progressState = ''
        exitCode = ''
        totalCaseCount = ''
        completedCaseCount = ''
        hardFailureCount = ''
        warningCount = ''
        currentCaseId = ''
        summaryJsonPath = ''
        progressJsonPath = $ProgressJsonPath
        updatedAt = ''
        note = ''
    })
}

function Update-ChunkStatusFromProgress {
    param(
        [object]$Status,
        [object]$Progress
    )

    if ($null -eq $Status -or $null -eq $Progress)
    {
        return
    }

    $Status.progressState = $Progress.State
    $Status.totalCaseCount = $Progress.TotalCaseCount
    $Status.completedCaseCount = $Progress.CompletedCaseCount
    $Status.hardFailureCount = $Progress.HardFailureCount
    $Status.warningCount = $Progress.WarningCount
    $Status.currentCaseId = $Progress.CurrentCaseId
    $Status.updatedAt = $Progress.UpdatedAtLocal
    $Status.note = $Progress.Note
}

function Try-ReadChunkProgress {
    param(
        [object]$Service,
        [string]$ProgressJsonPath
    )

    if (-not (Test-Path $ProgressJsonPath))
    {
        return $null
    }

    try
    {
        return $Service.ReadProgressFromJson($ProgressJsonPath)
    }
    catch
    {
        return $null
    }
}

function Read-ChunkSummary {
    param([string]$SummaryJsonPath)

    if (-not (Test-Path $SummaryJsonPath))
    {
        throw ('Summary not found: ' + $SummaryJsonPath)
    }

    return Get-Content -Path $SummaryJsonPath -Raw | ConvertFrom-Json
}

function Convert-ToStringList {
    param([object]$Values)

    $list = New-Object 'System.Collections.Generic.List[string]'
    if ($null -eq $Values)
    {
        return $list
    }

    foreach ($value in $Values)
    {
        [void]$list.Add([string]$value)
    }

    return $list
}

function Convert-ToCaseResult {
    param([object]$Case)

    $result = New-Object ProjectChronos.Services.TimelineReportStressCaseResult
    $result.CaseId = [string]$Case.caseId
    $result.Suite = [string]$Case.suite
    $result.Seed = [int]$Case.seed
    $result.Profile = [string]$Case.profile
    $result.Width = [double]$Case.width
    $result.UniqueGroupCount = [int]$Case.uniqueGroupCount
    $result.DuplicateDepth = [int]$Case.duplicateDepth
    $result.TotalEventCount = [int]$Case.totalEventCount
    $result.HardFail = [bool]$Case.hardFail
    $result.Warning = [bool]$Case.warning
    $result.FailureReason = [string]$Case.failureReason
    $result.WarningReason = [string]$Case.warningReason
    $result.FailureReasons = Convert-ToStringList $Case.failureReasons
    $result.WarningReasons = Convert-ToStringList $Case.warningReasons
    $result.GeneratedPngPath = [string]$Case.generatedPngPath
    $result.TimelineWidth = [double]$Case.timelineWidth
    $result.RenderedHeight = [double]$Case.renderedHeight
    $result.MaximumTimestampStack = [int]$Case.maximumTimestampStack
    return $result
}

function Write-AgentProgress {
    param(
        [string]$Root,
        [string]$State,
        [string]$CurrentChunkId,
        [object[]]$ChunkStatus
    )

    $progress = [pscustomobject]@{
        artifactRoot = $Root
        state = $State
        currentChunkId = $CurrentChunkId
        updatedAt = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
        chunks = $ChunkStatus
    }

    $jsonPath = Join-Path $Root 'agent_progress.json'
    $mdPath = Join-Path $Root 'agent_progress.md'

    $progress | ConvertTo-Json -Depth 6 | Set-Content -Path $jsonPath -Encoding UTF8

    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add('# Stress Agent Progress')
    [void]$lines.Add('')
    [void]$lines.Add('- artifactRoot: `' + $Root + '`')
    [void]$lines.Add('- state: `' + $State + '`')
    [void]$lines.Add('- currentChunkId: `' + $CurrentChunkId + '`')
    [void]$lines.Add('- updatedAt: `' + $progress.updatedAt + '`')
    [void]$lines.Add('')
    [void]$lines.Add('| Chunk | Status | ProgressState | Completed | HardFail | Warning | CurrentCase | UpdatedAt | Summary |')
    [void]$lines.Add('| --- | --- | --- | ---: | ---: | ---: | --- | --- | --- |')

    foreach ($chunk in $ChunkStatus)
    {
        $completedLabel = [string]$chunk.completedCaseCount
        if (-not [string]::IsNullOrWhiteSpace([string]$chunk.totalCaseCount))
        {
            $completedLabel = $completedLabel + ' / ' + [string]$chunk.totalCaseCount
        }

        [void]$lines.Add('| ' + $chunk.id + ' | ' + $chunk.status + ' | ' + $chunk.progressState + ' | ' + $completedLabel + ' | ' + $chunk.hardFailureCount + ' | ' + $chunk.warningCount + ' | ' + $chunk.currentCaseId + ' | ' + $chunk.updatedAt + ' | ' + $chunk.summaryJsonPath + ' |')
    }

    $lines | Set-Content -Path $mdPath -Encoding UTF8
}

$chunkScript = Join-Path $PSScriptRoot 'run_stress_chunk.ps1'
$assemblyPath = Join-Path $PSScriptRoot '..\bin\Release\ProjectChronos.exe'
$chunksRoot = Join-Path $ArtifactRoot 'chunks'

if ((Test-Path $ArtifactRoot) -and -not $Resume)
{
    Remove-Item $ArtifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $chunksRoot -Force | Out-Null
[void][System.Reflection.Assembly]::LoadFrom($assemblyPath)
$service = New-Object ProjectChronos.Services.TimelineReportStressService

$chunkPlan = New-ChunkPlan
$chunkStatus = New-Object System.Collections.Generic.List[object]

foreach ($chunk in $chunkPlan)
{
    $chunkRoot = Join-Path $chunksRoot $chunk.Id
    $chunkSummaryPath = Join-Path $chunkRoot 'summary.json'
    $chunkProgressPath = Join-Path $chunkRoot 'progress.json'
    $status = New-ChunkStatus -ChunkId $chunk.Id -ProgressJsonPath $chunkProgressPath

    if ($Resume -and (Test-Path $chunkSummaryPath))
    {
        $chunkSummary = Read-ChunkSummary -SummaryJsonPath $chunkSummaryPath
        $status.status = 'completed'
        $status.progressState = 'completed'
        $status.exitCode = $chunkSummary.exitCode
        $status.totalCaseCount = $chunkSummary.totalCaseCount
        $status.completedCaseCount = $chunkSummary.totalCaseCount
        $status.hardFailureCount = $chunkSummary.hardFailureCount
        $status.warningCount = $chunkSummary.warningCount
        $status.summaryJsonPath = $chunkSummaryPath
        $status.updatedAt = $chunkSummary.generatedAtLocal
        $status.note = 'resume-skip'
        [void]$chunkStatus.Add($status)
        Write-AgentProgress -Root $ArtifactRoot -State 'running' -CurrentChunkId $chunk.Id -ChunkStatus $chunkStatus
        continue
    }

    $status.status = 'running'
    $status.note = 'chunk-started'
    [void]$chunkStatus.Add($status)
    Write-AgentProgress -Root $ArtifactRoot -State 'running' -CurrentChunkId $chunk.Id -ChunkStatus $chunkStatus

    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $chunkScript,
        '-ArtifactRoot', $chunkRoot,
        '-IncludeDeterministic', ($(if ($chunk.IncludeDeterministic) { 'true' } else { 'false' })),
        '-IncludeRandom', ($(if ($chunk.IncludeRandom) { 'true' } else { 'false' }))
    )

    if (-not [string]::IsNullOrWhiteSpace($chunk.DeterministicWidths))
    {
        $arguments += @('-DeterministicWidths', $chunk.DeterministicWidths)
    }

    if (-not [string]::IsNullOrWhiteSpace($chunk.RandomWidths))
    {
        $arguments += @('-RandomWidths', $chunk.RandomWidths)
    }

    if ([int]$chunk.RandomSeedCount -gt 0)
    {
        $arguments += @('-RandomSeedStart', $chunk.RandomSeedStart, '-RandomSeedCount', $chunk.RandomSeedCount)
    }

    $process = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -PassThru -WindowStyle Hidden

    while (-not $process.HasExited)
    {
        $chunkProgress = Try-ReadChunkProgress -Service $service -ProgressJsonPath $chunkProgressPath
        Update-ChunkStatusFromProgress -Status $chunkStatus[$chunkStatus.Count - 1] -Progress $chunkProgress
        Write-AgentProgress -Root $ArtifactRoot -State 'running' -CurrentChunkId $chunk.Id -ChunkStatus $chunkStatus
        Start-Sleep -Seconds 5
        $process.Refresh()
    }

    $process.WaitForExit()
    $chunkExitCode = $process.ExitCode
    $chunkProgress = Try-ReadChunkProgress -Service $service -ProgressJsonPath $chunkProgressPath
    Update-ChunkStatusFromProgress -Status $chunkStatus[$chunkStatus.Count - 1] -Progress $chunkProgress

    if (-not (Test-Path $chunkSummaryPath))
    {
        $chunkStatus[$chunkStatus.Count - 1].status = 'failed'
        $chunkStatus[$chunkStatus.Count - 1].exitCode = $chunkExitCode
        $chunkStatus[$chunkStatus.Count - 1].note = 'chunk-failed-without-summary'
        Write-AgentProgress -Root $ArtifactRoot -State 'failed' -CurrentChunkId $chunk.Id -ChunkStatus $chunkStatus
        throw ('Chunk failed without summary output: ' + $chunk.Id)
    }

    $chunkSummary = Read-ChunkSummary -SummaryJsonPath $chunkSummaryPath
    $chunkStatus[$chunkStatus.Count - 1].status = 'completed'
    $chunkStatus[$chunkStatus.Count - 1].progressState = 'completed'
    $chunkStatus[$chunkStatus.Count - 1].exitCode = $chunkSummary.exitCode
    $chunkStatus[$chunkStatus.Count - 1].totalCaseCount = $chunkSummary.totalCaseCount
    $chunkStatus[$chunkStatus.Count - 1].completedCaseCount = $chunkSummary.totalCaseCount
    $chunkStatus[$chunkStatus.Count - 1].hardFailureCount = $chunkSummary.hardFailureCount
    $chunkStatus[$chunkStatus.Count - 1].warningCount = $chunkSummary.warningCount
    $chunkStatus[$chunkStatus.Count - 1].summaryJsonPath = $chunkSummaryPath
    $chunkStatus[$chunkStatus.Count - 1].updatedAt = $chunkSummary.generatedAtLocal
    $chunkStatus[$chunkStatus.Count - 1].note = 'chunk-summary-written'
    Write-AgentProgress -Root $ArtifactRoot -State 'running' -CurrentChunkId $chunk.Id -ChunkStatus $chunkStatus
}

$allCases = New-Object 'System.Collections.Generic.List[ProjectChronos.Services.TimelineReportStressCaseResult]'

foreach ($chunk in $chunkStatus)
{
    $chunkSummary = Read-ChunkSummary -SummaryJsonPath $chunk.summaryJsonPath
    foreach ($case in $chunkSummary.cases)
    {
        [void]$allCases.Add((Convert-ToCaseResult -Case $case))
    }
}

$finalOptions = [ProjectChronos.Services.TimelineReportStressOptions]::CreateDefault($ArtifactRoot)
$finalOptions.IncludeDeterministic = $true
$finalOptions.IncludeRandom = $true
$finalOptions.RandomSeedStart = 1
$finalOptions.RandomSeedCount = 20
$finalOptions.ProgressJsonPath = Join-Path $ArtifactRoot 'progress.json'
$finalOptions.ChunkId = 'aggregate'

$finalSummary = $service.WriteAggregateSummary(
    $finalOptions,
    $allCases,
    $ArtifactRoot,
    $chunksRoot,
    $chunksRoot)

$resultPath = Join-Path $ArtifactRoot 'agent_result.txt'
@(
    ('ArtifactRoot=' + $finalSummary.ArtifactRoot)
    ('ExitCode=' + $finalSummary.ExitCode)
    ('SummaryJsonPath=' + $finalSummary.SummaryJsonPath)
    ('SummaryMarkdownPath=' + $finalSummary.SummaryMarkdownPath)
    ('TotalCaseCount=' + $finalSummary.TotalCaseCount)
    ('HardFailureCount=' + $finalSummary.HardFailureCount)
    ('WarningCount=' + $finalSummary.WarningCount)
    ('MinimumSafeWidth=' + $finalSummary.MinimumSafeWidth)
    ('RecommendedWidth=' + $finalSummary.RecommendedWidth)
) | Set-Content -Path $resultPath -Encoding UTF8

Write-AgentProgress -Root $ArtifactRoot -State 'completed' -CurrentChunkId '' -ChunkStatus $chunkStatus

Write-Output ('ArtifactRoot=' + $finalSummary.ArtifactRoot)
Write-Output ('ExitCode=' + $finalSummary.ExitCode)
Write-Output ('SummaryJsonPath=' + $finalSummary.SummaryJsonPath)
Write-Output ('SummaryMarkdownPath=' + $finalSummary.SummaryMarkdownPath)
Write-Output ('TotalCaseCount=' + $finalSummary.TotalCaseCount)
Write-Output ('HardFailureCount=' + $finalSummary.HardFailureCount)
Write-Output ('WarningCount=' + $finalSummary.WarningCount)
Write-Output ('MinimumSafeWidth=' + $finalSummary.MinimumSafeWidth)
Write-Output ('RecommendedWidth=' + $finalSummary.RecommendedWidth)

exit $finalSummary.ExitCode
