using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia;
using Avalonia.Animation;
using System;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using SchoolOrganizer.Models;
using SchoolOrganizer.Views.Windows;
using SchoolOrganizer.Views.Windows.ImageCrop;

namespace SchoolOrganizer.Views.ProfileCards
{
    public abstract partial class BaseProfileCard : UserControl
    {
        // Map of Avalonia properties to target control names and fallback strings
        private readonly Dictionary<AvaloniaProperty, (string controlName, string fallback)> _propertyMappings
            = new Dictionary<AvaloniaProperty, (string, string)>();

        public event EventHandler? BackButtonClicked;
        public event EventHandler<Student>? ProfileImageClicked;
        public event EventHandler<(Student student, string imagePath)>? ProfileImageUpdated;

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
        }

        private void OnLoaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Unsubscribe so this runs only once per load
            Loaded -= OnLoaded;
            SetupEventHandlers();
            UpdateAllControls();
            UpdateProfileImageBorder();
            UpdateProfileImage();
        }

        private void OnUnloaded(object? s, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Clean up event subscription to avoid leaks
            Unloaded -= OnUnloaded;
        }

        // Allow derived classes to opt out of hover effects without overriding the entire SetupEventHandlers
        protected virtual bool EnableHover => true;

        protected virtual void UpdateAllControls()
        {
            // Iterate mappings and update target TextBlocks
            foreach (var kvp in _propertyMappings)
            {
                var prop = kvp.Key;
                var (controlName, fallback) = kvp.Value;
                if (this.FindControl<TextBlock>(controlName) is { } textBlock)
                {
                    var value = GetValue(prop) as string ?? fallback;
                    textBlock.Text = value;
                }
            }
        }

        protected virtual void SetupEventHandlers()
        {
            if (this.FindControl<Button>("BackButton") is { } backButton)
                backButton.Click += (s, e) => BackButtonClicked?.Invoke(this, EventArgs.Empty);

            if (this.FindControl<Border>("ProfileImageBorder") is { } profileImageBorder)
            {
                profileImageBorder.PointerPressed += (s, e) =>
                {
                    if (DataContext is Student student)
                    {
                        ProfileImageClicked?.Invoke(this, student);
                        e.Handled = true; // Prevent event bubbling
                    }
                };

                // Always add hover effects for profile image, regardless of card hover setting
                profileImageBorder.PointerEntered += OnProfileImagePointerEntered;
                profileImageBorder.PointerExited += OnProfileImagePointerExited;
            }

            if (this.FindControl<Border>("CardBorder") is { } cardBorder)
            {
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

        protected virtual void OnProfileImagePointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            if (sender is Border profileImageBorder)
            {
                // Create a stronger shadow for profile image hover
                var shadow = new BoxShadows(
                    new BoxShadow
                    {
                        Color = Color.FromArgb(120, 0, 0, 0), // Much more opaque shadow
                        Blur = 20,
                        OffsetX = 0,
                        OffsetY = 8,
                        Spread = 2
                    }
                );
                
                // Add smooth transition
                var transition = new Transitions
                {
                    new BoxShadowsTransition
                    {
                        Property = Border.BoxShadowProperty,
                        Duration = TimeSpan.FromMilliseconds(200),
                        Easing = new Avalonia.Animation.Easings.CubicEaseOut()
                    }
                };
                
                profileImageBorder.Transitions = transition;
                profileImageBorder.BoxShadow = shadow;
            }
        }

        protected virtual void OnProfileImagePointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            if (sender is Border profileImageBorder)
            {
                // Add smooth transition for exit
                var transition = new Transitions
                {
                    new BoxShadowsTransition
                    {
                        Property = Border.BoxShadowProperty,
                        Duration = TimeSpan.FromMilliseconds(200),
                        Easing = new Avalonia.Animation.Easings.CubicEaseOut()
                    }
                };
                
                profileImageBorder.Transitions = transition;
                profileImageBorder.BoxShadow = new BoxShadows(); // Transparent shadow
            }
        }

        private void UpdateProfileImageBorder()
        {
            try
            {
                if (this.FindControl<Avalonia.Controls.Shapes.Ellipse>("ProfileImageEllipse") is { } profileImageEllipse)
                {
                    // Always enforce a black border
                    if (this.FindResource("BlackColor") is IBrush blackBrush)
                    {
                        profileImageEllipse.Stroke = blackBrush;
                        return;
                    }
                }
            }
            catch
            {
                // Swallow exceptions here to avoid crashing UI on resource lookup issues
            }
        }

        // Helper to map properties to controls so derived classes can register their own mappings
        protected void MapPropertyToControl(AvaloniaProperty property, string controlName, string fallback)
        {
            _propertyMappings[property] = (controlName, fallback);
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

            // Handle PictureUrl changes specially for the image
            if (change.Property == PictureUrlProperty)
            {
                UpdateProfileImage();
            }
        }

        private void UpdateProfileImage()
        {
            try
            {
                if (this.FindControl<Ellipse>("ProfileImageEllipse") is { } ellipse)
                {
                    var pictureUrl = PictureUrl;

                    if (!string.IsNullOrEmpty(pictureUrl))
                    {
                        // Use UniversalImageConverter to load the image
                        var converter = new SchoolOrganizer.Views.Converters.UniversalImageConverter();
                        var bitmap = converter.Convert(pictureUrl, typeof(Avalonia.Media.Imaging.Bitmap), null, System.Globalization.CultureInfo.CurrentCulture);

                        if (bitmap is Avalonia.Media.Imaging.Bitmap bmp)
                        {
                            ellipse.Fill = new ImageBrush(bmp) { Stretch = Avalonia.Media.Stretch.UniformToFill };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BaseProfileCard: Error updating profile image: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens the ImageCropper window for editing the profile image.
        /// </summary>
        protected virtual async Task OpenImageCropperAsync(Student student)
        {
            try
            {
                // Get the parent window
                var parentWindow = TopLevel.GetTopLevel(this) as Window;
                if (parentWindow == null)
                {
                    System.Diagnostics.Debug.WriteLine("BaseProfileCard: Could not find parent window");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"BaseProfileCard: Opening ImageCropper for student: {student.Name} (ID: {student.Id})");

                // Open the ImageCrop window with student context, passing existing ORIGINAL image and crop settings
                var result = await ImageCropWindow.ShowForStudentAsync(
                    parentWindow,
                    student.Id,
                    student.OriginalImagePath,  // Load the original, not the cropped result
                    student.CropSettings);

                System.Diagnostics.Debug.WriteLine($"BaseProfileCard: ImageCropper returned: imagePath={result.imagePath ?? "NULL"}, cropSettings={result.cropSettings ?? "NULL"}, original={result.originalImagePath ?? "NULL"}");

                if (!string.IsNullOrEmpty(result.imagePath))
                {
                    System.Diagnostics.Debug.WriteLine($"BaseProfileCard: Image saved to: {result.imagePath}");
                    System.Diagnostics.Debug.WriteLine($"BaseProfileCard: Raising ProfileImageUpdated event for student {student.Id}");

                    // Update student with new image path, crop settings, and original image path before raising event
                    student.PictureUrl = result.imagePath;
                    student.CropSettings = result.cropSettings;
                    student.OriginalImagePath = result.originalImagePath;

                    // Raise event to notify that student data changed with the new image path
                    ProfileImageUpdated?.Invoke(this, (student, result.imagePath));

                    System.Diagnostics.Debug.WriteLine($"BaseProfileCard: ProfileImageUpdated event raised successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("BaseProfileCard: ImageCropper closed without saving (result was null or empty)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BaseProfileCard: Error opening ImageCropper: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"BaseProfileCard: Stack trace: {ex.StackTrace}");
            }
        }
    }
}
