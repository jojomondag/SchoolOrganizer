using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SchoolOrganizer.Models;

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
                var args = new ViewAssignmentsClickedEventArgs(ViewAssignmentsClickedEvent, student);
                RaiseEvent(args);
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
