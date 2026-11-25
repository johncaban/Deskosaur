# Deskosaur uninstaller (keeps your save in %APPDATA%\Deskosaur unless you choose to delete it)
$ErrorActionPreference = 'SilentlyContinue'
Get-Process -Name Deskosaur | Stop-Process -Force
Start-Sleep -Milliseconds 300

Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'Deskosaur.lnk') -Force
Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Deskosaur.lnk') -Force
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Deskosaur' -Recurse -Force

$saves = Join-Path $env:APPDATA 'Deskosaur'
if (Test-Path $saves) {
    $a = Read-Host "Also delete your Deskosaur progress and outfits? (y/N)"
    if ($a -match '^[yY]') { Remove-Item $saves -Recurse -Force }
}

$dest = Join-Path $env:LOCALAPPDATA 'Programs\Deskosaur'
# remove the install folder after this script exits
Start-Process cmd.exe -ArgumentList "/c timeout /t 2 /nobreak >nul & rmdir /s /q `"$dest`"" -WindowStyle Hidden
Write-Host "Deskosaur uninstalled. Rawr." -ForegroundColor Green
Start-Sleep -Seconds 2
