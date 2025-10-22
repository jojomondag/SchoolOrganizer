# PowerShell script to clean up Data folders and their contents
# This script removes the Data folder from both the build output and project root
# Cross-platform compatible for Windows and macOS

Write-Host "Starting cleanup process..." -ForegroundColor Green

# Get the current script directory and project root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = $scriptDir

# Define the paths to clean (cross-platform)
$buildDataPath = Join-Path $projectRoot "bin\Debug\net9.0\Data"
$projectDataPath = Join-Path $projectRoot "Data"
$logsPath = Join-Path $projectRoot "logs"

# Set TLH Download path based on platform
if ($IsMacOS -or $env:OS -eq $null) {
    # macOS/Linux
    $tlhDownloadPath = Join-Path $env:HOME "Desktop/TLH Download"
} else {
    # Windows
    $userProfile = if ($env:USERPROFILE) { $env:USERPROFILE } else { $env:HOME }
    $tlhDownloadPath = Join-Path $userProfile "Desktop\TLH Download"
}

# Function to safely remove directory and its contents
function Remove-DirectorySafely {
    param(
        [string]$Path,
        [string]$Description
    )
    
    if (Test-Path $Path) {
        Write-Host "Removing $Description at: $Path" -ForegroundColor Yellow
        try {
            Remove-Item -Path $Path -Recurse -Force -ErrorAction Stop
            Write-Host "Successfully removed $Description" -ForegroundColor Green
        }
        catch {
            Write-Host "Error removing $Description : $($_.Exception.Message)" -ForegroundColor Red
            return $false
        }
    }
    else {
        Write-Host "$Description not found at: $Path" -ForegroundColor Gray
    }
    return $true
}

# Remove Data folder from build output
$buildSuccess = Remove-DirectorySafely -Path $buildDataPath -Description "Build Data folder"

# Remove Data folder from project root
$projectSuccess = Remove-DirectorySafely -Path $projectDataPath -Description "Project Data folder"

# Remove contents of TLH Download folder (but keep the folder itself)
if (Test-Path $tlhDownloadPath) {
    Write-Host "Removing contents of TLH Download folder at: $tlhDownloadPath" -ForegroundColor Yellow
    try {
        # Get all items in the folder and remove them
        $items = Get-ChildItem -Path $tlhDownloadPath -Force
        foreach ($item in $items) {
            Remove-Item -Path $item.FullName -Recurse -Force -ErrorAction Stop
        }
        Write-Host "Successfully removed contents of TLH Download folder" -ForegroundColor Green
        $tlhSuccess = $true
    }
    catch {
        Write-Host "Error removing TLH Download folder contents: $($_.Exception.Message)" -ForegroundColor Red
        $tlhSuccess = $false
    }
} else {
    Write-Host "TLH Download folder not found at: $tlhDownloadPath" -ForegroundColor Gray
    $tlhSuccess = $true
}

# Remove logs folder and its contents
$logsSuccess = Remove-DirectorySafely -Path $logsPath -Description "Logs folder"

# Empty the trash bin (cross-platform)
Write-Host "Emptying trash bin..." -ForegroundColor Yellow
try {
    $trashSuccess = $true
    
    # Check if we're on Windows
    if ($IsWindows -or $env:OS -eq "Windows_NT") {
        # On Windows, use PowerShell to empty recycle bin
        if (Get-Command Clear-RecycleBin -ErrorAction SilentlyContinue) {
            $trashResult = Clear-RecycleBin -Force -ErrorAction Stop
            Write-Host "✓ Trash bin has been emptied successfully" -ForegroundColor Green
        } else {
            Write-Host "⚠ Clear-RecycleBin cmdlet not available on this system" -ForegroundColor Yellow
        }
    } else {
        # On macOS/Linux, use rm command to empty trash
        $trashPath = if ($IsMacOS) { "$env:HOME/.Trash" } else { "$env:HOME/.local/share/Trash" }
        if (Test-Path $trashPath) {
            Remove-Item -Path "$trashPath/*" -Recurse -Force -ErrorAction SilentlyContinue
            Write-Host "✓ Trash bin has been emptied successfully" -ForegroundColor Green
        } else {
            Write-Host "⚠ Trash directory not found at: $trashPath" -ForegroundColor Yellow
        }
    }
}
catch {
    Write-Host "⚠ Error emptying trash bin: $($_.Exception.Message)" -ForegroundColor Red
    $trashSuccess = $false
}

# Summary
Write-Host "`nCleanup Summary:" -ForegroundColor Cyan
if ($buildSuccess -and $projectSuccess -and $tlhSuccess -and $logsSuccess -and $trashSuccess) {
    Write-Host "✓ All folders have been successfully removed and trash bin emptied" -ForegroundColor Green
} else {
    Write-Host "⚠ Some operations could not be completed. Check the errors above." -ForegroundColor Yellow
}

Write-Host "`nCleanup process completed." -ForegroundColor Green
