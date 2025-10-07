using Avalonia.Controls;

namespace SchoolOrganizer.Views.ProfileCards
{
    public partial class ProfileCardMedium : BaseProfileCard
    {
        public ProfileCardMedium()
        {
            InitializeComponent();

            // Register medium-specific properties so base class will update controls
            MapPropertyToControl(ClassProperty, "ClassText", "No Class");
            MapPropertyToControl(MentorProperty, "MentorText", "No Mentor");
        }

        // Properties specific to ProfileCardMedium
        public string? Class
        {
            get => GetValue(ClassProperty);
            set => SetValue(ClassProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> ClassProperty =
            Avalonia.AvaloniaProperty.Register<ProfileCardMedium, string?>("Class");

        public string? Mentor
        {
            get => GetValue(MentorProperty);
            set => SetValue(MentorProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> MentorProperty =
            Avalonia.AvaloniaProperty.Register<ProfileCardMedium, string?>("Mentor");
    }
}
