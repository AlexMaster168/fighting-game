@echo off
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.0.77f1\Editor\Unity.exe"
echo Building Shadow Arena, this takes a few minutes...
%UNITY% -batchmode -nographics -quit -projectPath "%~dp0." -executeMethod Builder.BuildWindows -logFile "%~dp0build.log"
if errorlevel 1 goto fail
echo.
echo BUILD OK: "%~dp0Build\ShadowArena.exe"
start "" "%~dp0Build\ShadowArena.exe"
goto end
:fail
echo BUILD FAILED - see build.log
:end
pause
