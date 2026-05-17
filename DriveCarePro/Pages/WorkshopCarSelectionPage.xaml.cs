using DriveCarePro.Services.ServiceBooking;
using DriveCarePro.Windows;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages
{
    public partial class WorkshopCarSelectionPage : Page
    {
        private readonly ServiceBookingContext _ctx;

        public WorkshopCarSelectionPage(ServiceBookingContext ctx)
        {
            _ctx = ctx;
            InitializeComponent();
            Loaded += (_, __) => RefreshUi();
        }

        private void RefreshUi()
        {
            var login = _ctx.FoundUser?.Login ?? "—";
            var email = _ctx.FoundUser?.Email ?? _ctx.SearchEmail;
            var phone = _ctx.FoundUser?.Phone ?? _ctx.SearchPhone;
            ClientInfoText.Text = $"Клиент: {login}\nEmail: {email}\nТелефон: {phone}";

            CarsList.ItemsSource = _ctx.UserCars;
            var hasCars = _ctx.UserCars != null && _ctx.UserCars.Count > 0;
            NoCarsText.Visibility = hasCars ? Visibility.Collapsed : Visibility.Visible;
            BtnSelect.IsEnabled = hasCars;
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopClientLookupPage(_ctx));

        private void CarsList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            BtnSelect.IsEnabled = CarsList.SelectedItem is UserCarOption;

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (!(CarsList.SelectedItem is UserCarOption car))
            {
                MessageBox.Show("Выберите автомобиль в списке.", "Автомобиль", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ApplyCar(car);
            _ctx.ClientPath = ServiceClientPath.ExistingUserWithSelectedCar;
            AppState.Navigate(new WorkshopBookServicePage(_ctx));
        }

        private void AddCar_Click(object sender, RoutedEventArgs e)
        {
            var owner = Window.GetWindow(this);
            if (!ServiceAddCarWindow.Show(owner, _ctx, linkToUser: true))
                return;

            _ctx.ClientPath = ServiceClientPath.ExistingUserWithNewCar;
            AppState.Navigate(new WorkshopBookServicePage(_ctx));
        }

        private void GuestCar_Click(object sender, RoutedEventArgs e)
        {
            if (!ServiceManualClientWindow.Show(Window.GetWindow(this), _ctx, carOnly: true))
                return;

            _ctx.ClientPath = ServiceClientPath.ExistingUserGuestCar;
            AppState.Navigate(new WorkshopBookServicePage(_ctx));
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (_ctx.UserCars != null && _ctx.UserCars.Count > 0)
            {
                MessageBox.Show("Выберите машину, добавьте новую или укажите без привязки к аккаунту.",
                    "Автомобиль", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddCar_Click(sender, e);
        }

        private void ApplyCar(UserCarOption car)
        {
            _ctx.SelectedCarId = car.CarId;
            _ctx.SelectedUserCarId = car.UserCarId;
            _ctx.SelectedCarDisplay = car.DisplayName;
            _ctx.CarDescription = car.DisplayName;
            _ctx.Vin = car.Vin ?? string.Empty;
            _ctx.PlateNumber = car.PlateNumber ?? string.Empty;
            _ctx.Year = car.Year?.ToString() ?? string.Empty;
            _ctx.Color = car.Color ?? string.Empty;
        }
    }
}
