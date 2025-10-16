using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia;
using Avalonia.Animation;
using System;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SchoolOrganizer.Src.Models.Students;
using SchoolOrganizer.Src.Views.Windows;
using SchoolOrganizer.Src.Views.Windows.ImageCrop;

namespace SchoolOrganizer.Src.Views.ProfileCards
{
    public abstract partial class BaseProfileCard : UserControl
    {
        // Map of Avalonia properties to target control names and fallback strings
        private readonly Dictionary<AvaloniaProperty, (string controlName, string fallback)> _propertyMappings
            = new Dictionary<AvaloniaProperty, (string, string)>();

        public event EventHandler? BackButtonClicked;
        public event EventHandler<IPerson>? CardClicked;
        public event EventHandler<IPerson>? CardDoubleClicked;

        public BaseProfileCard()
        {
            // Register common property -> control mappings
            MapPropertyToControl(NameProperty, "NameText", "Unknown");
            MapPropertyToControl(EmailProperty, "EmailText", "No Email");
            MapPropertyToControl(SecondaryInfoProperty, "SecondaryText", "No Info");
            MapPropertyToControl(EnrollmentDateProperty, "EnrollmentText", "Unknown");
            MapPropertyToControl(IdProperty, "IdText", "Unknown");
            MapPropertyToControl(InitialsProperty, "InitialsText", "??");

            // Ensure we perform setup once the visual tree is ready
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
        {
            SetupEventHandlers();
            UpdateAllControls();
        }

        private void OnUnloaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Clean up event subscription to avoid leaks
            Unloaded -= OnUnloaded;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            // Only update if the visual tree is loaded
            if (IsLoaded)
            {
                UpdateAllControls();
            }
        }


        // Allow derived classes to opt out of hover effects without overriding the entire SetupEventHandlers
        protected virtual bool EnableHover => true;

        protected virtual void UpdateAllControls()
        {
            // Try to get data from DataContext first (direct source)
            if (DataContext is IPerson person)
            {
                // Update from person directly - this ensures immediate display of real data
                UpdateTextBlockFromPerson("NameText", person.Name, "Unknown");
                UpdateTextBlockFromPerson("EmailText", person.Email, "No Email");
                UpdateTextBlockFromPerson("SecondaryText", person.SecondaryInfo, "No Info");
                
                if (this.FindControl<TextBlock>("EnrollmentText") is { } enrollmentText)
                {
                    if (person is Student student)
                        enrollmentText.Text = student.EnrollmentDate.ToString("MMMM d, yyyy");
                    else
                        enrollmentText.Text = "Unknown";
                }
                    
                UpdateTextBlockFromPerson("IdText", person.Id.ToString(), "Unknown");
                
                if (this.FindControl<TextBlock>("InitialsText") is { } initialsText)
                {
                    var name = person.Name ?? "";
                    var initials = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(word => word.Length > 0 ? word[0].ToString().ToUpper() : "")
                        .Take(2);
                    initialsText.Text = string.Join("", initials);
                }
            }
            else
            {
                // Fallback to StyledProperty values when DataContext isn't set
                foreach (var kvp in _propertyMappings)
                {
                    var prop = kvp.Key;
                    var (controlName, fallback) = kvp.Value;
                    var value = GetValue(prop) as string ?? fallback;
                    UpdateTextBlockFromPerson(controlName, value, fallback);
                }
            }
        }

        protected virtual void SetupEventHandlers()
        {
            if (this.FindControl<Button>("BackButton") is { } backButton)
                backButton.Click += (s, e) => BackButtonClicked?.Invoke(this, EventArgs.Empty);

            // ProfileImageBorder event handlers are handled directly in XAML templates
            // No need for programmatic subscription since XAML already has ImageClicked="OnProfileImageClicked"

            if (this.FindControl<Border>("CardBorder") is { } cardBorder)
            {
                // Add click and double-click handlers to CardBorder
                cardBorder.PointerPressed += OnCardPointerPressed;
                cardBorder.DoubleTapped += OnCardDoubleTapped;
                
                // Attach hover handlers only when enabled
                if (EnableHover)
                {
                    cardBorder.PointerEntered += OnCardPointerEntered;
                    cardBorder.PointerExited += OnCardPointerExited;
                }
                else
                {
                    // Ensure no hover state is set on cards that opt out
                    if (this.FindResource("ShadowLight") is BoxShadows normalShadow)
                        cardBorder.BoxShadow = normalShadow;
                    cardBorder.RenderTransform = null;
                }
            }
        }

