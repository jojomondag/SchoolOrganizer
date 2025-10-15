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
        // Removed automatic Full view for 1 student - always use grid view
        
        // Determine the best size based on student count
        if (studentCount <= 8) // 1-8 students = Medium
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
            <= 0 => ProfileCardDisplayLevel.Medium, // Handle empty case
            <= 8 => ProfileCardDisplayLevel.Medium, // 1-8 students = Medium (removed Full for 1 student)
            _ => ProfileCardDisplayLevel.Small
        };
    }
}
