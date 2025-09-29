using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace SchoolOrganizer.Models;

public partial class Student : ObservableObject, IPerson
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string pictureUrl = string.Empty;

    [ObservableProperty]
    private string className = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> mentors = new();

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private DateTime enrollmentDate;

    // Backward compatibility property for JSON serialization
    [JsonPropertyName("Mentor")]
    public string? LegacyMentor 
    { 
        get => Mentors.FirstOrDefault(); 
        set 
        { 
            if (!string.IsNullOrWhiteSpace(value) && !Mentors.Contains(value))
            {
                Mentors.Clear();
                Mentors.Add(value);
            }
        } 
    }

    // IPerson implementation
    public string RoleInfo => ClassName;
    public string? SecondaryInfo => Mentors.Count == 0 ? null : 
        Mentors.Count == 1 ? $"Mentor: {Mentors[0]}" : 
        $"Mentors:\n{string.Join("\n", Mentors)}";
    public PersonType PersonType => PersonType.Student;

    public Student()
    {
        EnrollmentDate = DateTime.Now;
    }

    public Student(int id, string name, string className, IEnumerable<string>? mentors = null, string email = "")
    {
        Id = id;
        Name = name;
        PictureUrl = string.Empty;
        ClassName = className;
        if (mentors != null)
        {
            Mentors = new ObservableCollection<string>(mentors.Where(m => !string.IsNullOrWhiteSpace(m)));
        }
        Email = email;
        EnrollmentDate = DateTime.Now;
    }

    // Legacy constructor for backward compatibility
    public Student(int id, string name, string className, string mentor, string email = "")
        : this(id, name, className, !string.IsNullOrWhiteSpace(mentor) ? new[] { mentor } : null, email)
    {
    }

    // Helper methods for mentor management
    public void AddMentor(string mentor)
    {
        if (!string.IsNullOrWhiteSpace(mentor) && !Mentors.Contains(mentor))
        {
            Mentors.Add(mentor);
        }
    }

    public void RemoveMentor(string mentor)
    {
        Mentors.Remove(mentor);
    }

    public void ClearMentors()
    {
        Mentors.Clear();
    }
}
