using DriveCare.Pages.User;
using DriveCare.Windows;
using DriveCareCore.Bookings;
using DriveCareCore.Maps;
using DriveCareCore.Messaging;
using DriveCareCore.Painting;
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
    public partial class PaintCarPage : Page
    {
        private readonly PaintCarViewModel _vm;
        private readonly List<WorkshopMapPin> _allPins = new List<WorkshopMapPin>();
        private Guid _mapSelectedWorkshopId = Guid.Empty;
        private string _mapSelectedPhone;
        private List<WorkshopPaintServiceOffer> _shopServices = new List<WorkshopPaintServiceOffer>();

        public PaintCarPage()
        {
            InitializeComponent();
            _vm = new PaintCarViewModel();
            DataContext = _vm;
            YandexMap.WorkshopSelected += (_, id) => _ = SelectWorkshopOnMapAsync(id);
            Loaded += PaintCarPage_Loaded;
        }

        private async void PaintCarPage_Loaded(object sender, RoutedEventArgs e)
        {
            _vm.Refresh();
            await LoadPaintMapAsync().ConfigureAwait(true);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
        }

        private void PaintWheels_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            RunPaintDialog(CarPaintKind.Wheels);
        }

        private void PaintFullCar_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            RunPaintDialog(CarPaintKind.FullCar);
        }

        private void RepaintDetail_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            RunPaintDialog(CarPaintKind.Part);
        }

        private void RunPaintDialog(CarPaintKind kind)
        {
            if (_vm.SelectedCar == null)
            {
                MessageBox.Show("Сначала выберите автомобиль.", "Покраска",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var colors = CarPaintService.LoadColorOptions();
                var owner = Window.GetWindow(this) ?? Application.Current?.MainWindow;
                var win = new PaintJobWindow(kind, colors);
                if (owner != null)
                    win.Owner = owner;

                if (win.ShowDialog() != true)
                    return;

                var (ok, error) = _vm.TryRecordPaint(
                    kind,
                    win.SelectedColorId,
                    win.CustomColorName,
                    win.PartName,
                    win.Notes);

                if (!ok)
                {
                    MessageBox.Show(error ?? "Не удалось сохранить.", "Покраска",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show("Запись добавлена в историю покраски.", "Покраска",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось открыть окно покраски: " + ex.Message,
                    "Покраска",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void MapRefresh_Click(object sender, RoutedEventArgs e) =>
            await LoadPaintMapAsync().ConfigureAwait(true);

        private async Task LoadPaintMapAsync()
        {
            MapStatusText.Text = "Загрузка мастерских покраски…";
            HideShopPanel();

            try
            {
                var data = await WorkshopMapService.LoadPinsForMapAsync().ConfigureAwait(true);
                _allPins.Clear();
                _allPins.AddRange(data.Pins ?? new List<WorkshopMapPin>());

                var filtered = FilterPaintPins(_allPins);
                await YandexMap.LoadPinsAsync(filtered).ConfigureAwait(true);

                var skipped = data.SkippedNoAddress.Count + data.SkippedGeocodeFailed.Count;
                MapStatusText.Text = filtered.Count > 0
                    ? $"На карте: {filtered.Count} (покраска). Нажмите метку мастерской." +
                      (skipped > 0 ? $" Без координат: {skipped}." : string.Empty)
                    : "Нет мастерских покраски на карте. Проверьте тип «Покраска» у мастерских в базе.";
            }
            catch (Exception ex)
            {
                MapStatusText.Text = "Ошибка карты: " + ex.Message;
            }
        }

        private static List<WorkshopMapPin> FilterPaintPins(IEnumerable<WorkshopMapPin> pins) =>
            (pins ?? Array.Empty<WorkshopMapPin>())
                .Where(p => WorkshopServiceKinds.MatchesFilter(p, WorkshopServiceKindCode.Painting))
                .ToList();

        private async Task SelectWorkshopOnMapAsync(Guid workshopId)
        {
            if (workshopId == Guid.Empty)
                return;

            await LoadShopPanelAsync(workshopId).ConfigureAwait(true);
            await YandexMap.FocusWorkshopAsync(workshopId).ConfigureAwait(true);
        }

        private async Task LoadShopPanelAsync(Guid workshopId)
        {
            _mapSelectedWorkshopId = workshopId;
            var detail = WorkshopPaintCatalogService.LoadShopDetail(workshopId);
            if (detail == null)
            {
                HideShopPanel();
                return;
            }

            ShopNameText.Text = detail.WorkshopName ?? "Мастерская";
            ShopAddressText.Text = string.IsNullOrWhiteSpace(detail.AddressLine)
                ? "Адрес не указан"
                : detail.AddressLine;

            _mapSelectedPhone = NormalizePhone(detail.Phone);
            MapCallButton.Visibility = string.IsNullOrEmpty(_mapSelectedPhone)
                ? Visibility.Collapsed
                : Visibility.Visible;

            _shopServices = WorkshopPaintCatalogService.LoadServicesForWorkshop(workshopId);
            ShopServicesList.ItemsSource = null;
            ShopServicesList.ItemsSource = _shopServices;
            if (_shopServices.Count > 0)
                ShopServicesList.SelectedIndex = 0;

            var colors = WorkshopPaintCatalogService.LoadColorsForWorkshop(workshopId);
            ShopColorsCombo.ItemsSource = colors;
            if (colors.Count > 0)
                ShopColorsCombo.SelectedIndex = 0;

            ShopPartNameBox.Text = string.Empty;
            ShopNotesBox.Text = string.Empty;
            UpdateShopPartPanelVisibility();

            ShopPanelColumn.Width = new GridLength(ShopDetailPanel.Width);
            ShopDetailPanel.Visibility = Visibility.Visible;
        }

        private void ShopServicesList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            UpdateShopPartPanelVisibility();

        private void UpdateShopPartPanelVisibility()
        {
            var svc = ShopServicesList.SelectedItem as WorkshopPaintServiceOffer;
            ShopPartPanel.Visibility = svc != null && svc.PaintKind == CarPaintKind.Part
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void HideShopPanel()
        {
            _mapSelectedWorkshopId = Guid.Empty;
            _mapSelectedPhone = null;
            _shopServices.Clear();
            ShopPanelColumn.Width = new GridLength(0);
            ShopDetailPanel.Visibility = Visibility.Collapsed;
        }

        private void CloseShopPanel_Click(object sender, RoutedEventArgs e) => HideShopPanel();

        private void SendShopInquiry_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedCar == null)
            {
                MessageBox.Show("Сначала выберите автомобиль в гараже.", "Запрос",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_mapSelectedWorkshopId == Guid.Empty)
            {
                MessageBox.Show("Выберите мастерскую на карте.", "Запрос",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!WorkshopPaintCatalogService.TablesExist())
            {
                MessageBox.Show(
                    "Таблицы покраски не настроены.\n\nВыполните SQL:\nDriveCareCore/Data/BD/Sql/WorkshopPaintServices_Tables.sql",
                    "Запрос", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var svc = ShopServicesList.SelectedItem as WorkshopPaintServiceOffer;
            if (svc == null)
            {
                MessageBox.Show("Выберите услугу покраски.", "Запрос",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var color = ShopColorsCombo.SelectedItem as WorkshopPaintColorOffer;
            var colorName = color?.ColorName?.Trim();
            if (string.IsNullOrEmpty(colorName))
            {
                MessageBox.Show("Выберите цвет.", "Запрос",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var partName = (ShopPartNameBox.Text ?? string.Empty).Trim();
            var notes = (ShopNotesBox.Text ?? string.Empty).Trim();

            var serviceId = svc.RowId != Guid.Empty ? (Guid?)svc.RowId : null;

            var (ok, error, _) = WorkshopPaintCatalogService.CreateInquiry(
                AppState.CurrentUserId,
                _vm.SelectedCar.UserCarId,
                _vm.SelectedCar.CarId,
                _mapSelectedWorkshopId,
                serviceId,
                svc.PaintKind,
                color?.ColorId,
                colorName,
                partName,
                notes);

            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось отправить запрос.", "Запрос",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var paintResult = _vm.TryRecordPaint(
                svc.PaintKind,
                color?.ColorId,
                colorName,
                partName,
                notes);

            if (!paintResult.ok)
            {
                MessageBox.Show(
                    "Запрос в мастерскую сохранён, но не удалось добавить запись в ваш журнал: " +
                    (paintResult.error ?? "ошибка"),
                    "Запрос", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                "Запрос отправлен в мастерскую и добавлен в историю покраски вашего авто.",
                "Запрос", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MapCall_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_mapSelectedPhone))
                return;
            try
            {
                Process.Start(new ProcessStartInfo("tel:" + _mapSelectedPhone) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть набор номера: " + ex.Message, "Звонок",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void MapChat_Click(object sender, RoutedEventArgs e)
        {
            if (_mapSelectedWorkshopId == Guid.Empty)
            {
                MessageBox.Show("Сначала выберите мастерскую на карте.", "Сообщения",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (AppState.CurrentUserId == Guid.Empty)
            {
                MessageBox.Show("Войдите в аккаунт.", "Сообщения",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (ok, error, convId) = await WorkshopMessagingService.GetOrCreateConversationForUserAsync(
                _mapSelectedWorkshopId, AppState.CurrentUserId).ConfigureAwait(true);

            if (!ok || !convId.HasValue)
            {
                MessageBox.Show(error ?? "Не удалось открыть чат.", "Сообщения",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AppState.PendingOpenConversationId = convId.Value;
            AppState.Navigate(new MessagesPage());
        }

        private void MapBook_Click(object sender, RoutedEventArgs e)
        {
            if (_mapSelectedWorkshopId == Guid.Empty)
            {
                MessageBox.Show("Сначала выберите мастерскую на карте.", "Запись",
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

            var win = new WorkshopOnlineBookingWindow(_mapSelectedWorkshopId, ShopNameText.Text)
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }

        private static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            return digits.Length >= 10 ? digits : null;
        }
    }
}
