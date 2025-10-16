using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using SchoolOrganizer.Src.Views.ProfileCards.Components;
using Serilog;

namespace SchoolOrganizer.Src.Views.ProfileCards
{
    public partial class AddStudentProfileCard : BaseProfileCard
    {
        public new static readonly StyledProperty<double> WidthProperty =
            AvaloniaProperty.Register<AddStudentProfileCard, double>(nameof(Width), 320.0);

        public new static readonly StyledProperty<double> HeightProperty =
            AvaloniaProperty.Register<AddStudentProfileCard, double>(nameof(Height), 320.0);

        public new static readonly StyledProperty<Thickness> MarginProperty =
            AvaloniaProperty.Register<AddStudentProfileCard, Thickness>(nameof(Margin), new Thickness(20, 40, 20, 40));

        public new static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
            AvaloniaProperty.Register<AddStudentProfileCard, CornerRadius>(nameof(CornerRadius), new CornerRadius(16));

        public new static readonly StyledProperty<Thickness> PaddingProperty =
            AvaloniaProperty.Register<AddStudentProfileCard, Thickness>(nameof(Padding), new Thickness(12));

        public static readonly StyledProperty<double> IconSizeProperty =
            AvaloniaProperty.Register<AddStudentProfileCard, double>(nameof(IconSize), 90.0);

        public static readonly StyledProperty<double> TitleFontSizeProperty =
            AvaloniaProperty.Register<AddStudentProfileCard, double>(nameof(TitleFontSize), 20.0);

        public static readonly StyledProperty<string> SubtitleTextProperty =
            AvaloniaProperty.Register<AddStudentProfileCard, string>(nameof(AddStudentSubtitleText), "Click to add a new student");

        public static readonly StyledProperty<double> SubtitleFontSizeProperty =
            AvaloniaProperty.Register<AddStudentProfileCard, double>(nameof(SubtitleFontSize), 12.0);

        public new double Width
        {
            get => GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        public new double Height
        {
            get => GetValue(HeightProperty);
            set => SetValue(HeightProperty, value);
        }

        public new Thickness Margin
        {
            get => GetValue(MarginProperty);
            set => SetValue(MarginProperty, value);
        }

        public new CornerRadius CornerRadius
        {
            get => GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public new Thickness Padding
        {
            get => GetValue(PaddingProperty);
            set => SetValue(PaddingProperty, value);
        }

        public double IconSize
        {
            get => GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public double TitleFontSize
        {
            get => GetValue(TitleFontSizeProperty);
            set => SetValue(TitleFontSizeProperty, value);
        }

        public string AddStudentSubtitleText
        {
            get => GetValue(SubtitleTextProperty);
            set => SetValue(SubtitleTextProperty, value);
        }

        public double SubtitleFontSize
        {
            get => GetValue(SubtitleFontSizeProperty);
            set => SetValue(SubtitleFontSizeProperty, value);
        }

        public AddStudentProfileCard()
        {
            InitializeComponent();
            
            // Update layout when properties change
            var properties = new AvaloniaProperty[] { WidthProperty, HeightProperty, MarginProperty, CornerRadiusProperty, 
                                   PaddingProperty, IconSizeProperty, TitleFontSizeProperty, 
                                   SubtitleTextProperty, SubtitleFontSizeProperty };
            foreach (var property in properties)
            {
                property.Changed.AddClassHandler<AddStudentProfileCard>((control, e) => control.UpdateLayout());
            }
            
            // Initial layout update
            Loaded += (s, e) => UpdateLayout();
        }
        
        private new void UpdateLayout()
        {
            UpdateControl<Grid>("MainGrid", grid => grid.Margin = Margin);
            UpdateControl<Border>("CardBorder", border => {
                border.CornerRadius = CornerRadius;
                border.Padding = Padding;
                border.Width = Width;
                border.Height = Height;
            });
            UpdateControl<ProfileImage>("AddIconBorder", icon => {
                icon.Width = IconSize;
                icon.Height = IconSize;
            });
            UpdateControl<TextBlock>("AddStudentText", text => text.FontSize = TitleFontSize);
            UpdateControl<TextBlock>("SubtitleText", text => {
                text.Text = AddStudentSubtitleText;
                text.FontSize = SubtitleFontSize;
            });
        }

        private void UpdateControl<T>(string name, Action<T> updateAction) where T : Control
        {
            if (this.FindControl<T>(name) is { } control)
                updateAction(control);
        }

        private void OnAddStudentClicked(object? sender, EventArgs e)
        {
            // Create a dummy student for the "Add Student" action
            var dummyStudent = new Models.Students.Student { Name = "Add Student" };
            OnCardClicked(dummyStudent);
        }
    }
}
