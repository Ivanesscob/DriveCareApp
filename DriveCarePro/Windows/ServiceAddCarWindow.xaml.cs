using DriveCarePro.Services.ServiceBooking;
using System;
using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class ServiceAddCarWindow : Window
    {
        private readonly ServiceBookingContext _ctx;
        private readonly bool _linkToUser;

        private ServiceAddCarWindow(ServiceBookingContext ctx, bool linkToUser)
        {
            _ctx = ctx;
            _linkToUser = linkToUser;
            InitializeComponent();
        }

        public static bool Show(Window owner, ServiceBookingContext ctx, bool linkToUser)
        {
            var dlg = new ServiceAddCarWindow(ctx, linkToUser) { Owner = owner };
            return dlg.ShowDialog() == true;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CarDescriptionBox.Text))
            {
                MessageBox.Show("Укажите марку и модель.", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _ctx.CarDescription = CarDescriptionBox.Text.Trim();
            _ctx.Vin = VinBox.Text?.Trim() ?? string.Empty;
            _ctx.PlateNumber = PlateBox.Text?.Trim() ?? string.Empty;
            _ctx.Year = YearBox.Text?.Trim() ?? string.Empty;
            _ctx.Color = ColorBox.Text?.Trim() ?? string.Empty;
            _ctx.SelectedCarId = null;
            _ctx.SelectedUserCarId = null;

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
