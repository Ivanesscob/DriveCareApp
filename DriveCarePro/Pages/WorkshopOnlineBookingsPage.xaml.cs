using DriveCareCore.Bookings;
using DriveCarePro.Services;
using DriveCarePro.Services.ServiceBooking;
using DriveCarePro.Windows;
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
        /// <summary>0 — ожидают, 1 — подтверждённые, 2 — все.</summary>
        int _filterMode;

        public WorkshopOnlineBookingsPage()
        {
            InitializeComponent();
            FilterCombo.Items.Add("Ожидают");
            FilterCombo.Items.Add("Подтверждённые");
            FilterCombo.Items.Add("Все записи");
            FilterCombo.SelectedIndex = 0;
            Loaded += async (_, __) => await ReloadAsync().ConfigureAwait(true);
        }

        private void Back_Click(object sender, RoutedEventArgs e) => ProNavigation.GoHome();

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync().ConfigureAwait(true);

        private async void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || FilterCombo.SelectedIndex < 0)
                return;
            _filterMode = FilterCombo.SelectedIndex;
            await ReloadAsync().ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task ReloadAsync()
        {
            _workshopIds.Clear();
            if (OwnerOrganizationScope.TryResolve(out var scope, out _))
                _workshopIds.AddRange(scope.WorkshopIds);
            else if (AppState.CurrentEmployee?.WorkshopId is Guid ws && ws != Guid.Empty)
                _workshopIds.Add(ws);

            var canManage = AppState.HasPermission(ProPermissions.ConfirmWorkshopBooking)
                            || AppState.IsCurrentEmployeeOwner;

            if (!WorkshopOnlineBookingService.TablesExist())
            {
                HintText.Text = "Выполните SQL WorkshopOnlineBookings_Tables.sql на сервере БД.";
                BookingsList.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
                return;
            }

            if (_workshopIds.Count == 0)
            {
                HintText.Text = "Мастерская не назначена.";
                BookingsList.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
                return;
            }

            HintText.Text = canManage
                ? "Примите или отклоните заявки. При принятии создаётся задание — отметка «Клиент не пришёл» доступна в карточке задания."
                : "Просмотр заявок. Принять и отклонить могут сотрудники с правом CONFIRM_WORKSHOP_BOOKING.";

            var list = await WorkshopOnlineBookingService.ListForWorkshopsAsync(_workshopIds, _filterMode)
                .ConfigureAwait(true);

            BookingsList.ItemsSource = list;
            EmptyText.Visibility = list == null || list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool EnsureCanManage()
        {
            if (AppState.HasPermission(ProPermissions.ConfirmWorkshopBooking) || AppState.IsCurrentEmployeeOwner)
                return true;

            MessageBox.Show(
                "Нет разрешения на обработку записей.\n\nНазначьте роль «Подтверждение записей» или право CONFIRM_WORKSHOP_BOOKING.",
                "Записи", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private static WorkshopOnlineBookingItem ResolveBooking(object sender)
        {
            if (sender is Button btn && btn.Tag is WorkshopOnlineBookingItem item)
                return item;
            return null;
        }

        private static string BuildSummary(WorkshopOnlineBookingItem b) =>
            (b.ClientDisplayName ?? "Клиент") + "\n"
            + (b.CarDisplayName ?? "Автомобиль") + " · " + b.IssueCategoryLabel
            + (b.PreferredDate.HasValue ? "\nДень визита: " + b.PreferredDateLabel : string.Empty);

        private static string DefaultVisitWhen(WorkshopOnlineBookingItem b) =>
            b.PreferredDate.HasValue
                ? b.PreferredDate.Value.ToString("dd.MM.yyyy") + " в 10:00"
                : DateTime.Today.AddDays(1).ToString("dd.MM.yyyy") + " в 10:00";

        private async void Accept_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCanManage())
                return;

            var booking = ResolveBooking(sender);
            var emp = AppState.CurrentEmployee;
            if (booking == null || emp == null)
                return;

            var win = new BookingAcceptWindow(BuildSummary(booking), DefaultVisitWhen(booking))
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() != true)
                return;

            var accept = await WorkshopOnlineBookingAcceptanceService.AcceptAndCreateTaskAsync(
                booking.BookingId, emp.RowId, win.VisitWhenText).ConfigureAwait(true);

            if (accept == null || !accept.Ok)
            {
                MessageBox.Show(accept?.Error ?? "Не удалось принять запись.", "Записи",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (accept.TaskId.HasValue)
            {
                MessageBox.Show(
                    "Запись принята. Создано задание — откройте его в разделе «Мои задания».",
                    "Записи", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            FilterCombo.SelectedIndex = 1;
            _filterMode = 1;

            if (!string.IsNullOrWhiteSpace(accept.ChatWarning))
            {
                MessageBox.Show(
                    "Запись принята, но сообщение в чат не отправлено:\n" + accept.ChatWarning,
                    "Записи", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            DriveCareCore.Analytics.ActivityTracker.TrackEmployee(
                DriveCareCore.Analytics.ActivityEventCodes.WorkshopOnlineBookingConfirm,
                emp.RowId,
                booking.WorkshopId,
                entityType: "WorkshopOnlineBooking",
                entityId: booking.BookingId);

            await ReloadAsync().ConfigureAwait(true);
        }

        private async void Reject_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureCanManage())
                return;

            var booking = ResolveBooking(sender);
            var emp = AppState.CurrentEmployee;
            if (booking == null || emp == null)
                return;

            var win = new BookingRejectWindow(BuildSummary(booking))
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() != true)
                return;

            var reject = await WorkshopOnlineBookingService.RejectBookingAsync(
                booking.BookingId, emp.RowId, win.RejectReason).ConfigureAwait(true);

            if (reject == null || !reject.Ok)
            {
                MessageBox.Show(reject?.Error ?? "Не удалось отклонить запись.", "Записи",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DriveCareCore.Analytics.ActivityTracker.TrackEmployee(
                DriveCareCore.Analytics.ActivityEventCodes.WorkshopOnlineBookingReject,
                emp.RowId,
                booking.WorkshopId,
                entityType: "WorkshopOnlineBooking",
                entityId: booking.BookingId);

            if (!string.IsNullOrWhiteSpace(reject.ChatWarning))
            {
                MessageBox.Show(
                    "Запись отклонена, но сообщение в чат не отправлено:\n" + reject.ChatWarning,
                    "Записи", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await ReloadAsync().ConfigureAwait(true);
        }
    }
}
