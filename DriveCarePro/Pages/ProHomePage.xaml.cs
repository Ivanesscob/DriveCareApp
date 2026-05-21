using DriveCareCore.Data.BD;
using DriveCareCore.Messaging;
using DriveCarePro;
using DriveCarePro.Pages.Admin;
using DriveCarePro.Services;
using DriveCarePro.Services.ServiceBooking;
using DriveCarePro.Services.ServiceDocuments;
using DriveCarePro.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DriveCarePro.Pages
{
    public partial class ProHomePage : Page, INotifyPropertyChanged
    {
        public ProHomePage()
        {
            EmployeeNotificationItems = new ObservableCollection<ProEmployeeNotificationItem>();
            InitializeComponent();
            DataContext = this;
            Loaded += ProHomePage_Loaded;
            Unloaded += ProHomePage_Unloaded;
        }

        public ObservableCollection<ProEmployeeNotificationItem> EmployeeNotificationItems { get; }

        public Visibility NoNotificationsVisibility =>
            EmployeeNotificationItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NotificationsListVisibility =>
            EmployeeNotificationItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        private async void ProHomePage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyPermissionLayout();
            WorkshopChatRealtimeClient.MessageReceived -= OnClientChatMessageReceived;
            WorkshopChatRealtimeClient.MessageReceived += OnClientChatMessageReceived;
            var workshopIds = ResolveWorkshopIdsForMessages();
            if (workshopIds.Count > 0)
                WorkshopChatRealtimeClient.StartForWorkshops(workshopIds);
            await LoadDataAsync().ConfigureAwait(true);
            await ReloadEmployeeNotificationsAsync().ConfigureAwait(true);
            await ReloadClientMessagesBadgeAsync().ConfigureAwait(true);
        }

        private void ProHomePage_Unloaded(object sender, RoutedEventArgs e)
        {
            WorkshopChatRealtimeClient.MessageReceived -= OnClientChatMessageReceived;
        }

        private async void OnClientChatMessageReceived(ChatPushEventArgs e)
        {
            if (e == null)
                return;
            await Dispatcher.InvokeAsync(async () =>
            {
                await ReloadClientMessagesBadgeAsync().ConfigureAwait(true);
            });
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

            AppState.SetControlVisible(BtnEmployeeNotifications, AppState.CanAccessEmployeeTasks);
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
            AppState.SetControlVisible(BtnModerationWorkshopTypes, hasAdmin);
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
            AppState.SetControlVisible(BtnPaintShopSettings, canServices);
            AppState.SetControlVisible(BtnShop, AppState.CanAccessPurchaserShop);
            AppState.SetControlVisible(BtnOnlineBookings, AppState.CanAccessEmployeeWorkspace);
            AppState.SetControlVisible(BtnWorkSchedule,
                AppState.HasPermission(ProPermissions.ManageWorkshopSchedule) || AppState.IsCurrentEmployeeOwner);

            UpdateSectionHeader(SectionWorkLabel, WorkActionsPanel, BtnBookRepair, BtnBookPainting, BtnRepairCars, BtnWorkshopServices, BtnPaintShopSettings, BtnShop, BtnOnlineBookings, BtnWorkSchedule, BtnClientMessages);

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
                TasksTree.ItemsSource = null;
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

        private void PaintShopSettings_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopPaintShopSettingsPage());

        private void ClientMessages_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopMessagesPage());

        private void OnlineBookings_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopOnlineBookingsPage());

        private void WorkSchedule_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new WorkshopWorkSchedulePage());

        private List<Guid> ResolveWorkshopIdsForMessages()
        {
            var ids = new List<Guid>();
            if (OwnerOrganizationScope.TryResolve(out var scope, out _) && scope?.WorkshopIds != null)
                ids.AddRange(scope.WorkshopIds.Where(id => id != Guid.Empty));
            else if (AppState.CurrentEmployee?.WorkshopId is Guid ws && ws != Guid.Empty)
                ids.Add(ws);
            return ids.Distinct().ToList();
        }

        private async System.Threading.Tasks.Task ReloadClientMessagesBadgeAsync()
        {
            if (ClientMessagesBadge == null)
                return;

            var workshopIds = ResolveWorkshopIdsForMessages();
            if (workshopIds.Count == 0 || !WorkshopMessagingService.TablesExist())
            {
                UpdateClientMessagesBadge(0);
                return;
            }

            try
            {
                var unread = await WorkshopMessagingService.CountUnreadForWorkshopsAsync(workshopIds)
                    .ConfigureAwait(true);
                UpdateClientMessagesBadge(unread);
            }
            catch
            {
                UpdateClientMessagesBadge(0);
            }
        }

        private void UpdateClientMessagesBadge(int unread)
        {
            if (ClientMessagesBadge == null)
                return;

            if (unread <= 0)
            {
                ClientMessagesBadge.Visibility = Visibility.Collapsed;
                return;
            }

            ClientMessagesBadge.Visibility = Visibility.Visible;
            ClientMessagesBadgeText.Text = unread > 99 ? "99+" : unread.ToString();
        }

        private void Shop_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.CanAccessPurchaserShop)
                return;

            var emp = AppState.CurrentEmployee;
            if (emp == null || !emp.WorkshopId.HasValue || emp.WorkshopId.Value == Guid.Empty)
            {
                MessageBox.Show(
                    "Не определена мастерская сотрудника.\n\nПривяжите сотрудника к мастерской в карточке персонала.",
                    "Магазин",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            AppState.Navigate(new ProTaskShopPage(emp.WorkshopId.Value));
        }

        private void CompletedTasks_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new CompletedTasksPage());

        private async void RefreshTasks_Click(object sender, RoutedEventArgs e) =>
            await LoadTasksGridAsync().ConfigureAwait(true);

        private void OpenTaskFromMenu_Click(object sender, RoutedEventArgs e) => OpenSelectedTaskFromTree();

        private async System.Threading.Tasks.Task LoadTasksGridAsync()
        {
            if (!AppState.CanAccessEmployeeTasks)
            {
                TasksTree.ItemsSource = null;
                return;
            }

            var emp = AppState.CurrentEmployee;
            if (emp == null)
            {
                TasksTree.ItemsSource = null;
                return;
            }

            var forest = await ServiceDocumentService.BuildForestForEmployeeAsync(emp.RowId).ConfigureAwait(true);
            if (forest.Count == 0)
            {
                var rows = await ProHomeDataService.LoadTasksAsync(emp.RowId).ConfigureAwait(true);
                var flat = rows.Select(r => new TaskTreeNodeVm
                {
                    TaskId = r.TaskId,
                    Title = r.Title,
                    StatusDisplay = r.StatusDisplay,
                    IsCompleted = r.CompletedDisplay == "Да",
                    IsReadyToComplete = r.IsReadyToComplete,
                    IsCurrentEmployeeTask = true
                }).ToList();
                EmployeeTaskListPresentation.AssignLevelNumbers(flat);
                TasksTree.ItemsSource = flat;
            }
            else
            {
                EmployeeTaskListPresentation.EnrichTaskForest(forest);
                TasksTree.ItemsSource = forest;
            }

            await ReloadEmployeeNotificationsAsync().ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task ReloadEmployeeNotificationsAsync()
        {
            EmployeeNotificationItems.Clear();
            var emp = AppState.CurrentEmployee;
            if (emp == null || !AppState.CanAccessEmployeeTasks)
            {
                UpdateNotificationsBadge(0);
                OnPropertyChanged(nameof(NoNotificationsVisibility));
                OnPropertyChanged(nameof(NotificationsListVisibility));
                return;
            }

            var items = await ProEmployeeNotificationService.LoadForEmployeeAsync(emp.RowId).ConfigureAwait(true);
            foreach (var item in items)
                EmployeeNotificationItems.Add(item);

            var unread = await ProEmployeeNotificationService.CountUnreadAsync(emp.RowId).ConfigureAwait(true);
            UpdateNotificationsBadge(unread);
            OnPropertyChanged(nameof(NoNotificationsVisibility));
            OnPropertyChanged(nameof(NotificationsListVisibility));
        }

        private void UpdateNotificationsBadge(int unread)
        {
            if (NotificationsBadge == null)
                return;

            if (unread <= 0)
            {
                NotificationsBadge.Visibility = Visibility.Collapsed;
                return;
            }

            NotificationsBadge.Visibility = Visibility.Visible;
            NotificationsBadgeText.Text = unread > 9 ? "9+" : unread.ToString();
        }

        private async void EmployeeNotificationsButton_Click(object sender, RoutedEventArgs e)
        {
            await ReloadEmployeeNotificationsAsync().ConfigureAwait(true);
            EmployeeNotificationsPopup.IsOpen = !EmployeeNotificationsPopup.IsOpen;
        }

        private async void EmployeeNotificationsList_Click(object sender, MouseButtonEventArgs e)
        {
            if (!(EmployeeNotificationsList.SelectedItem is ProEmployeeNotificationItem item))
                return;

            if (item.TaskId == Guid.Empty)
                return;

            EmployeeNotificationsPopup.IsOpen = false;

            if (!item.IsRead)
            {
                await ProEmployeeNotificationService.MarkReadAsync(item.EmployeeNotificationId).ConfigureAwait(true);
                item.IsRead = true;
                await ReloadEmployeeNotificationsAsync().ConfigureAwait(true);
            }

            AppState.Navigate(new EmployeeTaskCardPage(item.TaskId));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void TasksTree_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelectedTaskFromTree();

        private void OpenTaskFromTreeMenu_Click(object sender, RoutedEventArgs e) => OpenSelectedTaskFromTree();

        private void OpenSelectedTaskFromTree()
        {
            if (TasksTree.SelectedItem is TaskTreeNodeVm node && node.TaskId != Guid.Empty)
                AppState.Navigate(new EmployeeTaskCardPage(node.TaskId));
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

        private void ModerationWorkshopTypes_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new AdminWorkshopTypesModerationPage());

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
