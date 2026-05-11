using DriveCareCore.Data.BD;
using DriveCareCore.Data.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
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
    public partial class BuyCarPage : Page, INotifyPropertyChanged
    {
        private const char PhotoListSeparator = '|';
        private const string FallbackImagePath = "pack://application:,,,/DriveCare;component/Data/NotPhotoCar.png";
        public ObservableCollection<BuyCarSaleItemVm> SalesItems { get; } = new ObservableCollection<BuyCarSaleItemVm>();
        public ObservableCollection<string> Brands { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Models { get; } = new ObservableCollection<string>();
        private readonly List<BuyCarSaleItemVm> _allSales = new List<BuyCarSaleItemVm>();

        private string _searchText = string.Empty;
        private string _minPriceText = string.Empty;
        private string _maxPriceText = string.Empty;
        private string _selectedBrand = "Все";
        private string _selectedModel = "Все";
        private string _minYearText = string.Empty;
        private string _maxYearText = string.Empty;

        public string SearchText { get => _searchText; set { _searchText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); } }
        public string MinPriceText { get => _minPriceText; set { _minPriceText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); } }
        public string MaxPriceText { get => _maxPriceText; set { _maxPriceText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); } }
        public string MinYearText { get => _minYearText; set { _minYearText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); } }
        public string MaxYearText { get => _maxYearText; set { _maxYearText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); } }

        public string SelectedBrand
        {
            get => _selectedBrand;
            set
            {
                _selectedBrand = string.IsNullOrWhiteSpace(value) ? "Все" : value;
                OnPropertyChanged();
                RebuildModelFilter();
                ApplyFilters();
            }
        }

        public string SelectedModel
        {
            get => _selectedModel;
            set
            {
                _selectedModel = string.IsNullOrWhiteSpace(value) ? "Все" : value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public BuyCarPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += async (_, __) => await LoadSalesAsync();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
        }

        private async Task LoadSalesAsync()
        {
            _allSales.Clear();
            var pendingPhotoItems = new List<(BuyCarSaleItemVm Item, string PhotoPath)>();
            var fallback = LoadImageOrFallback(null);
            try
            {
                var db = AppConnect.model1;
                var approvedId = CarSaleModerationStatuses.ResolveCarSaleStatusIdByName(db, CarSaleModerationStatuses.ApprovedModeration);
                const string sql = @"
SELECT
    cs.RowId AS SaleId,
    cs.Title AS SaleTitle,
    b.Name AS BrandName,
    m.Name AS ModelName,
    m.Description AS ModelDescription,
    c.[Year] AS CarYear,
    clr.Name AS ColorName,
    ISNULL(lastPrice.Price, 0) AS LastPrice,
    cs.PhotoPath
FROM CarSales cs
INNER JOIN Cars c ON c.RowId = cs.CarId
LEFT JOIN Models m ON m.RowId = c.ModelId
LEFT JOIN Brands b ON b.RowId = m.BrandId

OUTER APPLY (
    SELECT TOP 1 col.Name
    FROM CarColors cc
    LEFT JOIN Colors col ON col.RowId = cc.ColorId
    WHERE cc.CarId = c.RowId
    ORDER BY cc.StartDate DESC
) clr

OUTER APPLY (
    SELECT TOP 1 p.Price
    FROM CarSalePrices p
    WHERE p.CarSaleId = cs.RowId
      AND p.EndDate IS NULL
    ORDER BY p.StartDate DESC
) lastPrice
WHERE cs.StatusId = @p0";

                var rows = approvedId.HasValue
                    ? db.Database.SqlQuery<BuyCarSqlRow>(sql, approvedId.Value).ToList()
                    : new List<BuyCarSqlRow>();
                foreach (var row in rows)
                {
                    var brand = Safe(row.BrandName, "Марка");
                    var model = Safe(row.ModelName, "Модель");
                    var color = Safe(row.ColorName, "Без цвета");
                    var yearPart = row.CarYear.HasValue ? $" · {row.CarYear.Value}" : string.Empty;

                    var item = new BuyCarSaleItemVm
                    {
                        SaleId = row.SaleId,
                        SaleTitle = Safe(row.SaleTitle, "Объявление"),
                        Brand = brand,
                        Model = model,
                        ModelDescription = row.ModelDescription ?? string.Empty,
                        Color = color,
                        Year = row.CarYear,
                        Price = row.LastPrice,
                        CarLabel = $"{brand} {model}{yearPart} · {color}",
                        ModelInfoLabel = string.IsNullOrWhiteSpace(row.ModelDescription) ? "Характеристики модели не указаны" : row.ModelDescription.Trim(),
                        PriceLabel = $"{row.LastPrice:0} ₽",
                        SellerLabel = Safe(row.SaleTitle, "Объявление"),
                        Photo = fallback,
                        IsPhotoLoading = true,
                        PhotoLoadProgressPercent = 0
                    };

                    _allSales.Add(item);
                    pendingPhotoItems.Add((item, row.PhotoPath));
                }
            }
            catch
            {
                // Если таблицы/поля пока не совпадают с БД, просто оставляем пустой список.
            }

            RebuildBrandFilter();
            RebuildModelFilter();
            ApplyFilters();

            await LoadPhotosInBackgroundAsync(pendingPhotoItems, fallback);
        }

        private async Task LoadPhotosInBackgroundAsync(List<(BuyCarSaleItemVm Item, string PhotoPath)> pendingPhotoItems, ImageSource fallback)
        {
            if (pendingPhotoItems == null || pendingPhotoItems.Count == 0)
                return;

            var semaphore = new SemaphoreSlim(4);
            var tasks = pendingPhotoItems.Select(async tuple =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var image = await Task.Run(() => ResolveSaleImage(tuple.PhotoPath, progress =>
                    {
                        _ = Dispatcher.InvokeAsync(() => tuple.Item.PhotoLoadProgressPercent = progress);
                    }));
                    var resolved = image ?? fallback;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        tuple.Item.Photo = resolved;
                        tuple.Item.PhotoLoadProgressPercent = 100;
                        tuple.Item.IsPhotoLoading = false;
                    });
                }
                catch
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        tuple.Item.Photo = fallback;
                        tuple.Item.IsPhotoLoading = false;
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);
        }

        private void RebuildBrandFilter()
        {
            var current = SelectedBrand;
            Brands.Clear();
            Brands.Add("Все");
            foreach (var b in _allSales.Select(s => s.Brand).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().OrderBy(v => v))
                Brands.Add(b);
            SelectedBrand = Brands.Contains(current) ? current : "Все";
        }

        private void RebuildModelFilter()
        {
            var current = SelectedModel;
            Models.Clear();
            Models.Add("Все");

            var baseQuery = _allSales.AsEnumerable();
            if (!string.Equals(SelectedBrand, "Все", StringComparison.OrdinalIgnoreCase))
                baseQuery = baseQuery.Where(s => string.Equals(s.Brand, SelectedBrand, StringComparison.OrdinalIgnoreCase));

            foreach (var m in baseQuery.Select(s => s.Model).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().OrderBy(v => v))
                Models.Add(m);

            SelectedModel = Models.Contains(current) ? current : "Все";
        }

        private void ApplyFilters()
        {
            IEnumerable<BuyCarSaleItemVm> query = _allSales;
            var search = (SearchText ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                query = query.Where(s =>
                    ContainsCi(s.CarLabel, search) ||
                    ContainsCi(s.SaleTitle, search) ||
                    ContainsCi(s.Brand, search) ||
                    ContainsCi(s.Model, search) ||
                    ContainsCi(s.Color, search) ||
                    ContainsCi(s.ModelDescription, search));
            }

            if (!string.Equals(SelectedBrand, "Все", StringComparison.OrdinalIgnoreCase))
                query = query.Where(s => string.Equals(s.Brand, SelectedBrand, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(SelectedModel, "Все", StringComparison.OrdinalIgnoreCase))
                query = query.Where(s => string.Equals(s.Model, SelectedModel, StringComparison.OrdinalIgnoreCase));

            if (TryParseDecimal(MinPriceText, out var minPrice))
                query = query.Where(s => s.Price >= minPrice);
            if (TryParseDecimal(MaxPriceText, out var maxPrice))
                query = query.Where(s => s.Price <= maxPrice);

            if (int.TryParse((MinYearText ?? string.Empty).Trim(), out var minYear))
                query = query.Where(s => !s.Year.HasValue || s.Year.Value >= minYear);
            if (int.TryParse((MaxYearText ?? string.Empty).Trim(), out var maxYear))
                query = query.Where(s => !s.Year.HasValue || s.Year.Value <= maxYear);

            SalesItems.Clear();
            foreach (var item in query)
                SalesItems.Add(item);
        }

        private static bool TryParseDecimal(string text, out decimal value)
        {
            var t = (text ?? string.Empty).Trim();
            return decimal.TryParse(t, NumberStyles.Any, CultureInfo.CurrentCulture, out value)
                || decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static bool ContainsCi(string source, string part)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static ImageSource ResolveSaleImage(string photoPathFromDb, Action<double> progress)
        {
            var raw = (photoPathFromDb ?? string.Empty).Trim();
            var primary = GetPrimaryPhotoToken(raw);
            if (string.IsNullOrWhiteSpace(raw))
            {
                progress?.Invoke(100);
                return LoadImageOrFallback(null);
            }

            try
            {
                // Если в БД уже лежит локальный путь и файл есть — используем его.
                if (File.Exists(primary))
                {
                    progress?.Invoke(100);
                    return LoadImageOrFallback(primary);
                }

                // Сначала пробуем как "имя на сервере" (как в вашем GET-методе).
                var downloadedByRaw = PhotoTcpStorageService.DownloadPhotoByName(primary, (read, total) =>
                {
                    if (total <= 0) return;
                    progress?.Invoke(Math.Min(100, read * 100.0 / total));
                });
                if (!string.IsNullOrWhiteSpace(downloadedByRaw))
                {
                    progress?.Invoke(100);
                    return LoadImageOrFallback(downloadedByRaw);
                }

                // Если в БД попал путь, а не имя — fallback на имя файла.
                var serverFileName = Path.GetFileName(primary);
                if (string.IsNullOrWhiteSpace(serverFileName))
                {
                    progress?.Invoke(100);
                    return LoadImageOrFallback(null);
                }

                var downloadedByName = PhotoTcpStorageService.DownloadPhotoByName(serverFileName, (read, total) =>
                {
                    if (total <= 0) return;
                    progress?.Invoke(Math.Min(100, read * 100.0 / total));
                });
                progress?.Invoke(100);
                return LoadImageOrFallback(downloadedByName);
            }
            catch
            {
                progress?.Invoke(100);
                return LoadImageOrFallback(null);
            }
        }

        private static string GetPrimaryPhotoToken(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            var first = raw
                .Split(new[] { PhotoListSeparator }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(first) ? raw : first;
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
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
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

        private void SaleDetails_Click(object sender, RoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            var item = fe?.DataContext as BuyCarSaleItemVm;
            OpenSaleDetails(item);
        }

        private void SaleCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            var fe = sender as FrameworkElement;
            var item = fe?.DataContext as BuyCarSaleItemVm;
            OpenSaleDetails(item);
            e.Handled = true;
        }

        private void OpenSaleDetails(BuyCarSaleItemVm item)
        {
            if (item == null || item.SaleId == Guid.Empty)
                return;

            AppState.Navigate(new SaleDetailsPage(item.SaleId));
        }

        private void MyAds_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<MyCarSalesPage>();
        }

        private void AddAd_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<AddCarSalePage>();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            SearchText = string.Empty;
            MinPriceText = string.Empty;
            MaxPriceText = string.Empty;
            MinYearText = string.Empty;
            MaxYearText = string.Empty;
            SelectedBrand = "Все";
            SelectedModel = "Все";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public sealed class BuyCarSaleItemVm : INotifyPropertyChanged
    {
        public Guid SaleId { get; set; }
        public string SaleTitle { get; set; }
        public string SaleDescription { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string ModelDescription { get; set; }
        public string Color { get; set; }
        public int? Year { get; set; }
        public decimal Price { get; set; }
        public string CarLabel { get; set; }
        public string ModelInfoLabel { get; set; }
        public string PriceLabel { get; set; }
        public string SellerLabel { get; set; }
        /// <summary>Текст статуса модерации (для «Мои объявления»).</summary>
        public string ModerationStatus { get; set; }
        public string PhotoPathRaw { get; set; }
        private bool _isPhotoLoading;
        private double _photoLoadProgressPercent;
        private ImageSource _photo;
        public bool IsPhotoLoading
        {
            get => _isPhotoLoading;
            set
            {
                if (_isPhotoLoading == value)
                    return;
                _isPhotoLoading = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPhotoLoading)));
            }
        }
        public double PhotoLoadProgressPercent
        {
            get => _photoLoadProgressPercent;
            set
            {
                if (Math.Abs(_photoLoadProgressPercent - value) < 0.01)
                    return;
                _photoLoadProgressPercent = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PhotoLoadProgressPercent)));
            }
        }
        public ImageSource Photo
        {
            get => _photo;
            set
            {
                if (Equals(_photo, value))
                    return;
                _photo = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Photo)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    internal sealed class BuyCarSqlRow
    {
        public Guid SaleId { get; set; }
        public string SaleTitle { get; set; }
        public string SaleDescription { get; set; }
        public string BrandName { get; set; }
        public string ModelName { get; set; }
        public string ModelDescription { get; set; }
        public string ColorName { get; set; }
        public int? CarYear { get; set; }
        public decimal LastPrice { get; set; }
        public string PhotoPath { get; set; }
        public Guid? StatusId { get; set; }
    }
}

