# PowerShell script to clean up Data folders and their contents
# This script removes the Data folder from both the build output and project root

Write-Host "Starting cleanup process..." -ForegroundColor Green

# Define the paths to clean
$buildDataPath = "/Users/josef.nobach/Documents/GitHub/SchoolOrganizer/bin/Debug/net9.0/Data"
$projectDataPath = "/Users/josef.nobach/Documents/GitHub/SchoolOrganizer/Data"
$tlhDownloadPath = "/Users/josef.nobach/Desktop/TLH Download"
$logsPath = "/Users/josef.nobach/Documents/GitHub/SchoolOrganizer/logs"

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

# Empty the trash bin
Write-Host "Emptying trash bin..." -ForegroundColor Yellow
try {
    # On macOS, use the rm command to empty trash
    $trashSuccess = $true
    $trashResult = & osascript -e 'tell application "Finder" to empty trash' 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Trash bin has been emptied successfully" -ForegroundColor Green
    } else {
        Write-Host "⚠ Could not empty trash bin: $trashResult" -ForegroundColor Yellow
        $trashSuccess = $false
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
