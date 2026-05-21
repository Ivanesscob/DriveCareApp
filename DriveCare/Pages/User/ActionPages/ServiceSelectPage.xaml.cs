using DriveCare.Pages.User;
using DriveCare.Windows;
using DriveCareCore.Bookings;
using DriveCareCore.Maps;
using DriveCareCore.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class ServiceSelectPage : Page
    {
        Guid _selectedWorkshopId = Guid.Empty;
        string _selectedPhone;
        List<WorkshopMapPin> _allPins = new List<WorkshopMapPin>();
        WorkshopServiceKindCode? _kindFilter;

        public ServiceSelectPage()
        {
            InitializeComponent();
            YandexMap.WorkshopSelected += (_, id) => _ = SelectWorkshopAsync(id);
            Loaded += async (_, __) =>
            {
                _kindFilter = null;
                UpdateFilterButtonStyles();
                await LoadMapAsync().ConfigureAwait(true);
            };
        }

        private void Back_Click(object sender, RoutedEventArgs e) => AppState.SetFrame<UserHomePage>();

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadMapAsync().ConfigureAwait(true);

        private async void KindFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                SetKindFilterFromTag(btn.Tag as string);

            UpdateFilterButtonStyles();
            if (!IsLoaded || _allPins.Count == 0)
                return;
            await ApplyMapFilterAsync().ConfigureAwait(true);
        }

        void SetKindFilterFromTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                _kindFilter = null;
            else if (Enum.TryParse(tag, out WorkshopServiceKindCode code))
                _kindFilter = code;
            else
                _kindFilter = null;
        }

        WorkshopServiceKindCode? ReadKindFilter() => _kindFilter;

        void UpdateFilterButtonStyles()
        {
            SetFilterStyle(FilterAll, _kindFilter == null);
            SetFilterStyle(FilterAuto, _kindFilter == WorkshopServiceKindCode.AutoService);
            SetFilterStyle(FilterPaint, _kindFilter == WorkshopServiceKindCode.Painting);
            SetFilterStyle(FilterTire, _kindFilter == WorkshopServiceKindCode.TireService);
        }

        static void SetFilterStyle(Button button, bool active)
        {
            if (button == null)
                return;
            button.Style = (Style)button.FindResource(active ? "App.Button.Primary" : "App.Button.Outline");
        }

        private async Task LoadMapAsync()
        {
            StatusText.Text = "Загрузка автосервисов…";
            HideDetail();

            try
            {
                var data = await WorkshopMapService.LoadPinsForMapAsync().ConfigureAwait(true);
                _allPins = data.Pins ?? new List<WorkshopMapPin>();
                _kindFilter = ReadKindFilter();
                await ApplyMapFilterAsync(data).ConfigureAwait(true);

                var skipped = data.SkippedNoAddress.Count + data.SkippedGeocodeFailed.Count;
                var filterLabel = GetFilterLabel(_kindFilter);
                StatusText.Text = _allPins.Count > 0
                    ? $"На карте: {CountFilteredPins()} ({filterLabel}). Нажмите метку." +
                      (skipped > 0 ? $" Без координат: {skipped}." : string.Empty)
                    : "Нет точек. Добавьте адреса мастерским в базе.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка: " + ex.Message;
            }
        }

        async Task ApplyMapFilterAsync(WorkshopMapLoadResult data = null)
        {
            var filtered = FilterPins(_allPins);
            await YandexMap.LoadPinsAsync(filtered).ConfigureAwait(true);

            if (data != null)
                return;

            var filterLabel = GetFilterLabel(_kindFilter);
            StatusText.Text = filtered.Count > 0
                ? $"На карте: {filtered.Count} ({filterLabel}). Нажмите метку."
                : $"Нет сервисов для фильтра «{filterLabel}». Выберите «Все» или другой тип.";
        }

        List<WorkshopMapPin> FilterPins(IReadOnlyList<WorkshopMapPin> pins) =>
            (pins ?? Array.Empty<WorkshopMapPin>())
            .Where(p => WorkshopServiceKinds.MatchesFilter(p, _kindFilter))
            .ToList();

        int CountFilteredPins() => FilterPins(_allPins).Count;

        static string GetFilterLabel(WorkshopServiceKindCode? filter)
        {
            if (!filter.HasValue)
                return "все типы";
            switch (filter.Value)
            {
                case WorkshopServiceKindCode.Painting: return "покраска";
                case WorkshopServiceKindCode.TireService: return "шиномонтаж";
                case WorkshopServiceKindCode.AutoService: return "автосервис";
                default: return "фильтр";
            }
        }

        async Task SelectWorkshopAsync(Guid workshopId)
        {
            if (workshopId == Guid.Empty)
                return;
            await LoadWorkshopDetailAsync(workshopId).ConfigureAwait(true);
            await YandexMap.FocusWorkshopAsync(workshopId).ConfigureAwait(true);
        }

        private async Task LoadWorkshopDetailAsync(Guid workshopId)
        {
            _selectedWorkshopId = workshopId;
            var detail = await WorkshopOnlineBookingService.LoadWorkshopDetailAsync(workshopId).ConfigureAwait(true);

            if (detail == null)
            {
                HideDetail();
                return;
            }

            var pin = _allPins.FirstOrDefault(p => p.WorkshopId == workshopId
                || (p.WorkshopIds != null && p.WorkshopIds.Contains(workshopId)));
            DetailKindText.Text = pin != null && !string.IsNullOrWhiteSpace(pin.ServiceKindsLabel)
                ? pin.ServiceKindsLabel
                : WorkshopServiceKinds.GetDisplayName(detail.BusinessTypeId, detail.ServiceKindName);
            DetailNameText.Text = detail.WorkshopName ?? "Автосервис";
            DetailCompanyText.Text = detail.CompanyName ?? string.Empty;
            DetailAddressText.Text = string.IsNullOrWhiteSpace(detail.AddressLine)
                ? "Адрес не указан"
                : detail.AddressLine;

            _selectedPhone = NormalizePhone(detail.Phone);
            if (string.IsNullOrEmpty(_selectedPhone))
            {
                DetailPhoneText.Text = "Телефон не указан";
                CallPhoneButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                DetailPhoneText.Text = "Тел.: " + detail.Phone.Trim();
                CallPhoneButton.Visibility = Visibility.Visible;
            }

            DetailDescriptionText.Text = string.IsNullOrWhiteSpace(detail.Description)
                ? string.Empty
                : detail.Description;
            DetailDescriptionText.Visibility = string.IsNullOrWhiteSpace(detail.Description)
                ? Visibility.Collapsed
                : Visibility.Visible;

            PanelColumn.Width = new GridLength(LeftDetailPanel.Width);
            LeftDetailPanel.Visibility = Visibility.Visible;
        }

        void HideDetail()
        {
            _selectedWorkshopId = Guid.Empty;
            _selectedPhone = null;
            PanelColumn.Width = new GridLength(0);
            LeftDetailPanel.Visibility = Visibility.Collapsed;
        }

        private void CloseDetailPanel_Click(object sender, RoutedEventArgs e) => HideDetail();

        private void CallPhone_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPhone))
                return;
            try
            {
                Process.Start(new ProcessStartInfo("tel:" + _selectedPhone) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть набор номера: " + ex.Message, "Звонок",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            return digits.Length >= 10 ? digits : null;
        }

        private async void Chat_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWorkshopId == Guid.Empty)
            {
                MessageBox.Show("Сначала выберите автосервис на карте.", "Сообщения",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (AppState.CurrentUserId == Guid.Empty)
            {
                MessageBox.Show("Войдите в аккаунт.", "Сообщения", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (ok, error, convId) = await WorkshopMessagingService.GetOrCreateConversationForUserAsync(
                _selectedWorkshopId, AppState.CurrentUserId).ConfigureAwait(true);

            if (!ok || !convId.HasValue)
            {
                MessageBox.Show(error ?? "Не удалось открыть чат.", "Сообщения",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AppState.PendingOpenConversationId = convId.Value;
            AppState.Navigate(new MessagesPage());
        }

        private void Book_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedWorkshopId == Guid.Empty)
            {
                MessageBox.Show("Сначала выберите автосервис на карте.", "Запись",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!WorkshopOnlineBookingService.TablesExist())
            {
                MessageBox.Show(
                    "Таблица записей не настроена.\n\nВыполните SQL:\nDriveCareCore/Data/BD/Sql/WorkshopOnlineBookings_Tables.sql",
                    "Запись", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var win = new WorkshopOnlineBookingWindow(_selectedWorkshopId, DetailNameText.Text)
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }
    }
}
