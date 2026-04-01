@echo off
setlocal

set "ROOT=%~dp0.."
set "INSTALLER_VERSION=%~1"
if "%INSTALLER_VERSION%"=="" set "INSTALLER_VERSION=dev"

set "PUBLISH_DIR=%ROOT%\installer\publish\win-x64"
set "OUTPUT_DIR=%ROOT%\installer\output"
set "NSI_FILE=%ROOT%\installer\vlcshimdebugfr.nsi"

if exist "%PUBLISH_DIR%" rmdir /s /q "%PUBLISH_DIR%"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Publishing self-contained win-x64 build...
dotnet publish "%ROOT%\vlcshimdebugfr.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "%PUBLISH_DIR%"
if errorlevel 1 exit /b 1

set "MAKENSIS="
if defined NSIS_HOME if exist "%NSIS_HOME%\makensis.exe" set "MAKENSIS=%NSIS_HOME%\makensis.exe"
if not defined MAKENSIS if defined NSISDIR if exist "%NSISDIR%\makensis.exe" set "MAKENSIS=%NSISDIR%\makensis.exe"
for %%I in (makensis.exe) do set "MAKENSIS=%%~$PATH:I"
if not defined MAKENSIS if exist "%ProgramFiles(x86)%\NSIS\makensis.exe" set "MAKENSIS=%ProgramFiles(x86)%\NSIS\makensis.exe"
if not defined MAKENSIS if exist "%ProgramFiles%\NSIS\makensis.exe" set "MAKENSIS=%ProgramFiles%\NSIS\makensis.exe"

if not defined MAKENSIS (
    echo Could not find makensis.exe. Install NSIS or add it to PATH.
    exit /b 1
)

echo Building installer...
"%MAKENSIS%" /DVERSION=%INSTALLER_VERSION% "/DPUBLISH_DIR=%PUBLISH_DIR%" "/DOUT_DIR=%OUTPUT_DIR%" "%NSI_FILE%"
if errorlevel 1 exit /b 1

echo Installer created in "%OUTPUT_DIR%"
endlocal
