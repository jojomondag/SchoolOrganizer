# Star Rating Crash and Display Issues - Fixed

## Date Fixed
October 30, 2025

## Problem Description

When clicking on star ratings in the assignment viewer to rate assignments, the application would crash or the stars would not display correctly. The issues included:

1. **Application Crashing** - Clicking a star would cause the app to terminate unexpectedly
2. **Stars Not Filling Correctly** - When clicking star 3, only star 1 would fill instead of stars 1, 2, and 3
3. **Random Star Behavior** - Stars would fill in seemingly random patterns

## Root Causes

### 1. Null Dictionary References
The `AssignmentRatings` dictionary (and related dictionaries) in the `Student` model could be `null` when deserialized from JSON without these properties, causing `NullReferenceException` when trying to access them.

**Location**: `src/ViewModels/StudentDetailViewModel.cs` - `SaveAssignmentRatingAsync` method

### 2. TwoWay Binding Feedback Loop
In the `OnAssignmentRatingChanged` event handler, the code was manually setting `assignmentGroup.Rating = newRating`, which created a feedback loop with the TwoWay binding:
- User clicks star → StarRating updates
- TwoWay binding updates AssignmentGroup.Rating
- Event handler manually sets AssignmentGroup.Rating again ⚠️
- This triggered binding to update StarRating.Rating again
- Feedback loop caused random behavior

**Location**: `src/Views/AssignmentManagement/AssignmentViewer.axaml.cs` - Line 610

### 3. Wrong Icon Types
The code was using incorrect `MaterialIconKind` enum values:
- Using `MaterialIconKind.Star` which resolved to `Kind=Grade` instead of a filled star
- Using `MaterialIconKind.StarOutline` which resolved to `Kind=StarBorder` instead of an outlined star

**Location**: `src/Views/Components/StarRating.axaml.cs` - `UpdateStars` method

### 4. Missing Resource
The style was referencing `{DynamicResource StarGoldBrush}` which depended on `{StaticResource StarGoldColor}`, but this resource wasn't properly initialized in the theme dictionaries, causing a "Static resource 'StarGoldColor' not found" error.

**Location**: `App.axaml` - Star rating styles

### 5. Incorrect Initial Icon State
Stars in the XAML were initialized with `Kind="Star"` (filled) instead of `Kind="StarBorder"` (empty), preventing the `UpdateStars()` method from properly toggling their state.

**Location**: `src/Views/Components/StarRating.axaml`

## Solutions Applied

### 1. Added Null Checks and Dictionary Initialization

**File**: `src/ViewModels/StudentDetailViewModel.cs`

```csharp
public async Task SaveAssignmentRatingAsync(string assignmentName, int rating)
{
    if (Student == null)
    {
        Log.Warning("Cannot save assignment rating - Student object is null");
        return;
    }

    if (string.IsNullOrWhiteSpace(assignmentName))
    {
        Log.Warning("Cannot save assignment rating - assignment name is null or empty");
        return;
    }

    try
    {
        // Initialize AssignmentRatings if null (can happen if deserialized from JSON without this property)
        if (Student.AssignmentRatings == null)
        {
            Student.AssignmentRatings = new Dictionary<string, int>();
        }

        // Update the rating in the student's data
        if (rating > 0)
        {
            Student.AssignmentRatings[assignmentName] = rating;
        }
        else
        {
            // Remove rating if set to 0
            if (Student.AssignmentRatings.ContainsKey(assignmentName))
            {
                Student.AssignmentRatings.Remove(assignmentName);
            }
        }

        // Save to JSON
        await SaveStudentToJson();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error saving assignment rating for {AssignmentName} - {ExceptionType}: {Message}", 
            assignmentName, ex.GetType().Name, ex.Message);
    }
}
```

Similar null checks were added to:
- `SaveAssignmentNoteAsync`
- `SaveAssignmentNotesSidebarWidthAsync`
- `SaveStudentToJson`

### 2. Removed Manual Assignment from Event Handler

**File**: `src/Views/AssignmentManagement/AssignmentViewer.axaml.cs`

**Before**:
```csharp
// Update the assignment group's rating first (in case binding didn't do it)
assignmentGroup.Rating = newRating;  // ❌ This caused feedback loop

// Save the rating
await viewModel.SaveAssignmentRatingAsync(assignmentGroup.AssignmentName, newRating);
```

