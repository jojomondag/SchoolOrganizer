# PowerShell script to migrate ImageCrop components from TestAva to SchoolOrganizer
# This script copies files and updates namespaces

$ErrorActionPreference = "Stop"

$sourceBase = "C:\Users\Josef\Desktop\TestAva\TestAva"
$destBase = "C:\Users\Josef\Documents\Github\SchoolOrganizer"

Write-Host "Starting ImageCrop migration..." -ForegroundColor Green

# Create necessary directories
Write-Host "Creating directories..."
$directories = @(
    "$destBase\Views\Windows\ImageCrop",
    "$destBase\Services",
    "$destBase\Views\Styles"
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "  Created: $dir" -ForegroundColor Cyan
    }
}

# Define file mappings: source -> destination
$fileMappings = @{
    # ImageCrop components
    "$sourceBase\Views\ImageCroper\CropPreview.axaml" = "$destBase\Views\Windows\ImageCrop\CropPreview.axaml"
    "$sourceBase\Views\ImageCroper\CropPreview.axaml.cs" = "$destBase\Views\Windows\ImageCrop\CropPreview.axaml.cs"
    "$sourceBase\Views\ImageCroper\ImageHistory.axaml" = "$destBase\Views\Windows\ImageCrop\ImageHistory.axaml"
    "$sourceBase\Views\ImageCroper\ImageHistory.axaml.cs" = "$destBase\Views\Windows\ImageCrop\ImageHistory.axaml.cs"
    "$sourceBase\Views\ImageCroper\ImageCrop.axaml" = "$destBase\Views\Windows\ImageCrop.axaml"
    "$sourceBase\Views\ImageCroper\ImageCrop.axaml.cs" = "$destBase\Views\Windows\ImageCrop.axaml.cs"

    # Theme files
    "$sourceBase\Styles\Colors.axaml" = "$destBase\Views\Styles\Colors.axaml"
    "$sourceBase\Styles\DarkTheme.axaml" = "$destBase\Views\Styles\DarkTheme.axaml"
    "$sourceBase\Styles\LightTheme.axaml" = "$destBase\Views\Styles\LightTheme.axaml"

    # Services
    "$sourceBase\Services\ThemeManager.cs" = "$destBase\Services\ThemeManager.cs"
}

# Namespace replacements
$namespaceReplacements = @{
    "namespace TestAva.Views;" = "namespace SchoolOrganizer.Views.Windows.ImageCrop;"
    "namespace TestAva.Services;" = "namespace SchoolOrganizer.Services;"
    "x:Class=`"TestAva.Views.CropPreview`"" = "x:Class=`"SchoolOrganizer.Views.Windows.ImageCrop.CropPreview`""
    "x:Class=`"TestAva.Views.ImageHistory`"" = "x:Class=`"SchoolOrganizer.Views.Windows.ImageCrop.ImageHistory`""
    "x:Class=`"TestAva.Views.ImageCrop`"" = "x:Class=`"SchoolOrganizer.Views.Windows.ImageCrop`""
    "x:Class=`"TestAva.Views.MainImageDisplay`"" = "x:Class=`"SchoolOrganizer.Views.Windows.ImageCrop.MainImageDisplay`""
    "xmlns:views=`"using:TestAva.Views`"" = "xmlns:views=`"using:SchoolOrganizer.Views.Windows.ImageCrop`""
    "avares://TestAva/" = "avares://SchoolOrganizer/"
    "TestAva.Services.ThemeManager" = "SchoolOrganizer.Services.ThemeManager"
    "TestAva.Services.AppTheme" = "SchoolOrganizer.Services.AppTheme"
}

# Copy and transform files
Write-Host "`nCopying and transforming files..." -ForegroundColor Green

foreach ($mapping in $fileMappings.GetEnumerator()) {
    $source = $mapping.Key
    $dest = $mapping.Value

    if (-not (Test-Path $source)) {
        Write-Host "  WARNING: Source file not found: $source" -ForegroundColor Yellow
        continue
    }

    Write-Host "  Processing: $(Split-Path $source -Leaf)" -ForegroundColor Cyan

    # Read source file
    $content = Get-Content -Path $source -Raw -Encoding UTF8

    # Apply namespace replacements
    foreach ($replacement in $namespaceReplacements.GetEnumerator()) {
        $content = $content -replace [regex]::Escape($replacement.Key), $replacement.Value
    }

    # Write to destination
    $content | Out-File -FilePath $dest -Encoding UTF8 -NoNewline
    Write-Host "    -> Saved to: $dest" -ForegroundColor Gray
}

Write-Host "`nFile migration complete!" -ForegroundColor Green

# Update App.axaml to include theme resources
Write-Host "`nUpdating App.axaml with theme resources..." -ForegroundColor Green

$appAxamlPath = "$destBase\App.axaml"
if (Test-Path $appAxamlPath) {
    $appContent = Get-Content -Path $appAxamlPath -Raw

    # Check if Colors.axaml is already referenced
    if ($appContent -notmatch "Colors.axaml") {
        Write-Host "  Adding theme resource references to App.axaml..." -ForegroundColor Cyan

        # Find the Application.Resources or Application.Styles section
        if ($appContent -match "<Application\.Resources>") {
            $insertion = @"
    <ResourceInclude Source="avares://SchoolOrganizer/Views/Styles/Colors.axaml"/>
    <ResourceInclude Source="avares://SchoolOrganizer/Views/Styles/LightTheme.axaml"/>
"@
            $appContent = $appContent -replace "(<Application\.Resources>)", "`$1`n$insertion"
        }
        elseif ($appContent -match "<Application\.Styles>") {
            # If no Resources section, add one before Styles
            $insertion = @"
<Application.Resources>
    <ResourceInclude Source="avares://SchoolOrganizer/Views/Styles/Colors.axaml"/>
    <ResourceInclude Source="avares://SchoolOrganizer/Views/Styles/LightTheme.axaml"/>
  </Application.Resources>


"@
            $appContent = $appContent -replace "(<Application\.Styles>)", "$insertion`$1"
        }

        $appContent | Out-File -FilePath $appAxamlPath -Encoding UTF8 -NoNewline
        Write-Host "    -> App.axaml updated" -ForegroundColor Gray
    }
    else {
        Write-Host "  Theme resources already referenced in App.axaml" -ForegroundColor Gray
    }
}

Write-Host "`nMigration Summary:" -ForegroundColor Green
Write-Host "  - Copied $($fileMappings.Count) files" -ForegroundColor White
Write-Host "  - Updated namespaces from TestAva to SchoolOrganizer" -ForegroundColor White
Write-Host "  - Created necessary directory structure" -ForegroundColor White
Write-Host "  - Updated App.axaml with theme references" -ForegroundColor White

Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "  1. Run: dotnet build" -ForegroundColor White
Write-Host "  2. Fix any remaining compilation errors" -ForegroundColor White
Write-Host "  3. Test the new ImageCrop functionality" -ForegroundColor White

Write-Host "`nMigration script completed successfully!" -ForegroundColor Green
