using DriveCare.Helpers;
using DriveCareCore.Shop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class StoreCheckoutPage : Page
    {
        private IReadOnlyList<OrderPickupPointItem> _allPoints = Array.Empty<OrderPickupPointItem>();
        private IReadOnlyList<OrderPickupPointItem> _visiblePoints = Array.Empty<OrderPickupPointItem>();
        private string _districtFilter = string.Empty;
        private OrderPickupPointItem _selectedPickup;
        private Guid _pendingOrderId = Guid.Empty;
        private string _pendingOrderNumber;

        public StoreCheckoutPage()
        {
            InitializeComponent();
            Loaded += async (_, __) => await OnLoadedAsync();
            PickupMap.WorkshopSelected += PickupMap_WorkshopSelected;
        }

        async System.Threading.Tasks.Task OnLoadedAsync()
        {
            if (!StoreCheckoutSession.HasItems)
            {
                MessageBox.Show("Корзина пуста.", "Заказ", MessageBoxButton.OK, MessageBoxImage.Information);
                AppState.SetFrame<ToolsStorePage>();
                return;
            }

            TotalHintText.Text = $"К оплате: {StoreCheckoutSession.Total:0} ₽ · позиций: {StoreCheckoutSession.Lines.Count}";

            if (!OrderPickupPointService.TableExists())
            {
                MapStatusText.Text = "Таблица пунктов выдачи не найдена. Выполните SQL OrderPickupPoints_Tables.sql и OrderPickupPoints_SpbSeed.sql.";
                return;
            }

            _allPoints = OrderPickupPointService.LoadActive();
            if (_allPoints.Count == 0)
            {
                MapStatusText.Text = "Нет активных пунктов выдачи. Выполните OrderPickupPoints_SpbSeed.sql.";
                return;
            }

            BuildDistrictFilters();
            await ApplyMapFilterAsync();
        }

        void BuildDistrictFilters()
        {
            DistrictFilterPanel.Children.Clear();
            AddDistrictButton("Все", string.Empty, true);
            foreach (var d in OrderPickupPointService.LoadDistricts(_allPoints))
                AddDistrictButton(d, d, false);
        }

        void AddDistrictButton(string label, string tag, bool active)
        {
            var btn = new Button
            {
                Content = label,
                Tag = tag ?? string.Empty,
                Height = 32,
                MinWidth = 72,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(10, 0, 10, 0),
                FontSize = 12,
                Style = (Style)FindResource(active ? "App.Button.Primary" : "App.Button.Outline")
            };
            btn.Click += DistrictFilter_Click;
            DistrictFilterPanel.Children.Add(btn);
        }

        async void DistrictFilter_Click(object sender, RoutedEventArgs e)
        {
            _districtFilter = (sender as Button)?.Tag as string ?? string.Empty;
            foreach (Button b in DistrictFilterPanel.Children.OfType<Button>())
            {
                var isActive = string.Equals(b.Tag as string ?? string.Empty, _districtFilter, StringComparison.Ordinal);
                b.Style = (Style)FindResource(isActive ? "App.Button.Primary" : "App.Button.Outline");
            }
            await ApplyMapFilterAsync();
        }

        async System.Threading.Tasks.Task ApplyMapFilterAsync()
        {
            _visiblePoints = string.IsNullOrEmpty(_districtFilter)
                ? _allPoints
                : _allPoints.Where(p => string.Equals(p.District, _districtFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            MapStatusText.Text = _visiblePoints.Count == 0
                ? "В этом районе нет пунктов — выберите «Все»."
                : $"На карте: {_visiblePoints.Count} пункт(ов). Нажмите метку, чтобы открыть оплату.";

            var pins = OrderPickupPointService.ToMapPins(_visiblePoints);
            await PickupMap.LoadPinsAsync(pins);
        }

        void PickupMap_WorkshopSelected(object sender, Guid pickupId)
        {
            _selectedPickup = OrderPickupPointService.FindById(pickupId, _allPoints);
            if (_selectedPickup == null)
                return;

            _pendingOrderId = Guid.Empty;
            _pendingOrderNumber = null;

            PickupHintText.Visibility = Visibility.Collapsed;
            PaymentPanel.Visibility = Visibility.Visible;
            SelectedPickupTitle.Text = _selectedPickup.ListTitle;
            SelectedPickupAddress.Text = _selectedPickup.FullAddress;

            var previewPayload = StoreCheckoutSession.BuildQrPayload("PREVIEW", _selectedPickup.RowId);
            QrImage.Source = DemoQrCodeHelper.Create(previewPayload, 160);
            PayButton.IsEnabled = true;
            PaymentErrorText.Visibility = Visibility.Collapsed;
        }

        private void PayButton_Click(object sender, RoutedEventArgs e)
        {
            PaymentErrorText.Visibility = Visibility.Collapsed;

            if (AppState.CurrentUserId == Guid.Empty)
            {
                ShowPayError("Войдите в аккаунт.");
                return;
            }

            if (_selectedPickup == null)
            {
                ShowPayError("Выберите пункт выдачи на карте.");
                return;
            }

            if (_pendingOrderId == Guid.Empty)
            {
                var created = StoreOrderService.TryCreateOrder(
                    AppState.CurrentUserId,
                    _selectedPickup.RowId,
                    StoreCheckoutSession.Total,
                    StoreCheckoutSession.Lines);

                if (!created.ok)
                {
                    ShowPayError(created.error);
                    return;
                }

                _pendingOrderId = created.orderId;
                _pendingOrderNumber = created.orderNumber;
                DriveCareCore.Analytics.ActivityTracker.TrackUser(
                    DriveCareCore.Analytics.ActivityEventCodes.StoreOrderCreate,
                    AppState.CurrentUserId,
                    entityType: "StoreOrder",
                    entityId: created.orderId);
                QrImage.Source = DemoQrCodeHelper.Create(
                    StoreCheckoutSession.BuildQrPayload(_pendingOrderNumber, _selectedPickup.RowId), 160);
            }

            var paid = StoreOrderService.TryMarkPaid(_pendingOrderId, AppState.CurrentUserId);
            if (!paid.ok)
            {
                ShowPayError(paid.error);
                return;
            }

            StoreCartService.Clear();
            StoreCheckoutSession.Clear();
            MessageBox.Show(
                $"Заказ {_pendingOrderNumber} оплачен.\nЗаберите в пункте:\n{_selectedPickup.FullAddress}",
                "Оплата",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            AppState.SetFrame<MyOrdersPage>();
        }

        void ShowPayError(string message)
        {
            PaymentErrorText.Text = message ?? "Ошибка";
            PaymentErrorText.Visibility = Visibility.Visible;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<ToolsStorePage>();
        }
    }
}
