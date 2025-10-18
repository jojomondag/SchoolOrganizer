namespace SchoolOrganizer.Src.Models.UI;

/// <summary>
/// Defines different display levels for ProfileCard - simplified to 3 sizes
/// </summary>
public enum ProfileCardDisplayLevel
{
    Small,
    Medium,
    Full
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
            ProfileCardDisplayLevel.Small => new()
            {
                Level = level,
                ImageSize = 70,
                CardWidth = 180,  // Was 160 - match actual profile card size
                CardHeight = 180,
                NameFontSize = 13,
                RoleFontSize = 11, // Class name - show this
                SecondaryFontSize = 1, // Teacher name - hide this
                ShowEmail = false,
                ShowEnrollmentDate = false,
                ShowSecondaryInfo = false // Only show class, not teacher
            },
            ProfileCardDisplayLevel.Medium => new()
            {
                Level = level,
                ImageSize = 90,  // Was 80 - match actual profile card size
                CardWidth = 240,  // Was 200 - match actual profile card size
                CardHeight = 240,  // Was 220 - match actual profile card size
                NameFontSize = 14,
                RoleFontSize = 11, // Class name
                SecondaryFontSize = 10, // Teacher name
                ShowEmail = false,
                ShowEnrollmentDate = false,
                ShowSecondaryInfo = true
            },
            ProfileCardDisplayLevel.Full => new()
            {
                Level = level,
                ImageSize = 120,
                CardWidth = 300,
                CardHeight = 400,
                NameFontSize = 18,
                RoleFontSize = 14, // Class name
                SecondaryFontSize = 12, // Teacher name
                ShowEmail = true,
                ShowEnrollmentDate = true,
                ShowSecondaryInfo = true
            },
            _ => GetConfig(ProfileCardDisplayLevel.Medium)
        };
    }
}