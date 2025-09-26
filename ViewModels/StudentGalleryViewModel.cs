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
using SchoolOrganizer.Views.Converters;

namespace SchoolOrganizer.ViewModels;

public partial class StudentGalleryViewModel : ViewModelBase
{
    private readonly StudentSearchService searchService = new();

    // Full, unfiltered dataset kept in-memory
    private ObservableCollection<Student> allStudents = new();

    public ObservableCollection<Student> AllStudents => allStudents;

    [ObservableProperty]
    private ObservableCollection<IPerson> students = new();

    [ObservableProperty]
    private Student? selectedStudent;

    [ObservableProperty]
    private string searchText = string.Empty;


    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private ProfileCardDisplayLevel currentDisplayLevel = ProfileCardDisplayLevel.Standard;

    [ObservableProperty]
    private ProfileCardDisplayConfig displayConfig = ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Standard);

    // Properties for controlling view mode
    public bool ShowSingleStudent => Students.Count == 2 && Students.Any(s => s is Student); // Only one actual student + add card
    public bool ShowMultipleStudents => Students.Count != 2 || !Students.Any(s => s is Student); // Multiple students or no students
    
    // Safe property to get first student without index errors
    public Student? FirstStudent => Students.OfType<Student>().FirstOrDefault();

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
                UpdateDisplayLevelBasedOnItemCount();
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
    private void SelectStudent(IPerson? person)
    {
        System.Diagnostics.Debug.WriteLine($"SelectStudent called with: {person?.Name ?? "null"}");
        
        if (person is AddStudentCard)
        {
            // Handle add student card click
            AddStudent();
            return;
        }
        
        if (person is Student student)
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

    partial void OnStudentsChanged(ObservableCollection<IPerson> value)
    {
        UpdateDisplayLevelBasedOnItemCount();
        OnPropertyChanged(nameof(ShowSingleStudent));
        OnPropertyChanged(nameof(ShowMultipleStudents));
        OnPropertyChanged(nameof(FirstStudent));
    }

    private void UpdateDisplayLevelBasedOnItemCount()
    {
        // Count only actual students, not the add card
        var studentCount = Students.OfType<Student>().Count();
        var newLevel = studentCount switch
        {
            <= 4 => ProfileCardDisplayLevel.Expanded,
            <= 8 => ProfileCardDisplayLevel.Detailed,
            <= 16 => ProfileCardDisplayLevel.Standard,
            _ => ProfileCardDisplayLevel.Compact
        };

        if (newLevel != CurrentDisplayLevel)
        {
            CurrentDisplayLevel = newLevel;
            DisplayConfig = ProfileCardDisplayConfig.GetConfig(newLevel);
        }
    }

    private System.Threading.CancellationTokenSource? searchCts;

    private Task ApplySearchImmediate()
    {
        try
        {
            var results = searchService.Search(allStudents, SearchText);
            
            // Instead of clearing and re-adding, create a new collection
            // This helps ensure proper event handling in the UI
            var newStudents = new ObservableCollection<IPerson>();
            foreach (var s in results)
            {
                newStudents.Add(s);
            }
            
            // Always add the AddStudentCard as the last item
            newStudents.Add(new AddStudentCard());
            
            // Replace the entire collection to ensure fresh event binding
            Students = newStudents;
            
            // Trigger property changes for view mode
            OnPropertyChanged(nameof(ShowSingleStudent));
            OnPropertyChanged(nameof(ShowMultipleStudents));
            OnPropertyChanged(nameof(FirstStudent));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
        }
        return Task.CompletedTask;
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

    [RelayCommand]
    private void SetDisplayLevel(ProfileCardDisplayLevel level)
    {
        CurrentDisplayLevel = level;
        DisplayConfig = ProfileCardDisplayConfig.GetConfig(level);
    }

    public async Task AddNewStudentAsync(string name, string className, List<string> mentors, string email, DateTime enrollmentDate, string picturePath)
    {
        try
        {
            var newStudent = new Student
            {
                Id = await GenerateNextStudentIdAsync(),
                Name = name,
                ClassName = className,
                Email = email,
                EnrollmentDate = enrollmentDate,
                PictureUrl = picturePath ?? string.Empty
            };

            // Add mentors
            foreach (var mentor in mentors ?? new List<string>())
            {
                newStudent.AddMentor(mentor);
            }

            allStudents.Add(newStudent);
            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding new student: {ex.Message}");
        }
    }

    public async Task UpdateExistingStudentAsync(Student student, string name, string className, List<string> mentors, string email, DateTime enrollmentDate, string picturePath)
    {
        try
        {
            UpdateStudentProperties(student, name, className, mentors, email, enrollmentDate, picturePath);

            // Ensure the AllStudents collection references the same object or update it
            var inAll = allStudents.FirstOrDefault(s => s.Id == student.Id);
            if (inAll != null && inAll != student)
            {
                UpdateStudentProperties(inAll, name, className, mentors, email, enrollmentDate, picturePath);
            }

            await ApplySearchImmediate();
            await SaveAllStudentsToJson();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating student: {ex.Message}");
        }
    }

    private static void UpdateStudentProperties(Student student, string name, string className, List<string> mentors, string email, DateTime enrollmentDate, string picturePath)
    {
        student.Name = name;
        student.ClassName = className;
        student.Email = email;
        student.EnrollmentDate = enrollmentDate;
        student.PictureUrl = picturePath ?? string.Empty;
        
        // Update mentors
        student.ClearMentors();
        foreach (var mentor in mentors ?? new List<string>())
        {
            student.AddMentor(mentor);
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

            // Helper method to clear cache and update picture URL
            void UpdateStudentPictureUrl(Student s, string oldPath, string newPath)
            {
                if (!string.IsNullOrWhiteSpace(oldPath))
                {
                    UniversalImageConverter.ClearCache(oldPath);
                }
                UniversalImageConverter.ClearCache(newPath);
                s.PictureUrl = newPath;
            }

            // Update the passed student object
            UpdateStudentPictureUrl(student, student.PictureUrl, newImagePath);

            // Update in current filtered collection
            var studentInCollection = Students.OfType<Student>().FirstOrDefault(s => s.Id == student.Id);
            if (studentInCollection != null && studentInCollection != student)
            {
                UpdateStudentPictureUrl(studentInCollection, studentInCollection.PictureUrl, newImagePath);
            }

            // Update in full collection
            var inAll = allStudents.FirstOrDefault(s => s.Id == student.Id);
            if (inAll != null && inAll != student)
            {
                UpdateStudentPictureUrl(inAll, inAll.PictureUrl, newImagePath);
            }
            
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
