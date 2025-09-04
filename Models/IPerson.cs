namespace SchoolOrganizer.Models;

/// <summary>
/// Interface representing a person in the school system (student, teacher, personnel)
/// </summary>
public interface IPerson
{
    int Id { get; }
    string Name { get; }
    string PictureUrl { get; }
    string Email { get; }
    
    /// <summary>
    /// Role-specific information (e.g., "Class 9A" for students, "Math Department" for teachers)
    /// </summary>
    string RoleInfo { get; }
    
    /// <summary>
    /// Secondary role information (e.g., "Mentor: John Doe" for students, "Head of Department" for teachers)
    /// </summary>
    string? SecondaryInfo { get; }
    
    /// <summary>
    /// The type of person (Student, Teacher, Personnel)
    /// </summary>
    PersonType PersonType { get; }
}

public enum PersonType
{
    Student,
    Teacher,
    Personnel
}