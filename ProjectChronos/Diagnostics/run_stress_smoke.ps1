$assemblyPath = Join-Path $PSScriptRoot '..\bin\Release\ProjectChronos.exe'
$artifactRoot = Join-Path $env:TEMP 'ProjectChronos\ReportExportStress\smoke-run'

if (Test-Path $artifactRoot)
{
    Remove-Item $artifactRoot -Recurse -Force
}

[void][System.Reflection.Assembly]::LoadFrom($assemblyPath)

$options = [ProjectChronos.Services.TimelineReportStressOptions]::CreateDefault($artifactRoot)
$options.DeterministicWidths = New-Object 'System.Collections.Generic.List[double]'
$options.DeterministicWidths.Add(650)
$options.DeterministicWidths.Add(932)
$options.DeterministicUniqueGroupCounts = New-Object 'System.Collections.Generic.List[int]'
$options.DeterministicUniqueGroupCounts.Add(1)
$options.DeterministicUniqueGroupCounts.Add(4)
$options.DeterministicDuplicateDepths = New-Object 'System.Collections.Generic.List[int]'
$options.DeterministicDuplicateDepths.Add(1)
$options.DeterministicDuplicateDepths.Add(2)
$options.RandomSeedCount = 1
$options.RandomWidths = New-Object 'System.Collections.Generic.List[double]'
$options.RandomWidths.Add(932)
$options.RandomUniqueGroupCounts = New-Object 'System.Collections.Generic.List[int]'
$options.RandomUniqueGroupCounts.Add(4)

$service = New-Object ProjectChronos.Services.TimelineReportStressService
$summary = $service.Run($options)

Write-Output ('ArtifactRoot=' + $summary.ArtifactRoot)
Write-Output ('ExitCode=' + $summary.ExitCode)
Write-Output ('SummaryJsonPath=' + $summary.SummaryJsonPath)
Write-Output ('SummaryMarkdownPath=' + $summary.SummaryMarkdownPath)
