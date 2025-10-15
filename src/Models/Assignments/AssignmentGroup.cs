using System.Collections.Generic;

namespace SchoolOrganizer.Src.Models.Assignments;

/// <summary>
/// Represents a group of files for an assignment
/// </summary>
public class AssignmentGroup
{
    public string AssignmentName { get; set; } = string.Empty;
    public List<StudentFile> Files { get; set; } = new();
}
