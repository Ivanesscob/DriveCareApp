using DriveCareCore.Data.BD;

using DriveCarePro.Services;

using DriveCarePro.Services.RepairWorkOrder;
using DriveCarePro.Windows;

using DriveCarePro.Services.ServiceBooking;

using DriveCarePro.Services.WorkshopServices;

using System.Text;

using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.Data.Entity;

using System.Linq;

using System.Threading.Tasks;

using System.Windows;

using System.Windows.Controls;

using Task = System.Threading.Tasks.Task;



namespace DriveCarePro.Pages

{

    public partial class EmployeeTaskCardPage : Page

    {

        private readonly Guid _taskId;

        private Guid? _repairHistoryId;

        private Guid _workshopId;

        private IList<WorkshopServiceItem> _catalog = new List<WorkshopServiceItem>();

        private IList<WorkshopPartItem> _partsCatalog = new List<WorkshopPartItem>();

        private readonly ObservableCollection<TaskServiceLineVm> _serviceLines = new ObservableCollection<TaskServiceLineVm>();

        private readonly ObservableCollection<TaskPartLineVm> _partLines = new ObservableCollection<TaskPartLineVm>();

        private bool _isCompleted;

        private bool _isPurchaseTask;

        private readonly bool _archiveView;



        public EmployeeTaskCardPage(Guid taskId) : this(taskId, archiveView: false) { }



        public EmployeeTaskCardPage(Guid taskId, bool archiveView)

        {

            _taskId = taskId;

            _archiveView = archiveView;

            InitializeComponent();

            ServicesGrid.ItemsSource = _serviceLines;

            PartsGrid.ItemsSource = _partLines;

            Loaded += EmployeeTaskCardPage_Loaded;

        }



        private async void EmployeeTaskCardPage_Loaded(object sender, RoutedEventArgs e)

        {

            Loaded -= EmployeeTaskCardPage_Loaded;

            await LoadTaskAsync().ConfigureAwait(true);

        }



        private void Back_Click(object sender, RoutedEventArgs e) =>

            AppState.Navigate(_archiveView ? (Page)new CompletedTasksPage() : new ProHomePage());



