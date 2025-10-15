using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using Serilog;

namespace SchoolOrganizer.Src.Services;

public class ClassroomDataService
{
    private readonly ClassroomService _classroomService = null!;

    public ClassroomDataService(ClassroomService classroomService)
    {
        _classroomService = classroomService ?? throw new ArgumentNullException(nameof(classroomService));
        Log.Information($"ClassroomDataService initialized with ClassroomService: {_classroomService != null}");
        Log.Information($"ClassroomService ApplicationName: {_classroomService?.ApplicationName ?? "null"}");
        Log.Information($"ClassroomService BaseUri: {_classroomService?.BaseUri}");
    }

    public async Task<IList<Course>> GetActiveClassroomsAsync()
    {
        try
        {
            var allCourses = await FetchAllPagesAsync<Course>(async (pageToken) =>
            {
                var request = _classroomService.Courses.List();
                request.CourseStates = CoursesResource.ListRequest.CourseStatesEnum.ACTIVE;
                request.PageToken = pageToken;
                
                var response = await request.ExecuteAsync();
                return (response.Courses ?? new List<Course>(), response.NextPageToken);
            });

            // Remove any duplicate courses based on their ID to ensure uniqueness
            var uniqueCourses = allCourses
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .ToList();

            Log.Information($"Fetched {allCourses.Count} total courses, {uniqueCourses.Count} unique courses");
            return uniqueCourses;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to fetch classrooms: {ex.Message}");
            return new List<Course>();
        }
    }

    public async Task<IList<StudentSubmission>> GetStudentSubmissionsAsync(string courseId)
    {
        var submissions = new List<StudentSubmission>();

        try
        {
            Log.Information($"Starting to fetch student submissions for course {courseId}");
            
            // Add timeout to prevent hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            
            var desiredStates = new[]
            {
                CoursesResource.CourseWorkResource.StudentSubmissionsResource.ListRequest.StatesEnum.CREATED,
                CoursesResource.CourseWorkResource.StudentSubmissionsResource.ListRequest.StatesEnum.TURNEDIN,
                CoursesResource.CourseWorkResource.StudentSubmissionsResource.ListRequest.StatesEnum.RETURNED,
                CoursesResource.CourseWorkResource.StudentSubmissionsResource.ListRequest.StatesEnum.RECLAIMEDBYSTUDENT
            };

            foreach (var state in desiredStates)
            {
                var request = _classroomService.Courses.CourseWork.StudentSubmissions.List(courseId, "-");
                request.States = state;

                do
                {
                    try
                    {
                        Log.Information($"Fetching submissions for state {state} for course {courseId}");
                        var response = await request.ExecuteAsync(cts.Token);
                        if (response.StudentSubmissions != null)
                        {
                            submissions.AddRange(response.StudentSubmissions);
                            Log.Information($"Retrieved {response.StudentSubmissions.Count} submissions for state {state}");
                        }

                        request.PageToken = response.NextPageToken;
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Error($"Timeout while fetching student submissions for course {courseId}");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Error fetching submissions for state {state} in course {courseId}: {ex.Message}");
                        throw;
                    }
                } while (!string.IsNullOrEmpty(request.PageToken));
            }

            Log.Information($"Completed fetching student submissions for course {courseId}, total: {submissions.Count}");
            return submissions;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to fetch student submissions for course {courseId}: {ex.Message}");
            return submissions;
        }
    }

