param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot "..\bin\Debug\ProjectChronos.exe"),
    [string]$OutputRoot = (Join-Path $env:TEMP ("ProjectChronos\Max5Harness\" + (Get-Date -Format "yyyyMMdd-HHmmss"))),
    [double]$CanvasWidth = 1920.0,
    [int]$BatchCount = 10
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

[void][System.Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath))

$bindingFlagsType = [System.Reflection.BindingFlags]
$stressService = [ProjectChronos.Services.TimelineReportStressService]::new()
$definitionService = [ProjectChronos.Services.TimelineReportDefinitionService]::new()
$exportService = [ProjectChronos.Services.TimelineReportExportService]::new()
$exerciseLayout = [ProjectChronos.Services.TimelineReportStressService].GetMethod(
    "ExerciseLayout",
    $bindingFlagsType::NonPublic -bor $bindingFlagsType::Static)
$validateGeometry = [ProjectChronos.Services.TimelineReportStressService].GetMethod(
    "ValidateGeometry",
    $bindingFlagsType::NonPublic -bor $bindingFlagsType::Instance)

if (-not $exerciseLayout -or -not $validateGeometry)
{
    throw "Failed to bind stress service validation methods."
}

if (Test-Path $OutputRoot)
{
    Remove-Item $OutputRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$templateEvents = @($definitionService.CreateRealDataVisibilityEvents())

function ConvertTo-HtmlSafeText {
    param(
        [AllowNull()]
        [string]$Value
    )

    if ($null -eq $Value)
    {
        return ""
    }

    return [System.Net.WebUtility]::HtmlEncode($Value)
}

function New-CopiedEvent {
    param(
        [ProjectChronos.Models.SimulationEventMarker]$Template,
        [double]$Timestamp
    )

    $event = [ProjectChronos.Models.SimulationEventMarker]::new()
    $event.Timestamp = [Math]::Round($Timestamp, 3)
    $event.Priority = $Template.Priority
    $event.Title = $Template.Title
    $event.DescriptionLabel = $Template.DescriptionLabel
    $event.Description = $Template.Description
    $event.RangeBTWLabel = $Template.RangeBTWLabel
    $event.RangeBTW = $Template.RangeBTW
    $event.SourceLabel = $Template.SourceLabel
    $event.Source = $Template.Source
    $event.IsPrimaryMarker = $Template.IsPrimaryMarker
    $event.MarkerPriority = $Template.MarkerPriority
    return $event
}

function Get-RelativePathForHtml {
    param(
        [string]$FromDirectory,
        [string]$TargetPath
    )

    $fromUri = [System.Uri]((Resolve-Path $FromDirectory).Path.TrimEnd('\') + '\')
    $targetUri = [System.Uri](Resolve-Path $TargetPath).Path
    return $fromUri.MakeRelativeUri($targetUri).ToString()
}

function New-CaseEvents {
    param(
        [double[]]$Timestamps
    )

    if ($Timestamps.Count -ne 5)
    {
        throw "Each harness case must define exactly 5 timestamps."
    }

    $events = [System.Collections.Generic.List[ProjectChronos.Models.SimulationEventMarker]]::new()
    for ($index = 0; $index -lt $Timestamps.Count; $index++)
    {
        $events.Add((New-CopiedEvent -Template $templateEvents[$index] -Timestamp $Timestamps[$index]))
    }

    return ,$events
}

function New-RandomCaseTimes {
    param(
        [int]$Seed,
        [string]$Profile
    )

    $random = [System.Random]::new($Seed)
    $times = [System.Collections.Generic.List[double]]::new()
    $current = 200.0 + ($random.NextDouble() * 20.0)
    $times.Add([Math]::Round($current, 3))

    for ($index = 1; $index -lt 5; $index++)
    {
        switch ($Profile)
        {
            "micro" {
                $gap = 0.01 + ($random.NextDouble() * 0.45)
            }
            "cluster" {
                if (($index % 2) -eq 0)
                {
                    $gap = 0.02 + ($random.NextDouble() * 0.25)
                }
                else
                {
                    $gap = 4.0 + ($random.NextDouble() * 8.0)
                }
            }
            default {
                $gap = 0.3 + ($random.NextDouble() * 9.0)
            }
        }

        $current += $gap
        $times.Add([Math]::Round($current, 3))
    }

    return $times.ToArray()
}

function New-CaseDefinitions {
    param(
        [int]$BatchIndex
    )

    $seedOffset = ($BatchIndex - 1) * 100

    return @(
        [pscustomobject]@{ Id = "01_realdata_like";      Timestamps = @(200.64, 213.13, 213.14, 213.14, 221.71) },
        [pscustomobject]@{ Id = "02_uniform";            Timestamps = @(100.0, 110.0, 120.0, 130.0, 140.0) },
        [pscustomobject]@{ Id = "03_front_cluster";      Timestamps = @(150.0, 150.05, 150.09, 161.0, 173.0) },
        [pscustomobject]@{ Id = "04_middle_cluster";     Timestamps = @(200.0, 214.0, 214.01, 214.02, 229.0) },
        [pscustomobject]@{ Id = "05_tail_cluster";       Timestamps = @(300.0, 312.0, 325.0, 325.06, 325.11) },
        [pscustomobject]@{ Id = "06_all_micro";          Timestamps = @(400.0, 400.08, 400.16, 400.24, 400.32) },
        [pscustomobject]@{ Id = "07_duplicate_pair";     Timestamps = @(500.0, 511.0, 511.0, 522.0, 534.0) },
        [pscustomobject]@{ Id = "08_seeded_micro";       Timestamps = (New-RandomCaseTimes -Seed (17 + $seedOffset) -Profile "micro") },
        [pscustomobject]@{ Id = "09_seeded_cluster";     Timestamps = (New-RandomCaseTimes -Seed (23 + $seedOffset) -Profile "cluster") },
        [pscustomobject]@{ Id = "10_seeded_mixed";       Timestamps = (New-RandomCaseTimes -Seed (41 + $seedOffset) -Profile "mixed") }
    )
}

function Invoke-LayoutValidation {
    param(
        [ProjectChronos.ViewModels.ReportTimelineExportViewModel]$ViewModel,
        [ProjectChronos.Services.TimelineReportStressCaseResult]$CaseResult
    )

    [void]$exerciseLayout.Invoke($null, [object[]]@($ViewModel))
    [void]$validateGeometry.Invoke($stressService, [object[]]@($ViewModel, $CaseResult))
}

function Write-BatchGallery {
    param(
        [string]$BatchId,
        [string]$GalleryPath,
        [System.Collections.IEnumerable]$Results
    )

    $cards = foreach ($result in $Results)
    {
        $title = ConvertTo-HtmlSafeText $result.case_id
        $meta = ConvertTo-HtmlSafeText ("Failures={0} Warnings={1} ScaleBreaks={2}" -f $result.failure_count, $result.warning_count, $result.scale_break_count)
        $timestamps = ConvertTo-HtmlSafeText (($result.timestamps | ForEach-Object { "{0:0.###}" -f $_ }) -join ", ")
        $statusClass = if ($result.failure_count -gt 0) { "fail" } elseif ($result.warning_count -gt 0) { "warn" } else { "ok" }
        $relativeImagePath = Get-RelativePathForHtml -FromDirectory (Split-Path $GalleryPath -Parent) -TargetPath $result.png_path

@"
<article class="case $statusClass">
  <h2>$title</h2>
  <div class="meta">$meta</div>
  <div class="timestamps">$timestamps</div>
  <img src="$relativeImagePath" alt="$title" loading="lazy" />
</article>
"@
    }

    $html = @"
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>$BatchId</title>
  <style>
    body { font-family: 'Segoe UI', sans-serif; margin: 24px; background: #f6f7f9; color: #1f2937; }
    h1 { margin: 0 0 18px; font-size: 28px; }
    .grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px; }
    .case { background: #ffffff; border: 1px solid #d7dce3; padding: 14px; box-shadow: 0 3px 10px rgba(15, 23, 42, 0.05); }
    .case.ok { border-left: 4px solid #0f766e; }
    .case.warn { border-left: 4px solid #d97706; }
    .case.fail { border-left: 4px solid #b91c1c; }
    .case h2 { margin: 0 0 6px; font-size: 18px; }
    .meta { font-size: 13px; color: #475569; margin-bottom: 4px; }
    .timestamps { font-size: 12px; color: #64748b; margin-bottom: 10px; word-break: break-word; }
    img { width: 100%; height: auto; border: 1px solid #e2e8f0; background: #fff; }
  </style>
</head>
<body>
  <h1>$BatchId</h1>
  <section class="grid">
$($cards -join [Environment]::NewLine)
  </section>
</body>
</html>
"@

    Set-Content -Path $GalleryPath -Value $html -Encoding UTF8
}

$allBatchResults = [System.Collections.Generic.List[object]]::new()

for ($batchIndex = 1; $batchIndex -le $BatchCount; $batchIndex++)
{
    $batchId = "batch_{0:00}" -f $batchIndex
    $batchDirectory = Join-Path $OutputRoot $batchId
    $imagesDirectory = Join-Path $batchDirectory "images"
    New-Item -ItemType Directory -Force -Path $imagesDirectory | Out-Null

    $caseDefinitions = New-CaseDefinitions -BatchIndex $batchIndex
    $results = [System.Collections.Generic.List[object]]::new()

    foreach ($caseDefinition in $caseDefinitions)
    {
        $events = New-CaseEvents -Timestamps $caseDefinition.Timestamps
        $pngPath = Join-Path $imagesDirectory ($caseDefinition.Id + ".png")
        $input = [ProjectChronos.ViewModels.TimelineReportExportInput]::new(
            "Harness",
            "Time-Event",
            [string[]]@(),
            [double]$CanvasWidth,
            [double]0,
            $events,
            $pngPath)

        $viewModel = [ProjectChronos.ViewModels.ReportTimelineExportViewModel]::new($input)
        $caseResult = [ProjectChronos.Services.TimelineReportStressCaseResult]::new()
        $caseResult.CaseId = "{0}_{1}" -f $batchId, $caseDefinition.Id
        $caseResult.Width = $CanvasWidth
        $caseResult.FailureReasons = [System.Collections.Generic.List[string]]::new()
        $caseResult.WarningReasons = [System.Collections.Generic.List[string]]::new()

        try
        {
            Invoke-LayoutValidation -ViewModel $viewModel -CaseResult $caseResult
        }
        catch
        {
            $caseResult.FailureReasons.Add("harness_validation_exception: " + $_.Exception.Message)
        }

        [void]$exportService.Export($input)

        $results.Add([pscustomobject]@{
            batch_id = $batchId
            case_id = $caseDefinition.Id
            timestamps = $caseDefinition.Timestamps
            failure_count = $caseResult.FailureReasons.Count
            warning_count = $caseResult.WarningReasons.Count
            failures = @($caseResult.FailureReasons)
            warnings = @($caseResult.WarningReasons)
            rendered_width = [Math]::Round($viewModel.RenderedCanvasWidth, 2)
            rendered_height = [Math]::Round($viewModel.RenderedCanvasHeight, 2)
            overview_height = [Math]::Round($viewModel.OverviewHeight, 2)
            detail_band_height = [Math]::Round($viewModel.DetailBandHeight, 2)
            slot_count = $viewModel.SlotItems.Count
            card_count = $viewModel.DetailCardItems.Count
            scale_break_count = $viewModel.ScaleBreakItems.Count
            png_path = $pngPath
        })
    }

    $summaryPath = Join-Path $batchDirectory "summary.json"
    $galleryPath = Join-Path $batchDirectory "gallery.html"
    $results | ConvertTo-Json -Depth 6 | Set-Content -Path $summaryPath -Encoding UTF8
    Write-BatchGallery -BatchId $batchId -GalleryPath $galleryPath -Results $results

    $failureCases = @($results | Where-Object { $_.failure_count -gt 0 })
    $warningCases = @($results | Where-Object { $_.warning_count -gt 0 })

    $allBatchResults.Add([pscustomobject]@{
        batch_id = $batchId
        failure_count = $failureCases.Count
        warning_count = $warningCases.Count
        summary_path = $summaryPath
        gallery_path = $galleryPath
        cases = @($results)
    })
}

$aggregatePath = Join-Path $OutputRoot "aggregate.json"
$allBatchResults | ConvertTo-Json -Depth 8 | Set-Content -Path $aggregatePath -Encoding UTF8

$failedBatches = @($allBatchResults | Where-Object { $_.failure_count -gt 0 })
$warnedBatches = @($allBatchResults | Where-Object { $_.warning_count -gt 0 })

Write-Output ("OutputRoot=" + $OutputRoot)
Write-Output ("AggregatePath=" + $aggregatePath)
Write-Output ("BatchCount=" + $BatchCount)
Write-Output ("FailedBatchCount=" + $failedBatches.Count)
Write-Output ("WarnedBatchCount=" + $warnedBatches.Count)

foreach ($batch in $allBatchResults)
{
    Write-Output ("Batch=" + $batch.batch_id + "; Failures=" + $batch.failure_count + "; Warnings=" + $batch.warning_count + "; Gallery=" + $batch.gallery_path)
}
