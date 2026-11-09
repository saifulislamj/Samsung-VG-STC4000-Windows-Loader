@echo off
echo ====================================================
echo  Building Samsung VG-STC4000 Windows Suite
echo  Developer: Saiful Islam (https://github.com/saifulislamj)
echo ====================================================
echo.

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
taskkill /F /IM SamsungCamLoader.exe >nul 2>&1
timeout /t 1 /nobreak >nul
if exist SamsungCamLoader_old.exe del /f /q SamsungCamLoader_old.exe >nul 2>&1

echo [1/2] Building Setup.exe with Admin Manifest and Icon...
%CSC% /target:exe /win32icon:camera.ico /win32manifest:app.manifest /out:Setup.exe Setup.cs
if %ERRORLEVEL% NEQ 0 goto error

echo [2/2] Building SamsungCamLoader.exe with Icon...
%CSC% /target:exe /win32icon:camera.ico /out:SamsungCamLoader.exe SamsungCamLoader.cs
if %ERRORLEVEL% NEQ 0 goto error

echo.
echo ====================================================
echo  Build successful! Setup.exe and SamsungCamLoader.exe ready!
echo ====================================================
goto end

:error
echo Build failed!

:end
pause
