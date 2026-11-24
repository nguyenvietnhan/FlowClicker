@echo off
title FlowClicker - Windows 11 AutoClicker
cd /d "%~dp0"

set "EXE_PATH=src\FlowClicker\bin\Release\net10.0-windows\FlowClicker.exe"

echo Building FlowClicker (Release)...
dotnet build src\FlowClicker\FlowClicker.csproj -c Release
if errorlevel 1 (
    echo.
    echo [ERROR] Build failed. If FlowClicker is currently running, close it and try again.
    pause
    exit /b 1
)

echo ========================================================
echo  Launching FlowClicker (Windows 11 AutoClicker)...
echo  Shortcuts: F6 (Auto Clicker) ^| F7 (Pick) ^| F8 (Multi-Point) ^| Shift+Esc (Stop All)
echo ========================================================
start "" "%EXE_PATH%"
exit /b 0
