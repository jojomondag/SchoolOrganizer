# ProfileCard Component

A generic, reusable UI component for displaying any person type that implements the `IPerson` interface.

## Features

- **Circular profile image** with fallback background
- **Responsive text layout** with proper text wrapping
- **Clean, modern design** with consistent spacing
- **Generic implementation** - works with any `IPerson`

## Usage

The ProfileCard is designed to be wrapped by consuming views that need additional functionality like selection, click handling, etc.

### Basic Usage (Display Only)
```xml
<profileCard:ProfileCard DataContext="{Binding SomePerson}" />
```

### With Selection (StudentGallery Example)
```xml
<Button Command="{Binding SelectCommand}" CommandParameter="{Binding}">
  <Border [styling for selection/hover effects]>
    <profileCard:ProfileCard DataContext="{Binding}" />
  </Border>
</Button>
```

## Requirements

Any model using ProfileCard must implement `IPerson`:
- `string Name`
- `string PictureUrl` 
- `string RoleInfo`
- `string? SecondaryInfo`

## Architecture

This component follows the separation of concerns principle:
- **ProfileCard**: Pure presentation component
- **Consuming Views**: Handle interaction, selection, and specialized styling