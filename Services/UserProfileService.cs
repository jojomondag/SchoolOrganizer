using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Serilog;

namespace SchoolOrganizer.Services;

public class UserProfileService
{
    private readonly GoogleAuthService _authService;
    private static readonly string[] CheerfulMessages = new[]
    {
        "Every student carries the potential to change the world.",
        "Your guidance today builds tomorrow's leaders.",
        "Together, we are shaping a brighter future.",
        "Inspiring curiosity now creates lifelong learners.",
        "Each day we grow, so do our students' possibilities.",
        "Education opens the door to endless opportunities.",
        "Every challenge is a step toward a stronger future.",
        "Empowering students today means transforming the future.",
        "A classroom full of questions is a classroom full of potential.",
        "Learning is a journey; your dedication makes it impactful.",
        "The seeds of knowledge we plant today will bloom for generations.",
        "Helping students find their path helps us shape the world.",
        "Teaching isn't just about today, it's about shaping the future.",
        "Your support makes students believe they can achieve anything.",
        "Together, we create an environment where students thrive.",
        "You're making a real difference, keep up the great work!",
        "Your passion for teaching shines bright every day!",
        "You're an inspiration—keep guiding those eager minds!",
        "You are capable of more than you know, keep pushing forward!",
        "Remember, your efforts today are creating success stories.",
        "You are a beacon of knowledge, lighting the way for others.",
        "Your positivity fuels the learning environment—keep it up!",
        "Believe in yourself as much as you believe in your students!",
        "You're a key part of something much bigger—keep the energy alive!",
        "Every smile you inspire makes a lasting impact."
    };

    public UserProfileService(GoogleAuthService authService)
    {
        _authService = authService;
    }

    public async Task<(Bitmap? ProfileImage, string CheerfulMessage)> LoadProfileImageAsync()
    {
        try
        {
            string imageUrl = await _authService.GetTeacherProfileImageUrlAsync();
            if (!string.IsNullOrEmpty(imageUrl))
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                if (imageBytes.Length > 0)
                {
                    using var ms = new MemoryStream(imageBytes);
                    var profileImage = new Bitmap(ms);
                    var random = new Random();
                    var cheerfulMessage = CheerfulMessages[random.Next(CheerfulMessages.Length)];
                    return (profileImage, cheerfulMessage);
                }
                Log.Warning("Downloaded image is empty");
                return (null, "Profile image is empty.");
            }
            Log.Warning("No profile image URL available");
            return (null, "Profile image not available.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load profile image");
            return (null, $"Failed to load profile image: {ex.Message}");
        }
    }
}
