using DriveCare;
using DriveCare.Pages.User;
using DriveCare.Windows;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class ServiceCarPage : Page
    {
        private readonly ServiceMaintenanceViewModel _vm;

        public ServiceCarPage()
        {
            InitializeComponent();
            _vm = new ServiceMaintenanceViewModel();
            DataContext = _vm;
            LoadingOverlay.Visibility = Visibility.Visible;
            Loaded += ServiceCarPage_Loaded;
        }

        async void ServiceCarPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ServiceCarPage_Loaded;
            await Task.Yield();
            try
            {
                await _vm.RefreshAsync().ConfigureAwait(true);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
        }

        private void OpenVisits_Click(object sender, RoutedEventArgs e)
        {
            var userCarId = _vm.SelectedCar?.UserCarId ?? Guid.Empty;
            AppState.Navigate(new ServiceVisitsPage(userCarId));
        }

        private void AddRealMileage_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedCar == null)
            {
                MessageBox.Show("Сначала выберите автомобиль.", "Пробег",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var minKm = _vm.GetMinimumAllowedMileageKm();
            var win = new EnterRealMileageWindow(minKm) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true || !win.EnteredMileageKm.HasValue)
                return;

            var (ok, error) = _vm.TryAddRealMileage(win.EnteredMileageKm.Value);
            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось сохранить пробег.", "Пробег",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Пробег сохранён. График и ориентиры обновлены.", "Пробег",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

