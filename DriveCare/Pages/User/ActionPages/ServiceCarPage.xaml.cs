using DriveCare;
using DriveCare.Pages.User;
using DriveCare.Windows;
using System.Windows;
using System.Windows.Controls;

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
            Loaded += (_, __) => _vm.Refresh();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
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

