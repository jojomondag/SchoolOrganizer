using SchoolOrganizer.Src.Models.UI;

namespace SchoolOrganizer.Src.Services;

/// <summary>
/// Manages card sizing logic with object-oriented approach
/// Handles the 3-size system: Small, Medium, Full
/// </summary>
public class CardSizeManager
{
    /// <summary>
    /// Determines the optimal card size based on student count
    /// </summary>
    public ProfileCardDisplayLevel DetermineOptimalSize(int studentCount, double windowWidth = 1200)
    {
        return DetermineSizeByCount(studentCount);
    }
    
    /// <summary>
    /// Determines card size based on student count only (for fast operations)
    /// </summary>
    public ProfileCardDisplayLevel DetermineSizeByCount(int studentCount)
    {
        return studentCount switch
        {
            <= 0 => ProfileCardDisplayLevel.Medium, // Handle empty case
            <= 8 => ProfileCardDisplayLevel.Medium, // 1-8 students = Medium
            _ => ProfileCardDisplayLevel.Small // 9+ students = Small
        };
    }
}
