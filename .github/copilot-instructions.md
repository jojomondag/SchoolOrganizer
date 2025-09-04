# Copilot Instructions for SchoolOrganizer

## Project Overview
SchoolOrganizer is a **C# Avalonia UI desktop application** using AXAML markup and MVVM pattern with CommunityToolkit.Mvvm. The app manages student profiles with image editing capabilities via an embedded `External/ImageSelector` project.

## Key Architecture Patterns

### IPerson Interface & Generic Components
- All person entities implement `IPerson` interface (`Models/IPerson.cs`)
- `Views/ProfileCard/ProfileCard.axaml` is a **generic, reusable component** for any `IPerson`
- Student model extends `IPerson` with `RoleInfo` (ClassName) and `SecondaryInfo` (Mentor info)
- Use ProfileCard for display-only; wrap with selection/interaction logic in consuming views

### MVVM with CommunityToolkit
- ViewModels inherit from `ViewModelBase` using `[ObservableProperty]` and `[RelayCommand]`
- **Always use `async Task` for async operations, never `async void`**
- Models like `Student` extend `ObservableObject` for data binding
- Example pattern: `[ObservableProperty] private string name = string.Empty;`

### Navigation & View Composition
- `MainWindowViewModel` manages navigation via `CurrentViewModel` property
- Views are resolved through DataTemplates in `MainWindow.axaml`
- Navigation uses tab-style ToggleButtons with custom styling
- Dynamic background colors based on active view (`ActiveContentBrush`)

## Critical Avalonia Patterns (See .cursorrules)

### Animation & Styling
- **Use Transitions, NOT Animations** for hover effects: `<Transitions><TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.2"/></Transitions>`
- Transform syntax: `Value="scale(1.05)"` NOT `<ScaleTransform/>`
- Hover states: `:pointerover` NOT `:hover`
- **Avoid complex KeyFrame animations - they cause crashes**

### File Extensions & Markup
- Use `.axaml` for Avalonia XAML files
- Namespace: `xmlns="https://github.com/avaloniaui"`
- Enable compiled bindings: `x:DataType` in templates, `AvaloniaUseCompiledBindingsByDefault` in csproj

## Data Management

### Student Data Flow
1. JSON persistence in `Data/students.json` (copied to output)
2. `StudentGalleryViewModel` loads via `StudentSearchService`
3. Profile images stored in `Data/ProfileImages/` with original/crop mapping
4. `ProfileImageStore` service handles image lifecycle and crop settings persistence

### Image Editing Workflow
1. ProfileCard raises `ImageClicked` event → triggers `ImageCropWindow.ShowForStudentAsync()`
2. `ImageCropWindow` embeds `External/ImageSelector` project for crop/edit functionality
3. Original images stored in `Data/ProfileImages/Originals/`, mapped via JSON
4. Crop settings persisted per student for re-editing

## External Dependencies

### ImageSelector Integration
- Separate Avalonia project in `External/ImageSelector/`
- Referenced as ProjectReference, excluded from main compilation
- Provides image selection, cropping, rotation via `ImageSelectorView`
- Configure with `SavePathProvider` delegate and `ImageSaved` event

## Development Workflows

### Build & Debug
```bash
dotnet build                    # Standard build
dotnet run                     # Run application
dotnet build --verbosity normal # Detailed build output
```

### Data Seeding
- Modify `Data/students.json` for test data
- Unsplash URLs used for demo profile images
- Profile images auto-copied to output directory

## Common Patterns

### Event-Driven Communication
```csharp
// In ProfileCard
public event EventHandler<Student>? ImageClicked;

// In consuming view
profileCard.ImageClicked += OnProfileImageClicked;
```

### Service Registration & DI
- Services manually instantiated (no DI container)
- `ProfileImageStore` is static utility class
- `StudentSearchService` instantiated in ViewModels

### Error Handling
- Services use try-catch with silent failures for non-critical operations
- File operations wrapped with existence checks
- Async operations properly awaited with error boundaries

## Project Structure Notes
- `External/` contains separate Avalonia projects (excluded from main build)
- `Data/` contains runtime data (JSON files, images) - copied to output
- `Views/ProfileCard/` demonstrates component documentation pattern
- Converters in `Views/` for AXAML data binding transformations

## Key Files for Context
- `.cursorrules` - Detailed Avalonia-specific development rules
- `Models/IPerson.cs` - Core interface for all person entities
- `Views/ProfileCard/README.md` - Component usage documentation
- `Services/ProfileImageStore.cs` - Image management patterns
- `ViewModels/StudentGalleryViewModel.cs` - Main business logic