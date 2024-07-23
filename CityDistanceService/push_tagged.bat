@echo off
setlocal enabledelayedexpansion

:: Step 1: Parse the command line argument for the message
set "MESSAGE="
:argloop
if "%1"=="" goto argdone
if "%1"=="-msg" (
    shift
    set "MESSAGE=%1"
    shift
    goto argloop
)
shift
goto argloop
:argdone

:: Check if message is empty
if "%MESSAGE%"=="" (
    echo Message is required. Use -msg to specify the commit message.
    exit /b 1
)

:: Step 2: Extract the version
for /f "tokens=*" %%i in ('findstr /R /C:"Version = " ".\src\Constants.cs"') do (
    set "line=%%i"
    for /f "tokens=2 delims==" %%a in ("!line!") do (
        set "VERSION=%%a"
    )
)
:: Remove quotes
set VERSION=%VERSION:"=%

:: Check if version is empty
if "%VERSION%"=="" (
    echo Version could not be extracted from .\src\Constants.cs
    exit /b 1
)

:: Step 3: Execute the Git commands
git add -A
git commit -m "%MESSAGE%"
git tag -d %VERSION%
git tag %VERSION%
git push origin :refs/tags/%VERSION%
git push origin %VERSION%