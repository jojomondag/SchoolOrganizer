using Avalonia.Controls;

namespace SchoolOrganizer.Views.ProfileCard
{
    public partial class ProfileCardSmall : BaseProfileCard
    {
        public ProfileCardSmall()
        {
            InitializeComponent();

            // Register small-specific property mapping
            MapPropertyToControl(RoleInfoProperty, "RoleText", "No Role");
        }

        // Properties specific to ProfileCardSmall
        public string? RoleInfo
        {
            get => GetValue(RoleInfoProperty);
            set => SetValue(RoleInfoProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> RoleInfoProperty =
            Avalonia.AvaloniaProperty.Register<ProfileCardSmall, string?>("RoleInfo");
    }
}
