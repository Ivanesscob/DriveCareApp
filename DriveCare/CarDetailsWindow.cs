using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DriveCare.Windows
{
    /// <summary>
    /// Заглушка окна для карточки конкретной машины.
    /// Ты потом допишешь, какую именно инфу показывать по CarId/UserCarId.
    /// </summary>
    public sealed class CarDetailsWindow : Window
    {
        public CarDisplayItem Item { get; }

        public CarDetailsWindow(CarDisplayItem item)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));

            Title = "Автомобиль";
            Width = 560;
            Height = 440;
            MinWidth = 420;
            MinHeight = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var rootBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 31)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(61, 61, 85)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 40,
                    ShadowDepth = 0,
                    Opacity = 0.5,
                    Color = Colors.Black
                }
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 12) };
            header.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(Item.Name) ? "Автомобиль" : Item.Name,
                Foreground = Brushes.White,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold
            });
            header.Children.Add(new TextBlock
            {
                Text = "Скоро добавится нужная инфа (по CarId/UserCarId).",
                Foreground = new SolidColorBrush(Color.FromRgb(154, 160, 176)),
                FontSize = 13,
                Margin = new Thickness(0, 6, 0, 0)
            });
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var imageBorder = new Border
            {
                Width = 220,
                Height = 220,
                Background = new SolidColorBrush(Color.FromRgb(15, 15, 24)),
                Margin = new Thickness(0, 0, 14, 0),
                ClipToBounds = true
            };
            imageBorder.Child = new Image
            {
                Source = Item.Photo,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(16)
            };

            Grid.SetColumn(imageBorder, 0);
            contentGrid.Children.Add(imageBorder);

            var meta = new StackPanel { Orientation = Orientation.Vertical };
            meta.Children.Add(new TextBlock
            {
                Text = $"CarId: {Item.CarId}",
                Foreground = new SolidColorBrush(Color.FromRgb(154, 160, 176)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            });
            meta.Children.Add(new TextBlock
            {
                Text = $"UserCarId: {Item.UserCarId}",
                Foreground = new SolidColorBrush(Color.FromRgb(154, 160, 176)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 18)
            });

            meta.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 40)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 68)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Child = new TextBlock
                {
                    Text = "Данные будут добавлены позже.",
                    Foreground = new SolidColorBrush(Color.FromRgb(208, 213, 224)),
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap
                }
            });

            Grid.SetColumn(meta, 1);
            contentGrid.Children.Add(meta);

            Grid.SetRow(contentGrid, 1);
            grid.Children.Add(contentGrid);

            var footer = new TextBlock
            {
                Text = "Закрой окно и продолжай.",
                Foreground = new SolidColorBrush(Color.FromRgb(74, 80, 96)),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            rootBorder.Child = grid;
            Content = rootBorder;
        }
    }
}

