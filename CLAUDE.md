# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SchoolOrganizer is a C# Avalonia UI desktop application for managing student profiles with Google Classroom integration. Built using MVVM pattern with CommunityToolkit.Mvvm and .NET 9.0.

## Build & Run Commands

**CRITICAL**: Always use build scripts instead of individual dotnet commands.

### Windows
```bash
build.bat          # Build and run the application
dotnet build       # Build only (Debug mode)
dotnet run         # Run after building
```

### macOS/Linux
```bash
./build.sh         # Build the project (does not run)
dotnet run         # Run the application
```

## Architecture

### Core Patterns

- **IPerson Interface**: All entities implement `Models/IPerson.cs` as the core abstraction
- **Reusable ProfileCard**: `Views/ProfileCard/ProfileCard.axaml` is a reusable component that can display any `IPerson` implementation
- **MVVM with CommunityToolkit**: ViewModels inherit from `ViewModelBase`, use `[ObservableProperty]` for properties and `[RelayCommand]` for commands
- **Async/Await**: Always use `async Task`, never `async void`
- **Navigation**: View switching via `MainWindowViewModel.CurrentViewModel` property with DataTemplates defined in `Views/MainWindow.axaml`

### Data Flow

1. **Student Data**: `Data/students.json` → `StudentSearchService` → `StudentGalleryViewModel`
2. **Profile Images**:
   - Original images stored in `Data/ProfileImages/Originals/`
   - Cropped images stored in `Data/ProfileImages/`
   - Mapping maintained via `ProfileImageStore` using `profile_sources.json` and `crop_settings.json`
3. **Image Editing Flow**: ProfileCard click event → `ImageCropWindow` → `External/ImageSelector` (embedded Avalonia app)

### Services Architecture

- **No DI Container**: Manual service instantiation throughout the app
- **Static Services**: `ProfileImageStore` (static class)
- **Instance Services**: `StudentSearchService`, `GoogleAuthService`, `UserProfileService`
- **Error Handling**: Try-catch with silent failures for non-critical operations
- **Logging**: Serilog configured in `App.axaml.cs` with console (warnings+) and file (all levels) outputs

### Google Integration

- **Authentication**: OAuth2 flow using `GoogleAuthService` with local server receiver
- **Scopes**: Google Classroom (courses, rosters, profiles) and Drive
- **Credentials**: `Resources/credentials.json` (gitignored, see `Resources/README.md` for setup)
- **Token Storage**: `%AppData%/SchoolOrganizer/` (Windows) or `~/.config/SchoolOrganizer/` (Unix)
- **Token Refresh**: Automatic with retry logic and file lock handling

## Avalonia-Specific Guidelines

### AXAML Essentials

- Extension: `.axaml` (not .xaml)
- Namespace: `xmlns="https://github.com/avaloniaui"`
- Always use `x:DataType` for compiled bindings
- Common controls: `TextBox.Watermark`, `Border.CornerRadius`, `BoxShadow`

### Animation & Transitions (Stability Critical)

- **Use Transitions only** for hover/property changes - never complex KeyFrame animations
- Hover effects: `:pointerover` pseudo-class with Style selectors
- Transform syntax: `Value="scale(1.05)"` (string-based)
- Duration: 0.1s-0.5s recommended
- Always set `RenderTransformOrigin="0.5,0.5"` for centered transforms
- **Test animations immediately** after implementation to avoid crashes

### ViewModels

```csharp
[ObservableProperty]
private string name = string.Empty;

[RelayCommand]
private async Task SaveAsync() { ... }
```

## Key Files & Their Responsibilities

- `App.axaml.cs`: Application entry, Serilog initialization, disables Avalonia validation
- `Models/IPerson.cs`: Core interface with `Id`, `Name`, `PictureUrl`, `Email`, `RoleInfo`, `SecondaryInfo`, `PersonType`
- `Services/ProfileImageStore.cs`: Static service managing image-to-student mapping and crop settings
- `Services/GoogleAuthService.cs`: Google OAuth2 authentication with retry logic
- `ViewModels/MainWindowViewModel.cs`: Root ViewModel, navigation controller, authentication state
- `ViewModels/StudentGalleryViewModel.cs`: Main gallery logic, search, filtering, display modes
- `Views/ProfileCard/ProfileCard.axaml`: Reusable person card component with configurable display levels
- `Views/ProfileCard/DetailedProfileCard.axaml`: Expanded detail view with back button
- `External/ImageSelector/`: Embedded Avalonia image cropping application

## External Projects

`External/ImageSelector/` is a separate Avalonia project for image selection and cropping:
- Built as a referenced project (`ImageSelector.csproj`)
- Excluded from main app compilation via `<Compile Remove="External/**" />` in `SchoolOrganizer.csproj`
- Used via `ImageCropWindow` which hosts the ImageSelector view

## ProfileCard Display System

ProfileCard supports multiple display levels via `ProfileCardDisplayConfig`:
- **Large**: Full detail with all info visible
- **Medium**: Standard size with basic info
- **Small**: Compact view with minimal info
- Configuration controls: image size, font sizes, visibility of email/enrollment/secondary info

## Event Wiring Pattern

ProfileCard instances use event handlers that require re-wiring when DataContext changes:
- `ImageClicked` event for profile image editing
- `CardDoubleClicked` event for detail view navigation
- Parent views (StudentGalleryView) handle event wiring in code-behind
- Special handling for dynamic collections with delayed event wiring after rendering

## Common Tasks

### Adding a New Student Property
1. Update `Models/Student.cs` with new property
2. Update `Data/students.json` with data
3. Modify `Views/ProfileCard/ProfileCard.axaml` if display needed
4. Update `ProfileCardDisplayConfig` if visibility control required

### Modifying Image Storage
- All image operations go through `Services/ProfileImageStore.cs`
- Original images preserved in `Originals/` subdirectory
- Crop settings stored separately from source mapping

### Authentication Flow Changes
- Authentication happens in `StudentGalleryViewModel` (not at app startup)
- `MainWindowViewModel` coordinates authentication state
- Always update both ViewModels when auth state changes
