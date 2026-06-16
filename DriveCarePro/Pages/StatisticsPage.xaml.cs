using DriveCarePro.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages
{
    public partial class StatisticsPage : Page
    {
        private bool _suppressPeriod;

        public StatisticsPage()
        {
            InitializeComponent();
            Loaded += StatisticsPage_Loaded;
        }

        private void StatisticsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!AppState.CanAccessStatistics)
            {
                AppState.Navigate(new ProHomePage());
                return;
            }

            _suppressPeriod = true;
            PeriodCombo.Items.Clear();
            PeriodCombo.Items.Add(new ComboItem("7 дней", 7));
            PeriodCombo.Items.Add(new ComboItem("30 дней", 30));
            PeriodCombo.Items.Add(new ComboItem("90 дней", 90));
            PeriodCombo.Items.Add(new ComboItem("Всё время", 0));
            PeriodCombo.SelectedIndex = 1;
            _suppressPeriod = false;

            _ = LoadAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync().ConfigureAwait(true);

        private async void PeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPeriod || PeriodCombo.SelectedItem == null)
                return;
            await LoadAsync().ConfigureAwait(true);
        }

        private void Back_Click(object sender, RoutedEventArgs e) => AppState.Navigate(new ProHomePage());

        private async System.Threading.Tasks.Task LoadAsync()
        {
            var days = (PeriodCombo.SelectedItem as ComboItem)?.Days ?? 30;
            var platformWide = AppState.HasPermission(ProPermissions.AdminPanel);
            Guid? companyId = null;
            IReadOnlyList<Guid> workshopIds = Array.Empty<Guid>();

            if (!platformWide)
            {
                var scope = await OwnerOrganizationScope.TryResolveAsync().ConfigureAwait(true);
                if (!scope.ok)
                {
                    ScopeHintText.Text = scope.error ?? "Не удалось определить организацию.";
                    SummaryGrid.ItemsSource = null;
                    CarViewsGrid.ItemsSource = null;
                    EventsGrid.ItemsSource = null;
                    return;
                }

                companyId = scope.scope.CompanyId;
                workshopIds = scope.scope.WorkshopIds ?? Array.Empty<Guid>();
            }

            try
            {
                var vm = await StatisticsQueryService.LoadAsync(days, platformWide, companyId, workshopIds)
                    .ConfigureAwait(true);

                ScopeHintText.Text = vm.ScopeHint ?? string.Empty;
                TotalHintText.Text = vm.TableMissing
                    ? "Данные недоступны"
                    : "Всего событий за период: " + vm.TotalEvents;

                MissingTableText.Visibility = vm.TableMissing ? Visibility.Visible : Visibility.Collapsed;
                MissingTableText.Text = vm.ScopeHint ?? string.Empty;

                SummaryGrid.ItemsSource = vm.Summary;
                CarViewsGrid.ItemsSource = vm.TopCarSaleViews;
                EventsGrid.ItemsSource = vm.RecentEvents;
            }
            catch (Exception ex)
            {
                ScopeHintText.Text = "Ошибка загрузки: " + ex.Message;
            }
        }

        private sealed class ComboItem
        {
            public ComboItem(string title, int days)
            {
                Title = title;
                Days = days;
            }

            public string Title { get; }
            public int Days { get; }
            public override string ToString() => Title;
        }
    }
}
