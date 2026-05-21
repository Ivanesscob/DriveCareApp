using DriveCarePro;
using DriveCarePro.Pages;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages.Admin
{
    public partial class ModerationHubPage : Page
    {
        public ModerationHubPage()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                if (!AppState.IsCurrentEmployeeProAdmin)
                {
                    AppState.Navigate(new ProHomePage());
                    return;
                }
                if (ModerationContent.Content == null)
                    NavCars_Click(null, null);
                else if (ModerationContent.Content is AdminCarSaleModerationPage)
                    SetNavStyles(NavCars);
                else if (ModerationContent.Content is AdminWorkshopTypesModerationPage)
                    SetNavStyles(NavWorkshopTypes);
                else
                    SetNavStyles(NavParts);
            };
        }

        private void Home_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());

        private void NavCars_Click(object sender, RoutedEventArgs e)
        {
            ModerationContent.Navigate(new AdminCarSaleModerationPage());
            SetNavStyles(NavCars);
        }

        private void NavParts_Click(object sender, RoutedEventArgs e)
        {
            ModerationContent.Navigate(new AdminPartsModerationPage());
            SetNavStyles(NavParts);
        }

        private void NavWorkshopTypes_Click(object sender, RoutedEventArgs e)
        {
            ModerationContent.Navigate(new AdminWorkshopTypesModerationPage());
            SetNavStyles(NavWorkshopTypes);
        }

        private void SetNavStyles(Button selected)
        {
            var primary = Application.Current.TryFindResource("App.Button.Primary") as Style;
            var outline = Application.Current.TryFindResource("App.Button.Outline") as Style;
            if (primary == null || outline == null)
                return;

            NavCars.Style = selected == NavCars ? primary : outline;
            NavParts.Style = selected == NavParts ? primary : outline;
            NavWorkshopTypes.Style = selected == NavWorkshopTypes ? primary : outline;
        }
    }
}
