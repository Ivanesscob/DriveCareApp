using DriveCarePro;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminBroadcastPage : Page
    {
        public AdminBroadcastPage()
        {
            InitializeComponent();
        }

        private void BackHome_Click(object sender, RoutedEventArgs e) => ProNavigation.GoHome();

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            TitleBox.Text = string.Empty;
            BodyBox.Text = string.Empty;
        }

        private void SendStub_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;
            MessageBox.Show(
                "Здесь позже можно создать записи в таблице Notifications и UserNotifications для выбранной аудитории. Сейчас это только макет формы.",
                "Рассылка",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
