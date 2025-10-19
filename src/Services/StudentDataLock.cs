using System.Threading;

namespace SchoolOrganizer.Src.Services;

/// <summary>
/// Provides a shared semaphore to prevent concurrent writes to students.json
/// </summary>
public static class StudentDataLock
{
    /// <summary>
    /// Static semaphore to ensure only one thread can write to students.json at a time
    /// </summary>
    public static readonly SemaphoreSlim FileLock = new(1, 1);
}
