using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class PaintCarPage : Page
    {
        public PaintCarPage()
        {
            InitializeComponent();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
        }
    }
}