        protected virtual void OnCardPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (DataContext is IPerson person)
            {
                CardClicked?.Invoke(this, person);
                e.Handled = true; // Prevent event bubbling to background deselection
            }
        }

        protected virtual void OnCardDoubleTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is IPerson person)
            {
                CardDoubleClicked?.Invoke(this, person);
                e.Handled = true; // Prevent event bubbling to background deselection
            }
        }

        protected virtual void OnCardPointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            if (sender is Border cardBorder)
            {
                cardBorder.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                cardBorder.RenderTransform = new ScaleTransform(1.05, 1.05);

                if (this.FindResource("ShadowStrong") is BoxShadows hoverShadow)
                    cardBorder.BoxShadow = hoverShadow;
            }
        }

        protected virtual void OnCardPointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            if (sender is Border cardBorder)
            {
                cardBorder.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                cardBorder.RenderTransform = new ScaleTransform(1.0, 1.0);

                if (this.FindResource("ShadowLight") is BoxShadows normalShadow)
                    cardBorder.BoxShadow = normalShadow;
            }
        }

        // Profile image hover effects are now handled by ProfileImageBorder component

        protected virtual void OnProfileImageClicked(object? sender, EventArgs e)
        {
            if (DataContext is Student student)
            {
                // Use StudentCoordinatorService directly to avoid double event handling
                Services.StudentCoordinatorService.Instance.PublishStudentImageChangeRequested(student);
            }
        }

        protected virtual void OnCardClicked(IPerson person)
        {
            CardClicked?.Invoke(this, person);
        }


        // Helper to map properties to controls so derived classes can register their own mappings
        protected void MapPropertyToControl(AvaloniaProperty property, string controlName, string fallback)
        {
            _propertyMappings[property] = (controlName, fallback);
        }

        // Helper to update TextBlock controls from DataContext person properties
        protected void UpdateTextBlockFromPerson(string controlName, string? value, string fallback = "")
        {
            if (this.FindControl<TextBlock>(controlName) is { } textBlock)
                textBlock.Text = value ?? fallback;
        }

        // Common Properties
        public new string? Name
        {
            get => GetValue(NameProperty);
            set => SetValue(NameProperty, value);
        }
        public new static readonly Avalonia.StyledProperty<string?> NameProperty =
            Avalonia.AvaloniaProperty.Register<BaseProfileCard, string?>("Name");

        public string? Email
        {
            get => GetValue(EmailProperty);
            set => SetValue(EmailProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> EmailProperty =
            Avalonia.AvaloniaProperty.Register<BaseProfileCard, string?>("Email");

        public string? SecondaryInfo
        {
            get => GetValue(SecondaryInfoProperty);
            set => SetValue(SecondaryInfoProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> SecondaryInfoProperty =
            Avalonia.AvaloniaProperty.Register<BaseProfileCard, string?>("SecondaryInfo");

        public string? EnrollmentDate
        {
            get => GetValue(EnrollmentDateProperty);
            set => SetValue(EnrollmentDateProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> EnrollmentDateProperty =
            Avalonia.AvaloniaProperty.Register<BaseProfileCard, string?>("EnrollmentDate");

        public string? Id
        {
            get => GetValue(IdProperty);
            set => SetValue(IdProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> IdProperty =
            Avalonia.AvaloniaProperty.Register<BaseProfileCard, string?>("Id");

        public string? PictureUrl
        {
            get => GetValue(PictureUrlProperty);
            set => SetValue(PictureUrlProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> PictureUrlProperty =
            Avalonia.AvaloniaProperty.Register<BaseProfileCard, string?>("PictureUrl");

        public string? Initials
        {
            get => GetValue(InitialsProperty);
            set => SetValue(InitialsProperty, value);
        }
        public static readonly Avalonia.StyledProperty<string?> InitialsProperty =
            Avalonia.AvaloniaProperty.Register<BaseProfileCard, string?>("Initials");

        protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // Only update controls if the visual tree is initialized
            if (!IsLoaded)
                return;

            if (_propertyMappings.TryGetValue(change.Property, out var mapping))
            {
                var (controlName, fallback) = mapping;
                if (this.FindControl<TextBlock>(controlName) is { } textBlock)
                    textBlock.Text = GetValue(change.Property) as string ?? fallback;
            }

        }


    }
}
