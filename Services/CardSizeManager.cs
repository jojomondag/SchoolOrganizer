using System;
using System.Linq;
using SchoolOrganizer.Models;

namespace SchoolOrganizer.Services;

/// <summary>
/// Manages card sizing logic with object-oriented approach
/// Handles the 3-size system: Small, Medium, Full
/// </summary>
public class CardSizeManager
{
    private const double MinWindowWidth = 800;
    private const double MaxCardsPerRow = 6;
    
    /// <summary>
    /// Determines the optimal card size based on student count and available space
    /// </summary>
    public ProfileCardDisplayLevel DetermineOptimalSize(int studentCount, double windowWidth = 1200)
    {
        // Handle edge cases
        if (studentCount <= 0) return ProfileCardDisplayLevel.Medium;
        if (studentCount == 1) return ProfileCardDisplayLevel.Full;
        
        // Calculate available width (account for padding and margins)
        var availableWidth = Math.Max(windowWidth - 100, MinWindowWidth);
        
        // Get configurations for each size
        var smallConfig = ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Small);
        var mediumConfig = ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Medium);
        var fullConfig = ProfileCardDisplayConfig.GetConfig(ProfileCardDisplayLevel.Full);
        
        // Calculate how many cards of each size can fit
        var smallCardsPerRow = CalculateCardsPerRow(availableWidth, smallConfig);
        var mediumCardsPerRow = CalculateCardsPerRow(availableWidth, mediumConfig);
        var fullCardsPerRow = CalculateCardsPerRow(availableWidth, fullConfig);
        
        // Calculate total cards that would fit for each size (max 3 rows)
        var smallTotal = smallCardsPerRow * 3;
        var mediumTotal = mediumCardsPerRow * 3;
        var fullTotal = fullCardsPerRow * 2; // Only 2 rows for full size
        
        // Determine the best size based on student count and available space
        if (studentCount <= 1)
        {
            return ProfileCardDisplayLevel.Full;
        }
        else if (studentCount <= 3) // 2-3 students = Medium
        {
            return ProfileCardDisplayLevel.Medium;
        }
        else
        {
            return ProfileCardDisplayLevel.Small; // 4+ students = Small
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
            <= 3 => ProfileCardDisplayLevel.Medium, // Test: 2-3 students = Medium
            _ => ProfileCardDisplayLevel.Small // Test: 4+ students = Small
        };
    }
    
    /// <summary>
    /// Checks if the current size is appropriate for the given student count
    /// </summary>
    public bool IsSizeAppropriate(ProfileCardDisplayLevel currentSize, int studentCount, double windowWidth = 1200)
    {
        var optimalSize = DetermineOptimalSize(studentCount, windowWidth);
        return currentSize == optimalSize;
    }
    
    /// <summary>
    /// Gets the next appropriate size when student count changes
    /// </summary>
    public ProfileCardDisplayLevel GetNextSize(ProfileCardDisplayLevel currentSize, int newStudentCount, double windowWidth = 1200)
    {
        var optimalSize = DetermineOptimalSize(newStudentCount, windowWidth);
        
        // Only change if the new size is significantly different
        if (Math.Abs(GetSizePriority(currentSize) - GetSizePriority(optimalSize)) >= 2)
        {
            return optimalSize;
        }
        
        return currentSize;
    }
    
    /// <summary>
    /// Calculates how many cards of a given size can fit in the available width
    /// </summary>
    private int CalculateCardsPerRow(double availableWidth, ProfileCardDisplayConfig config)
    {
        var cardWidth = config.CardWidth;
        var cardPadding = 12; // Margin around each card
        
        var cardsPerRow = (int)Math.Floor(availableWidth / (cardWidth + (cardPadding * 2)));
        return Math.Max(1, Math.Min(cardsPerRow, (int)MaxCardsPerRow));
    }
    
    /// <summary>
    /// Gets priority value for size comparison (higher = more detailed)
    /// </summary>
    private int GetSizePriority(ProfileCardDisplayLevel size)
    {
        return size switch
        {
            ProfileCardDisplayLevel.Small => 1,
            ProfileCardDisplayLevel.Medium => 2,
            ProfileCardDisplayLevel.Full => 3,
            _ => 2
        };
    }
    
    /// <summary>
    /// Validates that a size change is necessary and safe
    /// </summary>
    public bool ShouldChangeSize(ProfileCardDisplayLevel currentSize, ProfileCardDisplayLevel newSize, int studentCount)
    {
        // Don't change if sizes are the same
        if (currentSize == newSize) return false;
        
        // Don't change to Full if there are multiple students
        if (newSize == ProfileCardDisplayLevel.Full && studentCount > 1) return false;
        
        // Don't change from Full to Small if only 1 student
        if (currentSize == ProfileCardDisplayLevel.Full && newSize == ProfileCardDisplayLevel.Small && studentCount == 1) return false;
        
        return true;
    }
}
