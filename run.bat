@echo off
title FlowClicker - Windows 11 AutoClicker
cd /d "%~dp0"

set "EXE_PATH=src\FlowClicker\bin\Release\net10.0-windows\FlowClicker.exe"

echo Dang kiem tra ban build FlowClicker...
dotnet build src\FlowClicker\FlowClicker.csproj -c Release
if errorlevel 1 (
    echo.
    echo [LOI] Khong the bien dich. Neu FlowClicker dang mo, hay dong ung dung va chay lai.
    pause
    exit /b 1
)

echo ========================================================
echo  Dang khoi chay FlowClicker (Windows 11 AutoClicker)...
echo  Phim tat mac dinh: F6 (Bat/Tat) ^| Shift+Esc (Khan cap)
echo ========================================================
start "" "%EXE_PATH%"
exit /b 0
