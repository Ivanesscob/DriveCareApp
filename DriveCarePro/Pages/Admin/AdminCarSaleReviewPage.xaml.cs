using DriveCareCore.Data.BD;
using DriveCareCore.Data.Services;
using DriveCarePro;
using DriveCarePro.Pages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminCarSaleReviewPage : Page, INotifyPropertyChanged
    {
        private readonly Guid _saleId;
        private readonly List<ImageSource> _photos = new List<ImageSource>();
        private int _photoIndex;

        public string TitleText { get; private set; } = "Объявление";
        public string CarText { get; private set; } = string.Empty;
        public string PriceText { get; private set; } = "0 ₽";
        public string DescriptionText { get; private set; } = string.Empty;
        public string ModelInfoText { get; private set; } = string.Empty;
        public string SellerText { get; private set; } = string.Empty;
        public string StatusText { get; private set; } = string.Empty;
        public ImageSource CurrentPhoto => _photos.Count == 0 ? null : _photos[_photoIndex];
        public string PhotoPositionText => _photos.Count <= 1 ? "1/1" : $"{_photoIndex + 1}/{_photos.Count}";

        public AdminCarSaleReviewPage(Guid saleId)
        {
            _saleId = saleId;
            InitializeComponent();
            DataContext = this;
            Loaded += (_, __) =>
            {
                if (!AppState.IsCurrentEmployeeProAdmin)
                {
                    AppState.Navigate(new ProHomePage());
                    return;
                }
                LoadSale();
            };
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
    cs.StatusId AS StatusId,
    b.Name AS BrandName,
    m.Name AS ModelName,
    m.Description AS ModelDescription,
    c.[Year] AS CarYear,
    ISNULL(lastPrice.Price, 0) AS LastPrice,
    u.Login AS SellerLogin
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
    SELECT TOP 1 u2.Login
    FROM UserCarSales ucs
    INNER JOIN Users u2 ON u2.RowId = ucs.UserId
    WHERE ucs.CarSaleId = cs.RowId
) u
WHERE cs.RowId = @p0;";

                var row = AppConnect.model1.Database.SqlQuery<AdminReviewSqlRow>(sql, _saleId).FirstOrDefault();
                if (row == null)
                {
                    MessageBox.Show("Объявление не найдено.", "Модерация", MessageBoxButton.OK, MessageBoxImage.Warning);
                    GoBackToQueue();
                    return;
                }

                var brand = Safe(row.BrandName, "Марка");
                var model = Safe(row.ModelName, "Модель");
                var year = row.CarYear.HasValue ? $" {row.CarYear.Value}" : string.Empty;

                TitleText = Safe(row.SaleTitle, "Объявление");
                CarText = $"{brand} {model}{year}";
                PriceText = $"{row.LastPrice:0} ₽";
                DescriptionText = Safe(row.SaleDescription, "Описание не указано.");
                ModelInfoText = Safe(row.ModelDescription, "Характеристики модели не указаны.");
                SellerText = $"Продавец: {Safe(row.SellerLogin, "—")}";
                StatusText = $"Статус: {Safe(CarSaleModerationStatuses.FormatModerationStatusDisplay(AppConnect.model1, row.StatusId), "—")}";

                _photos.Clear();
                foreach (var token in CarSalePhotoUiHelper.ParsePhotoTokens(row.PhotoPath))
                {
                    var img = CarSalePhotoUiHelper.ResolveSaleImage(token);
                    if (img != null)
                        _photos.Add(img);
                }
                _photoIndex = 0;

                RaiseAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки: " + ex.Message, "Модерация", MessageBoxButton.OK, MessageBoxImage.Error);
                GoBackToQueue();
            }
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

        private void Back_Click(object sender, RoutedEventArgs e) => GoBackToQueue();

        private void Approve_Click(object sender, RoutedEventArgs e)
        {
            if (!TryCommitModeration(true))
                return;
            GoBackToQueue();
        }

        private void Reject_Click(object sender, RoutedEventArgs e)
        {
            if (!TryCommitModeration(false))
                return;
            GoBackToQueue();
        }

        private void GoBackToQueue() =>
            AppState.Navigate(new ModerationHubPage());

        private bool TryCommitModeration(bool approve)
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return false;
            try
            {
                var db = AppConnect.model1;
                var entity = db.CarSales.FirstOrDefault(c => c.RowId == _saleId);
                if (entity == null)
                {
                    MessageBox.Show("Объявление не найдено.", "Модерация", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (!CarSaleModerationStatuses.IsInModerationQueue(db, entity.StatusId))
                {
                    MessageBox.Show("Это объявление уже не в очереди на модерацию.", "Модерация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return false;
                }

                if (approve)
                {
                    var approvedId = CarSaleModerationStatuses.ResolveCarSaleStatusIdByName(db, CarSaleModerationStatuses.ApprovedModeration);
                    if (!approvedId.HasValue)
                    {
                        MessageBox.Show("В справочнике Statuses нет статуса «" + CarSaleModerationStatuses.ApprovedModeration + "».", "Модерация", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    entity.StatusId = approvedId;
                    CarSaleModerationNotifier.NotifySeller(db, entity.RowId,
                        "Объявление одобрено",
                        $"Ваше объявление «{entity.Title ?? "без названия"}» прошло модерацию и опубликовано в каталоге.");
                }
                else
                {
                    var returnedId = CarSaleModerationStatuses.ResolveCarSaleStatusIdByName(db, CarSaleModerationStatuses.ReturnedForCorrection);
                    if (!returnedId.HasValue)
                    {
                        MessageBox.Show("В справочнике Statuses нет статуса «" + CarSaleModerationStatuses.ReturnedForCorrection + "».", "Модерация", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                    entity.StatusId = returnedId;
                    CarSaleModerationNotifier.NotifySeller(db, entity.RowId,
                        "Объявление отклонено",
                        $"Ваше объявление «{entity.Title ?? "без названия"}» возвращено на корректировку. Внесите правки и сохраните — оно снова попадёт на проверку.");
                }

                db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить: " + ex.Message, "Модерация", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static string Safe(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(TitleText));
            OnPropertyChanged(nameof(CarText));
            OnPropertyChanged(nameof(PriceText));
            OnPropertyChanged(nameof(DescriptionText));
            OnPropertyChanged(nameof(ModelInfoText));
            OnPropertyChanged(nameof(SellerText));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CurrentPhoto));
            OnPropertyChanged(nameof(PhotoPositionText));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string prop = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        private sealed class AdminReviewSqlRow
        {
            public string SaleTitle { get; set; }
            public string SaleDescription { get; set; }
            public string PhotoPath { get; set; }
            public Guid? StatusId { get; set; }
            public string BrandName { get; set; }
            public string ModelName { get; set; }
            public string ModelDescription { get; set; }
            public int? CarYear { get; set; }
            public decimal LastPrice { get; set; }
            public string SellerLogin { get; set; }
        }
    }
}
