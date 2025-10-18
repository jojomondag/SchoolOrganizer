using SchoolOrganizer.Src.Models.Students;

namespace SchoolOrganizer.Src.Views.ProfileCards
{
    public partial class ProfileCardMedium : BaseProfileCard
    {
        public ProfileCardMedium()
        {
            InitializeComponent();

            // Register medium-specific properties so base class will update controls
            MapPropertyToControl(ClassProperty, "ClassText", "No Class");
            MapPropertyToControl(TeacherProperty, "TeacherText", "No Teacher");
        }

        // Properties specific to ProfileCardMedium
        public string? Class
        {
            get => GetValue(ClassProperty);
            set => SetValue(ClassProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> ClassProperty =
            Avalonia.AvaloniaProperty.Register<ProfileCardMedium, string?>("Class");

        public string? Teacher
        {
            get => GetValue(TeacherProperty);
            set => SetValue(TeacherProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> TeacherProperty =
            Avalonia.AvaloniaProperty.Register<ProfileCardMedium, string?>("Teacher");

        protected override void UpdateAllControls()
        {
            // Call base implementation first to update common controls
            base.UpdateAllControls();
            
            // Update medium-specific controls from DataContext
            if (DataContext is IPerson person)
            {
                UpdateTextBlockFromPerson("ClassText", person.RoleInfo, "No Class");
                UpdateTextBlockFromPerson("TeacherText", person.SecondaryInfo, "No Teacher");
            }
        }

    }
}
