@echo off
setlocal

echo ============================================
echo   QuickFreeplay - Build Script
echo ============================================
echo.

:: Check for dotnet
where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: dotnet SDK not found. Install .NET SDK 6.0+ from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

:: Build
echo Building QuickFreeplay...
dotnet build QuickFreeplay\QuickFreeplay.csproj -c Debug
if errorlevel 1 (
    echo.
    echo BUILD FAILED. If the DLL copy failed, make sure the game is closed first.
    pause
    exit /b 1
)

echo.
echo ============================================
echo   Build succeeded!
echo   DLL deployed to BepInEx\plugins
echo ============================================
pause
