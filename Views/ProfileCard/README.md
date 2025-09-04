# ProfileCard Component# ProfileCard Component



A generic, reusable UI component for displaying any person type that implements the `IPerson` interface.A generic, reusable UI component for displaying any person type that implements the `IPerson` interface.



## Features## Features



- **Clickable circular profile image** with fallback background and hover effects- **Circular profile image** with fallback background

- **Camera overlay on hover** to indicate the image is clickable for editing- **Responsive text layout** with proper text wrapping

- **Responsive text layout** with proper text wrapping- **Clean, modern design** with consistent spacing

- **Clean, modern design** with consistent spacing- **Generic implementation** - works with any `IPerson`

- **Generic implementation** - works with any `IPerson`

- **Event-driven** - raises `ImageClicked` event when profile image is clicked## Usage



## UsageThe ProfileCard is designed to be wrapped by consuming views that need additional functionality like selection, click handling, etc.



The ProfileCard is designed to be wrapped by consuming views that need additional functionality like selection, click handling, etc.### Basic Usage (Display Only)

```xml

### Basic Usage (Display Only)<profileCard:ProfileCard DataContext="{Binding SomePerson}" />

```xml```

<profileCard:ProfileCard DataContext="{Binding SomePerson}" />

```### With Selection (StudentGallery Example)

```xml

### With Selection and Image Editing (StudentGallery Example)<Button Command="{Binding SelectCommand}" CommandParameter="{Binding}">

```xml  <Border [styling for selection/hover effects]>

<Button Command="{Binding SelectCommand}" CommandParameter="{Binding}">    <profileCard:ProfileCard DataContext="{Binding}" />

  <Border [styling for selection/hover effects]>  </Border>

    <profileCard:ProfileCard DataContext="{Binding}" </Button>

                            ImageClicked="OnProfileImageClicked" />```

  </Border>

</Button>## Requirements

```

Any model using ProfileCard must implement `IPerson`:

### Code-behind Event Handling- `string Name`

```csharp- `string PictureUrl` 

private async void OnProfileImageClicked(object? sender, Student student)- `string RoleInfo`

{- `string? SecondaryInfo`

    // Handle image editing logic

    var newImagePath = await ImageCropWindow.ShowForStudentAsync(this, student.Id);## Architecture

    if (!string.IsNullOrEmpty(newImagePath))

    {This component follows the separation of concerns principle:

        await viewModel.UpdateStudentImage(student, newImagePath);- **ProfileCard**: Pure presentation component

    }- **Consuming Views**: Handle interaction, selection, and specialized styling
}
```

## Events

### ImageClicked
- **Type**: `EventHandler<Student>`
- **Description**: Raised when the user clicks on the profile image
- **Usage**: Connect this event to open image editing dialogs or image selection UI

## Requirements

Any model using ProfileCard must implement `IPerson`:
- `string Name`
- `string PictureUrl` 
- `string RoleInfo`
- `string? SecondaryInfo`

## Architecture

This component follows the separation of concerns principle:
- **ProfileCard**: Pure presentation component with image click handling
- **Consuming Views**: Handle interaction, selection, and specialized styling
- **Event-driven**: Uses events to communicate with parent components for image editing

## Visual Behavior

- **Normal State**: Clean circular image with subtle border
- **Hover State**: Slight opacity change and camera icon overlay
- **Click State**: Triggers `ImageClicked` event
- **Tooltip**: "Click to change profile image" appears on hover