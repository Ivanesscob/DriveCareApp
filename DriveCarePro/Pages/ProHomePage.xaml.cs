using DriveCareCore.Data.BD;
using DriveCarePro;
using DriveCarePro.Pages.Admin;
using DriveCarePro.Services;
using DriveCarePro.Services.ServiceBooking;
using DriveCarePro.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DriveCarePro.Pages
{
    public partial class ProHomePage : Page
    {
        public ProHomePage()
        {
            InitializeComponent();
            Loaded += ProHomePage_Loaded;
        }

        private async void ProHomePage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyPermissionLayout();
            await LoadDataAsync().ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task LoadDataAsync()
        {
            if (AppState.CanAccessAdminPanel && AppState.HasPermission(ProPermissions.AdminPanel))
                await LoadAdminStatsAsync().ConfigureAwait(true);

            if (AppState.CanAccessEmployeeTasks)
                await LoadTasksGridAsync().ConfigureAwait(true);
        }

        private void ApplyPermissionLayout()
        {
            var showAdmin = AppState.CanAccessAdminPanel;
            var showEmployee = AppState.CanAccessEmployeeWorkspace;

            AppState.SetControlVisible(AdminPanelCard, showAdmin);
            AppState.SetControlVisible(EmployeePanelCard, showEmployee);
            AppState.SetControlVisible(NoAccessHint, !showAdmin && !showEmployee);

            if (showAdmin)
                ApplyAdminPanelButtons();

            if (showEmployee)
                ApplyEmployeePanelButtons();

        }

        private void ApplyAdminPanelButtons()
        {
            var hasAdmin = AppState.HasPermission(ProPermissions.AdminPanel);

            AppState.SetControlVisible(AdminStatsSection, hasAdmin);

            var showQuick = hasAdmin || AppState.HasPermission(ProPermissions.CreateRoles);
            AppState.SetControlVisible(QuickActionsSection, showQuick);
            AppState.SetControlVisible(BtnQuickAddCompany, hasAdmin);
            AppState.SetControlVisible(BtnQuickAddPermissions, hasAdmin);
            AppState.SetControlVisible(BtnQuickAddRoles, AppState.HasPermission(ProPermissions.CreateRoles));

            AppState.SetControlVisible(BtnModerationCars, AppState.HasPermission(ProPermissions.ModerateSales));
            AppState.SetControlVisible(BtnModerationParts, hasAdmin);
            AppState.SetControlVisible(BtnOrganizations, hasAdmin);
            AppState.SetControlVisible(BtnSystemRoles, AppState.HasPermission(ProPermissions.CreateRoles));
            AppState.SetControlVisible(BtnDirectories, hasAdmin);
            AppState.SetControlVisible(BtnNotifications, AppState.HasPermission(ProPermissions.ViewNotifications));
        }

        private void ApplyEmployeePanelButtons()
        {
            AppState.SetControlVisible(BtnManageEmployees, AppState.CanManageOrganizationEmployees);
            AppState.SetControlVisible(BtnRolesConstructor, AppState.HasPermission(ProPermissions.CreateRoles));

            UpdateSectionHeader(SectionOrgLabel, OrgActionsPanel, BtnManageEmployees, BtnRolesConstructor);

            var canRepairs = AppState.HasAnyPermission(
                ProPermissions.ViewRepairs,
                ProPermissions.EditRepairs,
                ProPermissions.CreateRepairs);

            AppState.SetControlVisible(BtnBookRepair, AppState.HasPermission(ProPermissions.CreateRepairs));
            AppState.SetControlVisible(BtnBookPainting, AppState.HasPermission(ProPermissions.CreateRepairs));
            AppState.SetControlVisible(BtnRepairCars, canRepairs);

            var canServices = AppState.IsCurrentEmployeeOwner ||
                AppState.HasAnyPermission(ProPermissions.CreateRepairs, ProPermissions.EditRepairs);
            AppState.SetControlVisible(BtnWorkshopServices, canServices);

            UpdateSectionHeader(SectionWorkLabel, WorkActionsPanel, BtnBookRepair, BtnBookPainting, BtnRepairCars, BtnWorkshopServices);

            var canTasks = AppState.CanAccessEmployeeTasks;
            AppState.SetControlVisible(EmployeeTasksSection, canTasks);
            AppState.SetControlVisible(BtnCompletedTasks, canTasks);

            if (canTasks)
            {
                if (AppState.IsCurrentEmployeeDealershipHead && !AppState.IsCurrentEmployeeServiceWorker)
                    TasksSectionTitle.Text = "Задания автосалона";
                else if (AppState.IsCurrentEmployeeServiceWorker && !AppState.IsCurrentEmployeeDealershipHead)
                    TasksSectionTitle.Text = "Задания сервиса";
                else
                    TasksSectionTitle.Text = "Мои задания";

            }
            else
            {
                TasksGrid.ItemsSource = null;
            }
        }

        private static void UpdateSectionHeader(TextBlock header, Panel panel, params FrameworkElement[] buttons)
        {
            var anyVisible = buttons.Any(b => b != null && b.Visibility == Visibility.Visible);
            AppState.SetControlVisible(header, anyVisible);
            AppState.SetControlVisible(panel, anyVisible);
        }

        private void ManageEmployees_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new OwnerEmployeesManagePage());

        private void RolesConstructor_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new OwnerRolesConstructorPage(systemRolesMode: false));

        private void AdminSystemRoles_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new OwnerRolesConstructorPage(systemRolesMode: true));

        private void BookRepair_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopClientLookupPage(ServiceBookingContext.Create(ServiceBookingKind.Repair)));

        private void BookPainting_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopClientLookupPage(ServiceBookingContext.Create(ServiceBookingKind.Painting)));

        private void RepairCars_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopRepairCarsPage());

        private void WorkshopServices_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopServicesPage());

        private void CompletedTasks_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new CompletedTasksPage());

        private async void RefreshTasks_Click(object sender, RoutedEventArgs e) =>
            await LoadTasksGridAsync().ConfigureAwait(true);

        private void OpenTaskFromMenu_Click(object sender, RoutedEventArgs e) => OpenSelectedTask();

        private async System.Threading.Tasks.Task LoadTasksGridAsync()
        {
            if (!AppState.CanAccessEmployeeTasks)
            {
                TasksGrid.ItemsSource = null;
                return;
            }

            var emp = AppState.CurrentEmployee;
            if (emp == null)
            {
                TasksGrid.ItemsSource = null;
                return;
            }

            var rows = await ProHomeDataService.LoadTasksAsync(emp.RowId).ConfigureAwait(true);
            TasksGrid.ItemsSource = rows;
        }

        private void TasksGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!(e.Row.Item is ProHomeDataService.EmployeeTaskRowVm row))
                return;

            if (row.IsPartnerDone)
            {
                e.Row.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 245, 222));
                return;
            }

            e.Row.ClearValue(DataGridRow.BackgroundProperty);
        }

        private void TasksGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelectedTask();

        private void OpenSelectedTask()
        {
            if (TasksGrid.SelectedItem is ProHomeDataService.EmployeeTaskRowVm row)
                AppState.Navigate(new EmployeeTaskCardPage(row.TaskId));
        }

        private async System.Threading.Tasks.Task LoadAdminStatsAsync()
        {
            var stats = await ProHomeDataService.LoadAdminStatsAsync().ConfigureAwait(true);
            CntUsers.Text = stats.Users;
            CntEmployees.Text = stats.Employees;
            CntCarSales.Text = stats.CarSales;
            CntParts.Text = stats.Parts;
            CntCompanies.Text = stats.Companies;
            CntCars.Text = stats.Cars;
        }

        private void ModerationCars_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminCarSaleModerationPage());

        private void ModerationParts_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminPartsModerationPage());

        private void AdminOrganizations_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminOrganizationsPage());

        private void AdminDirectories_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminDirectoriesHubPage());

        private void QuickAddCompany_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.HasPermission(ProPermissions.AdminPanel))
                return;
            var owner = Window.GetWindow(this);
            var dlg = new CreateCompanyWindow();
            if (owner != null)
                dlg.Owner = owner;
            dlg.ShowDialog();
        }

        private void QuickAddPermissions_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.HasPermission(ProPermissions.AdminPanel))
                return;
            AppState.Navigate(new AdminPermissionsPage(embeddedInHub: false, focusAddForm: true));
        }

        private void QuickAddRoles_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.HasPermission(ProPermissions.CreateRoles))
                return;
            var owner = Window.GetWindow(this);
            RoleEditWindow.ShowCreate(owner, systemRolesMode: true, scope: null);
        }

        private void AdminBroadcast_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminBroadcastPage());

        private void Profile_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProfileProPage());

    }
}
