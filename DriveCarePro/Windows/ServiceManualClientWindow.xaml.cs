using DriveCarePro.Services.ServiceBooking;
using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class ServiceManualClientWindow : Window
    {
        private readonly ServiceBookingContext _ctx;
        private readonly bool _carOnly;

        private ServiceManualClientWindow(ServiceBookingContext ctx, bool carOnly)
        {
            _ctx = ctx;
            _carOnly = carOnly;
            InitializeComponent();
            if (_carOnly)
            {
                HeaderText.Text = "Автомобиль без привязки к аккаунту";
                ClientPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmailBox.Text = _ctx.SearchEmail;
                PhoneBox.Text = _ctx.SearchPhone;
            }
        }

        public static bool Show(Window owner, ServiceBookingContext ctx, bool carOnly = false)
        {
            var dlg = new ServiceManualClientWindow(ctx, carOnly) { Owner = owner };
            return dlg.ShowDialog() == true;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_carOnly)
            {
                if (string.IsNullOrWhiteSpace(NameBox.Text))
                {
                    MessageBox.Show("Укажите имя клиента.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                _ctx.ClientFullName = NameBox.Text.Trim();
                _ctx.ClientPhone = PhoneBox.Text?.Trim() ?? string.Empty;
                _ctx.ClientEmail = EmailBox.Text?.Trim() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(CarDescriptionBox.Text))
            {
                MessageBox.Show("Укажите автомобиль.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _ctx.CarDescription = CarDescriptionBox.Text.Trim();
            _ctx.Vin = VinBox.Text?.Trim() ?? string.Empty;
            _ctx.PlateNumber = PlateBox.Text?.Trim() ?? string.Empty;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
