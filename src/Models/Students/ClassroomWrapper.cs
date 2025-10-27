using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Google.Apis.Classroom.v1.Data;

namespace SchoolOrganizer.Src.Models.Students;

/// <summary>
/// Wrapper for Google Classroom Course objects to enable toggle functionality in UI
/// </summary>
public partial class ClassroomWrapper : ObservableObject
{
    [ObservableProperty]
    private bool isToggled;

    public Course Classroom { get; }

    public ClassroomWrapper(Course classroom)
    {
        Classroom = classroom ?? throw new ArgumentNullException(nameof(classroom));
    }

    /// <summary>
    /// Display name from Google Classroom course
    /// </summary>
    public string Name => Classroom.Name ?? "Unknown Classroom";

    /// <summary>
    /// Description from Google Classroom course
    /// </summary>
    public string Description => Classroom.Description ?? string.Empty;

    /// <summary>
    /// Section from Google Classroom course
    /// </summary>
    public string Section => Classroom.Section ?? string.Empty;

    /// <summary>
    /// Course ID for API calls
    /// </summary>
    public string ClassroomId => Classroom.Id ?? string.Empty;
    
    /// <summary>
    /// Alias for ClassroomId for backward compatibility
    /// </summary>
    public string Id => ClassroomId;
}