        private void Shop_Click(object sender, RoutedEventArgs e)
        {
            if (_workshopId == Guid.Empty)
            {
                MessageBox.Show("Не определена мастерская для каталога запчастей.", "Магазин",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            TaskPartShopSession.Begin(_taskId, _workshopId);
            AppState.Navigate(new ProTaskShopPage(_taskId, _workshopId));
        }



        private void AddServiceLine_Click(object sender, RoutedEventArgs e) =>

            _serviceLines.Add(NewServiceLine());



        private void AddPartLine_Click(object sender, RoutedEventArgs e) =>

            _partLines.Add(NewPartLine());



        private void RemoveServiceLine_Click(object sender, RoutedEventArgs e)
        {
            if (ServicesGrid.SelectedItem is TaskServiceLineVm selected)
                _serviceLines.Remove(selected);
            else if (_serviceLines.Count > 0)
                _serviceLines.RemoveAt(_serviceLines.Count - 1);
        }



        private void RemovePartLine_Click(object sender, RoutedEventArgs e)
        {
            if (PartsGrid.SelectedItem is TaskPartLineVm selected)
                _partLines.Remove(selected);
            else if (_partLines.Count > 0)
                _partLines.RemoveAt(_partLines.Count - 1);
        }



        private TaskServiceLineVm NewServiceLine() =>

            new TaskServiceLineVm { Catalog = _catalog };

        private TaskPartLineVm NewPartLine() =>

            new TaskPartLineVm { Catalog = _partsCatalog };

        private async Task LoadTaskAsync()

        {

            var emp = AppState.CurrentEmployee;

            if (emp == null)

            {

                AppState.Navigate(new ProHomePage());

                return;

            }



            if (!AppState.CanAccessEmployeeTasks)

            {

                MessageBox.Show("Карточки заданий доступны работникам сервиса и главе автосалона.", "Задание",

                    MessageBoxButton.OK, MessageBoxImage.Information);

                AppState.Navigate(new ProHomePage());

                return;

            }



            try

            {

                _workshopId = emp.WorkshopId ?? Guid.Empty;

                if (_workshopId == Guid.Empty)

                {

                    var scope = await OwnerOrganizationScope.TryResolveAsync().ConfigureAwait(true);

                    if (scope.ok && scope.scope.WorkshopIds.Count > 0)

                        _workshopId = scope.scope.WorkshopIds[0];

                }



                if (_workshopId != Guid.Empty)
                {
                    _catalog = await WorkshopServiceCatalogService.ListForWorkshopAsync(_workshopId).ConfigureAwait(true);
                    _partsCatalog = await WorkshopPartCatalogService.ListStockForWorkshopAsync(_workshopId).ConfigureAwait(true);
                }

                var taskId = _taskId;

                var data = await DatabaseExecutor.WithDbAsync(db => LoadTaskDataAsync(db, taskId, emp.RowId, _archiveView))

                    .ConfigureAwait(true);



                if (data == null)

                {

                    MessageBox.Show("Задание не найдено или назначено другому сотруднику.", "Задание",

                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    AppState.Navigate(new ProHomePage());

                    return;

                }



                var serviceRows = await TaskReportService.LoadServiceLinesAsync(taskId).ConfigureAwait(true);

                var partRows = await TaskReportService.LoadPartLinesAsync(taskId).ConfigureAwait(true);

                var purchaseRequest = await TaskPurchaseRequestService.TryLoadByPurchaseTaskAsync(taskId).ConfigureAwait(true);
                var purchaseStatus = purchaseRequest == null
                    ? await TaskPurchaseRequestService.TryLoadStatusForSourceAsync(taskId).ConfigureAwait(true)
                    : null;

                ApplyTaskData(data, serviceRows, partRows, purchaseRequest, purchaseStatus);

            }

            catch (Exception ex)

            {

                MessageBox.Show("Не удалось загрузить задание: " + ex.Message, "Задание",

                    MessageBoxButton.OK, MessageBoxImage.Warning);

                AppState.Navigate(new ProHomePage());

            }

        }



        private static async Task<TaskCardData> LoadTaskDataAsync(
            DriveCareDBEntities db,
            Guid taskId,
            Guid employeeId,
            bool archiveView)

        {
            DriveCareCore.Data.BD.Task task;
            if (archiveView)
            {
                var scope = await CompletedTasksDataService.GetScopeEmployeeIdsAsync(employeeId).ConfigureAwait(false);
                task = await db.Tasks.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.RowId == taskId && t.IsCompleted && scope.Contains(t.EmployeeId))
                    .ConfigureAwait(false);
            }
            else
            {
                task = await db.Tasks.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.RowId == taskId && t.EmployeeId == employeeId)
                    .ConfigureAwait(false);
            }

            if (task == null)

                return null;



            var extra = await ServiceBookingTaskService.TryLoadExtraAsync(db, taskId).ConfigureAwait(false);



            var visitReason = extra?.VisitReason;

            if (string.IsNullOrWhiteSpace(visitReason))

                visitReason = task.Description;



            var delegation = await TaskDelegationService.BuildCardInfoAsync(
                db, taskId, employeeId, archiveView, task.IsCompleted).ConfigureAwait(false);

            var hasCar = task.CarId.HasValue && task.CarId.Value != Guid.Empty;
            var serviceKind = extra?.ServiceKind;
            var isRepairOrPainting = IsRepairOrPaintingKind(serviceKind);

            return new TaskCardData
            {
                Title = string.IsNullOrWhiteSpace(task.Title) ? "Задание" : task.Title.Trim(),
                VisitReasonText = string.IsNullOrWhiteSpace(visitReason) ? "Описание не указано." : visitReason.Trim(),
                SpecialNotesText = string.IsNullOrWhiteSpace(extra?.SpecialNotes) ? "—" : extra.SpecialNotes.Trim(),
                CarInfoText = hasCar ? CarDisplayHelper.FormatCarById(db, task.CarId) : "Не указано.",
                ClientInfoText = BuildClientSummary(db, task.ClientUserId, extra),
                ReportText = task.ReportText ?? string.Empty,
                IsCompleted = task.IsCompleted,
                RepairHistoryId = extra?.RepairHistoryId,
                HasCar = hasCar,
                ShowCarSection = hasCar && isRepairOrPainting,
                CanDelegate = delegation.CanDelegate,
                DelegationStatusText = delegation.StatusText,
                DelegationChainText = delegation.ChainText
            };
        }

        private static bool IsRepairOrPaintingKind(string serviceKind)
        {
            if (string.IsNullOrWhiteSpace(serviceKind))
                return false;

            var k = serviceKind.Trim();
            if (k.Equals(nameof(ServiceBookingKind.Repair), StringComparison.OrdinalIgnoreCase))
                return true;
            if (k.Equals(nameof(ServiceBookingKind.Painting), StringComparison.OrdinalIgnoreCase))
                return true;

            var lower = k.ToLowerInvariant();
            return lower.Contains("ремонт") || lower.Contains("покраск")
                   || lower.Contains("repair") || lower.Contains("painting");
        }

        private static string BuildPurchaseTaskDescription(TaskPurchaseRequestInfo request, string carInfo)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Закупить запчасти для работы по автомобилю:");
            var car = string.IsNullOrWhiteSpace(carInfo) ? "—" : carInfo.Trim();
            if (car.Equals("Не указано.", StringComparison.Ordinal))
                car = "—";
            sb.AppendLine(car);
            sb.AppendLine();
            sb.AppendLine("Запросил: " + (string.IsNullOrWhiteSpace(request?.RequesterName) ? "—" : request.RequesterName.Trim()));

            if (request?.Lines != null && request.Lines.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Список к закупке:");
                foreach (var line in request.Lines)
                {
                    if (string.IsNullOrWhiteSpace(line.PartName))
                        continue;
                    sb.Append("— ").Append(line.PartName.Trim())
                        .Append(" — ").Append(line.Quantity.ToString("0.###"))
                        .Append(' ').Append(line.UnitName ?? "шт.");
                    if (line.UnitPrice > 0)
                        sb.Append(", ").Append(line.UnitPrice.ToString("N2")).Append(" ₽");
                    sb.AppendLine();
                }
            }

            return sb.ToString().Trim();
        }



