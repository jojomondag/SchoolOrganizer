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
            UpdateProfileImageBorder();
            UpdateProfileImage();
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
                // Defer image loading to improve startup performance
                Dispatcher.UIThread.Post(() => UpdateProfileImage(), DispatcherPriority.Background);
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
                if (this.FindControl<TextBlock>("NameText") is { } nameText)
                    nameText.Text = person.Name ?? "Unknown";
                    
                if (this.FindControl<TextBlock>("EmailText") is { } emailText)
                    emailText.Text = person.Email ?? "No Email";
                    
                if (this.FindControl<TextBlock>("SecondaryText") is { } secondaryText)
                    secondaryText.Text = person.SecondaryInfo ?? "No Info";
                    
                if (this.FindControl<TextBlock>("EnrollmentText") is { } enrollmentText)
                {
                    if (person is Student student)
                        enrollmentText.Text = student.EnrollmentDate.ToString("MMMM d, yyyy");
                    else
                        enrollmentText.Text = "Unknown";
                }
                    
                if (this.FindControl<TextBlock>("IdText") is { } idText)
                    idText.Text = person.Id.ToString();
                    
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
                    if (this.FindControl<TextBlock>(controlName) is { } textBlock)
                    {
                        var value = GetValue(prop) as string ?? fallback;
                        textBlock.Text = value;
                    }
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

        private bool _imageLoaded = false;
        private string? _lastPictureUrl = null;

        private async void UpdateProfileImage()
        {
            try
            {
                var pictureUrl = PictureUrl;
                
                // Skip loading for empty or invalid paths
                if (string.IsNullOrEmpty(pictureUrl) || pictureUrl == "ADD_CARD" || pictureUrl == "null")
                {
                    return;
                }

                // Skip if already loaded the same image
                if (_imageLoaded && _lastPictureUrl == pictureUrl)
                {
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"BaseProfileCard.UpdateProfileImage called with PictureUrl: {pictureUrl}");
                
                if (this.FindControl<Ellipse>("ProfileImageEllipse") is { } ellipse)
                {
                    // Use a shared static converter instance to leverage caching
                    var converter = SchoolOrganizer.Views.Converters.UniversalImageConverter.SharedInstance;
                    
                    // Load image asynchronously to prevent UI blocking
                    await Task.Run(() =>
                    {
                        try
                        {
                            var bitmap = converter.Convert(pictureUrl, typeof(Avalonia.Media.Imaging.Bitmap), null, System.Globalization.CultureInfo.CurrentCulture);

                            if (bitmap is Avalonia.Media.Imaging.Bitmap bmp)
                            {
                                // Update UI on UI thread
                                Dispatcher.UIThread.Post(() =>
                                {
                                    try
                                    {
                                        if (this.FindControl<Ellipse>("ProfileImageEllipse") is { } currentEllipse)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"BaseProfileCard: Successfully loaded bitmap, setting as Fill");
                                            currentEllipse.Fill = new ImageBrush(bmp) { Stretch = Avalonia.Media.Stretch.UniformToFill };
                                            _imageLoaded = true;
                                            _lastPictureUrl = pictureUrl;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"BaseProfileCard: Error setting image on UI thread: {ex.Message}");
                                    }
                                });
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"BaseProfileCard: Failed to load bitmap from {pictureUrl}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"BaseProfileCard: Error loading image in background: {ex.Message}");
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"BaseProfileCard: ProfileImageEllipse not found");
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

                    // Use StudentCoordinatorService to publish the image update
                    Services.StudentCoordinatorService.Instance.PublishStudentImageUpdated(student, result.imagePath, result.cropSettings, result.originalImagePath);

                    System.Diagnostics.Debug.WriteLine($"BaseProfileCard: StudentCoordinatorService.PublishStudentImageUpdated called successfully");
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
