using DriveCarePro;
using DriveCarePro.Pages;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminDirectoriesHubPage : Page
    {
        private AdminTableBrowserPage _browser;
        private string _selectedTable;

        public AdminDirectoriesHubPage()
        {
            InitializeComponent();
            Loaded += AdminDirectoriesHubPage_Loaded;
        }

        public AdminDirectoriesHubPage(string initialTable) : this()
        {
            _selectedTable = initialTable;
        }

        private void AdminDirectoriesHubPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
            {
                AppState.Navigate(new ProHomePage());
                return;
            }

            BuildTableNav();
            EnsureBrowser();

            var tables = AdminReferenceTables.Sorted();
            var first = string.IsNullOrWhiteSpace(_selectedTable)
                ? tables.FirstOrDefault()
                : tables.FirstOrDefault(t => string.Equals(t, _selectedTable, System.StringComparison.OrdinalIgnoreCase))
                  ?? tables.FirstOrDefault();
            if (!string.IsNullOrEmpty(first))
                SelectTable(first);
        }

        private void BuildTableNav()
        {
            TablesNavPanel.Children.Clear();
            var outline = Application.Current.TryFindResource("App.Button.Outline") as Style;
            foreach (var table in AdminReferenceTables.Sorted())
            {
                var btn = new Button
                {
                    Content = table,
                    Tag = table,
                    Height = 36,
                    Margin = new Thickness(0, 0, 0, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Style = outline
                };
                btn.Click += TableNav_Click;
                TablesNavPanel.Children.Add(btn);
            }
        }

        private void EnsureBrowser()
        {
            if (_browser != null)
                return;
            _browser = new AdminTableBrowserPage(embeddedInHub: true);
            DirectoriesContent.Navigate(_browser);
        }

        private void TableNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string table)
                SelectTable(table);
        }

        private void SelectTable(string table)
        {
            _selectedTable = table;
            EnsureBrowser();
            _browser.SelectTableAndLoad(table);
            HighlightTableNav(table);
        }

        private void HighlightTableNav(string table)
        {
            var primary = Application.Current.TryFindResource("App.Button.Primary") as Style;
            var outline = Application.Current.TryFindResource("App.Button.Outline") as Style;
            if (primary == null || outline == null)
                return;

            foreach (var child in TablesNavPanel.Children.OfType<Button>())
                child.Style = string.Equals(child.Tag as string, table, System.StringComparison.OrdinalIgnoreCase)
                    ? primary
                    : outline;
        }

        private void Home_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());
    }
}
