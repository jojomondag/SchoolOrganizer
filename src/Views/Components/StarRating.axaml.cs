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

            // Subscribe to property changes
            this.GetObservable(RatingProperty).Subscribe(OnRatingChanged);

            // Initialize the visual state
            UpdateStars();
        }

        private void OnRatingChanged(int newRating)
        {
            UpdateStars();
        }

        private void OnStarClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                if (int.TryParse(tag, out int starNumber))
                {
                    // If clicking the same star, set rating to 0 (clear rating)
                    Rating = (Rating == starNumber) ? 0 : starNumber;
                    RatingChanged?.Invoke(this, Rating);
                }
            }
        }

        private void UpdateStars()
        {
            for (int i = 0; i < 5; i++)
            {
                if (starIcons[i] != null)
                {
                    if (i < Rating)
                    {
                        starIcons[i].Kind = MaterialIconKind.Star;
                        starIcons[i].Classes.Remove("StarEmpty");
                        starIcons[i].Classes.Add("StarFilled");
                    }
                    else
                    {
                        starIcons[i].Kind = MaterialIconKind.StarOutline;
                        starIcons[i].Classes.Remove("StarFilled");
                        starIcons[i].Classes.Add("StarEmpty");
                    }
                }
            }
        }
    }
}
