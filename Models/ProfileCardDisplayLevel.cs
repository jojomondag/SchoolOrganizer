namespace SchoolOrganizer.Models;

/// <summary>
/// Defines different display levels for ProfileCard based on available space and context
/// </summary>
public enum ProfileCardDisplayLevel
{
    /// <summary>
    /// Compact view - Small card with minimal info (90x90 image, name only)
    /// Used when many cards are displayed or limited space
    /// </summary>
    Compact,
    
    /// <summary>
    /// Standard view - Default card size with basic info (120x120 image, name + role)
    /// Used for normal gallery view
    /// </summary>
    Standard,
    
    /// <summary>
    /// Detailed view - Larger card with extended info (150x150 image, name + role + secondary + email)
    /// Used when fewer cards are shown or more space is available
    /// </summary>
    Detailed,
    
    /// <summary>
    /// Expanded view - Maximum card size with all available information (180x180 image, all details + enrollment date)
    /// Used when very few cards are displayed or in single-card context
    /// </summary>
    Expanded
}

/// <summary>
/// Configuration for ProfileCard display based on the selected level
/// </summary>
public class ProfileCardDisplayConfig
{
    public ProfileCardDisplayLevel Level { get; set; }
    public double ImageSize { get; set; }
    public double CardWidth { get; set; }
    public double CardHeight { get; set; }
    public double NameFontSize { get; set; }
    public double RoleFontSize { get; set; }
    public double SecondaryFontSize { get; set; }
    public bool ShowEmail { get; set; }
    public bool ShowEnrollmentDate { get; set; }
    public bool ShowSecondaryInfo { get; set; }
    
    public static ProfileCardDisplayConfig GetConfig(ProfileCardDisplayLevel level)
    {
        return level switch
        {
            ProfileCardDisplayLevel.Compact => new()
            {
                Level = level,
                ImageSize = 70,
                CardWidth = 180,
                CardHeight = 240,
                NameFontSize = 14,
                RoleFontSize = 10,
                SecondaryFontSize = 9,
                ShowEmail = false,
                ShowEnrollmentDate = false,
                ShowSecondaryInfo = false
            },
            ProfileCardDisplayLevel.Standard => new()
            {
                Level = level,
                ImageSize = 90,
                CardWidth = 240,
                CardHeight = 320,
                NameFontSize = 16,
                RoleFontSize = 12,
                SecondaryFontSize = 10,
                ShowEmail = false,
                ShowEnrollmentDate = false,
                ShowSecondaryInfo = true
            },
            ProfileCardDisplayLevel.Detailed => new()
            {
                Level = level,
                ImageSize = 120,
                CardWidth = 300,
                CardHeight = 400,
                NameFontSize = 18,
                RoleFontSize = 14,
                SecondaryFontSize = 12,
                ShowEmail = true,
                ShowEnrollmentDate = false,
                ShowSecondaryInfo = true
            },
            ProfileCardDisplayLevel.Expanded => new()
            {
                Level = level,
                ImageSize = 150,
                CardWidth = 360,
                CardHeight = 480,
                NameFontSize = 20,
                RoleFontSize = 16,
                SecondaryFontSize = 14,
                ShowEmail = true,
                ShowEnrollmentDate = true,
                ShowSecondaryInfo = true
            },
            _ => GetConfig(ProfileCardDisplayLevel.Standard)
        };
    }
}