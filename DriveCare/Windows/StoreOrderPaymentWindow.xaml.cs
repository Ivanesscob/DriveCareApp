using DriveCare.Helpers;
using DriveCareCore.Shop;
using System;
using System.Windows;
using System.Windows.Media;

namespace DriveCare.Windows
{
    public partial class StoreOrderPaymentWindow : Window
    {
        private readonly Guid _orderId;
        private readonly Guid _userId;

        public bool PaymentConfirmed { get; private set; }

        public StoreOrderPaymentWindow(StoreOrderListItem order, Guid userId)
        {
            InitializeComponent();
            _orderId = order?.RowId ?? Guid.Empty;
            _userId = userId;

            OrderNumberText.Text = "Заказ " + (order?.OrderNumber ?? "—");
            TotalText.Text = order?.TotalLabel ?? string.Empty;
            PickupText.Text = (order?.PickupName ?? "") + "\n" + (order?.PickupAddress ?? "");

            var payload = order?.QrPayload ?? order?.OrderNumber ?? "DRIVECARE";
            QrImage.Source = DemoQrCodeHelper.Create(payload, 180);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Paid_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var result = StoreOrderService.TryMarkPaid(_orderId, _userId);
            if (!result.ok)
            {
                ErrorText.Text = result.error ?? "Не удалось подтвердить оплату.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            PaymentConfirmed = true;
            DialogResult = true;
            Close();
        }
    }
}
