# Fixed: Files Showing Same Content Bug

**Date Fixed:** October 21, 2025
**Severity:** Critical
**Component:** SyntaxHighlightedCodeViewer.cs

---

## Problem Description

When viewing assignments with multiple student files (especially files with the same name stored in different ID-based folders), ALL files were displaying the same content - specifically the content from the last file that was loaded. This happened even though:

- Files were correctly stored in separate ID-based folders (e.g., `2_Variabler/16Yn_3Ml/uppgift.java`, `2_Variabler/1bNzqF6h/main.java`)
- Each file had different actual content on disk
- The file sizes were different (213B, 388B, 528B, 736B, 855B, 1089B)
- Each `StudentFile` object had the correct unique file path

### Example of the Bug

In assignment `2_Variabler`, all files showed the content from `uppgift.java`:
```java
//TIP To <b>Run</b> code, press <shortcut actionId="Run"/> or
// click the <icon src="AllIcons.Actions.Execute"/> icon in the gutter.
public class uppgift 5 {
    public static void main(String[] args) {
```

Even though each file actually contained different code:
- `main.java` (388B) - Variables code with Nummer1/Nummer2
- `Main.java` (736B) - Ice cream/glass code
- `Main.java` (528B) - Scanner/name input code
- `Main.java` (855B) - Different content
- `Main.java` (1089B) - Different content
- `uppgift.java` (213B) - "uppgift 5" code

---

## Investigation Process

### Initial Hypotheses (All Wrong)

1. **Closure Capture Bug** - Thought async loop variables weren't properly captured
   - Added local variable copies before `Dispatcher.UIThread.InvokeAsync()`
   - This didn't fix the issue

2. **File Path Issues** - Thought files might have wrong paths
   - Added extensive logging
   - Logs showed file paths were correct and unique

3. **StudentFile Object Sharing** - Thought objects might be shared
   - Logs showed each StudentFile had correct, different content assigned

### The Breakthrough

By examining the application logs, I discovered:

```
2025-10-21 18:03:15.771 +02:00 [INF] SyntaxHighlightedCodeViewer: CodeContent changed, length: 525
2025-10-21 18:03:15.772 +02:00 [INF] SyntaxHighlightedCodeViewer: CodeContent changed, length: 525
2025-10-21 18:03:15.773 +02:00 [INF] SyntaxHighlightedCodeViewer: CodeContent changed, length: 525
2025-10-21 18:03:15.773 +02:00 [INF] SyntaxHighlightedCodeViewer: CodeContent changed, length: 525
```

The same content length appeared **multiple times in a row** across different viewer instances! This indicated that when ONE instance's property changed, ALL instances were being notified.

---

## Root Cause

**File:** `src/Controls/SyntaxHighlightedCodeViewer.cs`
**Lines:** 82-83 (original code)

The bug was in the `InitializeComponent()` method:

```csharp
private void InitializeComponent()
{
    _textBlock = new SelectableTextBlock { /* ... */ };
    Content = _textBlock;

    // ❌ BUG: These subscribe to GLOBAL property changes!
    CodeContentProperty.Changed.Subscribe(OnCodeContentChanged);
    FileExtensionProperty.Changed.Subscribe(OnFileExtensionChanged);
}
```

### Why This Was Wrong

In Avalonia UI (and WPF/XAML frameworks), when you subscribe to a `StyledProperty.Changed` event using `.Subscribe()`, you're subscribing to **ALL changes to that property across ALL instances** of the control, not just the current instance.

This meant:
1. When `File1.CodeContent` was set → ALL SyntaxHighlightedCodeViewer instances received the notification
2. Each instance updated its display with `File1.CodeContent`
3. When `File2.CodeContent` was set → ALL instances updated to show `File2.CodeContent`
4. This continued until the last file loaded, and all instances showed the last file's content

---

## The Solution

**File:** `src/Controls/SyntaxHighlightedCodeViewer.cs`
**Lines:** 84-104 (fixed code)

### Changes Made

1. **Removed global subscriptions:**
   ```csharp
   // REMOVED these lines:
   CodeContentProperty.Changed.Subscribe(OnCodeContentChanged);
   FileExtensionProperty.Changed.Subscribe(OnFileExtensionChanged);
   ```

