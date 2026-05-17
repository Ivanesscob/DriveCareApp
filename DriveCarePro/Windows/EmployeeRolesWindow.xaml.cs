using DriveCarePro.Services;

using System;

using System.Collections.Generic;

using System.Linq;


using System.Windows;

using System.Windows.Controls;



namespace DriveCarePro.Windows

{

    public partial class EmployeeRolesWindow : Window

    {

        private readonly OwnerOrganizationScope _scope;

        private readonly Guid _employeeId;

        private List<EmployeeManagementService.RolePickItem> _allRoles = new List<EmployeeManagementService.RolePickItem>();

        private bool _isSaving;



        private EmployeeRolesWindow(OwnerOrganizationScope scope, Guid employeeId, string employeeDisplayName)

        {

            _scope = scope;

            _employeeId = employeeId;

            InitializeComponent();

            HeaderText.Text = "Роли: " + employeeDisplayName;

            Loaded += async (_, __) => await LoadRolesAsync().ConfigureAwait(true);

        }



        public static bool? Show(Window owner, OwnerOrganizationScope scope, Guid employeeId, string employeeDisplayName)

        {

            var dlg = new EmployeeRolesWindow(scope, employeeId, employeeDisplayName) { Owner = owner };

            return dlg.ShowDialog();

        }



        private async System.Threading.Tasks.Task LoadRolesAsync()

        {

            try

            {

                _allRoles = await EmployeeManagementService.LoadRolesForEmployeeAsync(_employeeId, _scope).ConfigureAwait(true);

                RolesEmptyText.Visibility = _allRoles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                ApplyRoleFilter();

            }

            catch (Exception ex) when (AppState.IsDatabaseConnectionError(ex))

            {

                MessageBox.Show(AppState.BuildConnectionErrorMessage(ex), Title, MessageBoxButton.OK, MessageBoxImage.Warning);

                DialogResult = false;

                Close();

            }

        }



        private void ApplyRoleFilter()

        {

            var q = (RoleSearchBox.Text ?? string.Empty).Trim();

            IEnumerable<EmployeeManagementService.RolePickItem> src = _allRoles;

            if (q.Length > 0)

            {

                src = _allRoles.Where(r =>

                    (r.Name ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||

                    (r.ScopeHint ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

            }



            RolesItems.ItemsSource = src.ToList();

        }



        private void RoleSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyRoleFilter();



        private async void Save_Click(object sender, RoutedEventArgs e)

        {

            if (_isSaving)

                return;



            var roleIds = _allRoles.Where(r => r.IsSelected).Select(r => r.RoleId).ToList();

            _isSaving = true;

            try

            {

                var result = await EmployeeManagementService.AssignRolesAsync(_employeeId, roleIds, _scope).ConfigureAwait(true);

                if (!result.Success)

                {

                    MessageBox.Show(result.ErrorMessage, "Роли", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;

                }



                DialogResult = true;

                Close();

            }

            finally

            {

                _isSaving = false;

            }

        }



        private void Cancel_Click(object sender, RoutedEventArgs e)

        {

            DialogResult = false;

            Close();

        }



        private void OpenRoleConstructor_Click(object sender, RoutedEventArgs e)

        {

            MessageBox.Show(

                "Сначала создайте роли в «Конструктор ролей» на главной странице, затем отметьте их здесь и нажмите «Сохранить».",

                "Роли",

                MessageBoxButton.OK,

                MessageBoxImage.Information);

        }

    }

}

