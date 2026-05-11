using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class ToolsStorePage : Page, INotifyPropertyChanged
    {
        private const string FallbackImagePath = "pack://application:,,,/DriveCare;component/Data/NotPhotoCar.png";
        private string _activeCategory = "Engine";
        private decimal _cartTotal;

        public ObservableCollection<ShopProductVm> VisibleProducts { get; } = new ObservableCollection<ShopProductVm>();
        public ObservableCollection<CartItemVm> CartItems => StoreCartService.Items;
        public string CartTitle => $"Корзина ({StoreCartService.Items.Sum(i => i.Quantity)} шт.)";
        public string CartTotalText => $"Итого: {_cartTotal:0} ₽";

        public ToolsStorePage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += async (_, __) => await LoadCategoryAsync(_activeCategory);
            StoreCartService.Items.CollectionChanged += (_, __) => RecalculateCart();
            RecalculateCart();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
        }

        private async void Category_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var category = btn?.Tag as string;
            if (string.IsNullOrWhiteSpace(category))
                return;

            _activeCategory = category;
            await LoadCategoryAsync(_activeCategory);
        }

        private async Task LoadCategoryAsync(string category)
        {
            var all = BuildMockProducts(category);
            var fallback = LoadImageOrFallback(null);
            VisibleProducts.Clear();
            foreach (var p in all)
            {
                p.Photo = fallback;
                p.IsPhotoLoading = true;
                p.PhotoLoadProgressPercent = 0;
                VisibleProducts.Add(p);
            }

            var semaphore = new SemaphoreSlim(4);
            var tasks = VisibleProducts.Select(async p =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var img = await Task.Run(() => ResolveCategoryImage(category, progress => p.PhotoLoadProgressPercent = progress)) ?? fallback;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        p.Photo = img;
                        p.PhotoLoadProgressPercent = 100;
                        p.IsPhotoLoading = false;
                    });
                }
                catch
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        p.Photo = fallback;
                        p.IsPhotoLoading = false;
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);
        }

        private static List<ShopProductVm> BuildMockProducts(string category)
        {
            var names = new[]
            {
                "Стандарт", "Премиум", "Pro", "Sport", "Comfort", "Eco"
            };
            var result = new List<ShopProductVm>();
            for (var i = 0; i < names.Length; i++)
            {
                var price = 3500 + i * 1200 + (category?.Length ?? 0) * 100;
                result.Add(new ShopProductVm
                {
                    ProductId = Guid.NewGuid(),
                    Category = category,
                    Name = $"{CategoryRu(category)} {names[i]}",
                    Description = $"Категория: {CategoryRu(category)}",
                    Price = price,
                    PriceLabel = $"{price:0} ₽"
                });
            }
            return result;
        }

        private static string CategoryRu(string category)
        {
            switch (category)
            {
                case "Engine": return "Деталь двигателя";
                case "Transmission": return "Деталь трансмиссии";
                case "Body": return "Кузовной элемент";
                case "Tires": return "Шина";
                case "Accessories": return "Аксессуар";
                default: return "Товар";
            }
        }

        private static ImageSource ResolveCategoryImage(string category, Action<double> progress)
        {
            try
            {
                progress?.Invoke(20);
                Thread.Sleep(80);
                var uri = new Uri(CategoryImageUri(category), UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                progress?.Invoke(100);
                return bmp;
            }
            catch
            {
                progress?.Invoke(100);
                return LoadImageOrFallback(null);
            }
        }

        private static string CategoryImageUri(string category)
        {
            switch (category)
            {
                case "Engine": return "pack://application:,,,/DriveCareCore;component/Data/Pics/MainMenu/STO.png";
                case "Transmission": return "pack://application:,,,/DriveCareCore;component/Data/Pics/MainMenu/CarRepair.png";
                case "Body": return "pack://application:,,,/DriveCareCore;component/Data/Pics/MainMenu/SprayGun.png";
                case "Tires": return "pack://application:,,,/DriveCareCore;component/Data/Pics/MainMenu/BuyCar.png";
                case "Accessories": return "pack://application:,,,/DriveCareCore;component/Data/Pics/MainMenu/Bascket.png";
                default: return FallbackImagePath;
            }
        }

        private static ImageSource LoadImageOrFallback(string localPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(localPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch
            {
            }

            try
            {
                var fallback = new BitmapImage();
                fallback.BeginInit();
                fallback.UriSource = new Uri(FallbackImagePath, UriKind.Absolute);
                fallback.CacheOption = BitmapCacheOption.OnLoad;
                fallback.EndInit();
                fallback.Freeze();
                return fallback;
            }
            catch
            {
                return null;
            }
        }

        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.Tag as ShopProductVm;
            if (item == null)
                return;
            StoreCartService.Add(item);
            RecalculateCart();
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.Tag as CartItemVm;
            if (item == null)
                return;
            StoreCartService.RemoveOne(item.ProductId);
            RecalculateCart();
        }

        private void RecalculateCart()
        {
            _cartTotal = StoreCartService.Items.Sum(i => i.LineTotal);
            OnPropertyChanged(nameof(CartItems));
            OnPropertyChanged(nameof(CartTitle));
            OnPropertyChanged(nameof(CartTotalText));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    public sealed class ShopProductVm : INotifyPropertyChanged
    {
        public Guid ProductId { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string PriceLabel { get; set; }

        private ImageSource _photo;
        private bool _isPhotoLoading;
        private double _photoLoadProgressPercent;

        public ImageSource Photo { get => _photo; set { _photo = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Photo))); } }
        public bool IsPhotoLoading { get => _isPhotoLoading; set { _isPhotoLoading = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPhotoLoading))); } }
        public double PhotoLoadProgressPercent { get => _photoLoadProgressPercent; set { _photoLoadProgressPercent = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PhotoLoadProgressPercent))); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public sealed class CartItemVm : INotifyPropertyChanged
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        private int _quantity;
        public int Quantity { get => _quantity; set { _quantity = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LineTotal))); } }
        public decimal LineTotal => Price * Quantity;
        public string DisplayText => $"{Name} × {Quantity} — {LineTotal:0} ₽";
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public static class StoreCartService
    {
        public static ObservableCollection<CartItemVm> Items { get; } = new ObservableCollection<CartItemVm>();

        public static void Add(ShopProductVm product)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == product.ProductId);
            if (item == null)
            {
                Items.Add(new CartItemVm
                {
                    ProductId = product.ProductId,
                    Name = product.Name,
                    Price = product.Price,
                    Quantity = 1
                });
            }
            else
            {
                item.Quantity++;
            }
        }

        public static void RemoveOne(Guid productId)
        {
            var item = Items.FirstOrDefault(i => i.ProductId == productId);
            if (item == null)
                return;
            item.Quantity--;
            if (item.Quantity <= 0)
                Items.Remove(item);
        }
    }
}

