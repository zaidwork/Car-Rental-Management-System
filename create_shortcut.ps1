$ws = New-Object -ComObject WScript.Shell
$desktopPath = [Environment]::GetFolderPath("Desktop")
$shortcutFile = Join-Path $desktopPath "TAJ SYSTEM.lnk"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $scriptDir "src\CarRental.App\bin\Release\net10.0-windows\CarRental.App.exe"
$iconPath = Join-Path $scriptDir "src\CarRental.App\AppIcon.ico"

$s = $ws.CreateShortcut($shortcutFile)
$s.TargetPath = $exePath
$s.WorkingDirectory = Split-Path -Parent $exePath
$s.IconLocation = "$iconPath,0"
$s.Description = "TAJ SYSTEM"
$s.Save()

Write-Host "Desktop shortcut created successfully: $shortcutFile"
