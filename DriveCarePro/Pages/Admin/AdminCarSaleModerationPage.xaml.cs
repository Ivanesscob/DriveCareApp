using DriveCareCore.Data.BD;
using DriveCareCore.Data.Services;
using DriveCarePro;
using DriveCarePro.Pages;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminCarSaleModerationPage : Page
    {
        public AdminCarSaleModerationPage()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                if (!AppState.IsCurrentEmployeeProAdmin)
                {
                    AppState.Navigate(new ProHomePage());
                    return;
                }
                RefreshQueue();
            };
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshQueue();

        private void OpenCard_Click(object sender, RoutedEventArgs e) => OpenSelected();

        private void QueueGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();

        private void RefreshQueue()
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;
            try
            {
                var db = AppConnect.model1;
                var statusNames = db.Statuses
                    .ToList()
                    .GroupBy(s => s.RowId)
                    .ToDictionary(g => g.Key, g => (g.First().Name ?? string.Empty).Trim());

                var raw = db.CarSales
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(3000)
                    .ToList();

                var list = raw
                    .Where(c => CarSaleModerationStatuses.IsInModerationQueue(db, c.StatusId))
                    .Take(200)
                    .Select(c =>
                    {
                        var title = (c.Title ?? string.Empty).Trim();
                        return new ModerationQueueRow
                        {
                            RowId = c.RowId,
                            Title = title.Length == 0 ? "—" : title,
                            CreatedAtDisplay = c.CreatedAt.HasValue
                                ? c.CreatedAt.Value.ToString("dd.MM.yyyy HH:mm")
                                : "—",
                            ModerationStatus = CarSaleModerationStatuses.FormatModerationStatusDisplay(statusNames, c.StatusId)
                        };
                    })
                    .ToList();

                QueueGrid.ItemsSource = list;
                HintText.Text = list.Count == 0
                    ? "Нет объявлений со статусом, отличным от «Одобрено модерацией», среди последних 3000 по дате."
                    : $"В очереди: {list.Count}. Откройте карточку двойным щелчком или кнопкой.";
            }
            catch (Exception ex)
            {
                QueueGrid.ItemsSource = null;
                var root = ex;
                while (root.InnerException != null)
                    root = root.InnerException;
                var detail = root.Message ?? ex.Message;
                if (detail.IndexOf("Invalid column name", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    detail += Environment.NewLine + Environment.NewLine
                        + "Убедитесь, что в dbo.CarSales есть колонка StatusId (FK на Statuses.RowId). Скрипт: DriveCareCore\\Data\\BD\\Sql\\CarSales_EnsureModerationColumns.sql";
                }
                HintText.Text = "Ошибка загрузки: " + detail;
            }
        }

        private void OpenSelected()
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;
            var row = QueueGrid.SelectedItem as ModerationQueueRow;
            if (row == null)
            {
                MessageBox.Show("Выберите объявление в таблице.", "Модерация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            AppState.Navigate(new AdminCarSaleReviewPage(row.RowId));
        }

        public sealed class ModerationQueueRow
        {
            public Guid RowId { get; set; }
            public string Title { get; set; }
            public string CreatedAtDisplay { get; set; }
            public string ModerationStatus { get; set; }
        }
    }
}
