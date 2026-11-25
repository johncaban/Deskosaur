# Deskosaur installer (per-user, no admin needed)
param([switch]$Desktop)
$ErrorActionPreference = 'Stop'

$src  = Split-Path -Parent $PSScriptRoot
$exeSrc = Join-Path $src 'Deskosaur.exe'
if (-not (Test-Path $exeSrc)) {
    Write-Host "Can't find Deskosaur.exe next to the installer." -ForegroundColor Red
    Write-Host "Unzip the WHOLE Deskosaur folder first, then run 'Install Deskosaur.bat' from inside it."
    exit 1
}

$dest = Join-Path $env:LOCALAPPDATA 'Programs\Deskosaur'
Write-Host "Installing Deskosaur to $dest ..."

# close it if it's running so the exe can be replaced
Get-Process -Name Deskosaur -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item $exeSrc $dest -Force
Copy-Item (Join-Path $src 'README.txt') $dest -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') $dest -Force
# drop the "downloaded from the internet" flag so Windows doesn't nag every launch
Get-ChildItem $dest -Recurse | Unblock-File -ErrorAction SilentlyContinue

$exe = Join-Path $dest 'Deskosaur.exe'
$ws  = New-Object -ComObject WScript.Shell

function New-Shortcut($path) {
    $lnk = $ws.CreateShortcut($path)
    $lnk.TargetPath       = $exe
    $lnk.WorkingDirectory = $dest
    $lnk.IconLocation     = "$exe,0"
    $lnk.Description      = 'Deskosaur - a desktop platformer'
    $lnk.Save()
}

# Start menu shortcut -> searchable from the Windows key
$startLnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'Deskosaur.lnk'
New-Shortcut $startLnk
Write-Host "  + Start menu shortcut"

if ($Desktop) {
    New-Shortcut (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Deskosaur.lnk')
    Write-Host "  + Desktop shortcut"
}

# Show up in Settings > Apps (with a working Uninstall button)
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Deskosaur'
New-Item -Path $key -Force | Out-Null
$size = [int]((Get-ChildItem $dest -Recurse | Measure-Object Length -Sum).Sum / 1KB)
$props = @{
    DisplayName     = 'Deskosaur'
    DisplayVersion  = '1.2'
    Publisher       = 'Deskosaur'
    DisplayIcon     = "$exe,0"
    InstallLocation = $dest
    UninstallString = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$dest\uninstall.ps1`""
}
foreach ($k in $props.Keys) { Set-ItemProperty -Path $key -Name $k -Value $props[$k] }
New-ItemProperty -Path $key -Name NoModify -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $key -Name NoRepair -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -Path $key -Name EstimatedSize -Value $size -PropertyType DWord -Force | Out-Null
Write-Host "  + Listed in Settings > Apps"

Write-Host ""
Write-Host "Done! Press the Windows key and type 'Deskosaur' to play." -ForegroundColor Green
Write-Host "(Tip: right-click it in Start and choose 'Pin to Start' or 'Pin to taskbar'.)"
Write-Host "You can delete the folder you unzipped now - the game lives in $dest"
