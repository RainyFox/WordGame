@echo off
setlocal
cd /d "%~dp0"
set "WORDGAME_WEB_URL=http://127.0.0.1:5276/"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo .NET SDK was not found. Install .NET 10 SDK and try again.
  pause
  exit /b 1
)

powershell.exe -NoProfile -Command ^
  "try { $response = Invoke-WebRequest -UseBasicParsing -Uri '%WORDGAME_WEB_URL%' -TimeoutSec 1; if ($response.StatusCode -eq 200) { exit 0 } } catch { }; exit 1" >nul 2>nul
if not errorlevel 1 (
  echo WordGame Web is already running at %WORDGAME_WEB_URL%
  if /I not "%~1"=="--no-browser" start "" "%WORDGAME_WEB_URL%"
  exit /b 0
)

if /I not "%~1"=="--no-browser" (
  start "" /b powershell.exe -NoProfile -WindowStyle Hidden -Command ^
    "$url = '%WORDGAME_WEB_URL%'; for ($attempt = 0; $attempt -lt 60; $attempt++) { try { $response = Invoke-WebRequest -UseBasicParsing -Uri $url -TimeoutSec 1; if ($response.StatusCode -eq 200) { Start-Process $url; exit 0 } } catch { }; Start-Sleep -Milliseconds 500 }"
)

echo Starting WordGame Web at %WORDGAME_WEB_URL%
dotnet run --project "WebApp\WordGame.Web\WordGame.Web.csproj" --no-launch-profile --no-restore

if errorlevel 1 (
  echo.
  echo WordGame Web stopped because an error occurred.
  pause
)
