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
        SelectedStudent = student;
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
    private async Task Refresh()
    {
        await LoadStudents();
    }
}
