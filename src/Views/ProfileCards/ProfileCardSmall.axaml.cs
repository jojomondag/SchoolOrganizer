using Avalonia.Controls;
using SchoolOrganizer.Src.Models.Students;

namespace SchoolOrganizer.Src.Views.ProfileCards
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

        protected override void UpdateAllControls()
        {
            // Call base implementation first to update common controls
            base.UpdateAllControls();
            
            // Update small-specific controls from DataContext
            if (DataContext is IPerson person)
            {
                if (this.FindControl<TextBlock>("RoleText") is { } roleText)
                    roleText.Text = person.RoleInfo ?? "No Role";
            }
        }
    }
}