    public async Task<IList<Student>> GetStudentsInCourseAsync(string courseId)
    {
        try
        {
            Log.Information($"Starting to fetch students for course {courseId}");
            
            // Test if the API service is working at all
            Log.Information($"Testing API service - creating request for course {courseId}");
            var request = _classroomService.Courses.Students.List(courseId);
            Log.Information($"Request created successfully for course {courseId}");
            
            // Add timeout to prevent hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var result = await FetchAllPagesAsync<Student>(async (pageToken) =>
            {
                Log.Information($"Fetching students page for course {courseId}, pageToken: {pageToken}");
                var request = _classroomService.Courses.Students.List(courseId);
                request.PageToken = pageToken;
                
                try
                {
                    Log.Information($"About to call ExecuteAsync for course {courseId}");
                    var response = await request.ExecuteAsync(cts.Token);
                    Log.Information($"Received {response.Students?.Count ?? 0} students for course {courseId}");
                    return (response.Students ?? new List<Student>(), response.NextPageToken);
                }
                catch (OperationCanceledException)
                {
                    Log.Error($"Timeout while fetching students for course {courseId}");
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Error($"Error in API call for students in course {courseId}: {ex.Message}");
                    throw;
                }
            });
            Log.Information($"Completed fetching students for course {courseId}, total: {result.Count}");
            return result;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to fetch students in course {courseId}: {ex.Message}");
            return new List<Student>();
        }
    }

    public async Task<List<CourseWork>> GetCourseWorkAsync(string courseId)
    {
        try
        {
            Log.Information($"Starting to fetch course work for course {courseId}");
            
            // Add timeout to prevent hanging
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            
            var courseWorks = new List<CourseWork>();
            var request = _classroomService.Courses.CourseWork.List(courseId);

            do
            {
                try
                {
                    Log.Information($"Fetching course work page for course {courseId}");
                    var response = await request.ExecuteAsync(cts.Token);
                    if (response.CourseWork != null)
                    {
                        courseWorks.AddRange(response.CourseWork);
                        Log.Information($"Retrieved {response.CourseWork.Count} course work items");
                    }

                    request.PageToken = response.NextPageToken;
                }
                catch (OperationCanceledException)
                {
                    Log.Error($"Timeout while fetching course work for course {courseId}");
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Error($"Error fetching course work for course {courseId}: {ex.Message}");
                    throw;
                }
            } while (!string.IsNullOrEmpty(request.PageToken));

            Log.Information($"Completed fetching course work for course {courseId}, total: {courseWorks.Count}");
            return courseWorks.OrderBy(cw => cw.DueDate != null ? ConvertToDateTime(cw.DueDate) : DateTime.MaxValue).ToList();
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to fetch course work for course {courseId}: {ex.Message}");
            return new List<CourseWork>();
        }
    }

    private DateTime ConvertToDateTime(Date date)
    {
        int year = date.Year ?? 1;
        int month = date.Month ?? 1;
        int day = date.Day ?? 1;

        if (year < 1 || month < 1 || month > 12 || day < 1 || day > 31)
            return DateTime.MaxValue;

        string dateString = $"{year}-{month:D2}-{day:D2}";

        return DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt
            : DateTime.MaxValue;
    }

    public async Task<DateTime?> GetLatestSubmissionModificationTimeAsync(string courseId)
    {
        try
        {
            var request = _classroomService.Courses.CourseWork.StudentSubmissions.List(courseId, "-");

            DateTime? latestModificationTime = null;

            do
            {
                var response = await request.ExecuteAsync();
                if (response.StudentSubmissions != null)
                {
                    foreach (var submission in response.StudentSubmissions)
                    {
                        if (submission.UpdateTimeDateTimeOffset.HasValue)
                        {
                            var submissionUpdateTime = submission.UpdateTimeDateTimeOffset.Value.UtcDateTime;

                            if (latestModificationTime == null || submissionUpdateTime > latestModificationTime)
                            {
                                latestModificationTime = submissionUpdateTime;
                            }
                        }
                    }
                }
                request.PageToken = response.NextPageToken;
            } while (!string.IsNullOrEmpty(request.PageToken));

            return latestModificationTime;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to fetch latest submission modification time for course {courseId}: {ex.Message}");
            return null;
        }
    }

    private async Task<List<T>> FetchAllPagesAsync<T>(Func<string?, Task<(IList<T> Items, string? NextPageToken)>> fetchPage)
    {
        var allItems = new List<T>();
        string? pageToken = null;

        do
        {
            var (items, nextPageToken) = await fetchPage(pageToken);
            allItems.AddRange(items);
            pageToken = nextPageToken;
        } while (!string.IsNullOrEmpty(pageToken));

        return allItems;
    }
}