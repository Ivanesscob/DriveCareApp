using DriveCare;
using DriveCare.Pages.User;
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
    }
}

