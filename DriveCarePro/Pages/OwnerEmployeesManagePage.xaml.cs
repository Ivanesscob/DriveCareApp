using DriveCarePro.Services;

using DriveCarePro.Windows;

using System;


using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

using System.Windows.Media;



namespace DriveCarePro.Pages

{

    public partial class OwnerEmployeesManagePage : Page

    {

        private OwnerOrganizationScope _scope;

        private Guid? _selectedEmployeeId;

        private bool _isLoading;



        public OwnerEmployeesManagePage()

        {

            InitializeComponent();

            Loaded += OwnerEmployeesManagePage_Loaded;

        }



        private async void OwnerEmployeesManagePage_Loaded(object sender, RoutedEventArgs e)

        {

            if (!AppState.CanManageOrganizationEmployees)

            {

                MessageBox.Show("Нет разрешения на управление сотрудниками.", "Доступ",

                    MessageBoxButton.OK, MessageBoxImage.Information);

                AppState.Navigate(new ProHomePage());

                return;

            }



            ApplyPermissionButtons();



            var resolved = await OwnerOrganizationScope.TryResolveAsync().ConfigureAwait(true);

            if (!resolved.ok)

            {

                HintText.Text = resolved.error;

                Grid.ItemsSource = null;

                SetToolbarEnabled(false);

                return;

            }



            _scope = resolved.scope;

            HintText.Text = $"Организация: {_scope.CompanyName}. Правый щелчок по таблице — меню. Двойной щелчок — редактирование.";

            SetToolbarEnabled(true);

            await RefreshAsync().ConfigureAwait(true);

        }



        private void ApplyPermissionButtons()

        {

            AppState.SetControlVisible(BtnAdd, AppState.HasPermission(ProPermissions.CreateEmployees));

            AppState.SetControlVisible(BtnEdit, AppState.HasPermission(ProPermissions.EditEmployees));

            AppState.SetControlVisible(BtnDelete, AppState.HasPermission(ProPermissions.DeleteEmployees));

            AppState.SetControlVisible(BtnAssignRoles, AppState.HasPermission(ProPermissions.EditEmployees));

        }



        private void SetToolbarEnabled(bool enabled)

        {

            BtnAdd.IsEnabled = BtnEdit.IsEnabled = BtnRefresh.IsEnabled =

                BtnAssignRoles.IsEnabled = enabled && !_isLoading;

            if (!enabled)

            {

                BtnDelete.IsEnabled = false;

                return;

            }

            UpdateDeleteAvailability(Grid.SelectedItem is OwnerEmployeesDataService.EmployeeRowVm);

        }



        private void Back_Click(object sender, RoutedEventArgs e) =>

            AppState.Navigate(new ProHomePage());



        private async void Refresh_Click(object sender, RoutedEventArgs e) =>

            await RefreshAsync().ConfigureAwait(true);



        private async System.Threading.Tasks.Task RefreshAsync()

        {

            if (_scope == null || _isLoading)

                return;



            _isLoading = true;

            SetToolbarEnabled(true);



            try

            {

                var list = await OwnerEmployeesDataService.LoadGridAsync(_scope).ConfigureAwait(true);

                Grid.ItemsSource = list;

            }

            catch (Exception ex) when (AppState.IsDatabaseConnectionError(ex))

            {

                Grid.ItemsSource = null;

                HintText.Text = AppState.BuildConnectionErrorMessage(ex);

            }

            catch (Exception ex)

            {

                Grid.ItemsSource = null;

                HintText.Text = "Ошибка загрузки: " + ex.Message;

            }

            finally

            {

                _isLoading = false;

                SetToolbarEnabled(_scope != null);

            }

        }



        private void Add_Click(object sender, RoutedEventArgs e)

        {

            if (!AppState.HasPermission(ProPermissions.CreateEmployees) || _scope == null)

                return;



            var owner = Window.GetWindow(this);

            if (EmployeeEditWindow.ShowCreate(owner, _scope) == true)

                _ = RefreshAsync();

        }



        private void Edit_Click(object sender, RoutedEventArgs e)

        {

            if (!AppState.HasPermission(ProPermissions.EditEmployees))

                return;



            if (!TryGetSelectedEmployeeId(out var id))

                return;



            var owner = Window.GetWindow(this);

            if (EmployeeEditWindow.ShowEdit(owner, _scope, id) == true)

                _ = RefreshAsync();

        }



        private void AssignRoles_Click(object sender, RoutedEventArgs e)

