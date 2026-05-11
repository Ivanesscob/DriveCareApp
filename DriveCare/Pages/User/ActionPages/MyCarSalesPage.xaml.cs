using DriveCareCore.Data.BD;
using DriveCareCore.Data.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class MyCarSalesPage : Page, INotifyPropertyChanged
    {
        private const char PhotoListSeparator = '|';
        private const string FallbackImagePath = "pack://application:,,,/DriveCare;component/Data/NotPhotoCar.png";
        private const string NoSelectionTitle = "Объявление не выбрано";

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
        private BuyCarSaleItemVm _selectedSale;
        private string _editableDescription = string.Empty;
        private string _editablePriceText = string.Empty;
        private string _selectedSaleTitle = NoSelectionTitle;
        public ObservableCollection<SalePhotoEditItem> EditablePhotos { get; } = new ObservableCollection<SalePhotoEditItem>();

        public string SearchText { get => _searchText; set { _searchText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); } }
        public string MinPriceText { get => _minPriceText; set { _minPriceText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); } }
        public string MaxPriceText { get => _maxPriceText; set { _maxPriceText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); } }
        public string MinYearText { get => _minYearText; set { _minYearText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); } }
        public string MaxYearText { get => _maxYearText; set { _maxYearText = value ?? string.Empty; OnPropertyChanged(); ApplyFilters(); } }
        public string EditableDescription { get => _editableDescription; set { _editableDescription = value ?? string.Empty; OnPropertyChanged(); } }
        public string EditablePriceText { get => _editablePriceText; set { _editablePriceText = value ?? string.Empty; OnPropertyChanged(); } }
        public string SelectedSaleTitle { get => _selectedSaleTitle; set { _selectedSaleTitle = value ?? NoSelectionTitle; OnPropertyChanged(); } }

        public BuyCarSaleItemVm SelectedSale
        {
            get => _selectedSale;
            set
            {
                _selectedSale = value;
                OnPropertyChanged();
                LoadEditorFromSelection();
            }
        }

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
            Loaded += async (_, __) => await LoadSalesAsync();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<BuyCarPage>();
        }

        private void Catalog_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<BuyCarPage>();
        }

        private void AddAd_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<AddCarSalePage>();
        }

        private async Task LoadSalesAsync()
        {
            _allSales.Clear();
            var pendingPhotoItems = new List<(BuyCarSaleItemVm Item, string PhotoPath)>();
            var fallback = LoadImageOrFallback(null);
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
    cs.Description AS SaleDescription,
    b.Name AS BrandName,
    m.Name AS ModelName,
    m.Description AS ModelDescription,
    c.[Year] AS CarYear,
    clr.Name AS ColorName,
    ISNULL(lastPrice.Price, 0) AS LastPrice,
    cs.PhotoPath,
    cs.StatusId AS StatusId
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
                        SaleDescription = row.SaleDescription ?? string.Empty,
                        Brand = brand,
                        Model = model,
                        ModelDescription = row.ModelDescription ?? string.Empty,
                        Color = color,
                        Year = row.CarYear,
                        Price = row.LastPrice,
                        CarLabel = string.Format("{0} {1}{2} · {3}", brand, model, yearPart, color),
                        ModelInfoLabel = string.IsNullOrWhiteSpace(row.ModelDescription) ? "Характеристики модели не указаны" : row.ModelDescription.Trim(),
                        PriceLabel = string.Format("{0:0} ₽", row.LastPrice),
                        SellerLabel = Safe(row.SaleTitle, "Объявление"),
                        ModerationStatus = CarSaleModerationStatuses.FormatModerationStatusDisplay(
                            AppConnect.model1, row.StatusId),
                        Photo = fallback,
                        IsPhotoLoading = true,
                        PhotoLoadProgressPercent = 0,
                        PhotoPathRaw = row.PhotoPath ?? string.Empty
                    });
                    pendingPhotoItems.Add((_allSales[_allSales.Count - 1], row.PhotoPath));
                }
            }
            catch
            {
            }

            RebuildBrandFilter();
            RebuildModelFilter();
            ApplyFilters();
            SelectedSale = SalesItems.FirstOrDefault();
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

            if (SelectedSale == null || !SalesItems.Contains(SelectedSale))
                SelectedSale = SalesItems.FirstOrDefault();
        }

        private void LoadEditorFromSelection()
        {
            EditablePhotos.Clear();
            if (SelectedSale == null)
            {
                EditableDescription = string.Empty;
                EditablePriceText = string.Empty;
                SelectedSaleTitle = NoSelectionTitle;
                return;
            }

            EditableDescription = SelectedSale.SaleDescription ?? string.Empty;
            EditablePriceText = SelectedSale.Price.ToString("0", CultureInfo.InvariantCulture);
            SelectedSaleTitle = SelectedSale.SaleTitle ?? "Объявление";

            foreach (var token in ParsePhotoTokens(SelectedSale.PhotoPathRaw))
            {
                EditablePhotos.Add(new SalePhotoEditItem
                {
                    SourcePath = token,
                    DisplayName = token,
                    IsLocalFile = false
                });
            }
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
                if (File.Exists(primary))
                {
                    progress?.Invoke(100);
                    return LoadImageOrFallback(primary);
                }

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

        private static IEnumerable<string> ParsePhotoTokens(string raw)
        {
            var source = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source))
                yield break;

            foreach (var token in source.Split(new[] { PhotoListSeparator, ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = token.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    yield return trimmed;
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

        private void OpenSaleDetails(BuyCarSaleItemVm item)
        {
            if (item == null || item.SaleId == Guid.Empty)
                return;

            AppState.Navigate(new SaleDetailsPage(item.SaleId));
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

        private void AddEditPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSale == null)
            {
                MessageBox.Show("Сначала выберите объявление.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Выберите фото",
                Filter = "Изображения (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                CheckFileExists = true,
                Multiselect = true
            };

            if (dialog.ShowDialog() != true)
                return;

            foreach (var path in dialog.FileNames.Where(File.Exists))
            {
                if (EditablePhotos.Any(p => p.IsLocalFile && string.Equals(p.SourcePath, path, StringComparison.OrdinalIgnoreCase)))
                    continue;

                EditablePhotos.Add(new SalePhotoEditItem
                {
                    SourcePath = path,
                    DisplayName = Path.GetFileName(path),
                    IsLocalFile = true
                });
            }
        }

        private void RemoveEditPhoto_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as SalePhotoEditItem;
            if (item == null)
                return;

            EditablePhotos.Remove(item);
        }

        private async void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSale == null)
            {
                MessageBox.Show("Выберите объявление для изменения.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseDecimal(EditablePriceText, out var newPrice) || newPrice <= 0)
            {
                MessageBox.Show("Введите корректную цену.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sale = AppConnect.model1.CarSales.FirstOrDefault(s => s.RowId == SelectedSale.SaleId);
            if (sale == null)
            {
                MessageBox.Show("Объявление не найдено.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var now = DateTime.Now;
            sale.Description = (EditableDescription ?? string.Empty).Trim();
            sale.PhotoPath = BuildPhotoPathForSave();

            var returnedId = CarSaleModerationStatuses.ResolveCarSaleStatusIdByName(AppConnect.model1, CarSaleModerationStatuses.ReturnedForCorrection);
            if (returnedId.HasValue && sale.StatusId == returnedId)
            {
                sale.StatusId = CarSaleModerationStatuses.ResolveCarSaleStatusIdByName(AppConnect.model1, CarSaleModerationStatuses.SentForModeration)
                    ?? CarSaleModerationStatuses.ResolveCarSaleStatusIdByName(AppConnect.model1, CarSaleModerationStatuses.AwaitingModeration)
                    ?? CarSaleModerationStatuses.AwaitingModerationStatusGuid;
            }

            var activePrices = AppConnect.model1.CarSalePrices
                .Where(p => p.CarSaleId == SelectedSale.SaleId && p.EndDate == null)
                .ToList();
            foreach (var p in activePrices)
                p.EndDate = now;

            AppConnect.model1.CarSalePrices.Add(new CarSalePrices
            {
                RowId = Guid.NewGuid(),
                CarSaleId = SelectedSale.SaleId,
                Price = newPrice,
                StartDate = now,
                EndDate = null,
                Description = "Обновление цены владельцем"
            });

            try
            {
                AppConnect.model1.SaveChanges();
                MessageBox.Show("Изменения сохранены.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadSalesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить изменения: " + ex.Message, "DriveCare", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteSale_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSale == null)
            {
                MessageBox.Show("Выберите объявление для удаления.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                "Удалить объявление без возможности восстановления?",
                "DriveCare",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;

            var saleId = SelectedSale.SaleId;
            var sale = AppConnect.model1.CarSales.FirstOrDefault(s => s.RowId == saleId);
            if (sale == null)
                return;

            var prices = AppConnect.model1.CarSalePrices.Where(p => p.CarSaleId == saleId).ToList();
            var userLinks = AppConnect.model1.UserCarSales.Where(u => u.CarSaleId == saleId).ToList();

            try
            {
                foreach (var p in prices) AppConnect.model1.CarSalePrices.Remove(p);
                foreach (var u in userLinks) AppConnect.model1.UserCarSales.Remove(u);
                AppConnect.model1.CarSales.Remove(sale);
                AppConnect.model1.SaveChanges();
                MessageBox.Show("Объявление удалено.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadSalesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить объявление: " + ex.Message, "DriveCare", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildPhotoPathForSave()
        {
            var tokens = new List<string>();
            foreach (var photo in EditablePhotos)
            {
                if (photo == null || string.IsNullOrWhiteSpace(photo.SourcePath))
                    continue;

                if (!photo.IsLocalFile)
                {
                    tokens.Add(photo.SourcePath.Trim());
                    continue;
                }

                var uploaded = UploadPhotoToServer(photo.SourcePath);
                if (!string.IsNullOrWhiteSpace(uploaded))
                    tokens.Add(uploaded.Trim());
            }

            return tokens.Count == 0 ? null : string.Join(PhotoListSeparator.ToString(), tokens);
        }

        private static string UploadPhotoToServer(string localPath)
        {
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                return null;

            const string serverIp = "5.35.86.99";
            const int port = 5000;

            try
            {
                using (var client = new TcpClient())
                {
                    client.Connect(serverIp, port);
                    using (var stream = client.GetStream())
                    {
                        var fileName = Path.GetFileName(localPath);
                        var fileData = File.ReadAllBytes(localPath);

                        var commandBytes = Encoding.UTF8.GetBytes("UPLOAD");
                        stream.Write(BitConverter.GetBytes(commandBytes.Length), 0, 4);
                        stream.Write(commandBytes, 0, commandBytes.Length);

                        var nameBytes = Encoding.UTF8.GetBytes(fileName);
                        stream.Write(BitConverter.GetBytes(nameBytes.Length), 0, 4);
                        stream.Write(nameBytes, 0, nameBytes.Length);

                        stream.Write(BitConverter.GetBytes((long)fileData.Length), 0, 8);
                        stream.Write(fileData, 0, fileData.Length);

                        var lenBytes = new byte[4];
                        if (stream.Read(lenBytes, 0, 4) != 4)
                            return null;
                        var len = BitConverter.ToInt32(lenBytes, 0);
                        if (len <= 0)
                            return null;

                        var generatedName = new byte[len];
                        var total = 0;
                        while (total < len)
                        {
                            var read = stream.Read(generatedName, total, len - total);
                            if (read <= 0) break;
                            total += read;
                        }

                        return total <= 0 ? null : Encoding.UTF8.GetString(generatedName, 0, total).Trim();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }

    public sealed class SalePhotoEditItem
    {
        public string SourcePath { get; set; }
        public string DisplayName { get; set; }
        public bool IsLocalFile { get; set; }
    }
}
