@echo off
setlocal
set "DROP_PATH_ROOT=%~dp0"
set "DROP_PATH_EXE=%DROP_PATH_ROOT%bin\TerminalDropPath.exe"

if not exist "%DROP_PATH_EXE%" (
    powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%DROP_PATH_ROOT%build.ps1"
    if errorlevel 1 exit /b 1
)

"%DROP_PATH_EXE%" --shell cmd
exit /b %errorlevel%
