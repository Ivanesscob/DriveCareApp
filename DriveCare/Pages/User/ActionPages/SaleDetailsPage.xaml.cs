using DriveCareCore.Data.BD;
using DriveCareCore.Data.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class SaleDetailsPage : Page, INotifyPropertyChanged
    {
        private const char PhotoListSeparator = '|';
        private const string FallbackImagePath = "pack://application:,,,/DriveCare;component/Data/NotPhotoCar.png";
        private readonly Guid _saleId;
        private readonly List<ImageSource> _photos = new List<ImageSource>();
        private int _photoIndex;
        private string _sellerLogin = "Продавец";
        private string _sellerEmail = string.Empty;
        private string _sellerPhone = string.Empty;

        public string TitleText { get; private set; } = "Объявление";
        public string CarText { get; private set; } = string.Empty;
        public string PriceText { get; private set; } = "0 ₽";
        public string DescriptionText { get; private set; } = "Описание не указано.";
        public string ModelInfoText { get; private set; } = "Характеристики модели не указаны.";
        public string SellerText { get; private set; } = "Продавец: неизвестно";
        public ImageSource CurrentPhoto => _photos.Count == 0 ? LoadFallback() : _photos[_photoIndex];
        public string PhotoPositionText => _photos.Count <= 1 ? "1/1" : $"{_photoIndex + 1}/{_photos.Count}";
        public PlotModel PricePlotModel { get; private set; } = new PlotModel();

        public SaleDetailsPage(Guid saleId)
        {
            _saleId = saleId;
            InitializeComponent();
            DataContext = this;
            Loaded += (_, __) => LoadSale();
        }

        private void LoadSale()
        {
            try
            {
                const string sql = @"
SELECT TOP 1
    cs.Title AS SaleTitle,
    cs.Description AS SaleDescription,
    cs.PhotoPath AS PhotoPath,
    b.Name AS BrandName,
    m.Name AS ModelName,
    m.Description AS ModelDescription,
    c.[Year] AS CarYear,
    ISNULL(lastPrice.Price, 0) AS LastPrice,
    u.Login AS SellerLogin,
    u.Email AS SellerEmail,
    u.Phone AS SellerPhone
FROM CarSales cs
INNER JOIN Cars c ON c.RowId = cs.CarId
LEFT JOIN Models m ON m.RowId = c.ModelId
LEFT JOIN Brands b ON b.RowId = m.BrandId
OUTER APPLY (
    SELECT TOP 1 p.Price
    FROM CarSalePrices p
    WHERE p.CarSaleId = cs.RowId AND p.EndDate IS NULL
    ORDER BY p.StartDate DESC
) lastPrice
OUTER APPLY (
    SELECT TOP 1 u2.Login, u2.Email, u2.Phone
    FROM UserCarSales ucs
    INNER JOIN Users u2 ON u2.RowId = ucs.UserId
    WHERE ucs.CarSaleId = cs.RowId
) u
WHERE cs.RowId = @p0;";

                var row = AppConnect.model1.Database.SqlQuery<SaleDetailsSqlRow>(sql, _saleId).FirstOrDefault();
                if (row == null)
                    return;

                var userId = AppState.CurrentUserId;
                DriveCareCore.Analytics.ActivityTracker.TrackUser(
                    DriveCareCore.Analytics.ActivityEventCodes.CarSaleDetailView,
                    userId == Guid.Empty ? (Guid?)null : userId,
                    entityType: "CarSale",
                    entityId: _saleId);

                var brand = Safe(row.BrandName, "Марка");
                var model = Safe(row.ModelName, "Модель");
                var year = row.CarYear.HasValue ? $" {row.CarYear.Value}" : string.Empty;

                TitleText = Safe(row.SaleTitle, "Объявление");
                CarText = $"{brand} {model}{year}";
                PriceText = $"{row.LastPrice:0} ₽";
                DescriptionText = Safe(row.SaleDescription, "Описание не указано.");
                ModelInfoText = Safe(row.ModelDescription, "Характеристики модели не указаны.");

                _sellerLogin = Safe(row.SellerLogin, "Продавец");
                _sellerEmail = (row.SellerEmail ?? string.Empty).Trim();
                _sellerPhone = (row.SellerPhone ?? string.Empty).Trim();
                SellerText = $"Продавец: {_sellerLogin}";

                _photos.Clear();
                foreach (var token in ParsePhotoTokens(row.PhotoPath))
                {
                    var img = ResolveSaleImage(token);
                    if (img != null)
                        _photos.Add(img);
                }
                if (_photos.Count == 0)
                    _photos.Add(LoadFallback());
                _photoIndex = 0;

                LoadPriceHistoryChart();
                RaiseAll();
            }
            catch
            {
            }
        }

        private void LoadPriceHistoryChart()
        {
            try
            {
                const string sql = @"
SELECT p.StartDate, p.Price
FROM CarSalePrices p
WHERE p.CarSaleId = @p0
ORDER BY p.StartDate ASC;";

                var rows = AppConnect.model1.Database.SqlQuery<SalePricePointSqlRow>(sql, _saleId).ToList();
                var points = new List<DataPoint>();

                foreach (var row in rows)
                {
                    if (!row.StartDate.HasValue)
                        continue;
                    points.Add(DateTimeAxis.CreateDataPoint(row.StartDate.Value, (double)row.Price));
                }

                ApplyPriceChart(points);
            }
            catch
            {
                ApplyPriceChart(new List<DataPoint>());
            }
        }

        private void ApplyPriceChart(List<DataPoint> points)
        {
            if (points == null || points.Count == 0)
                points = new List<DataPoint> { DateTimeAxis.CreateDataPoint(DateTime.Now, 0d) };

            var model = new PlotModel
            {
                Background = OxyColor.FromArgb(0, 0, 0, 0),
                PlotAreaBorderColor = OxyColor.FromRgb(46, 46, 68),
                TextColor = OxyColor.FromRgb(208, 213, 224)
            };

            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Дата",
                StringFormat = "dd.MM",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.None,
                MajorGridlineColor = OxyColor.FromRgb(46, 46, 68),
                TicklineColor = OxyColor.FromRgb(154, 160, 176),
                TextColor = OxyColor.FromRgb(154, 160, 176),
                TitleColor = OxyColor.FromRgb(208, 213, 224)
            });

            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Цена, ₽",
                Minimum = 0,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.None,
                MajorGridlineColor = OxyColor.FromRgb(46, 46, 68),
                TicklineColor = OxyColor.FromRgb(154, 160, 176),
                TextColor = OxyColor.FromRgb(154, 160, 176),
                TitleColor = OxyColor.FromRgb(208, 213, 224)
            });

            var line = new LineSeries
            {
                Title = "Цена",
                Color = OxyColor.FromRgb(124, 108, 245),
                StrokeThickness = 3,
                MarkerType = MarkerType.Circle,
                MarkerSize = 3.5,
                MarkerFill = OxyColor.FromRgb(124, 108, 245)
            };

            foreach (var point in points)
                line.Points.Add(point);

            model.Series.Add(line);
            model.InvalidatePlot(true);

            PricePlotModel = model;
            OnPropertyChanged(nameof(PricePlotModel));
        }

        private void PrevPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_photos.Count <= 1)
                return;

            _photoIndex = (_photoIndex - 1 + _photos.Count) % _photos.Count;
            OnPropertyChanged(nameof(CurrentPhoto));
            OnPropertyChanged(nameof(PhotoPositionText));
        }

        private void NextPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (_photos.Count <= 1)
                return;

            _photoIndex = (_photoIndex + 1) % _photos.Count;
            OnPropertyChanged(nameof(CurrentPhoto));
            OnPropertyChanged(nameof(PhotoPositionText));
        }

        private void WriteSeller_Click(object sender, RoutedEventArgs e)
        {
            var lines = new List<string> { $"Логин: {_sellerLogin}" };
            if (!string.IsNullOrWhiteSpace(_sellerEmail))
                lines.Add($"Email: {_sellerEmail}");
            if (!string.IsNullOrWhiteSpace(_sellerPhone))
                lines.Add($"Телефон: {_sellerPhone}");
            if (lines.Count == 1)
                lines.Add("Контакты не указаны.");

            MessageBox.Show(string.Join(Environment.NewLine, lines), "Контакты продавца", MessageBoxButton.OK, MessageBoxImage.Information);
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

            return LoadFallback();
        }

        private static ImageSource LoadFallback()
        {
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

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static IEnumerable<string> ParsePhotoTokens(string raw)
        {
            var source = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source))
                yield break;

            // Поддержка нескольких форматов хранения: "a|b|c", "a;b;c", "a,b,c", переносы строк и простой XML.
            var xmlMatches = Regex.Matches(source, "<photo>(.*?)</photo>", RegexOptions.IgnoreCase);
            if (xmlMatches.Count > 0)
            {
                foreach (Match match in xmlMatches)
                {
                    var xmlToken = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : string.Empty;
                    if (!string.IsNullOrWhiteSpace(xmlToken))
                        yield return xmlToken;
                }

                yield break;
            }

            foreach (var token in source.Split(new[] { PhotoListSeparator, ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = token.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    yield return trimmed;
            }
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(TitleText));
            OnPropertyChanged(nameof(CarText));
            OnPropertyChanged(nameof(PriceText));
            OnPropertyChanged(nameof(DescriptionText));
            OnPropertyChanged(nameof(ModelInfoText));
            OnPropertyChanged(nameof(SellerText));
            OnPropertyChanged(nameof(CurrentPhoto));
            OnPropertyChanged(nameof(PhotoPositionText));
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<BuyCarPage>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    internal sealed class SaleDetailsSqlRow
    {
        public string SaleTitle { get; set; }
        public string SaleDescription { get; set; }
        public string PhotoPath { get; set; }
        public string BrandName { get; set; }
        public string ModelName { get; set; }
        public string ModelDescription { get; set; }
        public int? CarYear { get; set; }
        public decimal LastPrice { get; set; }
        public string SellerLogin { get; set; }
        public string SellerEmail { get; set; }
        public string SellerPhone { get; set; }
    }

    internal sealed class SalePricePointSqlRow
    {
        public DateTime? StartDate { get; set; }
        public decimal Price { get; set; }
    }
}
