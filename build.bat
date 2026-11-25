@echo off
rem Builds Deskosaur.exe with the C# compiler that ships with Windows (.NET Framework 4.x)
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find csc.exe from the .NET Framework 4.x.
  pause
  exit /b 1
)
cd /d "%~dp0"
"%CSC%" /nologo /target:winexe /optimize+ /out:Deskosaur.exe /win32icon:src\Win\deskosaur.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Core\*.cs src\Win\*.cs
if errorlevel 1 (
  echo.
  echo Build failed.
  pause
  exit /b 1
)
echo Built Deskosaur.exe
pause
