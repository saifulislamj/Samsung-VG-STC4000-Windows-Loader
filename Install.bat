@echo off
setlocal EnableDelayedExpansion
title Samsung VG-STC4000 Camera 1-Click Installer

:: Check for Administrator privileges, auto-elevate if needed
net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"

echo =========================================================
echo    Samsung VG-STC4000 TV Camera 1-Click Installer
echo =========================================================
echo.

echo [1/4] Installing driver security certificate...
certutil -addstore "TrustedPublisher" "%~dp0driver\driver.cer" >nul 2>&1
certutil -addstore "Root" "%~dp0driver\driver.cer" >nul 2>&1
echo       Done!

echo [2/4] Installing camera WinUSB driver into Windows...
pnputil /add-driver "%~dp0driver\usb2.0_uvc_hq_webcam_(interface_2).inf" /install
echo       Done!

echo [3/4] Initializing camera and uploading firmware...
"%~dp0SamsungCamLoader.exe"
echo.

echo [4/4] Setting up automatic background Plug-and-Play watcher...
set "STARTUP_FOLDER=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
powershell -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%STARTUP_FOLDER%\SamsungCamWatcher.lnk'); $s.TargetPath = '%~dp0StartCameraWatcher.vbs'; $s.WorkingDirectory = '%~dp0'; $s.Save()"
echo       Done!

echo.
echo =========================================================
echo    SUCCESS! Samsung VG-STC4000 is ready for use!
echo    You can now open the Windows Camera app, Zoom, etc.
echo =========================================================
echo.
pause
