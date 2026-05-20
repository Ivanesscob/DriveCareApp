using DriveCareCore.Bookings;
using DriveCarePro.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages
{
    public partial class WorkshopOnlineBookingsPage : Page
    {
        readonly List<Guid> _workshopIds = new List<Guid>();

        public WorkshopOnlineBookingsPage()
        {
            InitializeComponent();
            Loaded += async (_, __) => await ReloadAsync().ConfigureAwait(true);
        }

        private void Back_Click(object sender, RoutedEventArgs e) => ProNavigation.GoHome();

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync().ConfigureAwait(true);

        private async System.Threading.Tasks.Task ReloadAsync()
        {
            _workshopIds.Clear();
            if (OwnerOrganizationScope.TryResolve(out var scope, out _))
                _workshopIds.AddRange(scope.WorkshopIds);
            else if (AppState.CurrentEmployee?.WorkshopId is Guid ws && ws != Guid.Empty)
                _workshopIds.Add(ws);

            var canConfirm = AppState.HasPermission(ProPermissions.ConfirmWorkshopBooking);

            if (!WorkshopOnlineBookingService.TablesExist())
            {
                HintText.Text = "Выполните SQL WorkshopOnlineBookings_Tables.sql на сервере БД.";
                BookingsGrid.ItemsSource = null;
                return;
            }

            if (_workshopIds.Count == 0)
            {
                HintText.Text = "Мастерская не назначена.";
                BookingsGrid.ItemsSource = null;
                return;
            }

            if (!canConfirm)
            {
                HintText.Text = "Просмотр записей. Кнопка «Подтвердить» доступна роли с разрешением CONFIRM_WORKSHOP_BOOKING.";
            }
            else
            {
                HintText.Text = "Подтвердите ожидающие записи клиентов с карты автосервисов.";
            }

            var list = await WorkshopOnlineBookingService.ListForWorkshopsAsync(_workshopIds, pendingOnly: false)
                .ConfigureAwait(true);
            BookingsGrid.ItemsSource = list;
        }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.HasPermission(ProPermissions.ConfirmWorkshopBooking))
            {
                MessageBox.Show(
                    "Нет разрешения на подтверждение записей.\n\nНазначьте роль «Подтверждение записей» или право CONFIRM_WORKSHOP_BOOKING.",
                    "Записи", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var emp = AppState.CurrentEmployee;
            if (emp == null)
                return;

            Guid bookingId = Guid.Empty;
            if (sender is Button btn && btn.Tag is Guid tagId)
                bookingId = tagId;
            else if (BookingsGrid.SelectedItem is WorkshopOnlineBookingItem sel)
                bookingId = sel.BookingId;
            if (bookingId == Guid.Empty)
                return;

            var (ok, error) = await WorkshopOnlineBookingService.ConfirmBookingAsync(bookingId, emp.RowId)
                .ConfigureAwait(true);

            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось подтвердить.", "Записи",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await ReloadAsync().ConfigureAwait(true);
        }
    }
}
