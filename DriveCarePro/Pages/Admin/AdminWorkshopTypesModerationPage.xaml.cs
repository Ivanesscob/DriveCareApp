using DriveCareCore.Maps;
using DriveCarePro.Windows;
using System;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminWorkshopTypesModerationPage : Page
    {
        public AdminWorkshopTypesModerationPage()
        {
            InitializeComponent();
            Loaded += async (_, __) =>
            {
                if (!AppState.IsCurrentEmployeeProAdmin)
                {
                    AppState.Navigate(new ProHomePage());
                    return;
                }
                await RefreshQueueAsync().ConfigureAwait(true);
            };
        }

        private void BackHome_Click(object sender, RoutedEventArgs e) => ProNavigation.GoHome();

        private async void Refresh_Click(object sender, RoutedEventArgs e) =>
            await RefreshQueueAsync().ConfigureAwait(true);

        private async System.Threading.Tasks.Task RefreshQueueAsync()
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;

            if (!WorkshopBusinessTypeModerationService.TablesExist())
            {
                QueueGrid.ItemsSource = null;
                HintText.Text = "Выполните SQL: DriveCareCore\\Data\\BD\\Sql\\WorkshopBusinessTypeChangeRequests_Tables.sql";
                return;
            }

            try
            {
                var list = WorkshopBusinessTypeModerationService.ListPendingForAdmin();
                QueueGrid.ItemsSource = list;
                HintText.Text = list.Count == 0
                    ? "Нет заявок на смену типов мастерских."
                    : $"В очереди: {list.Count}. Одобрите или отклоните заявку.";
            }
            catch (Exception ex)
            {
                QueueGrid.ItemsSource = null;
                HintText.Text = "Ошибка загрузки: " + ex.Message;
            }
        }

        private async void Approve_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetRequestId(sender, out var requestId))
                return;

            var emp = AppState.CurrentEmployee;
            if (emp == null)
                return;

            if (MessageBox.Show("Одобрить смену типов мастерской? На карте DriveCare обновятся фильтры.",
                    "Одобрение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var (ok, error) = WorkshopBusinessTypeModerationService.Approve(requestId, emp.RowId);
            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось одобрить.", "Модерация",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Типы мастерской применены.", "Модерация",
                MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshQueueAsync().ConfigureAwait(true);
        }

        private async void Reject_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetRequestId(sender, out var requestId))
                return;

            var emp = AppState.CurrentEmployee;
            if (emp == null)
                return;

            var owner = Window.GetWindow(this);
            var dlg = new BookingRejectWindow("Отклонение заявки на смену типов мастерской");
            if (owner != null)
                dlg.Owner = owner;
            if (dlg.ShowDialog() != true)
                return;

            var (ok, error) = WorkshopBusinessTypeModerationService.Reject(requestId, emp.RowId, dlg.RejectReason);
            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось отклонить.", "Модерация",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Заявка отклонена. Владелец получит уведомление.", "Модерация",
                MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshQueueAsync().ConfigureAwait(true);
        }

        private static bool TryGetRequestId(object sender, out Guid requestId)
        {
            requestId = Guid.Empty;
            if ((sender as FrameworkElement)?.Tag is Guid id && id != Guid.Empty)
            {
                requestId = id;
                return true;
            }
            return false;
        }
    }
}