**After**:
```csharp
// Don't manually set assignmentGroup.Rating here - the TwoWay binding handles it automatically
// Setting it manually causes a feedback loop with the binding system

// Save the rating to database
await viewModel.SaveAssignmentRatingAsync(assignmentGroup.AssignmentName, newRating);
```

### 3. Fixed Icon Types

**File**: `src/Views/Components/StarRating.axaml.cs`

```csharp
// For filled stars
starIcons[i].Kind = MaterialIconKind.Star;  // Correct filled star icon

// For empty stars
starIcons[i].Kind = MaterialIconKind.StarBorder;  // Correct outline star icon
```

### 4. Changed Resource to Direct Color

**File**: `App.axaml`

**Before**:
```xml
<Style Selector="materialIcons|MaterialIcon.StarFilled">
    <Setter Property="Foreground" Value="{DynamicResource StarGoldBrush}"/>
</Style>
```

**After**:
```xml
<Style Selector="materialIcons|MaterialIcon.StarFilled">
    <Setter Property="Foreground" Value="#FFD700"/>
</Style>
```

### 5. Fixed Initial Icon State in XAML

**File**: `src/Views/Components/StarRating.axaml`

**Before**:
```xml
<materialIcons:MaterialIcon Classes="StarIcon" Name="Star1Icon" Kind="Star"/>
```

**After**:
```xml
<materialIcons:MaterialIcon Classes="StarIcon StarEmpty" Name="Star1Icon" Kind="StarBorder"/>
```

### 6. Added Global Exception Handler

**File**: `App.axaml.cs`

Added unhandled exception handlers to catch and log any future crashes:

```csharp
// Add global exception handler
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    var exception = e.ExceptionObject as Exception;
    Log.Fatal(exception, "UNHANDLED EXCEPTION - IsTerminating: {IsTerminating}", e.IsTerminating);
    Log.Fatal("Exception Type: {ExceptionType}", exception?.GetType().FullName);
    Log.Fatal("Stack Trace: {StackTrace}", exception?.StackTrace);
};

System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    Log.Fatal(e.Exception, "UNOBSERVED TASK EXCEPTION");
    e.SetObserved(); // Prevent process termination
};
```

### 7. Added Re-entrancy Protection

**File**: `src/Views/AssignmentManagement/AssignmentViewer.axaml.cs`

```csharp
private bool _isProcessingRatingChange = false;

private async void OnAssignmentRatingChanged(object? sender, int newRating)
{
    // Prevent re-entrancy
    if (_isProcessingRatingChange)
    {
        Log.Debug("OnAssignmentRatingChanged - Already processing a rating change, skipping");
        return;
    }

    _isProcessingRatingChange = true;
    try
    {
        // ... event handling logic ...
    }
    finally
    {
        _isProcessingRatingChange = false;
    }
}
```

## Files Modified

1. `src/ViewModels/StudentDetailViewModel.cs` - Added null checks and dictionary initialization
2. `src/Views/AssignmentManagement/AssignmentViewer.axaml.cs` - Removed feedback loop and added re-entrancy protection
3. `src/Views/Components/StarRating.axaml.cs` - Fixed icon types and improved error handling
4. `src/Views/Components/StarRating.axaml` - Fixed initial icon state
5. `App.axaml` - Changed resource reference to direct color
6. `App.axaml.cs` - Added global exception handlers

## Expected Behavior After Fix

✅ Clicking on star 3 fills stars 1, 2, and 3 (all stars up to the clicked one)
✅ Clicking on star 5 fills all 5 stars
✅ Clicking the same star twice clears all stars (rating = 0)
✅ Rating is saved to the database without crashes
✅ No feedback loops or random star behavior
✅ No null reference exceptions

## Testing Notes

- Test with students that have existing assignment ratings
- Test with students that have no assignment ratings (null dictionaries)
- Test clicking different stars in sequence
- Test clearing ratings by clicking the same star twice
- Verify ratings persist after closing and reopening the assignment viewer

## Lessons Learned

1. **Always initialize collections in models** - Dictionaries should never be null, even when deserialized from JSON
2. **Avoid manual updates with TwoWay bindings** - Let the binding system handle synchronization
3. **Use correct enum values** - Material icon kinds must match the actual icon names
4. **Prefer direct values over dynamic resources** - When resources aren't theme-dependent, use direct values for simplicity
5. **Add defensive programming** - Re-entrancy protection and null checks prevent cascading failures
6. **Global exception handlers are essential** - They help diagnose issues that would otherwise crash silently

