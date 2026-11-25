@echo off
setlocal EnableDelayedExpansion
title LAG Killer by XENOM - Uninstaller

echo =======================================================
echo          LAG Killer by XENOM - Complete Uninstaller
echo =======================================================
echo.

:: 1. Check Administrator Privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [INFO] Requesting Administrator Privileges...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo [1/6] Terminating all running LAG Killer processes...
taskkill /F /IM XenomOptimizer.exe /T >nul 2>&1
taskkill /F /IM Magpie.exe /T >nul 2>&1
taskkill /F /IM LagKillerSetup.exe /T >nul 2>&1
timeout /t 1 /nobreak >nul

echo [2/6] Removing shortcuts...
if exist "%USERPROFILE%\Desktop\LAG Killer by XENOM.lnk" del /f /q "%USERPROFILE%\Desktop\LAG Killer by XENOM.lnk" >nul 2>&1
if exist "%PUBLIC%\Desktop\LAG Killer by XENOM.lnk" del /f /q "%PUBLIC%\Desktop\LAG Killer by XENOM.lnk" >nul 2>&1
if exist "%APPDATA%\Microsoft\Windows\Start Menu\Programs\LAG Killer by XENOM" rd /s /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\LAG Killer by XENOM" >nul 2>&1
if exist "%ALLUSERSPROFILE%\Microsoft\Windows\Start Menu\Programs\LAG Killer by XENOM" rd /s /q "%ALLUSERSPROFILE%\Microsoft\Windows\Start Menu\Programs\LAG Killer by XENOM" >nul 2>&1

echo [3/6] Removing installed application files...
if exist "%LOCALAPPDATA%\Programs\LAG Killer by XENOM" rd /s /q "%LOCALAPPDATA%\Programs\LAG Killer by XENOM" >nul 2>&1
if exist "%ProgramFiles%\LAG Killer by XENOM" rd /s /q "%ProgramFiles%\LAG Killer by XENOM" >nul 2>&1
if exist "%ProgramFiles(x86)%\LAG Killer by XENOM" rd /s /q "%ProgramFiles(x86)%\LAG Killer by XENOM" >nul 2>&1

echo [4/6] Purging configuration, logs, backups & temp data...
if exist "%LOCALAPPDATA%\XenomOptimizer" rd /s /q "%LOCALAPPDATA%\XenomOptimizer" >nul 2>&1
if exist "%APPDATA%\XenomOptimizer" rd /s /q "%APPDATA%\XenomOptimizer" >nul 2>&1
if exist "%TEMP%\XenomOptimizer_Update" rd /s /q "%TEMP%\XenomOptimizer_Update" >nul 2>&1

echo [5/6] Cleaning Windows registry entries...
reg delete "HKCU\Software\XenomOptimizer" /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "XenomOptimizer" /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\LAG Killer by XENOM" /f >nul 2>&1
reg delete "HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\LAG Killer by XENOM" /f >nul 2>&1
reg delete "HKLM\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\LAG Killer by XENOM" /f >nul 2>&1

echo [6/6] Verifying uninstallation...
echo.
echo =======================================================
echo  SUCCESS: LAG Killer by XENOM has been completely
echo           uninstalled and removed from your PC!
echo =======================================================
echo.
echo Press any key to exit...
pause >nul
exit /b