        private void ApplyTaskData(
            TaskCardData data,
            List<TaskServiceLineRow> serviceRows,
            List<TaskPartLineRow> partRows,
            TaskPurchaseRequestInfo purchaseRequest,
            TaskPurchaseStatusInfo purchaseStatus)

        {

            _repairHistoryId = data.RepairHistoryId;

            _isCompleted = data.IsCompleted;

            _isPurchaseTask = !_archiveView &&
                (purchaseRequest != null || TaskPurchaseRequestService.LooksLikePurchaseTask(data.Title));



            TitleText.Text = data.Title;

            TaskDescriptionText.Text = _isPurchaseTask && purchaseRequest != null
                ? BuildPurchaseTaskDescription(purchaseRequest, data.CarInfoText)
                : data.VisitReasonText;

            SpecialNotesText.Text = data.SpecialNotesText;

            CarInfoText.Text = data.CarInfoText;

            ClientInfoText.Text = data.ClientInfoText;

            CarSectionBorder.Visibility = data.ShowCarSection && !_isPurchaseTask
                ? Visibility.Visible
                : Visibility.Collapsed;



            _serviceLines.Clear();

            _partLines.Clear();



            if (serviceRows.Count > 0)

            {

                foreach (var row in serviceRows)

                    _serviceLines.Add(TaskServiceLineVm.FromRow(row, _catalog));

            }

            else if (!_isCompleted)

                _serviceLines.Add(NewServiceLine());



            if (_isPurchaseTask && purchaseRequest != null)
            {
                foreach (var row in purchaseRequest.Lines)
                    _partLines.Add(TaskPartLineVm.FromRow(row, _partsCatalog));
            }
            else if (partRows.Count > 0)
            {
                foreach (var row in partRows)
                    _partLines.Add(TaskPartLineVm.FromRow(row, _partsCatalog));
            }

            ReportTextBox.Text = _isPurchaseTask
                ? string.Empty
                : ExtractFreeTextNote(data.ReportText, serviceRows, partRows);

            ReportTextBox.IsReadOnly = _isCompleted;



            var active = !data.IsCompleted && !_archiveView && !_isPurchaseTask;

            if (_isPurchaseTask)
            {
                ArchiveHintText.Visibility = Visibility.Visible;
                ArchiveHintText.Text = "Закупите позиции из списка ниже и нажмите «Закупка выполнена» — детали попадут в задание заказчика.";
            }
            else
                ArchiveHintText.Visibility = _archiveView ? Visibility.Visible : Visibility.Collapsed;

            if (purchaseStatus != null && purchaseStatus.HasOpenRequest && !string.IsNullOrWhiteSpace(purchaseStatus.StatusText))
            {
                PurchaseStatusSection.Visibility = Visibility.Visible;
                PurchaseStatusText.Text = purchaseStatus.StatusText;
            }
            else
                PurchaseStatusSection.Visibility = Visibility.Collapsed;

            CompletionSection.Visibility = (_archiveView || data.IsCompleted) ? Visibility.Collapsed : Visibility.Visible;

            if (_archiveView)
                WorkOrderButton.Content = "Скачать заказ-наряд";

            var showWorkOrder = data.ShowCarSection && _repairHistoryId.HasValue && !_isPurchaseTask;
            WorkOrderButton.Visibility = showWorkOrder ? Visibility.Visible : Visibility.Collapsed;
            WorkOrderButton.IsEnabled = showWorkOrder;

            ShopButton.Visibility = (_isPurchaseTask || _archiveView) ? Visibility.Collapsed : Visibility.Visible;
            ShopButton.IsEnabled = active;

            var canCompletePurchase = _isPurchaseTask && !data.IsCompleted && !_archiveView;
            CompleteButton.IsEnabled = active || canCompletePurchase;
            CompleteButton.Content = _isPurchaseTask ? "Закупка выполнена" : "Завершить задание";
            CompletionTitleText.Text = _isPurchaseTask ? "Завершение закупки" : "Завершение ремонта";

            AddServiceLineButton.IsEnabled = active;

            AddPartLineButton.IsEnabled = active;

            RemoveServiceLineButton.IsEnabled = active;

            RemovePartLineButton.IsEnabled = active;

            ServicesGrid.IsReadOnly = !active;
            ServicesGrid.Visibility = _isPurchaseTask ? Visibility.Collapsed : Visibility.Visible;
            AddServiceLineButton.Visibility = _isPurchaseTask ? Visibility.Collapsed : Visibility.Visible;
            RemoveServiceLineButton.Visibility = _isPurchaseTask ? Visibility.Collapsed : Visibility.Visible;

            PartsGrid.IsReadOnly = _isPurchaseTask || !active;
            AddPartLineButton.Visibility = (_isPurchaseTask || !active) ? Visibility.Collapsed : Visibility.Visible;
            RemovePartLineButton.Visibility = (_isPurchaseTask || !active) ? Visibility.Collapsed : Visibility.Visible;

            ReportTextBox.Visibility = _isPurchaseTask ? Visibility.Collapsed : Visibility.Visible;

            var showDelegation = !_archiveView && !_isPurchaseTask;
            DelegationSection.Visibility = showDelegation ? Visibility.Visible : Visibility.Collapsed;
            if (showDelegation)
            {
                DelegationStatusText.Text = string.IsNullOrWhiteSpace(data.DelegationStatusText)
                    ? "—"
                    : data.DelegationStatusText;
                if (string.IsNullOrWhiteSpace(data.DelegationChainText))
                {
                    DelegationChainText.Visibility = Visibility.Collapsed;
                    DelegationChainText.Text = string.Empty;
                }
                else
                {
                    DelegationChainText.Visibility = Visibility.Visible;
                    DelegationChainText.Text = data.DelegationChainText;
                }
                DelegateTaskButton.Visibility = data.CanDelegate ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void DelegateTask_Click(object sender, RoutedEventArgs e)
        {
            var emp = AppState.CurrentEmployee;
            if (emp == null || _archiveView || _isCompleted)
                return;

            var employees = await TaskDelegationService.ListDelegateTargetsAsync(emp.RowId, _taskId)
                .ConfigureAwait(true);
            if (employees.Count == 0)
            {
                MessageBox.Show("Нет других сотрудников в вашей организации для передачи.",
                    "Поручение", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new DelegateTaskWindow(_taskId, emp.RowId, employees) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
            {
                MessageBox.Show("Поручение передано. У сотрудника появилось своё задание.",
                    "Поручение", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadTaskAsync().ConfigureAwait(true);
            }
        }

        private static string ExtractFreeTextNote(string reportText, List<TaskServiceLineRow> services, List<TaskPartLineRow> parts)

        {

            if (services.Count == 0 && parts.Count == 0)

                return reportText ?? string.Empty;



            var built = TaskReportService.BuildReportText(services, parts, string.Empty);

            var full = (reportText ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(built))

                return full;

            if (full.StartsWith(built, StringComparison.Ordinal))

            {

                var rest = full.Substring(built.Length).Trim();

                return rest;

            }

            return full;

        }



        private async void WorkOrder_Click(object sender, RoutedEventArgs e)

        {

            if (!_repairHistoryId.HasValue)

            {

                MessageBox.Show("Нет связи с записью ремонта в базе.", "Заказ-наряд",

                    MessageBoxButton.OK, MessageBoxImage.Information);

                return;

            }



            WorkOrderButton.IsEnabled = false;

            try

            {

                var scope = await OwnerOrganizationScope.TryResolveAsync().ConfigureAwait(true);

                if (!scope.ok)

                {

                    MessageBox.Show(scope.error, "Заказ-наряд", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;

                }



                var repairId = _repairHistoryId.Value;

                var model = await DatabaseExecutor.WithDbAsync(db =>

                    ServiceBookingWorkOrderBuilder.BuildFromRepairAsync(db, repairId, scope.scope))

                    .ConfigureAwait(true);



                if (model == null)

                {

                    MessageBox.Show("Не удалось собрать данные для заказ-наряда.", "Заказ-наряд",

                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;

                }



                var workLines = TaskReportService.ToWorkOrderLines(_serviceLines.Select(v => v.ToRow()).ToList());

                if (workLines.Count > 0)

                    model.WorkLines = workLines;



                var path = RepairWorkOrderPrintService.GetDesktopOrderPath("Zakaz-naryad-vydacha");

                await Task.Run(() => RepairWorkOrderPrintService.GenerateFilled(model, path)).ConfigureAwait(true);

                RepairWorkOrderPrintService.OpenDocument(path);



                MessageBox.Show("Заказ-наряд для выдачи сохранён на рабочий стол:\n" + path,

                    "Заказ-наряд", MessageBoxButton.OK, MessageBoxImage.Information);

            }

            catch (Exception ex)

            {

                MessageBox.Show("Ошибка: " + ex.Message, "Заказ-наряд", MessageBoxButton.OK, MessageBoxImage.Warning);

            }

            finally

            {

                WorkOrderButton.IsEnabled = _repairHistoryId.HasValue;

            }

        }



        private static string BuildClientSummary(DriveCareDBEntities db, Guid? userId, TaskBookingExtra extra)

        {

            if (userId.HasValue)

            {

                var u = db.Users.AsNoTracking().FirstOrDefault(x => x.RowId == userId.Value);

                if (u != null)

                {

                    var login = string.IsNullOrWhiteSpace(u.Login) ? "—" : u.Login.Trim();

                    var phone = string.IsNullOrWhiteSpace(u.Phone) ? "—" : u.Phone.Trim();

                    var email = string.IsNullOrWhiteSpace(u.Email) ? "—" : u.Email.Trim();

                    return $"Логин: {login}\nТелефон: {phone}\nEmail: {email}";

                }

            }



            if (extra != null && (!string.IsNullOrWhiteSpace(extra.ClientName) ||

                                  !string.IsNullOrWhiteSpace(extra.ClientPhone)))

            {

                var name = string.IsNullOrWhiteSpace(extra.ClientName) ? "—" : extra.ClientName.Trim();

                var phone = string.IsNullOrWhiteSpace(extra.ClientPhone) ? "—" : extra.ClientPhone.Trim();

                var email = string.IsNullOrWhiteSpace(extra.ClientEmail) ? "—" : extra.ClientEmail.Trim();

                return $"Имя: {name}\nТелефон: {phone}\nEmail: {email}";

            }



            return "Не указано.";

        }



        private async void Complete_Click(object sender, RoutedEventArgs e)

        {

            var emp = AppState.CurrentEmployee;

            if (emp == null || !AppState.CanAccessEmployeeTasks)

                return;

            if (_isPurchaseTask || TaskPurchaseRequestService.LooksLikePurchaseTask(TitleText?.Text))
            {
                try
                {
                    var (fulfilled, error) = await TaskPurchaseRequestService.FulfillOnPurchaseCompleteAsync(
                        _taskId, emp.RowId).ConfigureAwait(true);

                    if (!fulfilled)
                    {
                        MessageBox.Show(error ?? "Не удалось завершить закупку.", "Закупка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    MessageBox.Show(
                        "Закупка отмечена выполненной. Детали добавлены в отчёт исходного задания.",
                        "Закупка", MessageBoxButton.OK, MessageBoxImage.Information);
                    AppState.Navigate(new ProHomePage());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка: " + ex.Message, "Закупка", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return;
            }

            var serviceRows = _serviceLines

                .Select(v => { v.Recalculate(); return v.ToRow(); })

                .Where(r => !string.IsNullOrWhiteSpace(r.ServiceName))

                .ToList();



            var partRows = _partLines

                .Select(v => { v.Recalculate(); return v.ToRow(); })

                .Where(r => !string.IsNullOrWhiteSpace(r.PartName))

                .ToList();



            var freeNote = (ReportTextBox.Text ?? string.Empty).Trim();

            var taskId = _taskId;



            try

            {

                var autoClosedCount = 0;

                var ok = await DatabaseExecutor.WithDbAsync(async db =>

                {

                    var task = await db.Tasks

                        .FirstOrDefaultAsync(t => t.RowId == taskId && t.EmployeeId == emp.RowId)

                        .ConfigureAwait(false);

                    if (task == null || task.IsCompleted)

                        return false;



                    await TaskReportService.SaveReportAsync(taskId, serviceRows, partRows, freeNote).ConfigureAwait(false);

                    task.IsCompleted = true;

                    task.EndDate = DateTime.Now;

                    var parentName = AppState.FormatEmployeeDisplayName(emp);
                    autoClosedCount = await TaskDelegationService.CompleteDescendantChainAsync(
                        db, taskId, parentName).ConfigureAwait(false);

                    await db.SaveChangesAsync().ConfigureAwait(false);

                    return true;

                }).ConfigureAwait(true);



                if (!ok)

                {

                    MessageBox.Show("Задание не найдено.", "Задание", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;

                }



                var links = await TaskDelegationService.TryLoadLinksAsync(taskId).ConfigureAwait(true);
                string msg;
                if (autoClosedCount > 0)
                    msg = $"Задание завершено. Автоматически закрыто поручений в цепочке: {autoClosedCount}.";
                else if (links.ParentTaskId.HasValue && links.DelegateTaskId.HasValue)
                    msg = "Ваш шаг выполнен. Ниже по цепочке ждут завершения; выше — увидят зелёную подсветку.";
                else if (links.ParentTaskId.HasValue)
                    msg = "Поручение выполнено. Предыдущему в цепочке задание подсветится зелёным.";
                else
                    msg = "Задание отмечено как выполненное.";

                MessageBox.Show(msg, "Задание", MessageBoxButton.OK, MessageBoxImage.Information);

                AppState.Navigate(new ProHomePage());

            }

            catch (Exception ex)

            {

                MessageBox.Show("Не удалось сохранить: " + ex.Message, "Задание", MessageBoxButton.OK, MessageBoxImage.Error);

            }

        }



        private sealed class TaskCardData

        {

            public string Title { get; set; }

            public string VisitReasonText { get; set; }

            public string SpecialNotesText { get; set; }

            public string CarInfoText { get; set; }

            public string ClientInfoText { get; set; }

            public string ReportText { get; set; }

            public bool IsCompleted { get; set; }

            public Guid? RepairHistoryId { get; set; }

            public bool HasCar { get; set; }

            public bool ShowCarSection { get; set; }

            public bool CanDelegate { get; set; }

            public string DelegationStatusText { get; set; }

            public string DelegationChainText { get; set; }
        }

    }

}

