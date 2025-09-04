namespace SchoolOrganizer.Models;

/// <summary>
/// Interface representing a person in the school system
/// </summary>
public interface IPerson
{
    int Id { get; }
    string Name { get; }
    string PictureUrl { get; }
    string Email { get; }
    
    /// <summary>
    /// Role-specific information (e.g., "Class 9A" for students)
    /// </summary>
    string RoleInfo { get; }
    
    /// <summary>
    /// Secondary role information (e.g., "Mentor: John Doe" for students)
    /// </summary>
    string? SecondaryInfo { get; }
    
    /// <summary>
    /// The type of person
    /// </summary>
    PersonType PersonType { get; }
}

public enum PersonType
{
    Student
}