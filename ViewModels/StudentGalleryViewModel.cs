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
using SchoolOrganizer.Services;
using SchoolOrganizer.Views;

namespace SchoolOrganizer.ViewModels;

public partial class StudentGalleryViewModel : ViewModelBase
{
    private readonly StudentSearchService searchService = new();

    // Full, unfiltered dataset kept in-memory
    private ObservableCollection<Student> allStudents = new();

    public ObservableCollection<Student> AllStudents => allStudents;

    [ObservableProperty]
    private ObservableCollection<Student> students = new();

    [ObservableProperty]
    private Student? selectedStudent;

    [ObservableProperty]
    private string searchText = string.Empty;


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
                
                allStudents.Clear();
                Students.Clear();
                if (studentList != null)
                {
                    foreach (var student in studentList)
                    {
                        allStudents.Add(student);
                    }
                }

                // Initial populate based on current query (may be empty)
                await ApplySearchImmediate();
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
        }
    }

    [RelayCommand]
    private void EditStudent(Student student)
    {
        EditStudentRequested?.Invoke(this, student);
    }

    [RelayCommand]
    private void ChangeImage(Student student)
    {
        StudentImageChangeRequested?.Invoke(this, student);
    }

    [RelayCommand]
    private void DeselectStudent()
    {
        SelectedStudent = null;
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = ApplySearchDebounced();
    }

    private System.Threading.CancellationTokenSource? searchCts;

    private async Task ApplySearchImmediate()
    {
        try
        {
            var results = searchService.Search(allStudents, SearchText);
            Students.Clear();
            foreach (var s in results)
            {
                Students.Add(s);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
        }
    }

    private async Task ApplySearchDebounced(int delayMs = 250)
    {
        searchCts?.Cancel();
        var cts = new System.Threading.CancellationTokenSource();
        searchCts = cts;
        try
        {
            await Task.Delay(delayMs, cts.Token);
            if (cts.IsCancellationRequested) return;
            await ApplySearchImmediate();
        }
        catch (TaskCanceledException)
        {
            // ignore
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

            allStudents.Add(newStudent);
            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
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

            // Ensure the AllStudents collection reflects the same object state
            var inAll = allStudents.FirstOrDefault(s => s.Id == student.Id);
            if (inAll != null)
            {
                inAll.Name = student.Name;
                inAll.ClassName = student.ClassName;
                inAll.Mentor = student.Mentor;
                inAll.Email = student.Email;
                inAll.EnrollmentDate = student.EnrollmentDate;
                inAll.PictureUrl = student.PictureUrl;
            }

            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
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

    private async Task SaveAllStudentsToJson()
    {
        try
        {
            var jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "students.json");
            var list = allStudents.ToList();
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
            System.Diagnostics.Debug.WriteLine($"UpdateStudentImage called for student {student.Id} with path: {newImagePath}");
            
            // Ensure the saved image file is readable before we update bindings
            await WaitForReadableFileAsync(newImagePath);

            // Clear the image cache for the old path if it exists
            if (!string.IsNullOrWhiteSpace(student.PictureUrl))
            {
                UniversalImageConverter.ClearCache(student.PictureUrl);
            }
            
            // Clear the cache for the new path as well (in case it's the same path but different image)
            UniversalImageConverter.ClearCache(newImagePath);

            var studentInCollection = Students.FirstOrDefault(s => s.Id == student.Id);
            if (studentInCollection != null)
            {
                System.Diagnostics.Debug.WriteLine($"Found student in collection, updating PictureUrl from '{studentInCollection.PictureUrl}' to '{newImagePath}'");
                
                // Clear cache for old path
                if (!string.IsNullOrWhiteSpace(studentInCollection.PictureUrl))
                {
                    UniversalImageConverter.ClearCache(studentInCollection.PictureUrl);
                }
                
                // Update the student's picture URL - this will trigger PropertyChanged due to ObservableProperty
                studentInCollection.PictureUrl = newImagePath;
                
                System.Diagnostics.Debug.WriteLine($"Updated student PictureUrl to: {studentInCollection.PictureUrl}");
            }
            
            // Also update the passed student object
            student.PictureUrl = newImagePath;

            // Update in allStudents as well
            var inAll = allStudents.FirstOrDefault(s => s.Id == student.Id);
            if (inAll != null)
            {
                // Clear cache for old path
                if (!string.IsNullOrWhiteSpace(inAll.PictureUrl))
                {
                    UniversalImageConverter.ClearCache(inAll.PictureUrl);
                }
                inAll.PictureUrl = newImagePath;
            }
            
            // Save to JSON
            await SaveAllStudentsToJson();
            
            System.Diagnostics.Debug.WriteLine("UpdateStudentImage completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating student image: {ex.Message}");
        }
    }

    private static async Task<bool> WaitForReadableFileAsync(string path, int maxAttempts = 20, int delayMs = 100)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (stream.Length > 0)
                {
                    return true;
                }
            }
            catch
            {
                // ignore
            }
            await Task.Delay(delayMs);
        }
        return false;
    }

    // Removed per new unified save method

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
