using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace SchoolOrganizer.Src.Models.Students;

public partial class Student : ObservableObject, IPerson
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string pictureUrl = string.Empty;

    [ObservableProperty]
    private string? originalImagePath;

    [ObservableProperty]
    private string className = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> teachers = new();

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private DateTime enrollmentDate;

    [ObservableProperty]
    private string? cropSettings;

    [ObservableProperty]
    private Dictionary<string, int> assignmentRatings = new();

    [ObservableProperty]
    private Dictionary<string, string> assignmentNotes = new();

    [ObservableProperty]
    private Dictionary<string, DateTime> assignmentNotesTimestamps = new();

    [ObservableProperty]
    private Dictionary<string, double> assignmentNotesSidebarWidths = new();

    // Backward compatibility property for JSON serialization
    [JsonPropertyName("Teacher")]
    public string? LegacyTeacher 
    { 
        get => Teachers.FirstOrDefault(); 
        set 
        { 
            if (!string.IsNullOrWhiteSpace(value) && !Teachers.Contains(value))
            {
                Teachers.Clear();
                Teachers.Add(value);
            }
        } 
    }

    // IPerson implementation
    public string RoleInfo => ClassName;
    public string? SecondaryInfo => Teachers.Count == 0 ? null : 
        Teachers.Count == 1 ? $"Teacher: {Teachers[0]}" : 
        $"Teachers:\n{string.Join("\n", Teachers)}";
    public PersonType PersonType => PersonType.Student;

    public Student()
    {
        EnrollmentDate = DateTime.Now;
    }

    public Student(int id, string name, string className, IEnumerable<string>? teachers = null, string email = "")
    {
        Id = id;
        Name = name;
        PictureUrl = string.Empty;
        ClassName = className;
        if (teachers != null)
        {
            Teachers = new ObservableCollection<string>(teachers.Where(t => !string.IsNullOrWhiteSpace(t)));
        }
        Email = email;
        EnrollmentDate = DateTime.Now;
    }

    // Legacy constructor for backward compatibility
    public Student(int id, string name, string className, string teacher, string email = "")
        : this(id, name, className, !string.IsNullOrWhiteSpace(teacher) ? new[] { teacher } : null, email)
    {
    }

    // Helper methods for teacher management
    public void AddTeacher(string teacher)
    {
        if (!string.IsNullOrWhiteSpace(teacher) && !Teachers.Contains(teacher))
        {
            Teachers.Add(teacher);
        }
    }

    public void RemoveTeacher(string teacher)
    {
        Teachers.Remove(teacher);
    }

    public void ClearTeachers()
    {
        Teachers.Clear();
    }
}