        {

            if (!AppState.HasPermission(ProPermissions.EditEmployees) || _scope == null)

                return;



            if (!TryGetSelectedEmployeeId(out var id))

                return;



            if (!(Grid.SelectedItem is OwnerEmployeesDataService.EmployeeRowVm row))

                return;



            var owner = Window.GetWindow(this);

            if (EmployeeRolesWindow.Show(owner, _scope, id, row.FullName) == true)

                _ = RefreshAsync();

        }



        private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)

        {

            if (AppState.HasPermission(ProPermissions.EditEmployees))

                Edit_Click(sender, e);

        }



        private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)

        {

            var dep = e.OriginalSource as DependencyObject;

            while (dep != null && dep != Grid)

            {

                if (dep is DataGridRow gridRow)

                {

                    gridRow.IsSelected = true;

                    Grid.SelectedItem = gridRow.Item;

                    break;

                }

                dep = VisualTreeHelper.GetParent(dep);

            }

        }



        private void GridContextMenu_Opened(object sender, RoutedEventArgs e)

        {

            var hasRow = Grid.SelectedItem is OwnerEmployeesDataService.EmployeeRowVm;

            CtxAddItem.IsEnabled = AppState.HasPermission(ProPermissions.CreateEmployees);

            CtxEditItem.IsEnabled = hasRow && AppState.HasPermission(ProPermissions.EditEmployees);

            CtxAssignRolesItem.IsEnabled = hasRow && AppState.HasPermission(ProPermissions.EditEmployees);

            UpdateDeleteAvailability(hasRow);

        }



        private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)

        {

            if (Grid.SelectedItem is OwnerEmployeesDataService.EmployeeRowVm row)

                _selectedEmployeeId = row.EmployeeId;

            UpdateDeleteAvailability(Grid.SelectedItem is OwnerEmployeesDataService.EmployeeRowVm);

        }



        private void UpdateDeleteAvailability(bool hasRow)

        {

            var canDelete = hasRow &&

                            AppState.HasPermission(ProPermissions.DeleteEmployees) &&

                            !IsSelectedCurrentEmployee() &&

                            !IsSelectedOrganizationOwner();

            BtnDelete.IsEnabled = canDelete && _scope != null && !_isLoading;

            CtxDeleteItem.IsEnabled = canDelete;

        }



        private static bool IsSelectedCurrentEmployee(Guid employeeId) =>

            AppState.IsLoggedInEmployee(employeeId);



        private bool IsSelectedCurrentEmployee()

        {

            if (Grid.SelectedItem is OwnerEmployeesDataService.EmployeeRowVm row)

                return IsSelectedCurrentEmployee(row.EmployeeId);

            return _selectedEmployeeId.HasValue && IsSelectedCurrentEmployee(_selectedEmployeeId.Value);

        }



        private bool IsSelectedOrganizationOwner()

        {

            if (Grid.SelectedItem is OwnerEmployeesDataService.EmployeeRowVm row)

                return row.IsOrganizationOwner;

            return false;

        }



        private bool TryGetSelectedEmployeeId(out Guid employeeId)

        {

            employeeId = Guid.Empty;

            if (Grid.SelectedItem is OwnerEmployeesDataService.EmployeeRowVm row)

            {

                employeeId = row.EmployeeId;

                _selectedEmployeeId = row.EmployeeId;

                return true;

            }



            if (_selectedEmployeeId.HasValue)

            {

                employeeId = _selectedEmployeeId.Value;

                return true;

            }



            MessageBox.Show("Выберите сотрудника в списке.", "Управление сотрудниками",

                MessageBoxButton.OK, MessageBoxImage.Information);

            return false;

        }



        private async void Delete_Click(object sender, RoutedEventArgs e)

        {

            if (!AppState.HasPermission(ProPermissions.DeleteEmployees) || _scope == null)

                return;



            if (!TryGetSelectedEmployeeId(out var id))

                return;



            if (IsSelectedCurrentEmployee(id))

            {

                MessageBox.Show("Нельзя удалить свою учётную запись.", "Удаление",

                    MessageBoxButton.OK, MessageBoxImage.Information);

                return;

            }



            if (Grid.SelectedItem is OwnerEmployeesDataService.EmployeeRowVm selectedRow && selectedRow.IsOrganizationOwner)

            {

                MessageBox.Show("Нельзя удалить сотрудника с ролью владельца.", "Удаление",

                    MessageBoxButton.OK, MessageBoxImage.Information);

                return;

            }



            if (MessageBox.Show("Удалить выбранного сотрудника?", "Удаление",

                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)

                return;



            var err = await EmployeeManagementService.TryDeleteAsync(id, _scope).ConfigureAwait(true);

            if (err != null)

            {

                MessageBox.Show(err, "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;

            }



            _selectedEmployeeId = null;

            await RefreshAsync().ConfigureAwait(true);

        }

    }

}

