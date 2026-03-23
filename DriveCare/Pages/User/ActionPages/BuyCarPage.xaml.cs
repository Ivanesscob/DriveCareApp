using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class BuyCarPage : Page
    {
        public BuyCarPage()
        {
            InitializeComponent();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
        }
    }
}

