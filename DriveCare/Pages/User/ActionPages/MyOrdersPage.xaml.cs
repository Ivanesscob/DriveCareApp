using DriveCare.Windows;
using DriveCareCore.Shop;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class MyOrdersPage : Page
    {
        public MyOrdersPage()
        {
            InitializeComponent();
            Loaded += (_, __) => Reload();
        }

        private void Reload()
        {
            if (AppState.CurrentUserId == Guid.Empty)
            {
                OrdersList.ItemsSource = null;
                EmptyText.Visibility = Visibility.Visible;
                return;
            }

            var orders = StoreOrderService.LoadForUser(AppState.CurrentUserId);
            OrdersList.ItemsSource = orders;
            EmptyText.Visibility = orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
        }

        private void PayOrder_Click(object sender, RoutedEventArgs e)
        {
            var order = (sender as FrameworkElement)?.Tag as StoreOrderListItem;
            if (order == null || AppState.CurrentUserId == Guid.Empty)
                return;

            var win = new StoreOrderPaymentWindow(order, AppState.CurrentUserId)
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() == true && win.PaymentConfirmed)
            {
                MessageBox.Show("Оплата подтверждена. Заказ передан в пункт выдачи.", "Оплата",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Reload();
            }
        }
    }
}