2. **Added instance-specific property change handling:**
   ```csharp
   protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
   {
       base.OnPropertyChanged(change);

       if (change.Property == CodeContentProperty)
       {
           OnCodeContentChanged(change as AvaloniaPropertyChangedEventArgs<string?> ??
               new AvaloniaPropertyChangedEventArgs<string?>(
                   this,
                   CodeContentProperty,
                   change.OldValue as string,
                   change.NewValue as string,
                   BindingPriority.LocalValue));
       }
       else if (change.Property == FileExtensionProperty)
       {
           OnFileExtensionChanged(change as AvaloniaPropertyChangedEventArgs<string> ??
               new AvaloniaPropertyChangedEventArgs<string>(
                   this,
                   FileExtensionProperty,
                   (change.OldValue as string) ?? string.Empty,
                   (change.NewValue as string) ?? string.Empty,
                   BindingPriority.LocalValue));
       }
   }
   ```

3. **Added required using statement:**
   ```csharp
   using Avalonia.Data; // For BindingPriority
   ```

### How This Fixes It

By overriding `OnPropertyChanged()`, each control instance now:
- Only responds to **its own** property changes
- Ignores property changes from other instances
- Maintains its own unique content independent of other instances

---

## Verification

After the fix, logs showed correct behavior:

```
[WRN] === BEFORE LOADING: File=main.java, FullPath=...\1bNzqF6h\main.java, FileSize=388 ===
[WRN] === AFTER LoadContentAsync: CodeContent length=388, First 50 chars=//TIP To <b>Run</b> code...
[WRN] === AFTER UI UPDATE: File=main.java, Path=...\1bNzqF6h\main.java, CodeContent.Length=388

[WRN] === BEFORE LOADING: File=Main.java, FullPath=...\1eRfVH61\Main.java, FileSize=736 ===
[WRN] === AFTER LoadContentAsync: CodeContent length=732, First 50 chars=public class Main {
[WRN] === AFTER UI UPDATE: File=Main.java, Path=...\1eRfVH61\Main.java, CodeContent.Length=732

[WRN] === BEFORE LOADING: File=Main.java, FullPath=...\1R-Pq4W6\Main.java, FileSize=528 ===
[WRN] === AFTER LoadContentAsync: CodeContent length=525, First 50 chars=//TIP To <b>Run</b> code...
[WRN] === AFTER UI UPDATE: File=Main.java, Path=...\1R-Pq4W6\Main.java, CodeContent.Length=525
```

Each file now correctly displays its own unique content.

---

## Files Modified

1. **`src/Controls/SyntaxHighlightedCodeViewer.cs`**
   - Removed global property subscriptions from `InitializeComponent()`
   - Added `OnPropertyChanged()` override for instance-specific handling
   - Added `using Avalonia.Data;` for `BindingPriority`

2. **`src/ViewModels/StudentDetailViewModel.cs`** (debugging code - can be cleaned up)
   - Added extensive logging in `LoadContentForAllFilesAsync()` method
   - Added local variable captures (not needed after the real fix, but doesn't hurt)

3. **`src/Models/UI/FileTreeNode.cs`** (debugging code - can be cleaned up)
   - Added logging in `LoadCodeContent()` method

---

## Lessons Learned

1. **Avalonia Property Subscriptions:** When subscribing to `StyledProperty.Changed`, you subscribe to **ALL** instances, not just the current one. Use `OnPropertyChanged()` override instead for instance-specific behavior.

2. **Debugging UI Issues:** Always check logs for patterns like repeated updates or shared state indicators.

3. **Data Flow Verification:** Just because the backend data is correct doesn't mean the UI layer is displaying it correctly. Always trace through the entire stack from data → ViewModel → UI Control → Display.

4. **Log Everything:** The extensive logging added during investigation was crucial for identifying the pattern of ALL controls updating together.

---

## Related Code Patterns to Watch For

If you ever see this pattern in Avalonia/WPF code:

```csharp
SomeProperty.Changed.Subscribe(handler);
```

Consider whether you actually want **global** notifications or **instance-specific** notifications. For instance-specific, always use:

```csharp
protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
{
    base.OnPropertyChanged(change);

    if (change.Property == SomeProperty)
    {
        // Handle the change for THIS instance only
    }
}
```

---

## Status

✅ **FIXED and VERIFIED**
Each file now displays its own correct content independent of other files.
