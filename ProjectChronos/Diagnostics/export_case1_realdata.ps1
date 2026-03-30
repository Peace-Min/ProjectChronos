param(
    [string]$OutputPath = "C:\Users\CEO\source\repos\ProjectChronos\ProjectChronos\output\case1_realdata.png"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

[void][System.Reflection.Assembly]::LoadFrom("C:\Users\CEO\source\repos\ProjectChronos\ProjectChronos\bin\Debug\ProjectChronos.exe")

$definitionService = [ProjectChronos.Services.TimelineReportDefinitionService]::new()
$exportService = [ProjectChronos.Services.TimelineReportExportService]::new()
$events = $definitionService.CreateRealDataVisibilityEvents()
$timestamps = @(200.64, 213.13, 213.14, 213.14, 221.71)

for ($index = 0; $index -lt $timestamps.Count; $index++)
{
    $events[$index].Timestamp = $timestamps[$index]
}

$input = [ProjectChronos.ViewModels.TimelineReportExportInput]::new(
    "Harness",
    "Time-Event",
    [string[]]@(),
    [double]1920.0,
    [double]0.0,
    $events,
    $OutputPath)

[void]$exportService.Export($input)
Write-Output $OutputPath
