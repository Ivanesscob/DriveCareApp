using DriveCareCore.Data.BD;
using DriveCareCore.Data.Services;

using DriveCarePro.Services;

using DriveCarePro.Services.RepairWorkOrder;
using DriveCarePro.Windows;

using DriveCarePro.Services.ServiceBooking;

using DriveCarePro.Services.ServiceDocuments;
using DriveCarePro.Services.WorkshopServices;

using System.ComponentModel;

using System.Text;

using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.Collections.Specialized;

using System.Data.Entity;

using System.Linq;

using System.Threading.Tasks;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

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

        private Guid _purchaseRequestId = Guid.Empty;

        private bool _suppressStockSync;

        private HashSet<Guid> _delegateSubtreePartIds = new HashSet<Guid>();

        /// <summary>Удалены из таблицы, но ещё не сохранены — показываем в списке локально (без возврата на склад).</summary>
        private readonly Dictionary<Guid, WorkshopPartItem> _sessionRemovedParts =
            new Dictionary<Guid, WorkshopPartItem>();

        private readonly bool _archiveView;

        private Guid? _rootTaskId;

        private Guid? _onlineBookingId;

        private bool _isLoadingTask;

        private bool _isPageBusy;

        private TaskReportSnapshot _reportSnapshot;

        private bool _reportChangeTrackingWired;

        private bool _evaluatingReportDirty;

        private string _reportNoteText = string.Empty;



        public EmployeeTaskCardPage(Guid taskId) : this(taskId, archiveView: false) { }



        public EmployeeTaskCardPage(Guid taskId, bool archiveView)

        {

            _taskId = taskId;

            _archiveView = archiveView;

            InitializeComponent();

            ServicesGrid.ItemsSource = _serviceLines;

            PartsGrid.ItemsSource = _partLines;

            PartsGrid.LoadingRow += PartsGrid_LoadingRow;

            _partLines.CollectionChanged += PartLines_CollectionChanged;

            _serviceLines.CollectionChanged += ServiceLines_CollectionChanged;

            Loaded += EmployeeTaskCardPage_Loaded;

            WireReportChangeTracking();

        }



        private async void EmployeeTaskCardPage_Loaded(object sender, RoutedEventArgs e)

        {

            Loaded -= EmployeeTaskCardPage_Loaded;

            await LoadTaskAsync().ConfigureAwait(true);

        }



        private async void Back_Click(object sender, RoutedEventArgs e)
        {
            if (!_archiveView && !_isCompleted && !_isPurchaseTask)
                await ReleaseSessionStockAsync().ConfigureAwait(true);

            AppState.Navigate(_archiveView ? (Page)new CompletedTasksPage() : new ProHomePage());
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_isPageBusy || !HasReportChanges())
                return;

            SetPageBusy(true, "Сохранение…");
            try
            {
                var (ok, error) = await SaveTaskReportAsync().ConfigureAwait(true);
                if (!ok)
                {
                    MessageBox.Show(error ?? "Не удалось сохранить задание.", "Сохранение",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var savedMsg = _isPurchaseTask
                    ? "Список закупки сохранён.\n\nМожно выйти и продолжить позже — нажмите «Сохранить» перед выходом."
                    : "Задание сохранено.\n\nУслуги, детали и комментарий записаны. Можно выйти и продолжить позже — нажмите «Сохранить» перед выходом, чтобы ничего не потерялось.";
                MessageBox.Show(savedMsg, "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                SetPageBusy(false);
            }
        }



        private async void Shop_Click(object sender, RoutedEventArgs e)
        {
            if (_workshopId == Guid.Empty)
            {
                MessageBox.Show("Не определена мастерская для каталога запчастей.", "Магазин",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await SaveTaskReportAsync(markPersisted: true).ConfigureAwait(true);

            TaskPartShopSession.Begin(_taskId, _workshopId);
            AppState.Navigate(new ProTaskShopPage(_taskId, _workshopId));
        }



        private void AddServiceLine_Click(object sender, RoutedEventArgs e) =>

            _serviceLines.Add(NewServiceLine());



        private void AddPartLine_Click(object sender, RoutedEventArgs e)
        {
            if (_partLines.Any(v => !v.IsPurchaseLine &&
                    (!v.WorkshopPartId.HasValue || v.WorkshopPartId.Value == Guid.Empty)))
        {
            MessageBox.Show(
                    "Сначала выберите деталь в текущей строке из списка.",
                    "Детали", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!HasStockInCatalog())
            {
                MessageBox.Show(
                    "На складе мастерской нет доступных деталей.\n\nОформите закупку через «Магазин».",
                    "Детали", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _partLines.Add(NewPartLine());
            PruneSessionRemovedParts();
            ScheduleRefreshPartComboDisplays();
        }

        private HashSet<Guid> CollectPickerPartIds()
        {
            var ids = new HashSet<Guid>();
            var inLines = new HashSet<Guid>();

            foreach (var line in _partLines.Where(v =>
                         !v.IsPurchaseLine && v.WorkshopPartId.HasValue && v.WorkshopPartId.Value != Guid.Empty))
            {
                ids.Add(line.WorkshopPartId.Value);
                inLines.Add(line.WorkshopPartId.Value);
            }

            if (_reportSnapshot != null)
            {
                foreach (var saved in _reportSnapshot.Parts)
                {
                    if (!saved.WorkshopPartId.HasValue || saved.WorkshopPartId.Value == Guid.Empty)
                        continue;
                    if (!inLines.Contains(saved.WorkshopPartId.Value))
                        ids.Add(saved.WorkshopPartId.Value);
                }
            }

            foreach (var id in _sessionRemovedParts.Keys)
                ids.Add(id);

            return ids;
        }

        private void RememberRemovedPartForPicker(TaskPartLineVm vm)
        {
            if (vm == null || vm.IsPurchaseLine)
                return;
            if (!vm.WorkshopPartId.HasValue || vm.WorkshopPartId.Value == Guid.Empty)
                return;

            var id = vm.WorkshopPartId.Value;
            var source = vm.SelectedCatalog ?? _partsCatalog?.FirstOrDefault(p => p.RowId == id);
            var clone = CloneWorkshopPartItem(source ?? CreateWorkshopPartItemFromLine(vm));
            var localQty = clone.QuantityOnHand + vm.LoadedQuantity;
            if (localQty <= 0 && vm.Quantity > 0)
                localQty = vm.Quantity;
            if (localQty <= 0 && vm.LoadedQuantity > 0)
                localQty = vm.LoadedQuantity;
            if (localQty <= 0)
                localQty = 1;
            clone.QuantityOnHand = localQty;
            _sessionRemovedParts[id] = clone;
        }

        private void PruneSessionRemovedParts()
        {
            foreach (var line in _partLines.Where(v =>
                         !v.IsPurchaseLine && v.WorkshopPartId.HasValue && v.WorkshopPartId.Value != Guid.Empty))
            {
                _sessionRemovedParts.Remove(line.WorkshopPartId.Value);
            }
        }

        private void ClearSessionRemovedParts() => _sessionRemovedParts.Clear();

        private static WorkshopPartItem CloneWorkshopPartItem(WorkshopPartItem src) =>
            new WorkshopPartItem
            {
                RowId = src.RowId,
                WorkshopId = src.WorkshopId,
                Name = src.Name,
                Article = src.Article,
                Description = src.Description,
                Price = src.Price,
                UnitName = src.UnitName ?? "шт.",
                QuantityOnHand = src.QuantityOnHand,
                Category = src.Category,
                IsActive = src.IsActive
            };

        private static WorkshopPartItem CreateWorkshopPartItemFromLine(TaskPartLineVm vm) =>
            new WorkshopPartItem
            {
                RowId = vm.WorkshopPartId ?? Guid.Empty,
                WorkshopId = Guid.Empty,
                Name = vm.PartName,
                UnitName = vm.UnitName ?? "шт.",
                Price = vm.UnitPrice,
                QuantityOnHand = 0,
                IsActive = true
            };

        private List<WorkshopPartItem> MergeSessionRemovedIntoCatalog(IList<WorkshopPartItem> catalog)
        {
            var list = catalog == null ? new List<WorkshopPartItem>() : catalog.ToList();
            foreach (var kv in _sessionRemovedParts)
            {
                var idx = list.FindIndex(p => p.RowId == kv.Key);
                if (idx >= 0)
                    list[idx] = kv.Value;
                else
                    list.Add(kv.Value);
            }

            return list.OrderBy(p => p.Name ?? string.Empty).ToList();
        }

        private bool HasStockInCatalog()
        {
            if (_sessionRemovedParts.Count > 0)
                return true;

            if (_partsCatalog == null || _partsCatalog.Count == 0)
                return false;

            if (_partsCatalog.Any(p => p.QuantityOnHand > 0))
                return true;

            var include = CollectPickerPartIds();
            return include.Count > 0 && _partsCatalog.Any(p => include.Contains(p.RowId));
        }



        private void RemoveServiceLine_Click(object sender, RoutedEventArgs e)
        {
            if (ServicesGrid.SelectedItem is TaskServiceLineVm selected)
                _serviceLines.Remove(selected);
            else if (_serviceLines.Count > 0)
                _serviceLines.RemoveAt(_serviceLines.Count - 1);
        }



        private async void RemovePartLine_Click(object sender, RoutedEventArgs e)
        {
            TaskPartLineVm vm = null;
            if (PartsGrid.SelectedItem is TaskPartLineVm selected)
                vm = selected;
            else if (_partLines.Count > 0)
                vm = _partLines[_partLines.Count - 1];

            if (vm == null)
                return;

            if (!_isPurchaseTask && !_archiveView && !_isCompleted)
            {
                RememberRemovedPartForPicker(vm);
                _suppressStockSync = true;
                _partLines.Remove(vm);
                _suppressStockSync = false;
                PruneSessionRemovedParts();
                await RefreshPartsCatalogAsync().ConfigureAwait(true);
                RefreshPartsUiState();
                OnReportContentChanged();
                ScheduleRefreshPartComboDisplays();
                return;
            }

            _partLines.Remove(vm);
            OnReportContentChanged();
        }

        private void PartLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (TaskPartLineVm vm in e.NewItems)
                    vm.PropertyChanged += ReportLine_PropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (TaskPartLineVm vm in e.OldItems)
                {
                    vm.PropertyChanged -= ReportLine_PropertyChanged;
                    vm.SuppressComboBindingUpdates = true;
                }
            }

            OnReportContentChanged();
        }

        private void ScheduleRefreshPartComboDisplays()
        {
            Dispatcher.BeginInvoke(new Action(RefreshLineComboDisplays), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ServiceLines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (TaskServiceLineVm vm in e.NewItems)
                    vm.PropertyChanged += ReportLine_PropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (TaskServiceLineVm vm in e.OldItems)
                    vm.PropertyChanged -= ReportLine_PropertyChanged;
            }

            OnReportContentChanged();
        }

        private void ReportTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _reportNoteText = (ReportTextBox.Text ?? string.Empty).Trim();
            OnReportContentChanged();
        }

        private void ReportLine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isLoadingTask || _suppressStockSync || _evaluatingReportDirty)
                return;

            if (e.PropertyName == nameof(TaskServiceLineVm.LineAmount)
                || e.PropertyName == nameof(TaskPartLineVm.LineAmount)
                || e.PropertyName == nameof(TaskPartLineVm.FilteredCatalog)
                || e.PropertyName == nameof(TaskPartLineVm.StockOnHand))
                return;

            OnReportContentChanged();
        }

        private void OnReportContentChanged()
        {
            if (_isLoadingTask || _suppressStockSync || _evaluatingReportDirty)
                return;

            UpdateSaveButtonState();
        }

        private void WireReportChangeTracking()
        {
            if (_reportChangeTrackingWired)
                return;

            _reportChangeTrackingWired = true;
            foreach (var vm in _serviceLines)
                vm.PropertyChanged += ReportLine_PropertyChanged;
            foreach (var vm in _partLines)
                vm.PropertyChanged += ReportLine_PropertyChanged;
        }

        private void DetachAllReportLineHandlers()
        {
            foreach (var vm in _serviceLines)
                vm.PropertyChanged -= ReportLine_PropertyChanged;
            foreach (var vm in _partLines)
                vm.PropertyChanged -= ReportLine_PropertyChanged;
        }

        private void SubscribeAllReportLines()
        {
            foreach (var vm in _serviceLines)
                vm.PropertyChanged += ReportLine_PropertyChanged;
            foreach (var vm in _partLines)
                vm.PropertyChanged += ReportLine_PropertyChanged;
        }

        private TaskReportSnapshot BuildCurrentSnapshot() =>
            TaskReportSnapshot.Capture(_reportNoteText, _serviceLines, _partLines);

        private void CaptureReportSnapshot() =>
            _reportSnapshot = BuildCurrentSnapshot();

        private bool HasReportChanges()
        {
            if (_evaluatingReportDirty)
                return false;

            _evaluatingReportDirty = true;
            try
            {
                return _reportSnapshot == null || !_reportSnapshot.Equals(BuildCurrentSnapshot());
            }
            finally
            {
                _evaluatingReportDirty = false;
            }
        }

        private void UpdateSaveButtonState()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateSaveButtonState));
                return;
            }

            if (SaveReportButton.Visibility != Visibility.Visible)
                return;

            var dirty = HasReportChanges();
            var canSave = dirty && !_isPageBusy;
            SaveReportButton.IsEnabled = canSave;
            SaveReportButton.Style = (Style)FindResource(dirty ? "App.Button.Primary" : "App.Button.Outline");
            SaveReportButton.Opacity = dirty ? 1.0 : 0.55;
        }

        private void UpdateViewDocumentButtonsState()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateViewDocumentButtonsState));
                return;
            }

            if (_isPurchaseTask)
                return;

            var visible = ViewDocumentButton.Visibility == Visibility.Visible;
            var enabled = visible && !_isPageBusy;
            ViewDocumentButton.IsEnabled = enabled;
            ViewDocumentToolbarButton.IsEnabled = enabled;
        }

        private void SetPageBusy(bool busy, string message = null)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => SetPageBusy(busy, message)));
                return;
            }

            _isPageBusy = busy;
            if (PageBusyOverlay != null)
                PageBusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (BusyOverlayText != null && !string.IsNullOrWhiteSpace(message))
                BusyOverlayText.Text = message;
            if (MainContentGrid != null)
                MainContentGrid.IsEnabled = !busy;
            UpdateSaveButtonState();
            UpdateViewDocumentButtonsState();
        }

        private void RefreshPartsUiState()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(RefreshPartsUiState));
                return;
            }

            var active = !_isCompleted && !_archiveView && !_isPurchaseTask;
            AddPartLineButton.IsEnabled = active && HasStockInCatalog();
        }

        private async Task<(bool ok, string error)> ApplyStockOnSaveAsync()
        {
            if (_workshopId == Guid.Empty || _isPurchaseTask)
                return (true, null);

            var currentPartIds = new HashSet<Guid>(
                _partLines
                    .Where(v => !v.IsPurchaseLine && v.WorkshopPartId.HasValue && v.WorkshopPartId.Value != Guid.Empty)
                    .Select(v => v.WorkshopPartId.Value));

            if (_reportSnapshot != null)
            {
                foreach (var saved in _reportSnapshot.Parts)
                {
                    if (!saved.WorkshopPartId.HasValue || saved.WorkshopPartId.Value == Guid.Empty)
                        continue;
                    if (currentPartIds.Contains(saved.WorkshopPartId.Value))
                        continue;
                    if (_delegateSubtreePartIds.Contains(saved.WorkshopPartId.Value))
                        continue;

                    var returnRow = new TaskPartLineRow
                    {
                        WorkshopPartId = saved.WorkshopPartId,
                        PartName = saved.PartName,
                        Quantity = saved.Quantity,
                        UnitName = saved.UnitName
                    };

                    var (returnOk, returnErr) = await DatabaseExecutor.WithDbAsync(db =>
                        WorkshopStockService.ReturnStockAsync(db, _workshopId, new[] { returnRow }))
                        .ConfigureAwait(true);
                    if (!returnOk)
                        return (false, returnErr);
                }
            }

            foreach (var line in _partLines.Where(v => !v.IsPurchaseLine))
            {
                if (string.IsNullOrWhiteSpace(line.PartName))
                    continue;
                if (!line.WorkshopPartId.HasValue || line.WorkshopPartId.Value == Guid.Empty)
                    continue;
                if (_delegateSubtreePartIds.Contains(line.WorkshopPartId.Value))
                    continue;

                var (ok, error) = await WorkshopTaskPartStockSync.SyncLineAsync(_workshopId, line)
                    .ConfigureAwait(true);
                if (!ok)
                    return (false, error);
            }

            return (true, null);
        }

        private async Task<(bool ok, string error)> SaveTaskReportAsync(bool markPersisted = true, bool applyStock = true)
        {
            if (_archiveView || _isCompleted)
                return (false, "Сохранение недоступно для этого задания.");

            if (_isPurchaseTask)
                return await SavePurchaseRequestAsync(markPersisted).ConfigureAwait(true);

            foreach (var line in _partLines.Where(v => !v.IsPurchaseLine))
            {
                if (string.IsNullOrWhiteSpace(line.PartName))
                    continue;

                if (!line.WorkshopPartId.HasValue || line.WorkshopPartId.Value == Guid.Empty)
                    return (false, "Есть строка с деталью без выбора со склада. Выберите деталь в списке или удалите пустую строку.");
            }

            if (applyStock)
            {
                var (stockOk, stockError) = await ApplyStockOnSaveAsync().ConfigureAwait(true);
                if (!stockOk)
                    return (false, stockError ?? "Не удалось списать детали со склада.");
            }

            var serviceRows = _serviceLines
                .Select(v => { v.Recalculate(); return v.ToRow(); })
                .Where(r => !string.IsNullOrWhiteSpace(r.ServiceName))
                .ToList();

            var partRows = new List<TaskPartLineRow>();
            foreach (var line in _partLines)
            {
                if (line.IsPurchaseLine || string.IsNullOrWhiteSpace(line.PartName))
                    continue;
                if (!line.WorkshopPartId.HasValue || line.WorkshopPartId.Value == Guid.Empty)
                    continue;
                try
                {
                    line.Recalculate();
                    partRows.Add(line.ToRow());
                }
                catch (InvalidOperationException ex)
                {
                    return (false, ex.Message);
                }
            }

            var freeNote = _reportNoteText;
            try
            {
                await TaskReportService.SaveReportAsync(_taskId, serviceRows, partRows, freeNote)
                    .ConfigureAwait(true);

                var docId = await ServiceDocumentService.TryGetDocumentIdForTaskAsync(_taskId).ConfigureAwait(true);
                if (docId.HasValue)
                    await ServiceDocumentService.SyncDocumentFromChainAsync(_taskId).ConfigureAwait(true);
                else
                {
                    var links = await TaskDelegationService.TryLoadLinksAsync(_taskId).ConfigureAwait(true);
                    if (links.ParentTaskId.HasValue)
                        await TaskDelegationService.SyncPartLinesToAncestorsAsync(_taskId).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }

            if (markPersisted)
            {
                ClearSessionRemovedParts();
                await RefreshPartsCatalogAsync().ConfigureAwait(true);

                foreach (var line in _partLines.Where(v => !v.IsPurchaseLine))
                    line.MarkAsPersistedToDatabase();

                CaptureReportSnapshot();
                UpdateSaveButtonState();
            }

            return (true, null);
        }

        private Task ReleaseSessionStockAsync() => ReleaseAllPartReservationsAsync();

        private async Task ReleaseAllPartReservationsAsync()
        {
            if (_isPurchaseTask || _archiveView || _workshopId == Guid.Empty)
                return;

            foreach (var vm in _partLines.Where(v => !v.IsPurchaseLine && v.ReservedQuantity > 0).ToList())
            {
                var (ok, error) = await WorkshopTaskPartStockSync.ReturnSessionDeltaAsync(_workshopId, vm)
                    .ConfigureAwait(true);
                if (!ok)
                {
                    MessageBox.Show(error ?? "Не удалось вернуть детали на склад.", "Склад",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                }
            }

            await RefreshPartsCatalogAsync().ConfigureAwait(true);
        }

        private void InitializePartReservationsFromSaved()
        {
            foreach (var vm in _partLines.Where(v =>
                         !v.IsPurchaseLine &&
                         v.WorkshopPartId.HasValue &&
                         v.WorkshopPartId.Value != Guid.Empty &&
                         v.Quantity > 0))
            {
                vm.SetReservation(vm.WorkshopPartId.Value, vm.LoadedQuantity, inCurrentSession: false);
            }
        }

        private async Task<(bool ok, string error)> SavePurchaseRequestAsync(bool markPersisted)
        {
            if (_purchaseRequestId == Guid.Empty)
                return (false, "Не найден запрос на закупку.");

            var rows = new List<TaskPartLineRow>();
            foreach (var line in _partLines.Where(v => v.IsPurchaseLine))
            {
                if (string.IsNullOrWhiteSpace(line.PartName))
                    continue;
                line.Recalculate();
                rows.Add(line.ToSnapshotRow());
            }

            if (rows.Count == 0)
                return (false, "Нет позиций для сохранения.");

            var (ok, error) = await TaskPurchaseRequestService.SaveRequestLinesAsync(_purchaseRequestId, rows)
                .ConfigureAwait(true);
            if (!ok)
                return (false, error);

            if (markPersisted)
            {
                CaptureReportSnapshot();
                UpdateSaveButtonState();
            }

            return (true, null);
        }

        private async Task RefreshPartsCatalogAsync()
        {
            if (_workshopId == Guid.Empty)
                return;

            PruneSessionRemovedParts();
            var includeIds = CollectPickerPartIds();
            var fromDb = await WorkshopPartCatalogService.ListPickerForWorkshopAsync(_workshopId, includeIds)
                .ConfigureAwait(true);
            _partsCatalog = MergeSessionRemovedIntoCatalog(fromDb);

            _suppressStockSync = true;
            try
            {
                foreach (var vm in _partLines.Where(v => !v.IsPurchaseLine))
                    vm.AttachCatalog(_partsCatalog, includeIds);
            }
            finally
            {
                _suppressStockSync = false;
            }

            ScheduleRefreshPartComboDisplays();
        }



        private TaskServiceLineVm NewServiceLine() =>

            new TaskServiceLineVm { Catalog = _catalog };

        private TaskPartLineVm NewPartLine() =>
            new TaskPartLineVm
            {
                Catalog = _partsCatalog,
                PickerIncludeZeroStockIds = CollectPickerPartIds()
            };

        private void ConfigurePartsGridColumns(bool purchaseList, bool lineFieldsEditable)
        {
            PartNameReadColumn.Visibility = purchaseList ? Visibility.Visible : Visibility.Collapsed;
            PartNamePickColumn.Visibility = purchaseList ? Visibility.Collapsed : Visibility.Visible;
            PartNamePickColumn.Header = purchaseList ? "Наименование" : "Наименование (поиск)";

            PartQtyColumn.IsReadOnly = !lineFieldsEditable;
            PartUnitColumn.IsReadOnly = !lineFieldsEditable;
            PartPriceColumn.IsReadOnly = !lineFieldsEditable;
        }

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

                if (_workshopId != Guid.Empty)
                    _catalog = await WorkshopServiceCatalogService.ListForWorkshopAsync(_workshopId).ConfigureAwait(true);

                var purchaseRequest = await TaskPurchaseRequestService.TryLoadByPurchaseTaskAsync(taskId).ConfigureAwait(true);
                var purchaseStatus = purchaseRequest == null
                    ? await TaskPurchaseRequestService.TryLoadStatusForSourceAsync(taskId).ConfigureAwait(true)
                    : null;

                _delegateSubtreePartIds = await TaskDelegationService
                    .GetWorkshopPartIdsInDelegateSubtreeAsync(taskId).ConfigureAwait(true);

                ApplyTaskData(data, serviceRows, partRows, purchaseRequest, purchaseStatus);

                await UpdateOnlineBookingNoShowUiAsync(taskId).ConfigureAwait(true);
                await LoadDocumentAsync(taskId).ConfigureAwait(true);
                await UpdateRootTaskNavigationAsync(taskId).ConfigureAwait(true);

                ClearSessionRemovedParts();
                InitializePartReservationsFromSaved();
                await RefreshPartsCatalogAsync().ConfigureAwait(true);
                RefreshPartsUiState();

                CaptureReportSnapshot();
                UpdateSaveButtonState();
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
            var isRepairOrPainting = IsRepairOrPaintingKind(serviceKind)
                || (hasCar && IsRepairOrPaintingKind(task.Title));

            var repairHistoryId = await TaskRepairLinkResolver.ResolveRepairHistoryIdAsync(
                db, taskId, task.CarId, extra?.RepairHistoryId).ConfigureAwait(false);

            return new TaskCardData
            {
                Title = string.IsNullOrWhiteSpace(task.Title) ? "Задание" : task.Title.Trim(),
                VisitReasonText = string.IsNullOrWhiteSpace(visitReason) ? "Описание не указано." : visitReason.Trim(),
                SpecialNotesText = string.IsNullOrWhiteSpace(extra?.SpecialNotes) ? "—" : extra.SpecialNotes.Trim(),
                CarInfoText = hasCar ? CarDisplayHelper.FormatCarById(db, task.CarId) : "Не указано.",
                ClientInfoText = BuildClientSummary(db, task.ClientUserId, extra),
                ReportText = task.ReportText ?? string.Empty,
                IsCompleted = task.IsCompleted,
                RepairHistoryId = repairHistoryId,
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
            _isLoadingTask = true;

            _suppressStockSync = true;

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



            DetachAllReportLineHandlers();

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
                _purchaseRequestId = purchaseRequest.RequestId;
                foreach (var row in purchaseRequest.Lines)
                    _partLines.Add(TaskPartLineVm.FromPurchaseRow(row));
            }
            else
            {
                _purchaseRequestId = Guid.Empty;
                if (partRows.Count > 0)
                {
                    foreach (var row in partRows)
                    {
                        var vm = TaskPartLineVm.FromRow(row, _partsCatalog);
                        vm.Catalog = _partsCatalog;
                        _partLines.Add(vm);
                        vm.ReattachSelectedCatalog(_partsCatalog);
                    }
                }
            }

            _reportNoteText = _isPurchaseTask
                ? string.Empty
                : ExtractFreeTextNote(data.ReportText, serviceRows, partRows).Trim();
            ReportTextBox.Text = _reportNoteText;

            ReportTextBox.IsReadOnly = _isCompleted;



            var active = !data.IsCompleted && !_archiveView && !_isPurchaseTask;

            if (_isPurchaseTask)
            {
                ArchiveHintText.Visibility = Visibility.Visible;
                ArchiveHintText.Text = "Купите позиции из списка ниже и нажмите «Завершить задание» — детали поступят на склад и в задание заказчика.";
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

            var showWorkOrder = _repairHistoryId.HasValue && !_isPurchaseTask;
            WorkOrderButton.Visibility = showWorkOrder ? Visibility.Visible : Visibility.Collapsed;
            WorkOrderButton.IsEnabled = showWorkOrder;
            if (!showWorkOrder && !_isPurchaseTask)
                WorkOrderButton.ToolTip = "Заказ-наряд доступен для заданий с записью на ремонт/покраску (нужна связь с RepairHistory в БД).";

            var reportEditable = !data.IsCompleted && !_archiveView;
            SaveReportButton.Visibility = reportEditable ? Visibility.Visible : Visibility.Collapsed;
            SaveReportButton.ToolTip = _isPurchaseTask
                ? "Сохранить количество и цены в списке закупки."
                : "Записать услуги, детали и комментарий. Нажмите перед выходом, чтобы продолжить задание позже.";

            ShopButton.Visibility = (_isPurchaseTask || _archiveView) ? Visibility.Collapsed : Visibility.Visible;
            ShopButton.IsEnabled = active;

            var canCompletePurchase = _isPurchaseTask && !data.IsCompleted && !_archiveView;
            CompleteButton.IsEnabled = active || canCompletePurchase;
            CompleteButton.Content = "Завершить задание";
            CompletionTitleText.Text = _isPurchaseTask ? "Завершение задания" : "Завершение ремонта";

            AddServiceLineButton.IsEnabled = active;

            RemoveServiceLineButton.IsEnabled = active;

            RemovePartLineButton.IsEnabled = active;

            ServicesGrid.IsReadOnly = !active;
            ServicesGrid.Visibility = _isPurchaseTask ? Visibility.Collapsed : Visibility.Visible;
            AddServiceLineButton.Visibility = _isPurchaseTask ? Visibility.Collapsed : Visibility.Visible;
            RemoveServiceLineButton.Visibility = _isPurchaseTask ? Visibility.Collapsed : Visibility.Visible;

            ConfigurePartsGridColumns(_isPurchaseTask, reportEditable);
            if (_isPurchaseTask)
            {
                PartsSectionTitleText.Text = "Список к закупке";
                PartsSectionHintText.Text = "Позиции из запроса. После «Завершить задание» они поступят на склад и в отчёт исходного задания.";
            }
            else
            {
                PartsSectionTitleText.Text = "Детали / запчасти";
                PartsSectionHintText.Text =
                    "Выберите деталь со склада мастерской (остаток больше 0). Списание — по кнопке «Сохранить». Если на складе пусто — «Магазин».";
            }

            PartsGrid.IsReadOnly = _isPurchaseTask || !active;
            AddPartLineButton.Visibility = (_isPurchaseTask || !active) ? Visibility.Collapsed : Visibility.Visible;
            RefreshPartsUiState();
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

            _suppressStockSync = false;

            _isLoadingTask = false;

            CaptureReportSnapshot();
            UpdateSaveButtonState();
            UpdateViewDocumentButtonsState();

            Dispatcher.BeginInvoke(new Action(RefreshLineComboDisplays), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private async Task UpdateOnlineBookingNoShowUiAsync(Guid taskId)
        {
            _onlineBookingId = null;
            ClientNoShowButton.Visibility = Visibility.Collapsed;
            OnlineBookingHintText.Visibility = Visibility.Collapsed;

            if (_archiveView || _isCompleted || _isPurchaseTask)
                return;

            var bookingId = await WorkshopOnlineBookingAcceptanceService.TryFindConfirmedBookingIdByTaskAsync(taskId)
                .ConfigureAwait(true);
            if (!bookingId.HasValue || bookingId.Value == Guid.Empty)
                return;

            _onlineBookingId = bookingId;
            ClientNoShowButton.Visibility = Visibility.Visible;
            OnlineBookingHintText.Visibility = Visibility.Visible;
        }

        private async void ClientNoShow_Click(object sender, RoutedEventArgs e)
        {
            var emp = AppState.CurrentEmployee;
            if (emp == null || !_onlineBookingId.HasValue || _isPageBusy)
                return;

            if (MessageBox.Show(
                    "Клиент не пришёл на приём?\n\nЗадание будет удалено, онлайн-запись получит статус «Клиент не явился».",
                    "Задание",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            SetPageBusy(true, "Отмечаем неявку…");
            try
            {
                var result = await WorkshopOnlineBookingAcceptanceService.MarkClientNoShowByTaskAsync(
                    _taskId, emp.RowId).ConfigureAwait(true);

                if (result == null || !result.Ok)
                {
                    MessageBox.Show(result?.Error ?? "Не удалось отметить неявку.", "Задание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(result.ChatWarning))
                {
                    MessageBox.Show(
                        "Неявка зафиксирована, но сообщение в чат не отправлено:\n" + result.ChatWarning,
                        "Задание", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                AppState.Navigate(new ProHomePage());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Задание", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetPageBusy(false);
            }
        }

        private void RefreshLineComboDisplays()
        {
            foreach (var vm in _serviceLines)
                vm.PushComboDisplayToView();

            foreach (var vm in _partLines.Where(v => !v.IsPurchaseLine))
            {
                vm.SuppressComboBindingUpdates = false;
                vm.RestoreCatalogSelection();
            }

            RefreshPartsGridComboBoxes();
        }

        private void RefreshPartsGridComboBoxes()
        {
            if (PartsGrid == null)
                return;

            foreach (var item in _partLines.Where(v => !v.IsPurchaseLine))
            {
                var row = PartsGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
            if (row == null)
                    continue;

                var combo = FindVisualChild<ComboBox>(row);
                if (combo != null)
                    ApplyComboLineDisplay(combo);
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;

                var nested = FindVisualChild<T>(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private void TaskLineComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is ComboBox combo))
                return;

            if (combo.DataContext is TaskPartLineVm partLine)
                partLine.SuppressComboBindingUpdates = false;

            combo.Unloaded -= TaskLineComboBox_Unloaded;
            combo.Unloaded += TaskLineComboBox_Unloaded;
            combo.DataContextChanged -= TaskLineComboBox_DataContextChanged;
            combo.DataContextChanged += TaskLineComboBox_DataContextChanged;
            ApplyComboLineDisplay(combo);
        }

        private void TaskLineComboBox_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is ComboBox combo && combo.DataContext is TaskPartLineVm partLine)
                partLine.SuppressComboBindingUpdates = true;
        }

        private void PartsGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (!(e.Row.Item is TaskPartLineVm partLine) || partLine.IsPurchaseLine)
                return;

            partLine.SuppressComboBindingUpdates = false;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Row.Item != partLine)
                    return;

                var combo = FindVisualChild<ComboBox>(e.Row);
                if (combo != null)
                    ApplyComboLineDisplay(combo);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void TaskLineComboBox_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is ComboBox combo)
                ApplyComboLineDisplay(combo);
        }

        private static void ApplyComboLineDisplay(ComboBox combo)
        {
            if (combo.DataContext is TaskServiceLineVm serviceLine)
            {
                serviceLine.PushComboDisplayToView();
                if (serviceLine.SelectedCatalog != null)
                    combo.SelectedItem = serviceLine.SelectedCatalog;
                var text = serviceLine.ServiceSearchText;
                if (!string.IsNullOrEmpty(text))
                    combo.Text = text;
                return;
            }

            if (combo.DataContext is TaskPartLineVm partLine && !partLine.IsPurchaseLine)
            {
                partLine.RestoreCatalogSelection();
                if (partLine.SelectedCatalog != null)
                    combo.SelectedItem = partLine.SelectedCatalog;
                var text = partLine.PartSearchText;
                if (string.IsNullOrEmpty(text))
                    text = partLine.PartName;
                if (!string.IsNullOrEmpty(text))
                    combo.Text = text;
            }
        }

        private async Task LoadDocumentAsync(Guid taskId)
        {
            if (_isPurchaseTask || _archiveView)
            {
                DocumentSection.Visibility = Visibility.Collapsed;
                ViewDocumentButton.Visibility = Visibility.Collapsed;
                ViewDocumentToolbarButton.Visibility = Visibility.Collapsed;
                return;
            }

            DocumentSection.Visibility = Visibility.Visible;
            ViewDocumentButton.Visibility = Visibility.Visible;
            ViewDocumentToolbarButton.Visibility = Visibility.Visible;

            var docInfo = await ServiceDocumentService.TryLoadInfoAsync(taskId).ConfigureAwait(true);
            if (docInfo != null)
            {
                DocumentTitleText.Text = docInfo.Title;
                var statusLine = docInfo.StatusDisplay;
                if (docInfo.IsCurrentTaskRoot)
                    statusLine += " · Вы на корневом задании — после вашего завершения документ будет закрыт.";
                else if (docInfo.Status == ServiceDocumentStatus.Open)
                    statusLine += " · Документ закроется, когда инициатор завершит своё задание.";
                DocumentStatusText.Text = statusLine;
            }
            else
            {
                DocumentTitleText.Text = "Сводка по цепочке заданий";
                DocumentStatusText.Text =
                    "Общий заказ-наряд не привязан к этому заданию. «Посмотреть документ» покажет услуги и детали по всей цепочке поручений.";
            }

            UpdateViewDocumentButtonsState();
        }

        private async Task UpdateRootTaskNavigationAsync(Guid taskId)
        {
            if (_archiveView || _isPurchaseTask)
            {
                _rootTaskId = null;
                OpenRootTaskButton.Visibility = Visibility.Collapsed;
                return;
            }

            var rootId = await TaskDelegationService.FindRootTaskIdAsync(taskId).ConfigureAwait(true);
            var show = rootId != Guid.Empty && rootId != taskId;
            _rootTaskId = show ? rootId : (Guid?)null;
            OpenRootTaskButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenRootTask_Click(object sender, RoutedEventArgs e)
        {
            if (_isPageBusy || !_rootTaskId.HasValue)
                return;

            AppState.Navigate(new EmployeeTaskCardPage(_rootTaskId.Value));
        }

        private async void ViewDocument_Click(object sender, RoutedEventArgs e)
        {
            if (_isPurchaseTask || _isPageBusy)
                return;

            SetPageBusy(true, "Открытие документа…");
            try
            {
                if (!_archiveView && !_isCompleted && HasReportChanges())
                {
                    var (saved, saveError) = await SaveTaskReportAsync(markPersisted: false, applyStock: false)
                        .ConfigureAwait(true);
                    if (!saved)
                    {
                        MessageBox.Show(saveError ?? "Сначала исправьте строки задания.", "Документ",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var preview = await ServiceDocumentService.TryLoadPreviewAsync(_taskId, syncFromChainFirst: true)
                    .ConfigureAwait(true);
                if (preview == null)
                {
                    MessageBox.Show(
                        "Не удалось собрать сводку по заданию.\n\nПроверьте цепочку поручений или выполните SQL ServiceDocuments_Tables.sql для заказ-наряда.",
                        "Документ", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ServiceDocumentPreviewWindow.Show(Window.GetWindow(this), preview);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть документ: " + ex.Message, "Документ",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                SetPageBusy(false);
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
            win.ShowDialog();
            if (win.Delegated)
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



        private bool CanGenerateWorkOrderDocx => !_isPurchaseTask && _repairHistoryId.HasValue;

        /// <summary>Собирает DOCX заказ-наряда; при openDocument — открывает без падения приложения.</summary>
        private async Task<(bool success, string savedPath, string error)> TryBuildAndSaveWorkOrderDocxAsync(bool openDocument)
        {
            if (!_repairHistoryId.HasValue)
                return (false, null, "Нет связи задания с записью ремонта (RepairHistoryId).");

            var scope = await OwnerOrganizationScope.TryResolveAsync().ConfigureAwait(true);
            if (!scope.ok)
                return (false, null, scope.error);

            var repairId = _repairHistoryId.Value;
            var model = await DatabaseExecutor.WithDbAsync(db =>
                    ServiceBookingWorkOrderBuilder.BuildFromRepairAsync(db, repairId, scope.scope))
                .ConfigureAwait(true);

            if (model == null)
                return (false, null, "Не удалось собрать данные для заказ-наряда.");

            var workLines = TaskReportService.ToWorkOrderLines(_serviceLines.Select(v => v.ToRow()).ToList());
            if (workLines.Count > 0)
                model.WorkLines = workLines;

            var partRows = _partLines
                .Where(v => !v.IsPurchaseLine)
                .Select(v => { v.Recalculate(); return v.ToRow(); })
                .Where(r => !string.IsNullOrWhiteSpace(r.PartName))
                .ToList();
            if (partRows.Count == 0)
                partRows = await TaskReportService.LoadPartLinesAsync(_taskId).ConfigureAwait(true);
            ServiceBookingWorkOrderBuilder.ApplyPartLinesToModel(model, partRows);

            var preferredPath = RepairWorkOrderPrintService.GetDesktopOrderPath("Zakaz-naryad-vydacha");
            var (genOk, savedPath, genError) = await Task.Run(() =>
                    RepairWorkOrderPrintService.TryGenerateFilled(model, preferredPath))
                .ConfigureAwait(true);

            if (!genOk)
                return (false, null, genError);

            if (!openDocument)
                return (true, savedPath, null);

            if (RepairWorkOrderPrintService.TryOpenDocument(savedPath, out var openError))
                return (true, savedPath, null);

            return (true, savedPath, openError);
        }

        private async void WorkOrder_Click(object sender, RoutedEventArgs e)
        {
            if (!_repairHistoryId.HasValue)
            {
                MessageBox.Show(
                    "Нет связи задания с записью ремонта (RepairHistoryId).\n\n" +
                    "Обычно поле заполняется при «Записать машину на ремонт/покраску».\n" +
                    "Если задание старое — выполните SQL Tasks_Add_ServiceBookingFields.sql и пересоздайте запись, " +
                    "либо откройте корневое задание цепочки.",
                    "Заказ-наряд",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            WorkOrderButton.IsEnabled = false;
            try
            {
                var (ok, path, error) = await TryBuildAndSaveWorkOrderDocxAsync(openDocument: true).ConfigureAwait(true);
                if (!ok)
                {
                    MessageBox.Show(error ?? "Не удалось сформировать заказ-наряд.", "Заказ-наряд",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    MessageBox.Show(
                        error + "\n\nФайл сохранён на рабочем столе:\n" + path,
                        "Заказ-наряд", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

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
                        MessageBox.Show(error ?? "Не удалось завершить задание.", "Задание",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
                    }

                    MessageBox.Show(
                        "Задание завершено. Детали приняты на склад и добавлены в отчёт исходного задания.",
                        "Задание", MessageBoxButton.OK, MessageBoxImage.Information);
                    AppState.Navigate(new ProHomePage());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка: " + ex.Message, "Задание", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                return;
            }

            var serviceRows = _serviceLines

                .Select(v => { v.Recalculate(); return v.ToRow(); })

                .Where(r => !string.IsNullOrWhiteSpace(r.ServiceName))

                .ToList();



            List<TaskPartLineRow> partRows;
            try
            {
                partRows = _partLines
                    .Select(v => { v.Recalculate(); return v.ToRow(); })
                    .Where(r => !string.IsNullOrWhiteSpace(r.PartName))
                    .ToList();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "Склад", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }



            var freeNote = _reportNoteText;

            var taskId = _taskId;
            var taskLinks = await TaskDelegationService.TryLoadLinksAsync(taskId).ConfigureAwait(true);

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

                    using (var tx = db.Database.BeginTransaction())
                    {
                        try
                        {
                            await TaskReportService.SaveReportAsync(db, taskId, serviceRows, partRows, freeNote)
                                .ConfigureAwait(false);

                            var docId = await ServiceDocumentService.TryGetDocumentIdForTaskAsync(db, taskId)
                                .ConfigureAwait(false);
                            ServiceDocumentInfo docInfo = null;
                            if (docId.HasValue)
                            {
                                await ServiceDocumentService.SyncDocumentFromChainAsync(db, taskId)
                                    .ConfigureAwait(false);
                                docInfo = await ServiceDocumentService.TryLoadInfoAsync(db, taskId)
                                    .ConfigureAwait(false);
                            }
                            else
                            {
                                await TaskDelegationService.SyncPartLinesToAncestorsAsync(db, taskId)
                                    .ConfigureAwait(false);
                            }

            task.IsCompleted = true;
            task.EndDate = DateTime.Now;

                            var parentName = AppState.FormatEmployeeDisplayName(emp);
                            var isRootTask = docInfo != null && docInfo.IsCurrentTaskRoot
                                || !taskLinks.ParentTaskId.HasValue || taskLinks.ParentTaskId.Value == Guid.Empty;

                            if (docInfo != null && docInfo.IsCurrentTaskRoot && docId.HasValue)
                            {
                                await ServiceDocumentService.TryCompleteForRootTaskAsync(db, taskId)
                                    .ConfigureAwait(false);
                                autoClosedCount = await TaskDelegationService.CompleteOpenTasksForDocumentAsync(
                                    db, docId.Value, parentName, excludeTaskId: taskId).ConfigureAwait(false);
                            }
                            else if (isRootTask)
                            {
                                autoClosedCount = await TaskDelegationService.CompleteEntireSubtreeAsync(
                                    db, taskId, parentName, excludeTaskId: taskId).ConfigureAwait(false);
                            }
                            else
                            {
                                autoClosedCount = await TaskDelegationService.CompleteDescendantChainAsync(
                                    db, taskId, parentName).ConfigureAwait(false);
                            }

                            EmployeeTaskNotifier.NotifyDelegateChildCompleted(db, taskId, parentName);

                            await db.SaveChangesAsync().ConfigureAwait(false);
                            tx.Commit();
                        }
                        catch
                        {
                            tx.Rollback();
                            throw;
                        }
                    }

                    return true;

                }).ConfigureAwait(true);



                if (!ok)

                {

                    MessageBox.Show("Задание не найдено.", "Задание", MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;

                }



                var docAfter = await ServiceDocumentService.TryLoadInfoAsync(taskId).ConfigureAwait(true);
                string msg;
                if (docAfter != null && docAfter.Status == ServiceDocumentStatus.Completed)
                    msg = autoClosedCount > 0
                        ? $"Задание завершено. Документ заказ-наряда закрыт, автоматически завершено связанных заданий: {autoClosedCount}."
                        : "Задание завершено. Единый документ заказ-наряда закрыт.";
                else if (autoClosedCount > 0)
                    msg = $"Задание завершено. Автоматически закрыто дочерних заданий: {autoClosedCount}.";
                else if (taskLinks.ParentTaskId.HasValue && taskLinks.DelegateTaskId.HasValue)
                    msg = "Ваш шаг выполнен. Изменения переданы в документ; выше по цепочке увидят обновление.";
                else if (taskLinks.ParentTaskId.HasValue)
                    msg = "Поручение выполнено. Данные переданы в документ инициатора.";
                else
                    msg = "Задание отмечено как выполненное.";

                MessageBox.Show(msg, "Задание", MessageBoxButton.OK, MessageBoxImage.Information);

                DriveCareCore.Analytics.ActivityTracker.TrackEmployee(
                    DriveCareCore.Analytics.ActivityEventCodes.TaskComplete,
                    emp.RowId,
                    AppState.CurrentEmployee?.WorkshopId,
                    entityType: "Task",
                    entityId: taskId);

                if (CanGenerateWorkOrderDocx)
                {
                    var openAsk = MessageBox.Show(
                        "Открыть заказ-наряд для выдачи?",
                        "Заказ-наряд",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (openAsk == MessageBoxResult.Yes)
                    {
                        var (docOk, docPath, docErr) = await TryBuildAndSaveWorkOrderDocxAsync(openDocument: true)
                            .ConfigureAwait(true);
                        if (!docOk)
                        {
                            MessageBox.Show(docErr ?? "Не удалось сформировать заказ-наряд.", "Заказ-наряд",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        else if (!string.IsNullOrEmpty(docErr))
                        {
                            MessageBox.Show(
                                docErr + "\n\nФайл сохранён на рабочем столе:\n" + docPath,
                                "Заказ-наряд", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }

                AppState.Navigate(new ProHomePage());

            }

            catch (Exception ex)

            {

                MessageBox.Show("Не удалось сохранить: " + ex.Message, "Задание", MessageBoxButton.OK, MessageBoxImage.Error);

            }

        }



        private sealed class TaskReportSnapshot
        {
            public string ReportNote { get; set; } = string.Empty;
            public List<TaskServiceLineRow> Services { get; set; } = new List<TaskServiceLineRow>();
            public List<TaskPartLineRow> Parts { get; set; } = new List<TaskPartLineRow>();

            public static TaskReportSnapshot Capture(
                string reportNote,
                IEnumerable<TaskServiceLineVm> serviceLines,
                IEnumerable<TaskPartLineVm> partLines)
            {
                var services = new List<TaskServiceLineRow>();
                foreach (var line in serviceLines ?? Enumerable.Empty<TaskServiceLineVm>())
                {
                    var row = line.ToSnapshotRow();
                    if (!string.IsNullOrWhiteSpace(row.ServiceName))
                        services.Add(row);
                }

                var parts = new List<TaskPartLineRow>();
                foreach (var line in partLines ?? Enumerable.Empty<TaskPartLineVm>())
                {
                    if (string.IsNullOrWhiteSpace(line.PartName))
                        continue;

                    if (line.IsPurchaseLine)
                    {
                        parts.Add(line.ToSnapshotRow());
                        continue;
                    }

                    if (!line.WorkshopPartId.HasValue || line.WorkshopPartId.Value == Guid.Empty)
                        continue;
                    parts.Add(line.ToSnapshotRow());
                }

                return new TaskReportSnapshot
                {
                    ReportNote = (reportNote ?? string.Empty).Trim(),
                    Services = services,
                    Parts = parts
                };
            }

            public bool Equals(TaskReportSnapshot other)
            {
                if (other == null)
                    return false;
                if (!string.Equals(ReportNote, other.ReportNote, StringComparison.Ordinal))
                    return false;
                if (Services.Count != other.Services.Count || Parts.Count != other.Parts.Count)
                    return false;

                for (var i = 0; i < Services.Count; i++)
                {
                    if (!ServiceRowEquals(Services[i], other.Services[i]))
                        return false;
                }

                for (var i = 0; i < Parts.Count; i++)
                {
                    if (!PartRowEquals(Parts[i], other.Parts[i]))
                        return false;
                }

                return true;
            }

            private static bool ServiceRowEquals(TaskServiceLineRow a, TaskServiceLineRow b)
            {
                if (a == null || b == null)
                    return false;
                return a.WorkshopServiceId == b.WorkshopServiceId
                       && string.Equals((a.ServiceName ?? string.Empty).Trim(), (b.ServiceName ?? string.Empty).Trim(), StringComparison.Ordinal)
                       && a.Quantity == b.Quantity
                       && string.Equals((a.UnitName ?? string.Empty).Trim(), (b.UnitName ?? string.Empty).Trim(), StringComparison.Ordinal)
                       && a.UnitPrice == b.UnitPrice
                       && a.DiscountPercent == b.DiscountPercent
                       && a.LineAmount == b.LineAmount;
            }

            private static bool PartRowEquals(TaskPartLineRow a, TaskPartLineRow b)
            {
                if (a == null || b == null)
                    return false;
                return a.WorkshopPartId == b.WorkshopPartId
                       && string.Equals((a.PartName ?? string.Empty).Trim(), (b.PartName ?? string.Empty).Trim(), StringComparison.Ordinal)
                       && a.Quantity == b.Quantity
                       && string.Equals((a.UnitName ?? string.Empty).Trim(), (b.UnitName ?? string.Empty).Trim(), StringComparison.Ordinal)
                       && a.UnitPrice == b.UnitPrice
                       && a.LineAmount == b.LineAmount;
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

