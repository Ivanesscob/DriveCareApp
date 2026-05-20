using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DriveCarePro.Services.WorkshopServices
{
    public sealed class TaskServiceLineVm : INotifyPropertyChanged
    {
        private WorkshopServiceItem _selectedCatalog;
        private string _serviceSearchText = string.Empty;
        private string _serviceName = string.Empty;
        private decimal _quantity = 1m;
        private string _unitName = "усл.";
        private decimal _unitPrice;
        private decimal _discountPercent;
        private decimal _lineAmount;
        private int _suppressComboSync;

        public IList<WorkshopServiceItem> Catalog { get; set; }

        /// <summary>Текст в редактируемом ComboBox (как PartSearchText у деталей).</summary>
        public string ServiceSearchText
        {
            get => _serviceSearchText;
            set
            {
                var v = value ?? string.Empty;
                if (string.Equals(_serviceSearchText, v, StringComparison.Ordinal))
                    return;

                SetServiceSearchTextInternal(v, notify: true);
                if (_suppressComboSync > 0)
                    return;

                TrySelectFromSearch(v);
            }
        }

        public WorkshopServiceItem SelectedCatalog
        {
            get => _selectedCatalog;
            set
            {
                if (_suppressComboSync > 0)
                {
                    _selectedCatalog = ResolveFromCatalog(value);
                    return;
                }

                ApplySelectedCatalog(value);
            }
        }

        public Guid? WorkshopServiceId { get; set; }

        public string ServiceName
        {
            get => _serviceName;
            set => SetServiceNameInternal(value ?? string.Empty, fromSearch: false);
        }

        /// <summary>Привязать услугу к экземпляру из ItemsSource ComboBox.</summary>
        public void ReattachSelectedCatalog(IList<WorkshopServiceItem> catalog)
        {
            Catalog = catalog;
            OnPropertyChanged(nameof(Catalog));

            _suppressComboSync++;
            try
            {
                if (!WorkshopServiceId.HasValue || catalog == null)
                {
                    SetServiceSearchTextInternal(ServiceName ?? string.Empty, notify: false);
                    PushComboDisplayToView();
                    return;
                }

                var match = catalog.FirstOrDefault(c => c.RowId == WorkshopServiceId.Value);
                if (match == null)
                {
                    SetServiceSearchTextInternal(ServiceName ?? string.Empty, notify: false);
                    PushComboDisplayToView();
                    return;
                }

                ApplySelectedCatalog(match, notifySelected: false);
                PushComboDisplayToView();
            }
            finally
            {
                _suppressComboSync--;
            }
        }

        /// <summary>Принудительно обновить текст в редактируемом ComboBox (после ItemsSource / загрузки строки).</summary>
        public void PushComboDisplayToView()
        {
            var display = (_selectedCatalog?.Name ?? _serviceSearchText ?? ServiceName ?? string.Empty).Trim();
            _suppressComboSync++;
            try
            {
                SetServiceSearchTextInternal(display, notify: false);
            }
            finally
            {
                _suppressComboSync--;
            }

            OnPropertyChanged(nameof(ServiceSearchText));
            if (_selectedCatalog != null)
                OnPropertyChanged(nameof(SelectedCatalog));
        }

        private void ApplySelectedCatalog(WorkshopServiceItem value, bool notifySelected = true)
        {
            var resolved = ResolveFromCatalog(value);
            if (resolved == null)
            {
                if (_selectedCatalog == null)
                    return;

                _selectedCatalog = null;
                WorkshopServiceId = null;
                if (notifySelected)
                    OnPropertyChanged(nameof(SelectedCatalog));
                return;
            }

            if (_selectedCatalog?.RowId == resolved.RowId)
            {
                _selectedCatalog = resolved;
                var display = resolved.Name ?? string.Empty;
                _suppressComboSync++;
                try
                {
                    SetServiceSearchTextInternal(display, notify: true);
                    SetServiceNameInternal(display, fromSearch: true);
                    SetUnitNameInternal(string.IsNullOrWhiteSpace(resolved.UnitName) ? "усл." : resolved.UnitName);
                    SetUnitPriceInternal(resolved.Price);
                    WorkshopServiceId = resolved.RowId;
                }
                finally
                {
                    _suppressComboSync--;
                }

                return;
            }

            _selectedCatalog = resolved;
            var name = resolved.Name ?? string.Empty;
            _suppressComboSync++;
            try
            {
                SetServiceSearchTextInternal(name, notify: true);
                SetServiceNameInternal(name, fromSearch: true);
                SetUnitNameInternal(string.IsNullOrWhiteSpace(resolved.UnitName) ? "усл." : resolved.UnitName);
                SetUnitPriceInternal(resolved.Price);
                WorkshopServiceId = resolved.RowId;
            }
            finally
            {
                _suppressComboSync--;
            }

            if (notifySelected)
                OnPropertyChanged(nameof(SelectedCatalog));
        }

        private void SetUnitNameInternal(string unit)
        {
            var v = unit ?? "усл.";
            if (string.Equals(_unitName, v, StringComparison.Ordinal))
                return;

            _unitName = v;
            OnPropertyChanged(nameof(UnitName));
        }

        private void SetUnitPriceInternal(decimal price)
        {
            var v = price < 0 ? 0 : price;
            if (_unitPrice == v)
                return;

            _unitPrice = v;
            OnPropertyChanged(nameof(UnitPrice));
            Recalculate();
        }

        private void SetServiceSearchTextInternal(string text, bool notify)
        {
            var v = text ?? string.Empty;
            if (string.Equals(_serviceSearchText, v, StringComparison.Ordinal))
                return;

            _serviceSearchText = v;
            if (notify)
                OnPropertyChanged(nameof(ServiceSearchText));
        }

        private void SetServiceNameInternal(string name, bool fromSearch)
        {
            var v = name ?? string.Empty;
            var changed = !string.Equals(_serviceName, v, StringComparison.Ordinal);
            if (!changed && (fromSearch || string.Equals(_serviceSearchText, v, StringComparison.Ordinal)))
                return;

            _serviceName = v;
            if (!fromSearch)
                SetServiceSearchTextInternal(v, notify: _suppressComboSync == 0);

            if (changed)
            {
                OnPropertyChanged(nameof(ServiceName));
                Recalculate();
            }
        }

        private WorkshopServiceItem ResolveFromCatalog(WorkshopServiceItem value)
        {
            if (value == null || Catalog == null)
                return value;

            return Catalog.FirstOrDefault(c => c.RowId == value.RowId) ?? value;
        }

        private void TrySelectFromSearch(string text)
        {
            var t = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(t))
            {
                _suppressComboSync++;
                try
                {
                    ApplySelectedCatalog(null);
                    SetServiceNameInternal(string.Empty, fromSearch: true);
                    WorkshopServiceId = null;
                }
                finally
                {
                    _suppressComboSync--;
                }

                return;
            }

            var exact = Catalog?.FirstOrDefault(c =>
                string.Equals((c.Name ?? string.Empty).Trim(), t, StringComparison.OrdinalIgnoreCase));

            if (exact != null)
            {
                _suppressComboSync++;
                try
                {
                    ApplySelectedCatalog(exact);
                }
                finally
                {
                    _suppressComboSync--;
                }

                return;
            }

            _suppressComboSync++;
            try
            {
                if (_selectedCatalog != null)
                    ApplySelectedCatalog(null);

                SetServiceNameInternal(t, fromSearch: true);
                WorkshopServiceId = null;
            }
            finally
            {
                _suppressComboSync--;
            }
        }

        public decimal Quantity
        {
            get => _quantity;
            set { _quantity = value < 0 ? 0 : value; OnPropertyChanged(); Recalculate(); }
        }

        public string UnitName
        {
            get => _unitName;
            set { _unitName = value ?? "усл."; OnPropertyChanged(); }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set { _unitPrice = value < 0 ? 0 : value; OnPropertyChanged(); Recalculate(); }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set { _discountPercent = value < 0 ? 0 : value; OnPropertyChanged(); Recalculate(); }
        }

        public decimal LineAmount
        {
            get => _lineAmount;
            private set
            {
                if (_lineAmount == value)
                    return;

                _lineAmount = value;
                OnPropertyChanged();
            }
        }

        public void Recalculate()
        {
            var gross = Quantity * UnitPrice;
            var discount = gross * (DiscountPercent / 100m);
            LineAmount = Math.Round(gross - discount, 2, MidpointRounding.AwayFromZero);
        }

        public TaskServiceLineRow ToSnapshotRow()
        {
            var gross = Quantity * UnitPrice;
            var discount = gross * (DiscountPercent / 100m);
            var amount = Math.Round(gross - discount, 2, MidpointRounding.AwayFromZero);
            return new TaskServiceLineRow
            {
                WorkshopServiceId = WorkshopServiceId,
                ServiceName = ServiceName,
                Quantity = Quantity,
                UnitName = UnitName,
                UnitPrice = UnitPrice,
                DiscountPercent = DiscountPercent,
                LineAmount = amount
            };
        }

        public TaskServiceLineRow ToRow() => new TaskServiceLineRow
        {
            WorkshopServiceId = WorkshopServiceId,
            ServiceName = ServiceName,
            Quantity = Quantity,
            UnitName = UnitName,
            UnitPrice = UnitPrice,
            DiscountPercent = DiscountPercent,
            LineAmount = LineAmount
        };

        public static TaskServiceLineVm FromRow(TaskServiceLineRow row, IList<WorkshopServiceItem> catalog)
        {
            var vm = new TaskServiceLineVm { Catalog = catalog };
            if (row != null)
            {
                vm.WorkshopServiceId = row.WorkshopServiceId;
                vm.Quantity = row.Quantity;
                vm.UnitName = row.UnitName;
                vm.UnitPrice = row.UnitPrice;
                vm.DiscountPercent = row.DiscountPercent;
                vm.LineAmount = row.LineAmount;
                var name = (row.ServiceName ?? string.Empty).Trim();
                vm.SetServiceNameInternal(name, fromSearch: true);
                vm.SetServiceSearchTextInternal(name, notify: false);
                if (row.WorkshopServiceId.HasValue && catalog != null)
                    vm.ReattachSelectedCatalog(catalog);
                else
                    vm.OnPropertyChanged(nameof(ServiceSearchText));
            }

            vm.Recalculate();
            return vm;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class TaskPartLineVm : INotifyPropertyChanged
    {
        private WorkshopPartItem _selectedCatalog;
        private IList<WorkshopPartItem> _catalog;
        private string _partSearchText = string.Empty;
        private string _partName = string.Empty;
        private decimal _quantity = 1m;
        private string _unitName = "шт.";
        private decimal _unitPrice;
        private decimal _lineAmount;
        private decimal _stockOnHand;
        private int _suppressComboSync;
        private readonly ObservableCollection<WorkshopPartItem> _filteredCatalog = new ObservableCollection<WorkshopPartItem>();

        /// <summary>Игнорировать Text/SelectedItem от ComboBox при пересоздании ячейки DataGrid.</summary>
        public bool SuppressComboBindingUpdates { get; set; }

        /// <summary>Позиции с нулевым остатком, которые всё равно показывать в списке (удалены из задания, но ещё не сохранены).</summary>
        public ISet<Guid> PickerIncludeZeroStockIds { get; set; }

        /// <summary>Строка списка закупки — только текст, без выбора со склада.</summary>
        public bool IsPurchaseLine { get; set; }

        /// <summary>Количество, уже зарезервированное (списанное) со склада для этой строки.</summary>
        public decimal ReservedQuantity { get; private set; }

        public Guid? ReservedPartId { get; private set; }

        /// <summary>Количество из БД при открытии задания (для отката только сессионных изменений).</summary>
        public decimal LoadedQuantity { get; private set; }

        /// <summary>Склад изменён в этой сессии (можно вернуть при «Назад»).</summary>
        public bool ReservedInCurrentSession { get; set; }

        public void SetReservation(Guid partId, decimal quantity, bool inCurrentSession = false)
        {
            ReservedPartId = partId;
            ReservedQuantity = quantity < 0 ? 0 : quantity;
            ReservedInCurrentSession = inCurrentSession;
        }

        public void ClearReservation()
        {
            ReservedPartId = null;
            ReservedQuantity = 0;
            ReservedInCurrentSession = false;
        }

        /// <summary>После «Сохранить»: зафиксировать количество в БД, при «Назад» не откатывать склад.</summary>
        public void MarkAsPersistedToDatabase()
        {
            if (!WorkshopPartId.HasValue || WorkshopPartId.Value == Guid.Empty || Quantity <= 0)
                return;

            LoadedQuantity = Quantity;
            SetReservation(WorkshopPartId.Value, Quantity, inCurrentSession: false);
        }

        public void RefreshStockOnHandFromCatalog(IList<WorkshopPartItem> catalog)
        {
            if (!WorkshopPartId.HasValue || catalog == null)
                return;

            var match = catalog.FirstOrDefault(c => c.RowId == WorkshopPartId.Value);
            if (match != null)
                StockOnHand = match.QuantityOnHand;
        }

        public IList<WorkshopPartItem> Catalog
        {
            get => _catalog;
            set
            {
                if (ReferenceEquals(_catalog, value))
                {
                    RefreshFilteredCatalog();
                    RestoreCatalogSelection();
                    return;
                }

                _catalog = value;
                RefreshFilteredCatalog();
                OnPropertyChanged();
                RestoreCatalogSelection();
            }
        }

        /// <summary>Обновить каталог и список выбора без сброса уже выбранной детали.</summary>
        public void AttachCatalog(IList<WorkshopPartItem> catalog, ISet<Guid> pickerIncludeZeroStockIds)
        {
            PickerIncludeZeroStockIds = pickerIncludeZeroStockIds;
            _catalog = catalog;
            OnPropertyChanged(nameof(Catalog));
            RefreshFilteredCatalog();
            RestoreCatalogSelection();
        }

        /// <summary>Первичная привязка при загрузке задания (обновляет список и выбор).</summary>
        public void ReattachSelectedCatalog(IList<WorkshopPartItem> catalog)
        {
            AttachCatalog(catalog, PickerIncludeZeroStockIds);
        }

        /// <summary>Обновить выбор после списания со склада без пересоздания ItemsSource.</summary>
        public void SyncFromCatalog(IList<WorkshopPartItem> catalog) =>
            AttachCatalog(catalog, PickerIncludeZeroStockIds);

        /// <summary>Принудительно обновить название в редактируемом ComboBox (только имя, без остатка).</summary>
        public void PushComboDisplayToView()
        {
            var display = (_selectedCatalog?.Name ?? PartName ?? string.Empty).Trim();
            _suppressComboSync++;
            try
            {
                SetPartSearchTextInternal(display, notify: false);
            }
            finally
            {
                _suppressComboSync--;
            }

            OnPropertyChanged(nameof(PartSearchText));
            if (_selectedCatalog != null)
                OnPropertyChanged(nameof(SelectedCatalog));
        }

        private void SetPartSearchTextInternal(string text, bool notify)
        {
            var v = text ?? string.Empty;
            if (string.Equals(_partSearchText, v, StringComparison.Ordinal))
                return;

            _partSearchText = v;
            if (notify)
                OnPropertyChanged(nameof(PartSearchText));
        }

        private void ApplySelectedCatalog(WorkshopPartItem value, bool notifySelected)
        {
            var resolved = ResolveFromCatalog(value);
            if (resolved == null)
            {
                if (_selectedCatalog == null)
                    return;

                _selectedCatalog = null;
                StockOnHand = 0;
                if (!IsPurchaseLine)
                    WorkshopPartId = null;
                SetPartSearchTextInternal(string.Empty, notify: true);
                if (notifySelected)
                    OnPropertyChanged(nameof(SelectedCatalog));
                PushComboDisplayToView();
                return;
            }

            if (_selectedCatalog?.RowId == resolved.RowId)
            {
                _selectedCatalog = resolved;
                var name = resolved.Name ?? string.Empty;
                SetPartSearchTextInternal(name, notify: true);
                if (!string.Equals(PartName, name, StringComparison.Ordinal))
                    PartName = name;
                UnitName = string.IsNullOrWhiteSpace(resolved.UnitName) ? "шт." : resolved.UnitName;
                UnitPrice = resolved.Price;
                WorkshopPartId = resolved.RowId;
                StockOnHand = resolved.QuantityOnHand;
                if (notifySelected)
                    OnPropertyChanged(nameof(SelectedCatalog));
                PushComboDisplayToView();
                return;
            }

            _selectedCatalog = resolved;
            var displayName = resolved.Name ?? string.Empty;
            SetPartSearchTextInternal(displayName, notify: true);
            PartName = displayName;
            UnitName = string.IsNullOrWhiteSpace(resolved.UnitName) ? "шт." : resolved.UnitName;
            UnitPrice = resolved.Price;
            WorkshopPartId = resolved.RowId;
            StockOnHand = resolved.QuantityOnHand;
            if (Quantity <= 0)
                Quantity = 1;
            else if (Quantity > StockOnHand + ReservedQuantity)
                Quantity = StockOnHand + ReservedQuantity > 0 ? StockOnHand + ReservedQuantity : Quantity;

            if (notifySelected)
                OnPropertyChanged(nameof(SelectedCatalog));

            PushComboDisplayToView();
        }

        private WorkshopPartItem ResolveFromCatalog(WorkshopPartItem value)
        {
            if (value == null || Catalog == null)
                return value;

            return Catalog.FirstOrDefault(c => c.RowId == value.RowId) ?? value;
        }

        public IList<WorkshopPartItem> FilteredCatalog => _filteredCatalog;

        /// <summary>Поиск по названию в выпадающем списке склада.</summary>
        public string PartSearchText
        {
            get => _partSearchText;
            set
            {
                if (IsPurchaseLine || SuppressComboBindingUpdates)
                    return;

                var v = value ?? string.Empty;
                if (string.Equals(_partSearchText, v, StringComparison.Ordinal))
                    return;

                SetPartSearchTextInternal(v, notify: true);
                if (_suppressComboSync > 0)
                    return;

                RefreshFilteredCatalog();
                TrySelectFromSearch(v);
            }
        }

        public decimal StockOnHand
        {
            get => _stockOnHand;
            private set { _stockOnHand = value; OnPropertyChanged(); }
        }

        public WorkshopPartItem SelectedCatalog
        {
            get => _selectedCatalog;
            set
            {
                if (SuppressComboBindingUpdates)
                    return;

                if (_suppressComboSync > 0)
                {
                    _selectedCatalog = ResolveFromCatalog(value);
                    return;
                }

                ApplySelectedCatalog(value, notifySelected: true);
            }
        }

        public Guid? WorkshopPartId { get; set; }

        public string PartName
        {
            get => _partName;
            set { _partName = value ?? string.Empty; OnPropertyChanged(); Recalculate(); }
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                var v = value < 0 ? 0 : value;
                var maxAllowed = StockOnHand + ReservedQuantity;
                if (maxAllowed > 0 && v > maxAllowed)
                    v = maxAllowed;
                _quantity = v;
                OnPropertyChanged();
                Recalculate();
            }
        }

        public string UnitName
        {
            get => _unitName;
            set { _unitName = value ?? "шт."; OnPropertyChanged(); }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set { _unitPrice = value < 0 ? 0 : value; OnPropertyChanged(); Recalculate(); }
        }

        public decimal LineAmount
        {
            get => _lineAmount;
            private set
            {
                if (_lineAmount == value)
                    return;

                _lineAmount = value;
                OnPropertyChanged();
            }
        }

        public void Recalculate() =>
            LineAmount = Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

        public TaskPartLineRow ToSnapshotRow() => new TaskPartLineRow
        {
            WorkshopPartId = WorkshopPartId,
            PartName = PartName,
            Quantity = Quantity,
            UnitName = UnitName,
            UnitPrice = UnitPrice,
            LineAmount = Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero)
        };

        public TaskPartLineRow ToRow()
        {
            if (!WorkshopPartId.HasValue || WorkshopPartId.Value == Guid.Empty)
                throw new InvalidOperationException("Выберите деталь со склада.");

            return new TaskPartLineRow
            {
                WorkshopPartId = WorkshopPartId,
                PartName = PartName,
                Quantity = Quantity,
                UnitName = UnitName,
                UnitPrice = UnitPrice,
                LineAmount = LineAmount
            };
        }

        public static TaskPartLineVm FromRow(TaskPartLineRow row, IList<WorkshopPartItem> catalog = null)
        {
            var vm = new TaskPartLineVm { Catalog = catalog };
            if (row != null)
            {
                vm.WorkshopPartId = row.WorkshopPartId;
                vm.PartName = row.PartName;
                vm._partSearchText = row.PartName ?? string.Empty;
                vm.Quantity = row.Quantity;
                vm.LoadedQuantity = row.Quantity;
                vm.UnitName = row.UnitName;
                vm.UnitPrice = row.UnitPrice;
                vm.LineAmount = row.LineAmount;
                if (row.WorkshopPartId.HasValue && catalog != null)
                    vm.ReattachSelectedCatalog(catalog);
                else
                {
                    vm.RefreshFilteredCatalog();
                    vm.OnPropertyChanged(nameof(FilteredCatalog));
                }
            }
            else
            {
                vm.RefreshFilteredCatalog();
            }
            vm.Recalculate();
            return vm;
        }

        public static TaskPartLineVm FromPurchaseRow(TaskPartLineRow row)
        {
            var vm = new TaskPartLineVm { IsPurchaseLine = true };
            if (row != null)
            {
                vm.PartName = row.PartName ?? string.Empty;
                vm.Quantity = row.Quantity;
                vm.UnitName = row.UnitName ?? "шт.";
                vm.UnitPrice = row.UnitPrice;
                vm.LineAmount = row.LineAmount;
            }
            vm.Recalculate();
            return vm;
        }

        /// <summary>Восстановить выбор и текст после обновления ItemsSource ComboBox.</summary>
        public void RestoreCatalogSelection()
        {
            if (IsPurchaseLine)
                return;

            if (WorkshopPartId.HasValue && WorkshopPartId.Value != Guid.Empty && Catalog != null)
            {
                var match = Catalog.FirstOrDefault(c => c.RowId == WorkshopPartId.Value);
                if (match != null)
                {
                    _suppressComboSync++;
                    try
                    {
                        _selectedCatalog = match;
                        StockOnHand = match.QuantityOnHand;
                        var name = (match.Name ?? PartName ?? string.Empty).Trim();
                        if (!string.IsNullOrEmpty(name))
                            SetPartSearchTextInternal(name, notify: false);
                    }
                    finally
                    {
                        _suppressComboSync--;
                    }

                    OnPropertyChanged(nameof(SelectedCatalog));
                    OnPropertyChanged(nameof(PartSearchText));
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(PartName))
                PushComboDisplayToView();
        }

        private void RefreshFilteredCatalog()
        {
            _filteredCatalog.Clear();
            if (IsPurchaseLine || Catalog == null)
            {
                OnPropertyChanged(nameof(FilteredCatalog));
                return;
            }

            var q = (PartSearchText ?? string.Empty).Trim();
            var seen = new HashSet<Guid>();
            foreach (var item in Catalog)
            {
                if (!seen.Add(item.RowId))
                    continue;
                if (item.QuantityOnHand <= 0 && !IsPickerIncludeZeroStock(item.RowId))
                    continue;
                if (string.IsNullOrEmpty(q) || PartNameMatches(item, q))
                    _filteredCatalog.Add(item);
            }

            OnPropertyChanged(nameof(FilteredCatalog));
        }

        private void TrySelectFromSearch(string text)
        {
            var t = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(t))
            {
                if (WorkshopPartId.HasValue && WorkshopPartId.Value != Guid.Empty &&
                    !string.IsNullOrWhiteSpace(PartName))
                {
                    RestoreCatalogSelection();
                    return;
                }

                _suppressComboSync++;
                try
                {
                    ApplySelectedCatalog(null, notifySelected: true);
                }
                finally
                {
                    _suppressComboSync--;
                }

                return;
            }

            var exact = Catalog?.FirstOrDefault(c =>
                string.Equals((c.Name ?? string.Empty).Trim(), t, StringComparison.OrdinalIgnoreCase));

            if (exact != null)
            {
                if (_selectedCatalog?.RowId != exact.RowId)
                {
                    _suppressComboSync++;
                    try
                    {
                        ApplySelectedCatalog(exact, notifySelected: true);
                    }
                    finally
                    {
                        _suppressComboSync--;
                    }
                }

                return;
            }

            if (SelectedCatalog != null &&
                (SelectedCatalog.Name ?? string.Empty).IndexOf(t, StringComparison.OrdinalIgnoreCase) < 0)
            {
                _suppressComboSync++;
                try
                {
                    ApplySelectedCatalog(null, notifySelected: true);
                }
                finally
                {
                    _suppressComboSync--;
                }
            }
        }

        private bool IsPickerIncludeZeroStock(Guid partId) =>
            PickerIncludeZeroStockIds != null && PickerIncludeZeroStockIds.Contains(partId);

        private static bool PartNameMatches(WorkshopPartItem item, string query)
        {
            var name = (item?.Name ?? string.Empty).Trim();
            return name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
