# Image Selector for School Organizer

## Overview
The Image Selector component allows users to select profile images for students. It provides both a gallery of existing images and the ability to browse for new images from the file system.

## Components Created

### 1. ImageSelectorViewModel.cs
- Manages the image gallery state
- Handles image selection logic
- Loads images from the ProfileImages folder
- Provides commands for browsing, selecting, and confirming image selection

### 2. ImageSelectorWindow.axaml
- XAML layout for the image selector window
- Displays images in a grid layout
- Provides browse button and selection controls
- Shows selected image information

### 3. ImageSelectorWindow.axaml.cs
- Code-behind for the image selector window
- Handles file dialog integration
- Manages image copying to the profile images folder
- Provides static method for easy dialog display

### 4. FileNameConverter.cs
- Custom converter for displaying file names in the UI
- Extracts filename from full file paths

## How to Use

### From Any Window:
```csharp
var selectedImagePath = await ImageSelectorWindow.ShowAsync(parentWindow);
if (!string.IsNullOrEmpty(selectedImagePath))
{
    // Use the selected image path
    Console.WriteLine($"Selected: {selectedImagePath}");
}
```

### Integration Example:
The StudentGalleryView demonstrates the integration:
1. Added "Add Student" button to the gallery
2. When clicked, opens the image selector
3. Selected image path is returned for use

## Features

### Image Gallery
- Displays all images from `Data/ProfileImages` folder
- Supports common image formats (JPG, PNG, BMP, GIF, WebP)
- Grid layout with image previews
- Shows filename below each image
- Visual selection indicator

### File Browser
- "Browse..." button opens system file dialog
- Filters for image files only
- Automatically copies selected images to the ProfileImages folder
- Handles duplicate filenames by appending numbers

### Image Management
- Images are stored in `Data/ProfileImages` folder
- Folder is created automatically if it doesn't exist
- Selected images are copied (not moved) to maintain originals
- Supports relative paths for portability

## Folder Structure
```
Data/
├── students.json
└── ProfileImages/          # Profile images gallery
    ├── student1.jpg
    ├── student2.png
    └── default_avatar.png
```

## Usage in Student Creation
The image selector can be integrated into student creation workflows:

1. Open image selector dialog
2. User selects or browses for image
3. Image is copied to ProfileImages folder
4. Returned path is used for student's PictureUrl property
5. Student data is saved with the image reference

## Customization
- Modify `ImageSelectorWindow.axaml` to change the UI layout
- Update `ImageSelectorViewModel.cs` to add new features
- Change the default images folder in the ViewModel constructor
- Add image validation or processing as needed

## Future Enhancements
- Image resizing/cropping functionality
- Multiple image format support
- Image metadata editing
- Batch image operations
- Cloud storage integration
