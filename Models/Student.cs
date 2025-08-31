using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolOrganizer.Models;

public partial class Student : ObservableObject
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
    private string mentor = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private DateTime enrollmentDate;

    public Student()
    {
        EnrollmentDate = DateTime.Now;
    }

    public Student(int id, string name, string className, string mentor, string email = "")
    {
        Id = id;
        Name = name;
        PictureUrl = string.Empty;
        ClassName = className;
        Mentor = mentor;
        Email = email;
        EnrollmentDate = DateTime.Now;
    }
}
