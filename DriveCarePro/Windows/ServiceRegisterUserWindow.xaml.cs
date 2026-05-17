using DriveCarePro.Services.ServiceBooking;
using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class ServiceRegisterUserWindow : Window
    {
        private readonly ServiceBookingContext _ctx;

        private ServiceRegisterUserWindow(ServiceBookingContext ctx)
        {
            _ctx = ctx;
            InitializeComponent();
            EmailBox.Text = _ctx.SearchEmail;
            PhoneBox.Text = _ctx.SearchPhone;
        }

        public static bool Show(Window owner, ServiceBookingContext ctx)
        {
            var dlg = new ServiceRegisterUserWindow(ctx) { Owner = owner };
            return dlg.ShowDialog() == true;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var result = await ServiceBookingPersistenceService.RegisterUserAsync(
                LoginBox.Text, EmailBox.Text, PhoneBox.Text, PasswordBox.Password).ConfigureAwait(true);

            if (!result.Success)
            {
                MessageBox.Show(result.ErrorMessage, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _ctx.FoundUser = result.User;
            _ctx.ClientPath = ServiceClientPath.NewUserRegistered;
            _ctx.ClientEmail = result.User.Email ?? string.Empty;
            _ctx.ClientPhone = result.User.Phone ?? string.Empty;
            _ctx.ClientFullName = result.User.Login ?? string.Empty;
            var reload = await ServiceClientLookupService.FindUserAsync(result.User.Email, result.User.Phone).ConfigureAwait(true);
            _ctx.UserCars = reload.Cars;

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
