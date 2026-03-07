param()

$ErrorActionPreference = "Stop"

Write-Host "Starting ProjectChronos for UI Test..."
$exePath = "$PSScriptRoot\bin\Debug\ProjectChronos.exe"

if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found at $exePath. Please build the project first."
    exit 1
}

$process = Start-Process -FilePath $exePath -PassThru
Write-Host "Process started with ID: $($process.Id)"

Write-Host "Waiting for WPF window to Initialize (6 seconds)..."
Start-Sleep -Seconds 6

try {
    Write-Host "Running UI Capture Script..."
    & powershell.exe -ExecutionPolicy Bypass -File "$PSScriptRoot\capture_ui.ps1" -WindowTitle "MainWindow"
    
    Write-Host "Running UI Tree Dump Script..."
    & powershell.exe -ExecutionPolicy Bypass -File "$PSScriptRoot\dump_ui_tree.ps1" -WindowTitle "MainWindow"
}
finally {
    Write-Host "Stopping ProjectChronos..."
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Write-Host "Test Run Complete."
}
