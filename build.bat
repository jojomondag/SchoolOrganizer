@echo off
REM Build script for SchoolOrganizer
REM Cleans, builds, and runs the Avalonia application

echo 🧹 Cleaning project...
::dotnet clean

echo.
echo 🔨 Building project in Debug mode...
dotnet build --configuration Debug

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ✅ Build successful! Starting application...
    echo.
    dotnet run
) else (
    echo.
    echo ❌ Build failed! Please check the errors above.
    exit /b 1
)
