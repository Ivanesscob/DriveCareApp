using DriveCarePro;
using DriveCarePro.Pages;
using DriveCarePro.Services;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages.Admin
{
    public partial class ModerationHubPage : Page
    {
        public ModerationHubPage()
        {
            InitializeComponent();
            Loaded += ModerationHubPage_Loaded;
        }

        async void ModerationHubPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
            {
                AppState.Navigate(new ProHomePage());
                return;
            }

            await RefreshBadgesAsync().ConfigureAwait(true);

            if (ModerationContent.Content == null)
                NavCars_Click(null, null);
            else if (ModerationContent.Content is AdminCarSaleModerationPage)
                SetNavStyles(NavCars);
            else if (ModerationContent.Content is AdminWorkshopTypesModerationPage)
                SetNavStyles(NavWorkshopTypes);
            else
                SetNavStyles(NavParts);
        }

        async System.Threading.Tasks.Task RefreshBadgesAsync()
        {
            try
            {
                var counts = await ModerationPendingCountsService.LoadAsync().ConfigureAwait(true);
                await Dispatcher.InvokeAsync(() =>
                {
                    ModerationBadgeHelper.Apply(NavCarsBadge, NavCarsBadgeText, counts.CarSales);
                    ModerationBadgeHelper.Apply(NavWorkshopTypesBadge, NavWorkshopTypesBadgeText, counts.WorkshopTypes);
                    ModerationBadgeHelper.Apply(NavPartsBadge, NavPartsBadgeText, 0);
                    ModerationBadgeHelper.Apply(TotalModerationBadge, TotalModerationBadgeText, counts.Total);
                });
            }
            catch
            {
            }
        }

        private void Home_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());

        private async void NavCars_Click(object sender, RoutedEventArgs e)
        {
            ModerationContent.Navigate(new AdminCarSaleModerationPage());
            SetNavStyles(NavCars);
            await RefreshBadgesAsync().ConfigureAwait(true);
        }

        private async void NavParts_Click(object sender, RoutedEventArgs e)
        {
            ModerationContent.Navigate(new AdminPartsModerationPage());
            SetNavStyles(NavParts);
            await RefreshBadgesAsync().ConfigureAwait(true);
        }

        private async void NavWorkshopTypes_Click(object sender, RoutedEventArgs e)
        {
            ModerationContent.Navigate(new AdminWorkshopTypesModerationPage());
            SetNavStyles(NavWorkshopTypes);
            await RefreshBadgesAsync().ConfigureAwait(true);
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
