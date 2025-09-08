# DetailedProfileCard

A specialized profile card component for displaying a single student's information in an expanded, detailed format that fills the available area.

## Purpose

`DetailedProfileCard` is automatically used by `StudentGalleryView` when exactly one student is displayed (e.g., when search results are filtered down to a single student). It provides a comprehensive view with:

- Large profile image (240x240px) on the left side
- All student details displayed clearly on the right side
- Professional layout optimized for readability
- Interactive image editing capability
- Quick action buttons for additional functionality

## Usage

The component is automatically shown by `StudentGalleryView` when `Students.Count == 1`. It's not intended for direct instantiation in other contexts.

```xml
<!-- This is handled automatically by StudentGalleryView -->
<profileCard:DetailedProfileCard DataContext="{Binding Student}" 
                                ImageClicked="OnDetailedProfileImageClicked"/>
```

## Features

### Layout
- **Left Side**: Large circular profile image with editing hint
- **Right Side**: Complete student information in organized sections
- **Full Width**: Utilizes entire available area with proper margins
- **Responsive**: Adapts to container size with proper spacing

### Information Display
- **Name**: Large prominent heading (36px)
- **Class/Role**: Highlighted badge with blue background
- **Email**: Full email address with clear labeling
- **Mentor**: Secondary information with proper formatting
- **Enrollment Date**: Formatted date display
- **Student ID**: Monospace font for technical readability

### Interactions
- **Image Click**: Triggers `ImageClicked` event for photo editing
- **Hover Effects**: Subtle card and image scaling animations
- **Clean Interface**: Focused on essential information without distracting action buttons

### Visual Design
- **Card Style**: White background with subtle shadow and rounded corners
- **Professional Colors**: App theme colors (#3b536b, #2c3e50) with gray text hierarchy
- **Spacing**: Optimized margins and padding for perfect fit under search bar
- **Typography**: Clear font sizes and weights for information hierarchy
- **Full Utilization**: Takes up entire available space in StudentGallery

## Events

### ImageClicked
```csharp
public event EventHandler<Student>? ImageClicked;
```
Raised when the profile image is clicked, enabling photo editing functionality.

## Dependencies

- Inherits from `UserControl`
- Uses `UniversalImageConverter` for image display
- Uses `StudentEnrollmentConverter` for date formatting
- Requires `IPerson` interface implementation

## Styling

The component uses Avalonia's transition system for smooth animations:
- Card hover scaling (scale 1.02)
- Image hover effects (scale 1.05)
- Shadow transitions for depth
- Professional color scheme matching the application theme

## Integration

This component integrates seamlessly with:
- `StudentGalleryView` for automatic display switching
- `ImageCropWindow` for profile photo editing
- Existing student data models and services
- Application-wide styling and theming