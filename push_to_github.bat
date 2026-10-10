@echo off
title Push to GitHub - Car Rental Management System
cls
echo ===================================================
echo   Pushing Car Rental Management System to GitHub
echo ===================================================
echo.

set "GIT_EXE=C:\Program Files\Git\cmd\git.exe"
set "PATH=C:\Program Files\Git\cmd;C:\Program Files\Git\mingw64\bin;%PATH%"

if not exist "%GIT_EXE%" (
    echo [ERROR] Git was not found at %GIT_EXE%
    pause
    exit /b 1
)

echo [1/2] Checking Git repository status...
"%GIT_EXE%" status --short
echo.

echo [2/2] Pushing branch main to GitHub origin...
"%GIT_EXE%" push -u origin main

if %ERRORLEVEL% equ 0 (
    echo.
    echo ===================================================
    echo [SUCCESS] Project uploaded successfully to GitHub!
    echo https://github.com/zaidwork/Car-Rental-Management-System
    echo ===================================================
) else (
    echo.
    echo ===================================================
    echo [ERROR] Push failed. Return code: %ERRORLEVEL%
    echo If a browser window opened, please complete sign-in.
    echo ===================================================
)

echo.
pause
