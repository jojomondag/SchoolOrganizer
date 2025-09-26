# SchoolOrganizer - C# Avalonia Development Rules

## Agent Approach
**CONCISE PROBLEM-SOLVING**: Focus on direct solutions with minimal explanation.

## Project Overview
C# Avalonia UI desktop app using AXAML, MVVM with CommunityToolkit.Mvvm. Manages student profiles with embedded `External/ImageSelector` for image editing.

## Architecture

### Core Patterns
- All entities implement `IPerson` interface (`Models/IPerson.cs`)
- `Views/ProfileCard/ProfileCard.axaml` is reusable component for any `IPerson`
- ViewModels: `ViewModelBase` + `[ObservableProperty]` + `[RelayCommand]`
- **Always use `async Task`, never `async void`**
- Navigation via `MainWindowViewModel.CurrentViewModel` + DataTemplates

### Data Flow
1. JSON in `Data/students.json` → `StudentSearchService` → `StudentGalleryViewModel`
2. Images: `Data/ProfileImages/` with original/crop mapping via `ProfileImageStore`
3. Image editing: ProfileCard event → `ImageCropWindow` → `External/ImageSelector`

### Services
- No DI container - manual instantiation
- `ProfileImageStore` (static), `StudentSearchService` (instance)
- Try-catch with silent failures for non-critical operations

## Build & Debug
**CRITICAL**: Always use build scripts instead of individual dotnet commands
```bash
./build.sh    # macOS/Linux
build.bat     # Windows
```

## Avalonia Guidelines

### Animations (Stability Critical)
- **Use Transitions only** for hover/property changes - never complex KeyFrame animations
- Hover: `:pointerover` + Style selectors with Transitions
- Transform syntax: `Value="scale(1.05)"` (string-based)
- Duration: 0.1s-0.5s, `RenderTransformOrigin="0.5,0.5"`

### AXAML Essentials
- Extension: `.axaml`
- Namespace: `xmlns="https://github.com/avaloniaui"`
- Use `x:DataType` for compiled bindings
- Controls: `TextBox.Watermark`, `Border.CornerRadius`, `BoxShadow`

### C# Code
- `[ObservableProperty] private string name = string.Empty;`
- Inherit from `ViewModelBase` or `ObservableObject`
- Null-conditional operators: `?.`
- Compiled bindings preferred

### Error Prevention
- Test animations immediately after implementation
- Use simple properties before adding transitions
- Prioritize stability over complex animations
- Incremental changes with validation

## Key Files
- `Models/IPerson.cs` - Core interface
- `Services/ProfileImageStore.cs` - Image patterns
- `ViewModels/StudentGalleryViewModel.cs` - Main logic
