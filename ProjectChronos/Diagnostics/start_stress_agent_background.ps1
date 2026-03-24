param(
    [string]$ArtifactRoot = $(Join-Path $env:TEMP ('ProjectChronos\ReportExportStress\agent-run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [switch]$Resume
)

$scriptPath = Join-Path $PSScriptRoot 'run_stress_agent.ps1'

if ((Test-Path $ArtifactRoot) -and -not $Resume)
{
    Remove-Item $ArtifactRoot -Recurse -Force
}

$arguments = @(
    '-NoProfile',
    '-ExecutionPolicy', 'Bypass',
    '-File', $scriptPath,
    '-ArtifactRoot', $ArtifactRoot
)

if ($Resume)
{
    $arguments += '-Resume'
}

$process = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -PassThru

Write-Output ('PID=' + $process.Id)
Write-Output ('ArtifactRoot=' + $ArtifactRoot)
Write-Output ('AgentProgressJsonPath=' + (Join-Path $ArtifactRoot 'agent_progress.json'))
Write-Output ('AgentProgressMarkdownPath=' + (Join-Path $ArtifactRoot 'agent_progress.md'))
