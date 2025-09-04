using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolOrganizer.Models;

public partial class Teacher : ObservableObject, IPerson
{
    [ObservableProperty]
    private int id;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string pictureUrl = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string department = string.Empty;

    [ObservableProperty]
    private string subject = string.Empty;

    [ObservableProperty]
    private bool isHeadOfDepartment;

    [ObservableProperty]
    private DateTime hireDate;

    // IPerson implementation
    public string RoleInfo => string.IsNullOrEmpty(Subject) ? Department : $"{Subject} - {Department}";
    public string? SecondaryInfo => IsHeadOfDepartment ? "Head of Department" : null;
    public PersonType PersonType => PersonType.Teacher;

    public Teacher()
    {
        HireDate = DateTime.Now;
    }

    public Teacher(int id, string name, string department, string subject, string email = "", bool isHeadOfDepartment = false)
    {
        Id = id;
        Name = name;
        Department = department;
        Subject = subject;
        Email = email;
        IsHeadOfDepartment = isHeadOfDepartment;
        HireDate = DateTime.Now;
    }
}