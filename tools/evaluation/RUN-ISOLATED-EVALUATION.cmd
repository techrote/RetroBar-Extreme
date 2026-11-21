@echo off
setlocal
set "HERE=%~dp0"
if not exist "%HERE%RetroBar-Evaluation.marker" (
    echo This is not a marked, isolated RetroBar evaluation package.
    echo Launch refused. Download the "RetroBar Isolated Evaluation 64-bit" CI artifact.
    pause
    exit /b 2
)
if not exist "%HERE%RetroBar.exe" (
    echo RetroBar.exe is missing. Launch refused.
    pause
    exit /b 2
)
echo Isolated RetroBar evaluation: %LOCALAPPDATA%\RetroBar-Evaluation
echo This temporarily replaces the Explorer taskbar if no other RetroBar is running.
echo Do not continue while other work relies on a responsive Windows shell.
start "" /wait "%HERE%RetroBar.exe" --isolated-evaluator
exit /b %errorlevel%
