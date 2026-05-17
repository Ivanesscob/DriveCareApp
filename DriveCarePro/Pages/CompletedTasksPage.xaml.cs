using DriveCarePro.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Pages
{
    public partial class CompletedTasksPage : Page
    {
        public CompletedTasksPage()
        {
            InitializeComponent();
            SearchBox.KeyDown += SearchBox_KeyDown;
            Loaded += CompletedTasksPage_Loaded;
        }

        private async void CompletedTasksPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= CompletedTasksPage_Loaded;
            await LoadAsync().ConfigureAwait(true);
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());

        private async void Refresh_Click(object sender, RoutedEventArgs e) =>
            await LoadAsync().ConfigureAwait(true);

        private async void Search_Click(object sender, RoutedEventArgs e) =>
            await LoadAsync().ConfigureAwait(true);

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await LoadAsync().ConfigureAwait(true);
        }

        private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
            OpenSelected();

        private async Task LoadAsync()
        {
            if (!AppState.CanAccessEmployeeTasks)
            {
                MessageBox.Show("Раздел недоступен.", "Завершённые задания",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                AppState.Navigate(new ProHomePage());
                return;
            }

            var emp = AppState.CurrentEmployee;
            if (emp == null)
            {
                AppState.Navigate(new ProHomePage());
                return;
            }

            var query = SearchBox.Text;
            var rows = await CompletedTasksDataService.LoadCompletedAsync(emp.RowId, query).ConfigureAwait(true);
            Grid.ItemsSource = rows;
        }

        private void OpenSelected()
        {
            if (!(Grid.SelectedItem is CompletedTaskRowVm row))
                return;

            AppState.Navigate(new EmployeeTaskCardPage(row.TaskId, archiveView: true));
        }
    }
}
