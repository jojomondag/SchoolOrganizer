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
            WidthProperty.Changed.AddClassHandler<AddStudentProfileCard>((control, e) =>
            {
                Log.Information("AddStudentProfileCard Width changed to: {Width}", control.Width);
                control.UpdateLayout();
            });
            
            HeightProperty.Changed.AddClassHandler<AddStudentProfileCard>((control, e) =>
            {
                Log.Information("AddStudentProfileCard Height changed to: {Height}", control.Height);
                control.UpdateLayout();
            });
            
            MarginProperty.Changed.AddClassHandler<AddStudentProfileCard>((control, e) =>
            {
                Log.Information("AddStudentProfileCard Margin changed to: {Margin}", control.Margin);
                control.UpdateLayout();
            });
            
            CornerRadiusProperty.Changed.AddClassHandler<AddStudentProfileCard>((control, e) =>
            {
                Log.Information("AddStudentProfileCard CornerRadius changed to: {CornerRadius}", control.CornerRadius);
                control.UpdateLayout();
            });
            
            PaddingProperty.Changed.AddClassHandler<AddStudentProfileCard>((control, e) =>
            {
                Log.Information("AddStudentProfileCard Padding changed to: {Padding}", control.Padding);
                control.UpdateLayout();
            });
            
            IconSizeProperty.Changed.AddClassHandler<AddStudentProfileCard>((control, e) =>
            {
                Log.Information("AddStudentProfileCard IconSize changed to: {IconSize}", control.IconSize);
                control.UpdateLayout();
            });
            
            TitleFontSizeProperty.Changed.AddClassHandler<AddStudentProfileCard>((control, e) =>
            {
                Log.Information("AddStudentProfileCard TitleFontSize changed to: {TitleFontSize}", control.TitleFontSize);
                control.UpdateLayout();
            });
            
            SubtitleTextProperty.Changed.AddClassHandler<AddStudentProfileCard>((control, e) =>
            {
                Log.Information("AddStudentProfileCard SubtitleText changed to: '{SubtitleText}'", control.AddStudentSubtitleText);
                control.UpdateLayout();
            });
            
            SubtitleFontSizeProperty.Changed.AddClassHandler<AddStudentProfileCard>((control, e) =>
            {
                Log.Information("AddStudentProfileCard SubtitleFontSize changed to: {SubtitleFontSize}", control.SubtitleFontSize);
                control.UpdateLayout();
            });
            
            // Initial layout update
            Loaded += (s, e) => UpdateLayout();
        }
        
        private new void UpdateLayout()
        {
            Log.Information("AddStudentProfileCard UpdateLayout - Width: {Width}, Height: {Height}, IconSize: {IconSize}, TitleFontSize: {TitleFontSize}", 
                Width, Height, IconSize, TitleFontSize);
            
            if (this.FindControl<Grid>("MainGrid") is { } mainGrid)
            {
                mainGrid.Margin = Margin;
                Log.Information("MainGrid Margin set to: {Margin}", Margin);
            }
            
            if (this.FindControl<Border>("CardBorder") is { } cardBorder)
            {
                cardBorder.CornerRadius = CornerRadius;
                cardBorder.Padding = Padding;
                cardBorder.Width = Width;
                cardBorder.Height = Height;
                Log.Information("CardBorder - CornerRadius: {CornerRadius}, Padding: {Padding}, Width: {Width}, Height: {Height}", CornerRadius, Padding, Width, Height);
            }
            
            if (this.FindControl<ProfileImage>("AddIconBorder") is { } addIconBorder)
            {
                addIconBorder.Width = IconSize;
                addIconBorder.Height = IconSize;
                Log.Information("AddIconBorder size set to: {IconSize}x{IconSize}", IconSize, IconSize);
            }
            
            if (this.FindControl<TextBlock>("AddStudentText") is { } addStudentText)
            {
                addStudentText.FontSize = TitleFontSize;
                Log.Information("AddStudentText FontSize set to: {TitleFontSize}", TitleFontSize);
            }
            
            if (this.FindControl<TextBlock>("SubtitleText") is { } subtitleText)
            {
                subtitleText.Text = AddStudentSubtitleText;
                subtitleText.FontSize = SubtitleFontSize;
                Log.Information("SubtitleText - Text: '{AddStudentSubtitleText}', FontSize: {SubtitleFontSize}", AddStudentSubtitleText, SubtitleFontSize);
            }
        }

        private void OnAddStudentClicked(object? sender, EventArgs e)
        {
            // Create a dummy student for the "Add Student" action
            var dummyStudent = new Models.Students.Student { Name = "Add Student" };
            OnCardClicked(dummyStudent);
        }
    }
}
