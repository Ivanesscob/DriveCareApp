using DriveCarePro.Services;
using DriveCarePro.Services.WorkshopServices;
using DriveCarePro.Windows;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Pages
{
    public partial class WorkshopServicesPage : Page
    {
        private Guid _workshopId;

        public WorkshopServicesPage()
        {
            InitializeComponent();
            Loaded += WorkshopServicesPage_Loaded;
        }

        private async void WorkshopServicesPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= WorkshopServicesPage_Loaded;
            await LoadAsync().ConfigureAwait(true);
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());

        private async void Refresh_Click(object sender, RoutedEventArgs e) =>
            await LoadAsync().ConfigureAwait(true);

        private async Task LoadAsync()
        {
            var emp = AppState.CurrentEmployee;
            if (emp == null)
            {
                AppState.Navigate(new ProHomePage());
                return;
            }

            if (!CanManage())
            {
                MessageBox.Show("Нет прав на управление каталогом услуг.", "Услуги",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                AppState.Navigate(new ProHomePage());
                return;
            }

            _workshopId = await ResolveWorkshopIdAsync(emp).ConfigureAwait(true);
            if (_workshopId == Guid.Empty)
            {
                HintText.Text = "Не удалось определить мастерскую. У сотрудника должен быть указан салон (WorkshopId).";
                Grid.ItemsSource = null;
                UnitsGrid.ItemsSource = null;
                return;
            }

            HintText.Text = "Описание услуги — до 500 символов. Единицы измерения задаются в справочнике выше.";
            var units = await WorkshopServiceUnitsService.ListForWorkshopAsync(_workshopId, activeOnly: false).ConfigureAwait(true);
            UnitsGrid.ItemsSource = units;

            var items = await WorkshopServiceCatalogService.ListForWorkshopAsync(_workshopId, activeOnly: false).ConfigureAwait(true);
            Grid.ItemsSource = items.OrderBy(x => x.Name).ToList();
        }

        private static bool CanManage() =>
            AppState.IsCurrentEmployeeOwner ||
            AppState.HasAnyPermission(ProPermissions.CreateRepairs, ProPermissions.EditRepairs);

        private static async System.Threading.Tasks.Task<Guid> ResolveWorkshopIdAsync(DriveCareCore.Data.BD.Employee emp)
        {
            if (emp.WorkshopId.HasValue)
                return emp.WorkshopId.Value;

            var scope = await OwnerOrganizationScope.TryResolveAsync().ConfigureAwait(false);
            if (scope.ok && scope.scope.WorkshopIds.Count > 0)
                return scope.scope.WorkshopIds[0];

            return Guid.Empty;
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            if (_workshopId == Guid.Empty)
                return;

            var item = new WorkshopServiceItem { WorkshopId = _workshopId, IsActive = true };
            var win = new WorkshopServiceEditWindow(item, isNew: true, _workshopId) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
                await LoadAsync().ConfigureAwait(true);
        }

        private async void AddUnit_Click(object sender, RoutedEventArgs e)
        {
            if (_workshopId == Guid.Empty)
                return;

            var item = new WorkshopServiceUnitItem { WorkshopId = _workshopId, IsActive = true };
            var win = new WorkshopServiceUnitEditWindow(item, isNew: true) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
                await LoadAsync().ConfigureAwait(true);
        }

        private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenEditor();

        private void EditRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is WorkshopServiceItem item)
                EditItem(item);
            else
                OpenEditor();
        }

        private void OpenEditor()
        {
            if (Grid.SelectedItem is WorkshopServiceItem item)
                EditItem(item);
        }

        private void EditItem(WorkshopServiceItem item)
        {
            var clone = new WorkshopServiceItem
            {
                RowId = item.RowId,
                WorkshopId = item.WorkshopId,
                Name = item.Name,
                Description = item.Description,
                Price = item.Price,
                UnitId = item.UnitId,
                UnitName = item.UnitName,
                IsActive = item.IsActive
            };
            var win = new WorkshopServiceEditWindow(clone, isNew: false, _workshopId) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
                _ = LoadAsync();
        }

        private void EditUnit_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is WorkshopServiceUnitItem item))
                return;

            var clone = new WorkshopServiceUnitItem
            {
                RowId = item.RowId,
                WorkshopId = item.WorkshopId,
                Name = item.Name,
                IsActive = item.IsActive
            };
            var win = new WorkshopServiceUnitEditWindow(clone, isNew: false) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
                _ = LoadAsync();
        }

        private async void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is WorkshopServiceItem item))
                return;

            if (MessageBox.Show($"Удалить услугу «{item.Name}» из каталога?", "Услуги",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var (ok, error) = await WorkshopServiceCatalogService.DeleteAsync(item.RowId).ConfigureAwait(true);
            if (!ok)
                MessageBox.Show(error ?? "Ошибка.", "Услуги", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                await LoadAsync().ConfigureAwait(true);
        }

        private async void DeleteUnit_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is WorkshopServiceUnitItem item))
                return;

            if (MessageBox.Show($"Удалить единицу «{item.Name}»?", "Единицы измерения",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var (ok, error) = await WorkshopServiceUnitsService.DeleteAsync(item.RowId).ConfigureAwait(true);
            if (!ok)
                MessageBox.Show(error ?? "Ошибка.", "Единицы измерения", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                await LoadAsync().ConfigureAwait(true);
        }
    }
}
