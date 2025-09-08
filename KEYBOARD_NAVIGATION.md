# Global Keyboard Navigation - SchoolOrganizer

This document describes the global keyboard navigation system implemented in SchoolOrganizer, providing terminal-like keyboard behavior for efficient navigation without using the mouse.

## Overview

The `GlobalKeyboardHandler` class provides comprehensive keyboard navigation throughout the application. When you're anywhere in the student gallery view, you can use keyboard shortcuts to interact with the search functionality and navigate students without touching the mouse.

## Keyboard Commands

### Typing & Search
- **Any letter/number/symbol**: Automatically focuses the search bar and starts typing
- **Backspace**: Focuses search bar and deletes the last character
- **Delete**: Focuses search bar and clears the entire search
- **Ctrl+A**: Focuses search bar and selects all text

### Navigation in Search Bar
- **Left Arrow**: Move cursor left in search bar
- **Right Arrow**: Move cursor right in search bar  
- **Home**: Move cursor to start of search bar
- **End**: Move cursor to end of search bar

### When Search Bar is Focused
- **Escape**: Clear search text and unfocus
- **Enter**: Select the first student from search results
- **Ctrl+Down**: Navigate to first student while keeping search focused
- **Ctrl+Up**: Clear student selection while keeping search focused

### Global Navigation (when search bar is not focused)
- **Up/Down Arrows**: Navigate through student list (with wrap-around)
- **Enter**: Select the first available student
- **Escape**: Clear search and deselect everything

## Implementation Details

### Architecture
The keyboard handling is implemented in a dedicated service class:
- **`Services/GlobalKeyboardHandler.cs`**: Contains all keyboard logic
- **`Views/StudentGallery/StudentGalleryView.axaml.cs`**: Integrates the handler

### Key Features
1. **Automatic Focus**: Any typing automatically focuses the search bar
2. **Context-Aware**: Different behavior when search bar is focused vs not focused  
3. **Terminal-Like**: Familiar navigation patterns for keyboard users
4. **Non-Destructive**: Original mouse functionality remains unchanged

### Integration
The handler is completely self-contained and manages all keyboard functionality:
```csharp
// In OnDataContextChanged
var searchTextBox = this.FindControl<TextBox>("SearchTextBox");
if (searchTextBox != null)
{
    _keyboardHandler?.Dispose(); // Clean up previous handler
    _keyboardHandler = new GlobalKeyboardHandler(viewModel, searchTextBox, this);
}
```

The GlobalKeyboardHandler automatically:
- Sets up keyboard event handling on the host control
- Manages focus behavior to ensure keyboard events are received
- Handles all key processing internally
- Provides proper cleanup when disposed

## Usage Examples

### Quick Search
1. Start typing anywhere in the application
2. Search bar automatically gets focus and your text appears
3. Use arrow keys to navigate results
4. Press Enter to select

### Navigate Without Mouse
1. Use Up/Down arrows to browse through students
2. Press Escape to clear selection
3. Start typing to search for specific students
4. Use Backspace to delete search characters

### Advanced Navigation
1. Ctrl+Down while in search to select first result
2. Ctrl+Up while in search to clear selection
3. Home/End to jump to start/end of search text

## Benefits

- **Speed**: No need to move hands from keyboard to mouse
- **Efficiency**: Quick search and navigation
- **Accessibility**: Better for users who prefer keyboard navigation
- **Familiar**: Uses common keyboard patterns from terminals and text editors
- **Non-Intrusive**: Doesn't interfere with existing functionality