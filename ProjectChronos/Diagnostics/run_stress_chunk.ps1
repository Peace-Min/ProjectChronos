param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactRoot,
    [object]$IncludeDeterministic = $true,
    [object]$IncludeRandom = $true,
    [string]$DeterministicWidths = '',
    [string]$DeterministicUniqueGroupCounts = '',
    [string]$DeterministicDuplicateDepths = '',
    [string]$RandomWidths = '',
    [string]$RandomUniqueGroupCounts = '',
    [int]$RandomSeedStart = 1,
    [int]$RandomSeedCount = 20,
    [object]$RenderProblemCases = $true
)

function Convert-ToBoolean {
    param(
        [object]$Value,
        [bool]$DefaultValue = $false
    )

    if ($null -eq $Value)
    {
        return $DefaultValue
    }

    if ($Value -is [bool])
    {
        return [bool]$Value
    }

    $text = $Value.ToString().Trim()
    if ([string]::IsNullOrWhiteSpace($text))
    {
        return $DefaultValue
    }

    switch ($text.ToLowerInvariant())
    {
        'true' { return $true }
        'false' { return $false }
        '1' { return $true }
        '0' { return $false }
        default { return [bool]::Parse($text) }
    }
}

function New-DoubleList {
    param([object[]]$Values)

    $list = New-Object 'System.Collections.Generic.List[double]'
    foreach ($value in $Values)
    {
        [void]$list.Add([double]$value)
    }

    return $list
}

function New-IntList {
    param([object[]]$Values)

    $list = New-Object 'System.Collections.Generic.List[int]'
    foreach ($value in $Values)
    {
        [void]$list.Add([int]$value)
    }

    return $list
}

function Convert-CsvToDoubleArray {
    param([string]$Csv)

    if ([string]::IsNullOrWhiteSpace($Csv))
    {
        return @()
    }

    return $Csv.Split(',') | ForEach-Object { [double]($_.Trim()) }
}

function Convert-CsvToIntArray {
    param([string]$Csv)

    if ([string]::IsNullOrWhiteSpace($Csv))
    {
        return @()
    }

    return $Csv.Split(',') | ForEach-Object { [int]($_.Trim()) }
}

$assemblyPath = Join-Path $PSScriptRoot '..\bin\Release\ProjectChronos.exe'

if (Test-Path $ArtifactRoot)
{
    Remove-Item $ArtifactRoot -Recurse -Force
}

[void][System.Reflection.Assembly]::LoadFrom($assemblyPath)

$includeDeterministicValue = Convert-ToBoolean $IncludeDeterministic $true
$includeRandomValue = Convert-ToBoolean $IncludeRandom $true
$renderProblemCasesValue = Convert-ToBoolean $RenderProblemCases $true

$options = [ProjectChronos.Services.TimelineReportStressOptions]::CreateDefault($ArtifactRoot)
$options.IncludeDeterministic = $includeDeterministicValue
$options.IncludeRandom = $includeRandomValue
$options.RenderProblemCases = $renderProblemCasesValue
$options.RandomSeedStart = $RandomSeedStart
$options.RandomSeedCount = $RandomSeedCount
$options.ProgressJsonPath = Join-Path $ArtifactRoot 'progress.json'
$options.ChunkId = Split-Path -Path $ArtifactRoot -Leaf

if ($includeDeterministicValue)
{
    $detWidths = Convert-CsvToDoubleArray $DeterministicWidths
    $detGroups = Convert-CsvToIntArray $DeterministicUniqueGroupCounts
    $detDepths = Convert-CsvToIntArray $DeterministicDuplicateDepths

    if ($detWidths.Count -gt 0)
    {
        $options.DeterministicWidths = New-DoubleList $detWidths
    }

    if ($detGroups.Count -gt 0)
    {
        $options.DeterministicUniqueGroupCounts = New-IntList $detGroups
    }

    if ($detDepths.Count -gt 0)
    {
        $options.DeterministicDuplicateDepths = New-IntList $detDepths
    }
}

if ($includeRandomValue)
{
    $randomWidths = Convert-CsvToDoubleArray $RandomWidths
    $randomGroups = Convert-CsvToIntArray $RandomUniqueGroupCounts

    if ($randomWidths.Count -gt 0)
    {
        $options.RandomWidths = New-DoubleList $randomWidths
    }

    if ($randomGroups.Count -gt 0)
    {
        $options.RandomUniqueGroupCounts = New-IntList $randomGroups
    }
}

$service = New-Object ProjectChronos.Services.TimelineReportStressService
$summary = $service.Run($options)

$resultPath = Join-Path $ArtifactRoot 'run_result.txt'
@(
    ('ArtifactRoot=' + $summary.ArtifactRoot)
    ('ExitCode=' + $summary.ExitCode)
    ('ProgressJsonPath=' + $options.ProgressJsonPath)
    ('SummaryJsonPath=' + $summary.SummaryJsonPath)
    ('SummaryMarkdownPath=' + $summary.SummaryMarkdownPath)
    ('TotalCaseCount=' + $summary.TotalCaseCount)
    ('HardFailureCount=' + $summary.HardFailureCount)
    ('WarningCount=' + $summary.WarningCount)
) | Set-Content -Path $resultPath -Encoding UTF8

Write-Output ('ArtifactRoot=' + $summary.ArtifactRoot)
Write-Output ('ExitCode=' + $summary.ExitCode)
Write-Output ('ProgressJsonPath=' + $options.ProgressJsonPath)
Write-Output ('SummaryJsonPath=' + $summary.SummaryJsonPath)
Write-Output ('SummaryMarkdownPath=' + $summary.SummaryMarkdownPath)
Write-Output ('TotalCaseCount=' + $summary.TotalCaseCount)
Write-Output ('HardFailureCount=' + $summary.HardFailureCount)
Write-Output ('WarningCount=' + $summary.WarningCount)

exit $summary.ExitCode
