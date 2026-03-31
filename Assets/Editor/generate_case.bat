@echo off
setlocal

set "PY=C:\SwarmUI\SwarmUI\dlbackend\comfy\python_embeded\python.exe"
set "SCRIPT=%~dp0QwenCaseGenerator.py"

if "%~1"=="" (
    echo Usage: generate_case.bat case.json output_dir [seed]
    exit /b 1
)

set "JSON=%~1"
set "OUT=%~2"
set "SEED="

if not "%~3"=="" set "SEED=--seed %~3"

if "%OUT%"=="" for %%F in ("%JSON%") do set "OUT=%%~dpF"

"%PY%" "%SCRIPT%" --json "%JSON%" --output "%OUT%" %SEED%
