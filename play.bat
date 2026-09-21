@echo off
set UNITY="C:\Program Files\Unity\Hub\Editor\6000.0.77f1\Editor\Unity.exe"
start "" %UNITY% -projectPath "%~dp0." -executeMethod Launcher.Play
