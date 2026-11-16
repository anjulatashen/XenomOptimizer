@echo off
echo ==============================================
echo  Clearing Windows Explorer Icon Cache...
echo ==============================================
echo.
taskkill /f /im explorer.exe
timeout /t 2 /nobreak >nul
del /f /q "%localappdata%\IconCache.db" 2>nul
del /f /q "%localappdata%\Microsoft\Windows\Explorer\iconcache*.db" 2>nul
start explorer.exe
echo.
echo Icon Cache cleared and Explorer restarted!
