using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SchoolOrganizer.Src.Models.Students;

/// <summary>
/// Groups students by their classroom for display purposes
/// </summary>
public partial class ClassroomStudentGroup : ObservableObject
{
    public ClassroomStudentGroup(string classroomName, string classroomId)
    {
        ClassroomName = classroomName;
        ClassroomId = classroomId;
        Students = new ObservableCollection<ClassroomStudentWrapper>();
    }

    [ObservableProperty]
    private string classroomName;

    [ObservableProperty]
    private string classroomId;

    public ObservableCollection<ClassroomStudentWrapper> Students { get; }

    /// <summary>
    /// Number of students in this group
    /// </summary>
    public int StudentCount => Students.Count;

    /// <summary>
    /// Number of selected students in this group
    /// </summary>
    public int SelectedStudentCount => Students.Count(s => s.IsSelected);
}
