using DriveCare.Pages.User;
using DriveCare.Windows;
using DriveCareCore.Bookings;
using DriveCareCore.Maps;
using DriveCareCore.Messaging;
using DriveCareCore.Reviews;
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
        enum ViewMode { Map, List }

        Guid _selectedWorkshopId = Guid.Empty;
        string _selectedPhone;
        List<WorkshopMapPin> _allPins = new List<WorkshopMapPin>();
        WorkshopServiceKindCode? _kindFilter;
        int? _minStarFilter;
        ViewMode _viewMode = ViewMode.Map;
        bool _suppressListSelection;

        public ServiceSelectPage()
        {
            InitializeComponent();
            YandexMap.WorkshopSelected += (_, id) => _ = SelectWorkshopAsync(id);
            Loaded += async (_, __) =>
            {
                _kindFilter = null;
                _minStarFilter = null;
                UpdateFilterButtonStyles();
                UpdateStarFilterButtonStyles();
                UpdateViewModeUi();
                await LoadMapAsync().ConfigureAwait(true);
            };
        }

        private void Back_Click(object sender, RoutedEventArgs e) => AppState.SetFrame<UserHomePage>();

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadMapAsync().ConfigureAwait(true);

        private void ViewModeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
                _viewMode = tag == "List" ? ViewMode.List : ViewMode.Map;
            UpdateViewModeUi();
            if (_viewMode == ViewMode.List)
                ApplyListFilter();
            else if (_allPins.Count > 0)
                _ = ApplyMapFilterAsync();
        }

        void UpdateViewModeUi()
        {
            var isMap = _viewMode == ViewMode.Map;
            ViewMapButton.Style = (Style)ViewMapButton.FindResource(isMap ? "App.Button.Primary" : "App.Button.Outline");
            ViewListButton.Style = (Style)ViewListButton.FindResource(isMap ? "App.Button.Outline" : "App.Button.Primary");
            MapPanel.Visibility = isMap ? Visibility.Visible : Visibility.Collapsed;
            ListPanel.Visibility = isMap ? Visibility.Collapsed : Visibility.Visible;
            SubtitleText.Text = isMap
                ? "Яндекс.Карта · нажмите метку автосервиса"
                : "Список автосервисов · выберите строку";
        }

        private async void KindFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                SetKindFilterFromTag(btn.Tag as string);

            UpdateFilterButtonStyles();
            await ApplyCurrentViewFilterAsync().ConfigureAwait(true);
        }

        private async void StarFilterButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                var tag = btn.Tag as string;
                if (string.IsNullOrEmpty(tag))
                    _minStarFilter = null;
                else if (int.TryParse(tag, out var min))
                    _minStarFilter = min;
                else
                    _minStarFilter = null;
            }

            UpdateStarFilterButtonStyles();
            await ApplyCurrentViewFilterAsync().ConfigureAwait(true);
        }

        async Task ApplyCurrentViewFilterAsync()
        {
            if (!IsLoaded || _allPins.Count == 0)
                return;

            if (_viewMode == ViewMode.List)
            {
                ApplyListFilter();
                UpdateStatusText();
            }
            else
            {
                await ApplyMapFilterAsync().ConfigureAwait(true);
            }
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

        void UpdateFilterButtonStyles()
        {
            SetFilterStyle(FilterAll, _kindFilter == null);
            SetFilterStyle(FilterAuto, _kindFilter == WorkshopServiceKindCode.AutoService);
            SetFilterStyle(FilterPaint, _kindFilter == WorkshopServiceKindCode.Painting);
            SetFilterStyle(FilterTire, _kindFilter == WorkshopServiceKindCode.TireService);
        }

        void UpdateStarFilterButtonStyles()
        {
            SetFilterStyle(StarFilterAll, !_minStarFilter.HasValue);
            SetFilterStyle(StarFilter5, _minStarFilter == 5);
            SetFilterStyle(StarFilter4, _minStarFilter == 4);
            SetFilterStyle(StarFilter3, _minStarFilter == 3);
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
                await ApplyCurrentViewFilterAsync().ConfigureAwait(true);

                var skipped = data.SkippedNoAddress.Count + data.SkippedGeocodeFailed.Count;
                UpdateStatusText(skipped);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка: " + ex.Message;
            }
        }

        WorkshopServiceKindCode? ReadKindFilter() => _kindFilter;

        void UpdateStatusText(int skipped = 0)
        {
            var filtered = FilterPins(_allPins);
            var filterLabel = GetFilterLabel(_kindFilter);
            var starLabel = GetStarFilterLabel(_minStarFilter);
            var modeLabel = _viewMode == ViewMode.Map ? "На карте" : "В списке";

            StatusText.Text = filtered.Count > 0
                ? $"{modeLabel}: {filtered.Count} ({filterLabel}, {starLabel}). Выберите сервис." +
                  (skipped > 0 ? $" Без координат: {skipped}." : string.Empty)
                : $"Нет сервисов для фильтра «{filterLabel}» / «{starLabel}». Измените фильтры.";
        }

        async Task ApplyMapFilterAsync(WorkshopMapLoadResult data = null)
        {
            var filtered = FilterPins(_allPins);
            await YandexMap.LoadPinsAsync(filtered).ConfigureAwait(true);

            if (data != null)
                return;

            UpdateStatusText();
        }

        void ApplyListFilter()
        {
            var filtered = FilterPins(_allPins);
            _suppressListSelection = true;
            WorkshopListView.ItemsSource = filtered;
            _suppressListSelection = false;
        }

        List<WorkshopMapPin> FilterPins(IReadOnlyList<WorkshopMapPin> pins) =>
            (pins ?? Array.Empty<WorkshopMapPin>())
            .Where(p => WorkshopServiceKinds.MatchesFilter(p, _kindFilter))
            .Where(p => MatchesStarFilter(p))
            .OrderByDescending(p => p.ReviewCount)
            .ThenByDescending(p => p.AvgRating ?? 0)
            .ThenBy(p => p.WorkshopName)
            .ToList();

        bool MatchesStarFilter(WorkshopMapPin pin)
        {
            if (!_minStarFilter.HasValue)
                return true;
            if (pin == null || !pin.HasReviews || !pin.AvgRating.HasValue)
                return false;
            var min = _minStarFilter.Value;
            if (min >= 5)
                return pin.AvgRating.Value >= 4.75m;
            return pin.AvgRating.Value >= min;
        }

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

        static string GetStarFilterLabel(int? minStars)
        {
            if (!minStars.HasValue)
                return "любой рейтинг";
            if (minStars >= 5)
                return "★ 5";
            return $"★ {minStars}+";
        }

        async Task SelectWorkshopAsync(Guid workshopId)
        {
            if (workshopId == Guid.Empty)
                return;
            await LoadWorkshopDetailAsync(workshopId).ConfigureAwait(true);
            if (_viewMode == ViewMode.Map)
                await YandexMap.FocusWorkshopAsync(workshopId).ConfigureAwait(true);
        }

        private async void WorkshopListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressListSelection)
                return;
            if (WorkshopListView.SelectedItem is WorkshopMapPin pin)
                await SelectWorkshopAsync(pin.WorkshopId).ConfigureAwait(true);
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

            var ratingTask = WorkshopReviewService.GetRatingSummaryAsync(workshopId);
            var reviewsTask = WorkshopReviewService.ListForWorkshopAsync(workshopId);
            await Task.WhenAll(ratingTask, reviewsTask).ConfigureAwait(true);
            var rating = ratingTask.Result;
            var reviews = reviewsTask.Result ?? new List<WorkshopReviewDisplay>();

            if (pin != null && pin.HasReviews)
            {
                DetailRatingText.Text = pin.RatingLabel;
                DetailRatingText.Visibility = Visibility.Visible;
            }
            else if (rating.HasReviews)
            {
                DetailRatingText.Text = rating.SummaryLine;
                DetailRatingText.Visibility = Visibility.Visible;
            }
            else
            {
                DetailRatingText.Text = "Пока нет отзывов";
                DetailRatingText.Visibility = Visibility.Visible;
            }

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

            ReviewsList.ItemsSource = reviews;
            DetailNoReviewsText.Visibility = reviews.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            PanelColumn.Width = new GridLength(LeftDetailPanel.Width);
            LeftDetailPanel.Visibility = Visibility.Visible;
        }

        void HideDetail()
        {
            _selectedWorkshopId = Guid.Empty;
            _selectedPhone = null;
            PanelColumn.Width = new GridLength(0);
            LeftDetailPanel.Visibility = Visibility.Collapsed;
            ReviewsList.ItemsSource = null;
        }

        private void CloseDetailPanel_Click(object sender, RoutedEventArgs e)
        {
            HideDetail();
            if (_viewMode == ViewMode.List)
            {
                _suppressListSelection = true;
                WorkshopListView.SelectedItem = null;
                _suppressListSelection = false;
            }
        }

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
                MessageBox.Show("Сначала выберите автосервис.", "Сообщения",
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
                MessageBox.Show("Сначала выберите автосервис.", "Запись",
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
