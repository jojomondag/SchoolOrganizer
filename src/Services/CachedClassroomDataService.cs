using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Classroom.v1.Data;
using Serilog;

namespace SchoolOrganizer.Src.Services
{
    /// <summary>
    /// Cached classroom data service that provides API throttling and caching
    /// </summary>
    public class CachedClassroomDataService
    {
        private readonly ClassroomDataService _underlyingService;
        
        public CachedClassroomDataService(ClassroomDataService underlyingService)
        {
            _underlyingService = underlyingService ?? throw new ArgumentNullException(nameof(underlyingService));
            Log.Information("CachedClassroomDataService initialized");
        }

        /// <summary>
        /// Gets active classrooms (not cached, as this changes frequently)
        /// </summary>
        public async Task<IList<Course>> GetActiveClassroomsAsync()
        {
            return await _underlyingService.GetActiveClassroomsAsync();
        }

        /// <summary>
        /// Gets course work with smart caching to reduce API calls
        /// </summary>
        public async Task<List<CourseWork>> GetCourseWorkAsync(string courseId)
        {
            try
            {
                // Check if we should make an API call based on throttling
                var shouldMakeCall = StateManager.Instance.ShouldMakeApiCall(courseId, "CourseWork", TimeSpan.FromMinutes(30));
                
                if (!shouldMakeCall)
                {
                    // Try to get from cache first
                    var cachedData = StateManager.Instance.LoadClassroomState(courseId);
                    if (cachedData?.Assignments != null && cachedData.Assignments.Count > 0)
                    {
                        Log.Information("Using cached course work for {CourseId} ({Count} assignments)", courseId, cachedData.Assignments.Count);
                        return cachedData.Assignments;
                    }
                }
                
                Log.Information("Getting course work for {CourseId} (API call)", courseId);
                var assignments = await _underlyingService.GetCourseWorkAsync(courseId);
                Log.Information("Retrieved {Count} assignments for course {CourseId}", assignments.Count, courseId);
                
                // Record the API call timestamp
                StateManager.Instance.RecordApiCall(courseId, "CourseWork");
                
                return assignments;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting course work for {CourseId}", courseId);
                throw;
            }
        }
        
        /// <summary>
        /// Gets students in course with smart caching to reduce API calls
        /// </summary>
        public async Task<IList<Student>> GetStudentsInCourseAsync(string courseId)
        {
            try
            {
                // Check if we should make an API call based on throttling
                var shouldMakeCall = StateManager.Instance.ShouldMakeApiCall(courseId, "Students", TimeSpan.FromMinutes(15));
                
                if (!shouldMakeCall)
                {
                    // Try to get from cache first
                    var cachedData = StateManager.Instance.LoadClassroomState(courseId);
                    if (cachedData?.Students != null && cachedData.Students.Count > 0)
                    {
                        Log.Information("Using cached students for {CourseId} ({Count} students)", courseId, cachedData.Students.Count);
                        return cachedData.Students;
                    }
                }
                
                Log.Information("Getting students for {CourseId} (API call)", courseId);
                var students = await _underlyingService.GetStudentsInCourseAsync(courseId);
                Log.Information("Retrieved {Count} students for course {CourseId}", students.Count, courseId);
                
                // Record the API call timestamp
                StateManager.Instance.RecordApiCall(courseId, "Students");
                
                return students;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting students for {CourseId}", courseId);
                throw;
            }
        }
        
        /// <summary>
        /// Gets student submissions with smart caching to reduce API calls
        /// </summary>
        public async Task<List<StudentSubmission>> GetStudentSubmissionsAsync(string courseId)
        {
            try
            {
                // Check if we should make an API call based on throttling
                var shouldMakeCall = StateManager.Instance.ShouldMakeApiCall(courseId, "Submissions", TimeSpan.FromMinutes(10));
                
                if (!shouldMakeCall)
                {
                    // Try to get from cache first
                    var cachedData = StateManager.Instance.LoadClassroomState(courseId);
                    if (cachedData?.Submissions != null && cachedData.Submissions.Count > 0)
                    {
                        Log.Information("Using cached submissions for {CourseId} ({Count} submissions)", courseId, cachedData.Submissions.Count);
                        return cachedData.Submissions;
                    }
                }
                
                Log.Information("Getting student submissions for {CourseId} (API call)", courseId);
                var submissions = await _underlyingService.GetStudentSubmissionsAsync(courseId);
                Log.Information("Retrieved {Count} submissions for course {CourseId}", submissions.Count, courseId);
                
                // Record the API call timestamp
                StateManager.Instance.RecordApiCall(courseId, "Submissions");
                
                return submissions.ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting student submissions for {CourseId}", courseId);
                throw;
            }
        }
    }
}
