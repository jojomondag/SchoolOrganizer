using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Google.Apis.Classroom.v1.Data;

namespace SchoolOrganizer.Src.Models.Students;

/// <summary>
/// Wrapper for Google Classroom Student objects to enable selection in UI
/// </summary>
public partial class ClassroomStudentWrapper : ObservableObject
{
    [ObservableProperty]
    private bool isSelected;

    public Google.Apis.Classroom.v1.Data.Student ClassroomStudent { get; }

    public ClassroomStudentWrapper(Google.Apis.Classroom.v1.Data.Student classroomStudent)
    {
        ClassroomStudent = classroomStudent ?? throw new ArgumentNullException(nameof(classroomStudent));
    }

    /// <summary>
    /// Display name from Google Classroom profile
    /// </summary>
    public string Name => ClassroomStudent.Profile?.Name?.FullName ?? "Unknown Student";

    /// <summary>
    /// Email address from Google Classroom profile
    /// </summary>
    public string Email => ClassroomStudent.Profile?.EmailAddress ?? string.Empty;

    /// <summary>
    /// Profile photo URL from Google Classroom (may need https: prefix)
    /// </summary>
    public string ProfilePhotoUrl
    {
        get
        {
            var photoUrl = ClassroomStudent.Profile?.PhotoUrl;
            if (string.IsNullOrEmpty(photoUrl))
                return string.Empty;
            
            // Handle URLs that start with "//" by adding "https:"
            if (photoUrl.StartsWith("//"))
                return "https:" + photoUrl;
            
            return photoUrl;
        }
    }

    /// <summary>
    /// Whether this student has a profile photo
    /// </summary>
    public bool HasProfilePhoto => !string.IsNullOrEmpty(ProfilePhotoUrl);
}
