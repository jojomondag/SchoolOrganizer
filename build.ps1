#!/usr/bin/env pwsh
# PowerShell script for building and running the SchoolOrganizer project
# Works on both Windows and macOS/Linux (requires PowerShell Core)

param(
    [switch]$Clean,
    [switch]$Build,
    [switch]$Run,
    [switch]$All
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Function to run dotnet commands
function Invoke-DotNetCommand {
    param(
        [string]$Command,
        [string]$Arguments = ""
    )
    
    Write-Host "Running: dotnet $Command $Arguments" -ForegroundColor Green
    
    try {
        if ($Arguments) {
            & dotnet $Command $Arguments.Split(' ')
        } else {
            & dotnet $Command
        }
        
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet $Command failed with exit code $LASTEXITCODE"
        }
        
        Write-Host "✓ dotnet $Command completed successfully" -ForegroundColor Green
    }
    catch {
        Write-Error "✗ dotnet $Command failed: $_"
        exit 1
    }
}

# Main execution logic
if ($All -or $Clean) {
    Write-Host "Cleaning project..." -ForegroundColor Yellow
    Invoke-DotNetCommand "clean"
}

if ($All -or $Build) {
    Write-Host "Building project..." -ForegroundColor Yellow
    Invoke-DotNetCommand "build"
}

if ($All -or $Run) {
    Write-Host "Running project..." -ForegroundColor Yellow
    Invoke-DotNetCommand "run"
}

# If no parameters provided, run all commands
if (-not ($Clean -or $Build -or $Run -or $All)) {
    Write-Host "No parameters provided. Running clean, build, and run..." -ForegroundColor Cyan
    Invoke-DotNetCommand "clean"
    Invoke-DotNetCommand "build"
    Invoke-DotNetCommand "run"
}

Write-Host "Script completed successfully!" -ForegroundColor Green
