param (
    [string]$WindowTitle = "MainWindow",
    [string]$OutputPath = "$PSScriptRoot\ui_tree_dump.json"
)

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

function Dump-Element ($element) {
    # 텍스트가 있는 요소들 위주로 필터링 (레이블 확인용)
    $name = $element.Current.Name
    $class = $element.Current.ClassName
    
    if (-not [string]::IsNullOrWhiteSpace($name) -or $class -match "TextBlock" -or $class -match "Border" -or $class -match "ContentControl") {
        $rect = $element.Current.BoundingRectangle

        $obj = [PSCustomObject]@{
            Name   = $name
            Class  = $class
            Left   = $rect.Left
            Top    = $rect.Top
            Width  = $rect.Width
            Height = $rect.Height
        }
        $global:elements += $obj
    }

    $condition = [System.Windows.Automation.Condition]::TrueCondition
    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    
    $child = $walker.GetFirstChild($element)
    while ($child -ne $null) {
        Dump-Element $child
        $child = $walker.GetNextSibling($child)
    }
}


$root = [System.Windows.Automation.AutomationElement]::RootElement

# Find the window
$condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $WindowTitle)
$window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)

if ($window -eq $null) {
    # 못 찾으면 프로세스 이름으로 다시 찾기 시도
    $proc = Get-Process -Name "ProjectChronos" -ErrorAction SilentlyContinue
    if ($proc) {
        $condition2 = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $proc.Id)
        $window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition2)
    }
}

if ($window -eq $null) {
    Write-Error "Error: Window '$WindowTitle' not found."
    exit 1
}

Write-Host "Window Found. Scanning UI Elements..."

$global:elements = @()
Dump-Element $window

$global:elements | ConvertTo-Json -Depth 2 | Out-File -FilePath $OutputPath -Encoding UTF8

Write-Host "UI Tree Dump saved to: $OutputPath"
