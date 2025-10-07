using Avalonia.Controls;

namespace SchoolOrganizer.Views.ProfileCards
{
    public partial class ProfileCardLarge : BaseProfileCard
    {
        public ProfileCardLarge()
        {
            InitializeComponent();

            // Register the RoleInfo property to the RoleText control
            MapPropertyToControl(RoleInfoProperty, "RoleText", "No Role");
        }

        // Large card opts out of hover scaling/shadow
        protected override bool EnableHover => false;

        // RoleInfo is specific to ProfileCardLarge
        public string? RoleInfo
        {
            get => GetValue(RoleInfoProperty);
            set => SetValue(RoleInfoProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> RoleInfoProperty =
            Avalonia.AvaloniaProperty.Register<ProfileCardLarge, string?>("RoleInfo");
    }
}
