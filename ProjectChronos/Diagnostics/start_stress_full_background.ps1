param(
    [string]$ArtifactRoot = $(Join-Path $env:TEMP ('ProjectChronos\ReportExportStress\full-run-' + (Get-Date -Format 'yyyyMMdd-HHmmss')))
)

$scriptPath = Join-Path $PSScriptRoot 'run_stress_full.ps1'

if (Test-Path $ArtifactRoot)
{
    Remove-Item $ArtifactRoot -Recurse -Force
}

$process = Start-Process -FilePath 'powershell.exe' `
    -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File',$scriptPath,'-ArtifactRoot',$ArtifactRoot `
    -PassThru

Write-Output ('PID=' + $process.Id)
Write-Output ('ArtifactRoot=' + $ArtifactRoot)
