using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.Interactivity;
using SchoolOrganizer.Src.ViewModels;

namespace SchoolOrganizer.Src.Services;

/// <summary>
/// Handles global keyboard navigation for the application, providing terminal-like keyboard behavior
/// where typing automatically focuses the search bar and navigation keys work as expected.
/// This class is completely self-contained and manages all keyboard-related functionality.
/// </summary>
public class GlobalKeyboardHandler
{
    private readonly StudentGalleryViewModel _viewModel;
    private readonly TextBox _searchTextBox;
    private readonly Control _hostControl;

    public GlobalKeyboardHandler(StudentGalleryViewModel viewModel, TextBox searchTextBox, Control hostControl)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _searchTextBox = searchTextBox ?? throw new ArgumentNullException(nameof(searchTextBox));
        _hostControl = hostControl ?? throw new ArgumentNullException(nameof(hostControl));
        
        Initialize();
    }

    /// <summary>
    /// Initializes the keyboard handler by setting up event handlers and focus behavior
    /// </summary>
    private void Initialize()
    {
        _hostControl.Focusable = true;
        _hostControl.KeyDown += OnKeyDown;
        _hostControl.AddHandler(Control.KeyDownEvent, OnKeyDown, handledEventsToo: true);
        
        if (_hostControl.IsLoaded)
        {
            SetupFocus();
        }
        else
        {
            _hostControl.Loaded += OnHostControlLoaded;
        }
    }

    /// <summary>
    /// Handles the host control loaded event to set up focus
    /// </summary>
    private void OnHostControlLoaded(object? sender, RoutedEventArgs e)
    {
        SetupFocus();
        _hostControl.Loaded -= OnHostControlLoaded;
    }

    /// <summary>
    /// Sets up focus for the host control to ensure keyboard events are received
    /// </summary>
    private void SetupFocus()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_hostControl.Focusable)
            {
                _hostControl.Focus();
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// Main keyboard event handler
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (HandleKeyDown(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles global key down events for the entire view.
    /// Returns true if the key was handled, false otherwise.
    /// </summary>
    public bool HandleKeyDown(KeyEventArgs e)
    {
        // Don't handle any keyboard events when in add student mode
        if (_viewModel.IsAddingStudent)
        {
            return false;
        }

        // If search box is already focused, let it handle most keys normally
        if (_searchTextBox.IsFocused)
        {
            return HandleSearchBoxKeys(e);
        }

        // Global key handlers when search box is not focused
        return HandleGlobalKeys(e);
    }

    /// <summary>
    /// Disposes of the keyboard handler and cleans up event subscriptions
    /// </summary>
    public void Dispose()
    {
        _hostControl.KeyDown -= OnKeyDown;
        _hostControl.RemoveHandler(Control.KeyDownEvent, OnKeyDown);
        _hostControl.Loaded -= OnHostControlLoaded;
    }

    /// <summary>
    /// Handles keys when the search box is focused
    /// </summary>
    private bool HandleSearchBoxKeys(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                // Clear search and unfocus
                _viewModel.SearchText = string.Empty;
                _searchTextBox.Focus(NavigationMethod.Unspecified);
                return true;

            case Key.Enter:
                // Could trigger search completion or navigate to first result
                if (_viewModel.Students.Count > 0)
                {
                    _viewModel.SelectStudentCommand.Execute(_viewModel.Students[0]);
                }
                return true;

            case Key.Down when (e.KeyModifiers & KeyModifiers.Control) != 0:
                // Ctrl+Down: Navigate to first student while keeping search focused
                if (_viewModel.Students.Count > 0)
                {
                    _viewModel.SelectStudentCommand.Execute(_viewModel.Students[0]);
                }
                return true;

            case Key.Up when (e.KeyModifiers & KeyModifiers.Control) != 0:
                // Ctrl+Up: Clear selection while keeping search focused
                _viewModel.DeselectStudentCommand.Execute(null);
                return true;

            case Key.Back when (e.KeyModifiers & KeyModifiers.Control) != 0:
                // Ctrl+Backspace: Clear entire search text
                _viewModel.SearchText = string.Empty;
                return true;
        }

        return false; // Let the textbox handle the key
    }

    /// <summary>
    /// Handles global keys when search box is not focused
    /// </summary>
    private bool HandleGlobalKeys(KeyEventArgs e)
    {
        switch (e.Key)
        {
            // Typing keys - focus search box and start typing
            case var key when IsTypingKey(key):
                FocusSearchBoxAndStartTyping(e);
                return true;

            case Key.Back:
                if ((e.KeyModifiers & (KeyModifiers.Meta | KeyModifiers.Alt)) != 0)
                {
                    if (_viewModel.SelectedStudent != null)
                    {
                        _viewModel.DeleteStudentCommand.Execute(_viewModel.SelectedStudent);
                    }
                    else
                    {
                        FocusSearchBoxAndDelete();
                    }
                    return true;
                }
                else
                {
                    FocusSearchBoxAndBackspace();
                    return true;
                }

            case Key.Delete:
                if (_viewModel.SelectedStudent != null)
                {
                    _viewModel.DeleteStudentCommand.Execute(_viewModel.SelectedStudent);
                }
                else
                {
                    FocusSearchBoxAndDelete();
                }
                return true;

            // Arrow keys for navigation
            case Key.Left:
                MoveCursorInSearchBox(-1);
                return true;

            case Key.Right:
                MoveCursorInSearchBox(1);
                return true;

            case Key.Home:
                MoveCursorToStart();
                return true;

            case Key.End:
                MoveCursorToEnd();
                return true;

            // Student selection navigation
            case Key.Down:
                NavigateStudentSelection(1);
                return true;

            case Key.Up:
                NavigateStudentSelection(-1);
                return true;

            // Escape - clear everything
            case Key.Escape:
                _viewModel.SearchText = string.Empty;
                _viewModel.DeselectStudentCommand.Execute(null);
                return true;

            // Enter - select first student if available
            case Key.Enter:
                if (_viewModel.Students.Count > 0)
                {
                    _viewModel.SelectStudentCommand.Execute(_viewModel.Students[0]);
                }
                return true;

            // Ctrl+A - focus search box and select all text
            case Key.A when (e.KeyModifiers & KeyModifiers.Control) != 0:
                FocusSearchBoxAndSelectAll();
                return true;

        }

        return false; // Key not handled
    }

    /// <summary>
    /// Determines if a key is a typing key (letters, numbers, symbols)
    /// </summary>
    private static bool IsTypingKey(Key key)
    {
        return (key >= Key.A && key <= Key.Z) ||
               (key >= Key.D0 && key <= Key.D9) ||
               (key >= Key.NumPad0 && key <= Key.NumPad9) ||
               key == Key.Space ||
               key == Key.OemMinus ||
               key == Key.OemPlus ||
               key == Key.OemPeriod ||
               key == Key.OemComma ||
               key == Key.OemSemicolon ||
               key == Key.OemQuotes ||
               key == Key.OemQuestion ||
               key == Key.OemOpenBrackets ||
               key == Key.OemCloseBrackets ||
               key == Key.OemPipe ||
               key == Key.OemBackslash ||
               key == Key.OemTilde;
    }

    /// <summary>
    /// Converts a key press to its character representation
    /// </summary>
    private static string? GetCharFromKey(Key key, KeyModifiers modifiers)
    {
        // Handle letters
        if (key >= Key.A && key <= Key.Z)
        {
            var letter = (char)('a' + (key - Key.A));
            return (modifiers & KeyModifiers.Shift) != 0 ? letter.ToString().ToUpper() : letter.ToString();
        }
        
        // Handle numbers and their shifted symbols
        if (key >= Key.D0 && key <= Key.D9)
        {
            if ((modifiers & KeyModifiers.Shift) != 0)
            {
                return key switch
                {
                    Key.D1 => "!",
                    Key.D2 => "@",
                    Key.D3 => "#",
                    Key.D4 => "$",
                    Key.D5 => "%",
                    Key.D6 => "^",
                    Key.D7 => "&",
                    Key.D8 => "*",
                    Key.D9 => "(",
                    Key.D0 => ")",
                    _ => null
                };
            }
            else
            {
                return ((char)('0' + (key - Key.D0))).ToString();
            }
        }

        // Handle numpad numbers
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return ((char)('0' + (key - Key.NumPad0))).ToString();
        }

        // Handle special characters
        return key switch
        {
            Key.Space => " ",
            Key.OemMinus => (modifiers & KeyModifiers.Shift) != 0 ? "_" : "-",
            Key.OemPlus => (modifiers & KeyModifiers.Shift) != 0 ? "+" : "=",
            Key.OemPeriod => (modifiers & KeyModifiers.Shift) != 0 ? ">" : ".",
            Key.OemComma => (modifiers & KeyModifiers.Shift) != 0 ? "<" : ",",
            Key.OemSemicolon => (modifiers & KeyModifiers.Shift) != 0 ? ":" : ";",
            Key.OemQuotes => (modifiers & KeyModifiers.Shift) != 0 ? "\"" : "'",
            Key.OemQuestion => (modifiers & KeyModifiers.Shift) != 0 ? "?" : "/",
            Key.OemOpenBrackets => (modifiers & KeyModifiers.Shift) != 0 ? "{" : "[",
            Key.OemCloseBrackets => (modifiers & KeyModifiers.Shift) != 0 ? "}" : "]",
            Key.OemPipe => (modifiers & KeyModifiers.Shift) != 0 ? "|" : "\\",
            Key.OemBackslash => (modifiers & KeyModifiers.Shift) != 0 ? "|" : "\\",
            Key.OemTilde => (modifiers & KeyModifiers.Shift) != 0 ? "~" : "`",
            _ => null
        };
    }

    /// <summary>
    /// Focuses the search box and starts typing with the given key
    /// </summary>
    private void FocusSearchBoxAndStartTyping(KeyEventArgs e)
    {
        _searchTextBox.Focus();
        
        var keyChar = GetCharFromKey(e.Key, e.KeyModifiers);
        if (keyChar != null)
        {
            _viewModel.SearchText = keyChar;
            _searchTextBox.CaretIndex = _searchTextBox.Text?.Length ?? 0;
        }
    }

    /// <summary>
    /// Focuses the search box and performs a backspace operation
    /// </summary>
    private void FocusSearchBoxAndBackspace()
    {
        _searchTextBox.Focus();
        
        if (!string.IsNullOrEmpty(_viewModel.SearchText))
        {
            _viewModel.SearchText = _viewModel.SearchText[..^1];
            _searchTextBox.CaretIndex = _searchTextBox.Text?.Length ?? 0;
        }
    }

    /// <summary>
    /// Focuses the search box and performs a delete operation
    /// </summary>
    private void FocusSearchBoxAndDelete()
    {
        _searchTextBox.Focus();
        _viewModel.SearchText = string.Empty;
        _searchTextBox.CaretIndex = 0;
    }

    /// <summary>
    /// Moves cursor in search box by specified offset
    /// </summary>
    private void MoveCursorInSearchBox(int offset)
    {
        _searchTextBox.Focus();
        
        var currentIndex = _searchTextBox.CaretIndex;
        var newIndex = Math.Max(0, Math.Min((_searchTextBox.Text?.Length ?? 0), currentIndex + offset));
        _searchTextBox.CaretIndex = newIndex;
    }

    /// <summary>
    /// Moves cursor to start of search box
    /// </summary>
    private void MoveCursorToStart()
    {
        _searchTextBox.Focus();
        _searchTextBox.CaretIndex = 0;
    }

    /// <summary>
    /// Moves cursor to end of search box
    /// </summary>
    private void MoveCursorToEnd()
    {
        _searchTextBox.Focus();
        _searchTextBox.CaretIndex = _searchTextBox.Text?.Length ?? 0;
    }

    /// <summary>
    /// Focuses search box and selects all text
    /// </summary>
    private void FocusSearchBoxAndSelectAll()
    {
        _searchTextBox.Focus();
        _searchTextBox.SelectAll();
    }

    /// <summary>
    /// Navigates student selection up or down
    /// </summary>
    private void NavigateStudentSelection(int direction)
    {
        if (_viewModel.Students.Count == 0) return;

        var currentIndex = -1;
        if (_viewModel.SelectedStudent != null)
        {
            for (int i = 0; i < _viewModel.Students.Count; i++)
            {
                if (_viewModel.Students[i].Id == _viewModel.SelectedStudent.Id)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        var newIndex = currentIndex + direction;
        
        // Wrap around navigation
        if (newIndex < 0)
        {
            newIndex = _viewModel.Students.Count - 1;
        }
        else if (newIndex >= _viewModel.Students.Count)
        {
            newIndex = 0;
        }

        _viewModel.SelectStudentCommand.Execute(_viewModel.Students[newIndex]);
    }
}