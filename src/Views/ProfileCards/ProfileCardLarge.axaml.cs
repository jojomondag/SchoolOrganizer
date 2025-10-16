using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Src.Models.Students;
using Material.Icons;
using Material.Icons.Avalonia;

namespace SchoolOrganizer.Src.Views.ProfileCards
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

        protected override void OnCardPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
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

        // Routed event for View Assignments button click
        public static readonly RoutedEvent<ViewAssignmentsClickedEventArgs> ViewAssignmentsClickedEvent =
            RoutedEvent.Register<ProfileCardLarge, ViewAssignmentsClickedEventArgs>(
                nameof(ViewAssignmentsClicked), RoutingStrategies.Bubble);

        public event EventHandler<ViewAssignmentsClickedEventArgs>? ViewAssignmentsClicked
        {
            add => AddHandler(ViewAssignmentsClickedEvent, value);
            remove => RemoveHandler(ViewAssignmentsClickedEvent, value);
        }

        private void OnViewAssignmentsButtonClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is Student student)
            {
                // Use StudentCoordinatorService to publish the view assignments request
                Services.StudentCoordinatorService.Instance.PublishViewAssignmentsRequested(student);
            }
        }

        protected override void OnProfileImageClicked(object? sender, EventArgs e)
        {
            base.OnProfileImageClicked(sender, e);
        }

        private void OnEmailClicked(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (DataContext is Student student && !string.IsNullOrWhiteSpace(student.Email))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"mailto:{student.Email}",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error opening email client: {ex.Message}");
                }
            }
        }

        private void OnEmailPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            if (this.FindControl<MaterialIcon>("EmailIcon") is { } emailIcon)
            {
                emailIcon.Kind = MaterialIconKind.EmailArrowRightOutline;
            }
        }

        private void OnEmailPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            if (this.FindControl<MaterialIcon>("EmailIcon") is { } emailIcon)
            {
                emailIcon.Kind = MaterialIconKind.Email;
            }
        }
    }

    // Custom event args for View Assignments clicked
    public class ViewAssignmentsClickedEventArgs : RoutedEventArgs
    {
        public Student Student { get; }

        public ViewAssignmentsClickedEventArgs(RoutedEvent routedEvent, Student student) : base(routedEvent)
        {
            Student = student;
        }
    }
}
