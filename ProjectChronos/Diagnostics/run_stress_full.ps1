param(
    [string]$ArtifactRoot = $(Join-Path $env:TEMP 'ProjectChronos\ReportExportStress\full-run')
)

$assemblyPath = Join-Path $PSScriptRoot '..\bin\Release\ProjectChronos.exe'

if (Test-Path $ArtifactRoot)
{
    Remove-Item $ArtifactRoot -Recurse -Force
}

[void][System.Reflection.Assembly]::LoadFrom($assemblyPath)

$options = [ProjectChronos.Services.TimelineReportStressOptions]::CreateDefault($ArtifactRoot)
$service = New-Object ProjectChronos.Services.TimelineReportStressService
$summary = $service.Run($options)

$resultPath = Join-Path $ArtifactRoot 'run_result.txt'
@(
    ('ArtifactRoot=' + $summary.ArtifactRoot)
    ('ExitCode=' + $summary.ExitCode)
    ('SummaryJsonPath=' + $summary.SummaryJsonPath)
    ('SummaryMarkdownPath=' + $summary.SummaryMarkdownPath)
) | Set-Content -Path $resultPath -Encoding UTF8

Write-Output ('ArtifactRoot=' + $summary.ArtifactRoot)
Write-Output ('ExitCode=' + $summary.ExitCode)
Write-Output ('SummaryJsonPath=' + $summary.SummaryJsonPath)
Write-Output ('SummaryMarkdownPath=' + $summary.SummaryMarkdownPath)

exit $summary.ExitCode
