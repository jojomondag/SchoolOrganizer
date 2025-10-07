using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Classroom.v1;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Serilog;

namespace SchoolOrganizer.Services;

public class GoogleAuthService
{
    private static readonly string[] Scopes = {
        ClassroomService.Scope.ClassroomCoursesReadonly,
        ClassroomService.Scope.ClassroomCourseworkMe,
        ClassroomService.Scope.ClassroomCourseworkStudents,
        ClassroomService.Scope.ClassroomRosters,
        ClassroomService.Scope.ClassroomProfileEmails,
        ClassroomService.Scope.ClassroomAnnouncementsReadonly,
        ClassroomService.Scope.ClassroomCourseworkStudentsReadonly,
        ClassroomService.Scope.ClassroomProfilePhotos,
        DriveService.Scope.Drive
    };

    private const string ApplicationName = "School Organizer";
    private const string TokenFileName = "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user";
    private static readonly string CredentialsPath = GetCredentialsPath();
    private static readonly string CredDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SchoolOrganizer");
    private static readonly string CredPath = Path.Combine(CredDirectory, TokenFileName);

    private UserCredential? _credential;
    public ClassroomService? ClassroomService { get; private set; }
    public DriveService? DriveService { get; private set; }
    public string UserEmail { get; private set; } = string.Empty;
    public string TeacherName { get; private set; } = string.Empty;

    public async Task<bool> AuthenticateAsync() => await AuthenticateInternalAsync();
    public async Task<bool> CheckAndAuthenticateAsync() => File.Exists(CredPath) && await AuthenticateInternalAsync();

    private async Task<bool> AuthenticateInternalAsync()
    {
        try
        {
            Log.Debug($"Starting authentication process...");
            Log.Debug($"Checking credentials file at: {CredentialsPath}");
            
            if (!File.Exists(CredentialsPath))
            {
                Log.Warning($"Client secrets file not found at '{CredentialsPath}'. Authentication will be skipped.");
                return false;
            }

            Log.Debug("Credentials file found, attempting to read...");
            var secrets = GoogleClientSecrets.FromFile(CredentialsPath);
            
            Log.Debug("Starting Google authorization...");
            
            // Use LocalServerCodeReceiver for desktop applications
            var codeReceiver = new LocalServerCodeReceiver();
            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets.Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(CredDirectory, true),
                codeReceiver
            );

            if (_credential == null)
            {
                Log.Error("Authorization failed: Credential is null");
                return false;
            }

            if (_credential.Token.IsStale)
            {
                Log.Debug("Token is stale, attempting refresh...");
                if (!await RefreshTokenAsync())
                {
                    return false;
                }
            }

            InitializeServices();
            await FetchTeacherNameAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Authentication failed with exception");
            return false;
        }
    }

    private async Task<bool> RefreshTokenAsync()
    {
        try
        {
            // Use a retry mechanism with file locking
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    bool refreshed = await _credential!.RefreshTokenAsync(CancellationToken.None);
                    Log.Debug(refreshed ? "Token refreshed successfully." : "Token refresh failed.");
                    return refreshed;
                }
                catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                {
                    Log.Warning($"Token refresh attempt {attempt + 1} failed due to file lock, retrying...");
                    if (attempt < 2)
                    {
                        await Task.Delay(100 * (attempt + 1)); // Exponential backoff
                        continue;
                    }
                    throw;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Token refresh failed after retries");
            return false;
        }
    }

    private void InitializeServices()
    {
        ClassroomService = new ClassroomService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = _credential,
            ApplicationName = ApplicationName
        });

        DriveService = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = _credential,
            ApplicationName = ApplicationName
        });

        Log.Debug("Google services initialized successfully.");
    }

    private async Task FetchTeacherNameAsync()
    {
        if (ClassroomService == null)
        {
            Log.Warning("ClassroomService is not initialized.");
            return;
        }

        try
        {
            var userProfile = await ClassroomService.UserProfiles.Get("me").ExecuteAsync();
            TeacherName = userProfile.Name?.FullName ?? "Unknown Teacher";
            Log.Information($"Authenticated as {TeacherName}.");
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching teacher's name: {ex.Message}");
            TeacherName = "Unknown Teacher";
        }
    }

    public async Task<string> GetTeacherProfileImageUrlAsync()
    {
        if (ClassroomService == null) return string.Empty;

        try
        {
            var userProfile = await ClassroomService.UserProfiles.Get("me").ExecuteAsync();
            return string.IsNullOrEmpty(userProfile.PhotoUrl) ? string.Empty : userProfile.PhotoUrl.StartsWith("//") ? "https:" + userProfile.PhotoUrl : userProfile.PhotoUrl;
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching profile image URL: {ex.Message}");
            return string.Empty;
        }
    }

    public void ClearCredentials()
    {
        try
        {
            if (File.Exists(CredPath))
            {
                File.Delete(CredPath);
            }

            _credential = null;
            ClassroomService = null;
            DriveService = null;
            UserEmail = string.Empty;
            TeacherName = string.Empty;

            Log.Information("User credentials cleared successfully.");
        }
        catch (Exception ex)
        {
            Log.Error($"Error clearing credentials: {ex.Message}");
            throw;
        }
    }

    private static string GetCredentialsPath()
    {
        // Check if running as bundled app
        var isBundled = AppContext.BaseDirectory.Contains(".app/Contents/MacOS");
        
        if (isBundled)
        {
            var baseDir = Path.GetDirectoryName(AppContext.BaseDirectory) 
                ?? throw new InvalidOperationException("Unable to determine base directory");
            var parentDir = Path.GetDirectoryName(baseDir) 
                ?? throw new InvalidOperationException("Unable to determine parent directory");
            return Path.Combine(parentDir, "Resources", "credentials.json");
        }
        // For development
        return Path.Combine(AppContext.BaseDirectory, "Resources", "credentials.json");
    }
}
