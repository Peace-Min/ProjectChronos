param (
    [string]$WindowTitle = "MainWindow",
    [string]$OutputPath = "$PSScriptRoot\ui_capture.png"
)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

# 창 핸들 찾기
Add-Type @"
    using System;
    using System.Runtime.InteropServices;
    public class Win32 {
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string sClassName, string sAppName);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
"@

$hwnd = [Win32]::FindWindow($null, $WindowTitle)

if ($hwnd -eq [IntPtr]::Zero) {
    # WPF 디폴트 타이틀 이름인 'MainWindow'나 프로젝트 이름으로도 못찾으면 프로세스로 찾기 시도
    $proc = Get-Process | Where-Object { $_.MainWindowTitle -match $WindowTitle -or $_.Name -match "ProjectChronos" } | Select-Object -First 1
    if ($proc) {
        $hwnd = $proc.MainWindowHandle
    }
}


if ($hwnd -eq [IntPtr]::Zero) {
    Write-Error "지정된 창($WindowTitle)을 찾을 수 없습니다. 앱이 실행 중인지 확인하세요."
    exit 1
}

# 창을 맨 앞으로 가져옴
[Win32]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500

$rect = New-Object Win32+RECT
[Win32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null

$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top

if ($width -le 0 -or $height -le 0) {
    Write-Error "유효하지 않은 창 크기입니다."
    exit 1
}

$bmp = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bmp)
$graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)

$bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)

$graphics.Dispose()
$bmp.Dispose()

Write-Host "UI Captured to: $OutputPath"
