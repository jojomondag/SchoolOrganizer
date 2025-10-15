using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Google.Apis.Classroom.v1.Data;
using Serilog;

namespace SchoolOrganizer.Src.Services
{
    /// <summary>
    /// Manages the saving and loading of application state, including classroom data
    /// </summary>
    public class StateManager
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        
        private static readonly string _appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SchoolOrganizer");
        
        // Singleton instance
        private static StateManager? _instance;
        public static StateManager Instance => _instance ??= new StateManager();
        
        // Private constructor for singleton pattern
        private StateManager()
        {
            // Create app data directory if it doesn't exist
            if (!Directory.Exists(_appDataPath))
            {
                Directory.CreateDirectory(_appDataPath);
            }
            
            Log.Information("StateManager initialized");
        }
        
        /// <summary>
        /// Checks if an API call should be made based on the last call timestamp
        /// </summary>
        public bool ShouldMakeApiCall(string courseId, string apiCallType, TimeSpan minInterval)
        {
            try
            {
                var courseData = LoadClassroomState(courseId);
                if (courseData == null || courseData.LastApiCallTimestamps == null)
                {
                    return true;
                }
                
                string key = $"{apiCallType}";
                if (courseData.LastApiCallTimestamps.TryGetValue(key, out var lastCallTime))
                {
                    // Check if enough time has passed since the last call
                    return (DateTime.Now - lastCallTime) > minInterval;
                }
                
                // No record of this API call, so we should make it
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error checking if API call should be made for course {courseId}");
                return true; // Default to making the call if there's an error
            }
        }
        
        /// <summary>
        /// Records an API call timestamp
        /// </summary>
        public void RecordApiCall(string courseId, string apiCallType)
        {
            try
            {
                var courseData = LoadClassroomState(courseId);
                if (courseData == null)
                {
                    courseData = new CourseDataCache
                    {
                        Timestamp = DateTime.Now,
                        CourseId = courseId,
                        LastApiCallTimestamps = new Dictionary<string, DateTime>()
                    };
                }
                
                if (courseData.LastApiCallTimestamps == null)
                {
                    courseData.LastApiCallTimestamps = new Dictionary<string, DateTime>();
                }
                
                string key = $"{apiCallType}";
                courseData.LastApiCallTimestamps[key] = DateTime.Now;
                
                SaveClassroomState(courseId, courseData);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error recording API call for course {courseId}");
            }
        }
        
        /// <summary>
        /// Saves the complete state of a classroom
        /// </summary>
        public void SaveClassroomState(string courseId, CourseDataCache courseData)
        {
            try
            {
                string filePath = Path.Combine(_appDataPath, $"classroom_state_{courseId}.json");
                
                // Set the course ID
                courseData.CourseId = courseId;
                
                // Serialize the data
                string jsonContent = JsonSerializer.Serialize(courseData, _jsonOptions);
                File.WriteAllText(filePath, jsonContent);
                
                Log.Information("Saved classroom state for course {CourseId} to {FilePath}", courseId, filePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error saving classroom state for course {CourseId}", courseId);
                throw;
            }
        }
        
        /// <summary>
        /// Loads the classroom state for a course
        /// </summary>
        public CourseDataCache? LoadClassroomState(string courseId)
        {
            try
            {
                string filePath = Path.Combine(_appDataPath, $"classroom_state_{courseId}.json");
                
                if (!File.Exists(filePath))
                {
                    return null;
                }
                
                string jsonContent = File.ReadAllText(filePath);
                var courseData = JsonSerializer.Deserialize<CourseDataCache>(jsonContent);
                
                Log.Information("Loaded classroom state for course {CourseId} from {FilePath}", courseId, filePath);
                return courseData;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading classroom state for course {CourseId}", courseId);
                return null;
            }
        }
    }
    
    /// <summary>
    /// Class to store course data cache
    /// </summary>
    public class CourseDataCache
    {
        /// <summary>
        /// Timestamp when the data was cached
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Course ID
        /// </summary>
        public string CourseId { get; set; } = string.Empty;
        
        /// <summary>
        /// List of assignments in the course
        /// </summary>
        public List<CourseWork> Assignments { get; set; } = new List<CourseWork>();
        
        /// <summary>
        /// List of students in the course
        /// </summary>
        public List<Student> Students { get; set; } = new List<Student>();
        
        /// <summary>
        /// List of student submissions in the course
        /// </summary>
        public List<StudentSubmission> Submissions { get; set; } = new List<StudentSubmission>();
        
        // Enhanced data for faster loading
        public string CourseName { get; set; } = string.Empty;
        public string CourseSection { get; set; } = string.Empty;
        public string CourseState { get; set; } = string.Empty;
        
        // Last API call timestamp to prevent unnecessary reloading
        public Dictionary<string, DateTime> LastApiCallTimestamps { get; set; } = new Dictionary<string, DateTime>();
    }
}