@echo off
title Samsung VG-STC4000 Uninstaller

net session >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

cd /d "%~dp0"

echo =========================================================
echo    Samsung VG-STC4000 Uninstaller
echo =========================================================
echo.

echo Removing startup shortcut...
del "%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\SamsungCamWatcher.lnk" >nul 2>&1

echo Uninstalling camera driver package...
for /f "tokens=*" %%i in ('pnputil /enum-drivers ^| findstr /i "usb2.0_uvc_hq_webcam_(interface_2).inf"') do (
    echo Found driver package: %%i
)
pnputil /delete-driver "%~dp0driver\usb2.0_uvc_hq_webcam_(interface_2).inf" /uninstall /force >nul 2>&1

echo Removing driver certificate...
certutil -delstore "TrustedPublisher" "USB\VID_04E8&PID_2061&MI_02" >nul 2>&1
certutil -delstore "Root" "USB\VID_04E8&PID_2061&MI_02" >nul 2>&1

echo.
echo Uninstallation complete!
pause
