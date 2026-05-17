using DriveCarePro.Services.ServiceBooking;
using DriveCarePro.Windows;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages
{
    public partial class WorkshopClientLookupPage : Page
    {
        private readonly ServiceBookingContext _ctx;

        public WorkshopClientLookupPage(ServiceBookingContext ctx)
        {
            _ctx = ctx;
            InitializeComponent();
            TitleText.Text = "Запись на " + (_ctx.Kind == ServiceBookingKind.Painting ? "покраску" : "ремонт");
            EmailBox.Text = _ctx.SearchEmail;
            PhoneBox.Text = _ctx.SearchPhone;
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());

        private void HideNotFoundHint() =>
            NotFoundHintText.Visibility = Visibility.Collapsed;

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            _ctx.SearchEmail = EmailBox.Text?.Trim() ?? string.Empty;
            _ctx.SearchPhone = PhoneBox.Text?.Trim() ?? string.Empty;
            HideNotFoundHint();

            BtnSearch.IsEnabled = false;
            try
            {
                var result = await ServiceClientLookupService.FindUserAsync(_ctx.SearchEmail, _ctx.SearchPhone).ConfigureAwait(true);
                if (!result.Success)
                {
                    MessageBox.Show(result.ErrorMessage, TitleText.Text, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (result.IsFound)
                {
                    _ctx.FoundUser = result.User;
                    _ctx.UserCars = result.Cars;
                    _ctx.ClientEmail = result.User.Email ?? _ctx.SearchEmail;
                    _ctx.ClientPhone = result.User.Phone ?? _ctx.SearchPhone;
                    _ctx.ClientFullName = result.User.Login ?? string.Empty;
                    AppState.Navigate(new WorkshopCarSelectionPage(_ctx));
                    return;
                }

                NotFoundHintText.Visibility = Visibility.Visible;
            }
            finally
            {
                BtnSearch.IsEnabled = true;
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            HideNotFoundHint();
            _ctx.SearchEmail = EmailBox.Text?.Trim() ?? string.Empty;
            _ctx.SearchPhone = PhoneBox.Text?.Trim() ?? string.Empty;

            var owner = Window.GetWindow(this);
            if (ServiceRegisterUserWindow.Show(owner, _ctx))
                AppState.Navigate(new WorkshopCarSelectionPage(_ctx));
        }

        private void ContinueWithoutRegistration_Click(object sender, RoutedEventArgs e)
        {
            HideNotFoundHint();
            _ctx.SearchEmail = EmailBox.Text?.Trim() ?? string.Empty;
            _ctx.SearchPhone = PhoneBox.Text?.Trim() ?? string.Empty;
            _ctx.FoundUser = null;
            _ctx.ClientPath = ServiceClientPath.ManualGuest;

            if (ServiceManualClientWindow.Show(Window.GetWindow(this), _ctx))
                AppState.Navigate(new WorkshopBookServicePage(_ctx));
        }
    }
}
