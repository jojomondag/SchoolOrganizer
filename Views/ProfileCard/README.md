# ProfileCard Usage Examples

The ProfileCard component is a reusable UI component that can display any person type (Student, Teacher, Personnel) that implements the `IPerson` interface.

## Basic Usage

### For Teachers
```xml
<ItemsControl ItemsSource="{Binding Teachers}">
  <ItemsControl.ItemTemplate>
    <DataTemplate x:DataType="models:Teacher">
      <profileCard:ProfileCard DataContext="{Binding}" />
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

### For Personnel
```xml
<ItemsControl ItemsSource="{Binding Personnel}">
  <ItemsControl.ItemTemplate>
    <DataTemplate x:DataType="models:Personnel">
      <profileCard:ProfileCard DataContext="{Binding}" />
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

### For Students (with selection functionality)
```xml
<ItemsControl ItemsSource="{Binding Students}">
  <ItemsControl.ItemTemplate>
    <DataTemplate x:DataType="models:Student">
      <!-- Use StudentProfileCard for selection features -->
      <studentGallery:StudentProfileCard DataContext="{Binding}" />
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

## Required Namespace Imports
```xml
xmlns:profileCard="using:SchoolOrganizer.Views.ProfileCard"
xmlns:models="using:SchoolOrganizer.Models"
xmlns:studentGallery="using:SchoolOrganizer.Views.StudentGallery"
```

## IPerson Interface Requirements
Any model using ProfileCard must implement:
- `int Id`
- `string Name`
- `string PictureUrl`
- `string Email`
- `string RoleInfo` (e.g., "Class 9A", "Math Department")
- `string? SecondaryInfo` (e.g., "Mentor: John", "Head of Department")
- `PersonType PersonType`

## Component Types
1. **ProfileCard**: Generic component for any IPerson
2. **StudentProfileCard**: Specialized with student selection and commands