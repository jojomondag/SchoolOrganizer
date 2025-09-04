using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolOrganizer.Models;

public partial class Personnel : ObservableObject, IPerson
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
    private string position = string.Empty;

    [ObservableProperty]
    private string department = string.Empty;

    [ObservableProperty]
    private DateTime hireDate;

    // IPerson implementation
    public string RoleInfo => Position;
    public string? SecondaryInfo => string.IsNullOrEmpty(Department) ? null : Department;
    public PersonType PersonType => PersonType.Personnel;

    public Personnel()
    {
        HireDate = DateTime.Now;
    }

    public Personnel(int id, string name, string position, string department = "", string email = "")
    {
        Id = id;
        Name = name;
        Position = position;
        Department = department;
        Email = email;
        HireDate = DateTime.Now;
    }
}