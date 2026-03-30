param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot "..\bin\Debug\ProjectChronos.exe"),
    [string]$OutputRoot = (Join-Path (Join-Path $PSScriptRoot "..\output") ("edge_policy_compare_" + (Get-Date -Format "yyyyMMdd-HHmmss"))),
    [double]$CanvasWidth = 1920.0
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

[void][System.Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))

$definitionService = [ProjectChronos.Services.TimelineReportDefinitionService]::new()
$exportService = [ProjectChronos.Services.TimelineReportExportService]::new()

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$templates = @($definitionService.CreateRealDataVisibilityEvents())
$timestamps = @(100.0, 110.0, 120.0, 130.0, 140.0)

for ($index = 0; $index -lt $timestamps.Count; $index++)
{
    $templates[$index].Timestamp = $timestamps[$index]
}

$events = [System.Collections.Generic.List[ProjectChronos.Models.SimulationEventMarker]]::new()
foreach ($item in $templates)
{
    $events.Add($item)
}

$modes = @(
    [pscustomobject]@{ Id = "detail_band"; Policy = "detail-band"; Title = "Detail Band Only Expand" },
    [pscustomobject]@{ Id = "root_expand"; Policy = "root-expand"; Title = "Overview + Detail Root Expand" }
)

$generated = @()
$previousPolicy = [Environment]::GetEnvironmentVariable("PC_TIMELINE_EDGE_CANVAS_POLICY", "Process")

try
{
    foreach ($mode in $modes)
    {
        [Environment]::SetEnvironmentVariable("PC_TIMELINE_EDGE_CANVAS_POLICY", $mode.Policy, "Process")
        $outputPath = Join-Path $OutputRoot ($mode.Id + ".png")
        $input = [ProjectChronos.ViewModels.TimelineReportExportInput]::new(
            "Edge Policy Compare",
            "Time-Event",
            [string[]]@(),
            [double]$CanvasWidth,
            [double]0.0,
            $events,
            $outputPath)

        [void]$exportService.Export($input)
        $generated += [pscustomobject]@{
            Id = $mode.Id
            Policy = $mode.Policy
            Title = $mode.Title
            Path = $outputPath
        }
    }
}
finally
{
    [Environment]::SetEnvironmentVariable("PC_TIMELINE_EDGE_CANVAS_POLICY", $previousPolicy, "Process")
}

$htmlPath = Join-Path $OutputRoot "compare.html"
$cards = foreach ($item in $generated)
{
@"
<article class="card">
  <h2>$($item.Title)</h2>
  <div class="meta">policy: $($item.Policy)</div>
  <img src="$($item.Id).png" alt="$($item.Title)" />
</article>
"@
}

$html = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>Edge Policy Compare</title>
  <style>
    body { font-family: 'Segoe UI', sans-serif; margin: 24px; background: #f6f7f9; color: #1f2937; }
    h1 { margin: 0 0 18px; font-size: 28px; }
    .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px; align-items: start; }
    .card { background: #fff; border: 1px solid #d7dce3; padding: 14px; box-shadow: 0 3px 10px rgba(15, 23, 42, 0.05); }
    .card h2 { margin: 0 0 6px; font-size: 18px; }
    .meta { font-size: 13px; color: #475569; margin-bottom: 10px; }
    img { width: 100%; height: auto; border: 1px solid #e2e8f0; background: #fff; }
  </style>
</head>
<body>
  <h1>Edge Policy Compare: 02_uniform</h1>
  <section class="grid">
$($cards -join [Environment]::NewLine)
  </section>
</body>
</html>
"@

Set-Content -Path $htmlPath -Value $html -Encoding UTF8

$generated | Select-Object Title, Path
Write-Output $htmlPath
