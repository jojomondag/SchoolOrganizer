using System;
using SchoolOrganizer.Src.Models.Students;

namespace SchoolOrganizer.Src.Models.UI;

/// <summary>
/// Special card model that represents the "Add Student" functionality in the gallery
/// </summary>
public class AddStudentCard : IPerson
{
    public int Id => -1; // Special ID to identify this as the add card
    public string Name => "Add Student";
    public string PictureUrl => "ADD_CARD"; // Special marker for the ProfileCard to show + sign
    public string Email => string.Empty;
    public string RoleInfo => "Click to add";
    public string? SecondaryInfo => null;
    public PersonType PersonType => PersonType.Student;
    
    /// <summary>
    /// Indicates this is the special add card
    /// </summary>
    public bool IsAddCard => true;
}
