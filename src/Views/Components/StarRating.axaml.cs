using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Material.Icons;
using Material.Icons.Avalonia;
using System;

namespace SchoolOrganizer.Src.Views.Components
{
    public partial class StarRating : UserControl
    {
        public static readonly StyledProperty<int> RatingProperty =
            AvaloniaProperty.Register<StarRating, int>(nameof(Rating), defaultValue: 0);

        public int Rating
        {
            get => GetValue(RatingProperty);
            set => SetValue(RatingProperty, value);
        }

    public event EventHandler<int>? RatingChanged;

    private MaterialIcon[] starIcons = new MaterialIcon[5];
    private bool _isUpdatingFromUser = false;

    public StarRating()
    {
        InitializeComponent();
        this.AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // Get references to the star icons
        starIcons[0] = this.FindControl<MaterialIcon>("Star1Icon")!;
        starIcons[1] = this.FindControl<MaterialIcon>("Star2Icon")!;
        starIcons[2] = this.FindControl<MaterialIcon>("Star3Icon")!;
        starIcons[3] = this.FindControl<MaterialIcon>("Star4Icon")!;
        starIcons[4] = this.FindControl<MaterialIcon>("Star5Icon")!;

        // Debug: Check if all icons were found
        for (int i = 0; i < 5; i++)
        {
            System.Diagnostics.Debug.WriteLine($"Star icon {i}: {(starIcons[i] != null ? "Found" : "NULL")}");
        }

        // Subscribe to property changes
        this.GetObservable(RatingProperty).Subscribe(OnRatingChanged);

        // Initialize the visual state
        UpdateStars();
    }

    private void OnRatingChanged(int newRating)
    {
        try
        {
            // Prevent feedback loop from binding - only update stars when the change comes from binding, not from user click
            if (_isUpdatingFromUser)
            {
                return;
            }

            UpdateStars();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnRatingChanged: {ex.Message}");
        }
    }

    private void OnStarClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Prevent event from bubbling to parent controls (e.g., assignment header button)
            e.Handled = true;
            
            if (sender is Button button && button.Tag is string tag)
            {
                if (int.TryParse(tag, out int starNumber))
                {
                    // Prevent binding feedback loop
                    _isUpdatingFromUser = true;
                    
                    // If clicking the same star, set rating to 0 (clear rating)
                    int newRating = (Rating == starNumber) ? 0 : starNumber;
                    
                    System.Diagnostics.Debug.WriteLine($"OnStarClick: Clicked star {starNumber}, Current Rating: {Rating}, New Rating: {newRating}");
                    
                    Rating = newRating;
                    
                    // Update UI immediately with the new rating value
                    UpdateStars(newRating);
                    
                    _isUpdatingFromUser = false;
                    
                    // Notify listeners (event handler will persist to database)
                    RatingChanged?.Invoke(this, newRating);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in OnStarClick: {ex.Message}\n{ex.StackTrace}");
            _isUpdatingFromUser = false;
            throw; // Re-throw to see the actual crash
        }
    }

    private void UpdateStars(int? ratingValue = null)
    {
        try
        {
            // Use provided rating value or fall back to the property value
            int currentRating = ratingValue ?? Rating;
            
            System.Diagnostics.Debug.WriteLine($"=== UpdateStars called: Rating = {currentRating} ===");
            
            // Check if starIcons array is initialized
            if (starIcons == null)
            {
                System.Diagnostics.Debug.WriteLine("UpdateStars: starIcons array is null");
                return;
            }

            for (int i = 0; i < 5; i++)
            {
                System.Diagnostics.Debug.WriteLine($"Processing star {i}: Icon is {(starIcons[i] != null ? "not null" : "NULL")}");
                
                if (starIcons[i] != null)
                {
                    // Fill stars from 0 up to (rating - 1)
                    // e.g., if rating is 3, fill stars 0, 1, 2
                    if (i < currentRating)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Star {i}: FILLING (i={i} < currentRating={currentRating})");
                        System.Diagnostics.Debug.WriteLine($"    Before: Kind={starIcons[i].Kind}, Classes={string.Join(", ", starIcons[i].Classes)}");
                        
                        // Use the correct icon kind for filled star
                        starIcons[i].Kind = MaterialIconKind.Star;
                        
                        // Update classes for styling - use direct color instead of resource
                        starIcons[i].Classes.Remove("StarEmpty");
                        starIcons[i].Classes.Remove("StarIcon");
                        starIcons[i].Classes.Add("StarIcon");
                        starIcons[i].Classes.Add("StarFilled");
                        
                        System.Diagnostics.Debug.WriteLine($"    After: Kind={starIcons[i].Kind}, Classes={string.Join(", ", starIcons[i].Classes)}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  Star {i}: EMPTYING (i={i} >= currentRating={currentRating})");
                        System.Diagnostics.Debug.WriteLine($"    Before: Kind={starIcons[i].Kind}, Classes={string.Join(", ", starIcons[i].Classes)}");
                        
                        // Use the correct icon kind for outline star
                        starIcons[i].Kind = MaterialIconKind.StarBorder;
                        
                        // Update classes for styling
                        starIcons[i].Classes.Remove("StarFilled");
                        starIcons[i].Classes.Remove("StarIcon");
                        starIcons[i].Classes.Add("StarIcon");
                        starIcons[i].Classes.Add("StarEmpty");
                        
                        System.Diagnostics.Debug.WriteLine($"    After: Kind={starIcons[i].Kind}, Classes={string.Join(", ", starIcons[i].Classes)}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  Star {i}: SKIPPED (icon is null)");
                }
            }
            System.Diagnostics.Debug.WriteLine("=== UpdateStars complete ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in UpdateStars: {ex.Message}\n{ex.StackTrace}");
        }
    }
    }
}
