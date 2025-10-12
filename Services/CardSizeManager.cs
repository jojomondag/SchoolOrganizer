using SchoolOrganizer.Models;

namespace SchoolOrganizer.Services;

/// <summary>
/// Manages card sizing logic with object-oriented approach
/// Handles the 3-size system: Small, Medium, Full
/// </summary>
public class CardSizeManager
{
    /// <summary>
    /// Determines the optimal card size based on student count and available space
    /// </summary>
    public ProfileCardDisplayLevel DetermineOptimalSize(int studentCount, double windowWidth = 1200)
    {
        // Handle edge cases
        if (studentCount <= 0) return ProfileCardDisplayLevel.Medium;
        if (studentCount == 1) return ProfileCardDisplayLevel.Full;
        
        // Determine the best size based on student count
        if (studentCount <= 8) // 2-8 students = Medium
        {
            return ProfileCardDisplayLevel.Medium;
        }
        else
        {
            return ProfileCardDisplayLevel.Small; // 9+ students = Small
        }
    }
    
    /// <summary>
    /// Determines card size based on student count only (for fast operations)
    /// </summary>
    public ProfileCardDisplayLevel DetermineSizeByCount(int studentCount)
    {
        return studentCount switch
        {
            <= 1 => ProfileCardDisplayLevel.Full,
            <= 8 => ProfileCardDisplayLevel.Medium,
            _ => ProfileCardDisplayLevel.Small
        };
    }
}
