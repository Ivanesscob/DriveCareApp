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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class MyCarSalesPage : Page, INotifyPropertyChanged
    {
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

        public MyCarSalesPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += (_, __) => LoadSales();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
        }

        private void Catalog_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<BuyCarPage>();
        }

        private void AddAd_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<AddCarSalePage>();
        }

        private void LoadSales()
        {
            _allSales.Clear();
            var userId = AppState.CurrentUserId;
            if (userId == Guid.Empty)
            {
                RebuildBrandFilter();
                RebuildModelFilter();
                ApplyFilters();
                return;
            }

            try
            {
                const string sql = @"
SELECT
    cs.RowId AS SaleId,
    cs.Title AS SaleTitle,
    b.Name AS BrandName,
    m.Name AS ModelName,
    c.[Year] AS CarYear,
    clr.Name AS ColorName,
    ISNULL(lastPrice.Price, 0) AS LastPrice,
    cs.PhotoPath
FROM CarSales cs
INNER JOIN UserCarSales ucs ON ucs.CarSaleId = cs.RowId AND ucs.UserId = @p0
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
) lastPrice;";

                var rows = AppConnect.model1.Database.SqlQuery<BuyCarSqlRow>(sql, userId).ToList();
                foreach (var row in rows)
                {
                    var brand = Safe(row.BrandName, "Марка");
                    var model = Safe(row.ModelName, "Модель");
                    var color = Safe(row.ColorName, "Без цвета");
                    var yearPart = row.CarYear.HasValue ? string.Format(" · {0}", row.CarYear.Value) : string.Empty;

                    _allSales.Add(new BuyCarSaleItemVm
                    {
                        SaleId = row.SaleId,
                        SaleTitle = Safe(row.SaleTitle, "Объявление"),
                        Brand = brand,
                        Model = model,
                        Color = color,
                        Year = row.CarYear,
                        Price = row.LastPrice,
                        CarLabel = string.Format("{0} {1}{2} · {3}", brand, model, yearPart, color),
                        PriceLabel = string.Format("{0:0} ₽", row.LastPrice),
                        SellerLabel = Safe(row.SaleTitle, "Объявление"),
                        Photo = ResolveSaleImage(row.PhotoPath)
                    });
                }
            }
            catch
            {
            }

            RebuildBrandFilter();
            RebuildModelFilter();
            ApplyFilters();
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
                    ContainsCi(s.Color, search));
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

        private static ImageSource ResolveSaleImage(string photoPathFromDb)
        {
            var raw = (photoPathFromDb ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return LoadImageOrFallback(null);

            try
            {
                if (File.Exists(raw))
                    return LoadImageOrFallback(raw);

                var downloadedByRaw = PhotoTcpStorageService.DownloadPhotoByName(raw);
                if (!string.IsNullOrWhiteSpace(downloadedByRaw))
                    return LoadImageOrFallback(downloadedByRaw);

                var serverFileName = Path.GetFileName(raw);
                if (string.IsNullOrWhiteSpace(serverFileName))
                    return LoadImageOrFallback(null);

                var downloadedByName = PhotoTcpStorageService.DownloadPhotoByName(serverFileName);
                return LoadImageOrFallback(downloadedByName);
            }
            catch
            {
                return LoadImageOrFallback(null);
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
            var item = fe != null ? fe.DataContext as BuyCarSaleItemVm : null;
            OpenSaleDetails(item);
        }

        private void SaleCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            var fe = sender as FrameworkElement;
            var item = fe != null ? fe.DataContext as BuyCarSaleItemVm : null;
            OpenSaleDetails(item);
            e.Handled = true;
        }

        private static void OpenSaleDetails(BuyCarSaleItemVm item)
        {
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
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
