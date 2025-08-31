using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.ViewModels;

public partial class StudentGalleryViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<Student> students = new();

    [ObservableProperty]
    private Student? selectedStudent;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedClass = "All Classes";

    [ObservableProperty]
    private ObservableCollection<string> availableClasses = new();

    [ObservableProperty]
    private bool isLoading = false;

    // Event for requesting image selection
    public event EventHandler? AddStudentRequested;
    
    // Event raised when a student's image should be changed (on card click)
    public event EventHandler<Student>? StudentImageChangeRequested;
    public event EventHandler<Student>? EditStudentRequested;


    public StudentGalleryViewModel()
    {
        _ = LoadStudents();
    }

    [RelayCommand]
    private async Task LoadStudents()
    {
        IsLoading = true;
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "students.json");
            
            if (File.Exists(jsonPath))
            {
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var studentList = JsonSerializer.Deserialize<List<Student>>(jsonContent);
                
                Students.Clear();
                if (studentList != null)
                {
                    foreach (var student in studentList)
                    {
                        Students.Add(student);
                    }
                }
                
                UpdateAvailableClasses();
            }
        }
        catch (Exception ex)
        {
            // In a real application, you would want to log this error
            // and possibly show a user-friendly message
            System.Diagnostics.Debug.WriteLine($"Error loading students: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectStudent(Student? student)
    {
        System.Diagnostics.Debug.WriteLine($"SelectStudent called with: {student?.Name ?? "null"}");
        
        if (student != null)
        {
            SelectedStudent = student;
            StudentImageChangeRequested?.Invoke(this, student);
        }
    }

    [RelayCommand]
    private void EditStudent(Student student)
    {
        EditStudentRequested?.Invoke(this, student);
    }

    [RelayCommand]
    private void FilterByClass(string className)
    {
        SelectedClass = className;
        ApplyFilters();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    partial void OnSelectedClassChanged(string value)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        // This is a simplified filtering approach
        // In a more complex scenario, you might want to use CollectionView filtering
        // For now, we'll reload and filter the data
        _ = LoadAndFilterStudents();
    }

    private async Task LoadAndFilterStudents()
    {
        IsLoading = true;
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "students.json");
            
            if (File.Exists(jsonPath))
            {
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var studentList = JsonSerializer.Deserialize<List<Student>>(jsonContent);
                
                if (studentList != null)
                {
                    // Apply filters
                    var filteredStudents = studentList.Where(s =>
                        (string.IsNullOrEmpty(SearchText) || s.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) &&
                        (SelectedClass == "All Classes" || s.ClassName == SelectedClass)
                    );

                    Students.Clear();
                    foreach (var student in filteredStudents)
                    {
                        Students.Add(student);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error filtering students: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateAvailableClasses()
    {
        AvailableClasses.Clear();
        AvailableClasses.Add("All Classes");
        
        var uniqueClasses = Students
            .Select(s => s.ClassName)
            .Distinct()
            .OrderBy(c => c);

        foreach (var className in uniqueClasses)
        {
            AvailableClasses.Add(className);
        }
    }


    [RelayCommand]
    private void AddStudent()
    {
        AddStudentRequested?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddNewStudentAsync(string name, string className, string mentor, string email, DateTime enrollmentDate, string picturePath)
    {
        try
        {
            var newStudent = new Student
            {
                Id = await GenerateNextStudentIdAsync(),
                Name = name,
                ClassName = className,
                Mentor = mentor,
                Email = email,
                EnrollmentDate = enrollmentDate,
                PictureUrl = picturePath ?? string.Empty
            };

            Students.Add(newStudent);
            UpdateAvailableClasses();
            await SaveStudentsCollectionToJson();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding new student: {ex.Message}");
        }
    }

    public async Task UpdateExistingStudentAsync(Student student, string name, string className, string mentor, string email, DateTime enrollmentDate, string picturePath)
    {
        try
        {
            student.Name = name;
            student.ClassName = className;
            student.Mentor = mentor;
            student.Email = email;
            student.EnrollmentDate = enrollmentDate;
            student.PictureUrl = picturePath ?? string.Empty;

            UpdateAvailableClasses();
            await SaveStudentsCollectionToJson();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating student: {ex.Message}");
        }
    }

    private async Task<int> GenerateNextStudentIdAsync()
    {
        var all = await LoadAllStudentsFromJson();
        var maxId = all.Any() ? all.Max(s => s.Id) : 0;
        return maxId + 1;
    }

    private async Task SaveStudentsCollectionToJson()
    {
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "students.json");
            var list = Students.ToList();
            var jsonContent = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(jsonPath, jsonContent);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving students collection: {ex.Message}");
        }
    }

    public async Task UpdateStudentImage(Student student, string newImagePath)
    {
        try
        {
            var studentInCollection = Students.FirstOrDefault(s => s.Id == student.Id);
            if (studentInCollection != null)
            {
                studentInCollection.PictureUrl = newImagePath;
            }
            student.PictureUrl = newImagePath;

            await SaveStudentsToJson(studentInCollection ?? student);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating student image: {ex.Message}");
        }
    }

    private async Task SaveStudentsToJson(Student updatedStudent)
    {
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "students.json");
            
            // Convert all students to a list for serialization
            var allStudents = await LoadAllStudentsFromJson();
            
            // Find and update the student in the complete list
            var studentToUpdate = allStudents.FirstOrDefault(s => s.Id == updatedStudent.Id);
            if (studentToUpdate != null)
            {
                studentToUpdate.PictureUrl = updatedStudent.PictureUrl;
                System.Diagnostics.Debug.WriteLine($"Updated student {updatedStudent.Name} with new image");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Could not find student with ID {updatedStudent.Id} to update");
            }

            var jsonContent = JsonSerializer.Serialize(allStudents, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(jsonPath, jsonContent);
            System.Diagnostics.Debug.WriteLine($"Successfully saved students to JSON");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving students to JSON: {ex.Message}");
        }
    }

    private async Task<List<Student>> LoadAllStudentsFromJson()
    {
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "students.json");
            
            if (File.Exists(jsonPath))
            {
                var jsonContent = await File.ReadAllTextAsync(jsonPath);
                var studentList = JsonSerializer.Deserialize<List<Student>>(jsonContent);
                return studentList ?? new List<Student>();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading all students from JSON: {ex.Message}");
        }
        
        return new List<Student>();
    }
}
