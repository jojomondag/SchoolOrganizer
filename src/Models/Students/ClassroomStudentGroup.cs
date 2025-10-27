using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolOrganizer.Src.Models.Students;

/// <summary>
/// Represents a group of students from a single classroom
/// </summary>
public partial class ClassroomStudentGroup : ObservableObject
{
    public string ClassroomId { get; }
    public string ClassroomName { get; }
    public ObservableCollection<ClassroomStudentWrapper> Students { get; }

    public ClassroomStudentGroup(string classroomId, string classroomName)
    {
        ClassroomId = classroomId;
        ClassroomName = classroomName;
        Students = new ObservableCollection<ClassroomStudentWrapper>();
    }
}
