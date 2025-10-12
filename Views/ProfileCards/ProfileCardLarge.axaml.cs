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
            
            // Handle clicks on the card to prevent them from bubbling up to the background button
            this.PointerPressed += OnCardPointerPressed;
        }

        // Large card opts out of hover scaling/shadow
        protected override bool EnableHover => false;

        private void OnCardPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            // Mark the event as handled to prevent it from bubbling up to the background button
            e.Handled = true;
        }

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
