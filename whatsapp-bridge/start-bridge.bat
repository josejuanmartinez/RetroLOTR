@echo off
chcp 65001 >nul
echo Starting WhatsApp Kimi Bridge...
echo.
cd /d "%~dp0"
node index.js
pause
