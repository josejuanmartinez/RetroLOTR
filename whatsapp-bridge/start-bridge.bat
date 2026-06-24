@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo Clearing stale browser locks from previous runs...
del /q ".wwebjs_auth\session\SingletonLock" 2>nul
del /q ".wwebjs_auth\session\SingletonCookie" 2>nul
del /q ".wwebjs_auth\session\SingletonSocket" 2>nul

echo Starting WhatsApp Claude Bridge...
echo.
node index.js
pause
