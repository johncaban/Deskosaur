@echo off
title Install Deskosaur
cd /d "%~dp0"
echo.
echo   DESKOSAUR INSTALLER
echo   -------------------
echo   Installs the game for your Windows user (no admin needed) and adds it
echo   to the Start menu so you can find it with the Windows key.
echo.
choice /c YN /m "  Also put a shortcut on your desktop"
set "DESK="
if not errorlevel 2 set "DESK=-Desktop"
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup\install.ps1" %DESK%
echo.
pause
