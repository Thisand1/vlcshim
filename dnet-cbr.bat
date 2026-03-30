@echo off
setlocal

dotnet clean
if errorlevel 1 exit /b %errorlevel%

dotnet build
if errorlevel 1 exit /b %errorlevel%

start "" "%~dp0bin\Debug\net8.0-windows10.0.26100.0\vlcshimdebugfr.exe" %*
