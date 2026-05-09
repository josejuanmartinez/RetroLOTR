@echo off
chcp 65001 >nul
echo Starting WhatsApp Claude Bridge...
echo.
cd /d "%~dp0"
node index.js
pause
