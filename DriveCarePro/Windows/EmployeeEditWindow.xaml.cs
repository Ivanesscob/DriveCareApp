using DriveCareCore.Data.BD;

using DriveCarePro.Services;

using System;

using System.Collections.Generic;

using System.Data.Entity;

using System.Linq;


using System.Windows;



namespace DriveCarePro.Windows

{

    public partial class EmployeeEditWindow : Window

    {

        private readonly OwnerOrganizationScope _scope;

        private readonly bool _isNew;

        private readonly Guid? _employeeId;

        private List<EmployeeManagementService.RolePickItem> _roleItems = new List<EmployeeManagementService.RolePickItem>();

        private bool _isSaving;



        private EmployeeEditWindow(OwnerOrganizationScope scope, bool isNew, Guid? employeeId)

        {

            _scope = scope;

            _isNew = isNew;

            _employeeId = employeeId;

            InitializeComponent();

            Loaded += EmployeeEditWindow_Loaded;

        }



        public static bool? ShowCreate(Window owner, OwnerOrganizationScope scope)

        {

            var dlg = new EmployeeEditWindow(scope, isNew: true, employeeId: null) { Owner = owner };

            return dlg.ShowDialog();

        }



        public static bool? ShowEdit(Window owner, OwnerOrganizationScope scope, Guid employeeId)

        {

            var dlg = new EmployeeEditWindow(scope, isNew: false, employeeId: employeeId) { Owner = owner };

            return dlg.ShowDialog();

        }



        private async void EmployeeEditWindow_Loaded(object sender, RoutedEventArgs e)

        {

            try

            {

                await LoadWorkshopsAsync().ConfigureAwait(true);



                if (_isNew)

                {

                    Title = "Новый сотрудник";

                    HeaderText.Text = "Новый сотрудник";

                    SubHeaderText.Text = $"Организация: {_scope.CompanyName}. Поля с * обязательны.";

                    PasswordLabel.Text = "Пароль *";

                    HireDatePicker.SelectedDate = DateTime.Today;

                    IsActiveCheck.IsChecked = true;

                    if (WorkshopCombo.Items.Count > 0)

                        WorkshopCombo.SelectedIndex = 0;



                    _roleItems = await EmployeeManagementService.LoadAssignableRolesAsync(_scope).ConfigureAwait(true);

                }

                else

                {

                    var model = await EmployeeManagementService.LoadEmployeeAsync(_employeeId.Value, _scope).ConfigureAwait(true);

                    if (model == null)

                    {

                        MessageBox.Show("Сотрудник не найден.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);

                        DialogResult = false;

                        Close();

                        return;

                    }



                    var displayName = AppState.FormatEmployeeDisplayName(new Employee

                    {

                        LastName = model.LastName,

                        FirstName = model.FirstName,

                        MidName = model.MidName,

                        Login = model.Login

                    });

                    Title = "Редактирование сотрудника";

                    HeaderText.Text = displayName;

                    SubHeaderText.Text = "Измените данные и нажмите «Сохранить».";

                    PasswordLabel.Text = "Новый пароль (пусто — не менять)";



                    LastNameBox.Text = model.LastName;

                    FirstNameBox.Text = model.FirstName;

                    MidNameBox.Text = model.MidName;

                    LoginBox.Text = model.Login;

                    EmailBox.Text = model.Email;

                    PhoneBox.Text = model.Phone;

                    DescriptionBox.Text = model.Description;

                    BirthDatePicker.SelectedDate = model.BirthDate;

                    HireDatePicker.SelectedDate = model.HireDate;

                    IsActiveCheck.IsChecked = model.IsActive;

                    WorkshopCombo.SelectedValue = model.WorkshopId;



                    _roleItems = await EmployeeManagementService.LoadRolesForEmployeeAsync(_employeeId.Value, _scope).ConfigureAwait(true);

                }



                RolesEmptyText.Visibility = _roleItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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

            IEnumerable<EmployeeManagementService.RolePickItem> src = _roleItems;

            if (q.Length > 0)

            {

                src = _roleItems.Where(r =>

                    (r.Name ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||

                    (r.ScopeHint ?? string.Empty).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);

            }



            RolesItems.ItemsSource = src.ToList();

        }



        private void RoleSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>

            ApplyRoleFilter();



        private async System.Threading.Tasks.Task LoadWorkshopsAsync()

        {

            var items = await DatabaseExecutor.WithDbAsync(async db =>

            {

                return await db.Workshops

                    .Where(w => w.CompanyId == _scope.CompanyId)

                    .OrderBy(w => w.Name)

                    .ToListAsync()

                    .ConfigureAwait(false);

            }).ConfigureAwait(true);



            WorkshopCombo.ItemsSource = items.Select(w => new WorkshopItem

            {

                RowId = w.RowId,

                Name = string.IsNullOrWhiteSpace(w.Name) ? "—" : w.Name.Trim()

            }).ToList();

        }



        private async void Save_Click(object sender, RoutedEventArgs e)

        {

            if (_isSaving)

                return;



            Guid? workshopId = null;

            if (WorkshopCombo.SelectedValue is Guid ws)

                workshopId = ws;



            var model = new EmployeeManagementService.EmployeeEditModel

            {

                EmployeeId = _employeeId,

                LastName = LastNameBox.Text,

                FirstName = FirstNameBox.Text,

                MidName = MidNameBox.Text,

                Login = LoginBox.Text,

                Password = PasswordBox.Password,

                Email = EmailBox.Text,

                Phone = PhoneBox.Text,

                Description = DescriptionBox.Text,

                BirthDate = BirthDatePicker.SelectedDate,

                HireDate = HireDatePicker.SelectedDate,

                IsActive = IsActiveCheck.IsChecked == true,

                WorkshopId = workshopId,

                RoleIds = _roleItems.Where(r => r.IsSelected).Select(r => r.RoleId).ToList()

            };



            _isSaving = true;

            try

            {

                var result = await EmployeeManagementService.SaveAsync(model, _scope, _isNew).ConfigureAwait(true);

                if (!result.Success)

                {

                    MessageBox.Show(result.ErrorMessage, "Сохранение", MessageBoxButton.OK, MessageBoxImage.Warning);

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



        private sealed class WorkshopItem

        {

            public Guid RowId { get; set; }

            public string Name { get; set; }

        }

    }

}

