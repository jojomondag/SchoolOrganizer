using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SchoolOrganizer.Src.Models.Assignments;

/// <summary>
/// Represents a group of files for an assignment
/// </summary>
public class AssignmentGroup : INotifyPropertyChanged
{
    private int _rating;
    private string _notes = string.Empty;
    private DateTime? _lastModified;
    private bool _isNotesExpanded;
    private double _notesSidebarWidth = 220; // Default width
    private bool _showEmbeddedGoogleDocForAssignment = true; // Default to embedded view
    private bool _isExpanded = false; // Default to collapsed state

    public string AssignmentName { get; set; } = string.Empty;
    public List<StudentFile> Files { get; set; } = new();

    /// <summary>
    /// Returns true if this assignment has any Google Docs files
    /// </summary>
    public bool HasGoogleDocs => Files.Any(f => f.IsGoogleDoc);

    public int Rating
    {
        get => _rating;
        set
        {
            if (_rating != value)
            {
                _rating = value;
                OnPropertyChanged();
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set
        {
            if (_notes != value)
            {
                _notes = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime? LastModified
    {
        get => _lastModified;
        set
        {
            if (_lastModified != value)
            {
                _lastModified = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastModifiedText));
            }
        }
    }

    public bool IsNotesExpanded
    {
        get => _isNotesExpanded;
        set
        {
            if (_isNotesExpanded != value)
            {
                _isNotesExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public double NotesSidebarWidth
    {
        get => _notesSidebarWidth;
        set
        {
            if (Math.Abs(_notesSidebarWidth - value) > 0.01)
            {
                _notesSidebarWidth = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowEmbeddedGoogleDocForAssignment
    {
        get => _showEmbeddedGoogleDocForAssignment;
        set
        {
            if (_showEmbeddedGoogleDocForAssignment != value)
            {
                _showEmbeddedGoogleDocForAssignment = value;
                OnPropertyChanged();

                // Update all Google Docs files in this assignment
                foreach (var file in Files.Where(f => f.IsGoogleDoc))
                {
                    file.ShowEmbeddedGoogleDoc = value;
                }
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public string LastModifiedText
    {
        get
        {
            if (LastModified == null)
                return string.Empty;

            return $"Last edited: {LastModified.Value:yyyy-MM-dd HH:mm}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
