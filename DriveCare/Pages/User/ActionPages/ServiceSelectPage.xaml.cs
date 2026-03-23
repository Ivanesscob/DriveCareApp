using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class ServiceSelectPage : Page
    {
        public ServiceSelectPage()
        {
            InitializeComponent();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
        }
    }
}

