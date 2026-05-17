using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DriveCarePro.Services.WorkshopServices
{
    public sealed class TaskServiceLineVm : INotifyPropertyChanged
    {
        private WorkshopServiceItem _selectedCatalog;
        private string _serviceName = string.Empty;
        private decimal _quantity = 1m;
        private string _unitName = "усл.";
        private decimal _unitPrice;
        private decimal _discountPercent;
        private decimal _lineAmount;

        public IList<WorkshopServiceItem> Catalog { get; set; }

        public WorkshopServiceItem SelectedCatalog
        {
            get => _selectedCatalog;
            set
            {
                if (!Equals(_selectedCatalog, value))
                {
                    _selectedCatalog = value;
                    OnPropertyChanged();
                    if (value != null)
                    {
                        ServiceName = value.Name;
                        UnitName = string.IsNullOrWhiteSpace(value.UnitName) ? "усл." : value.UnitName;
                        UnitPrice = value.Price;
                        WorkshopServiceId = value.RowId;
                    }
                }
            }
        }

        public Guid? WorkshopServiceId { get; set; }

        public string ServiceName
        {
            get => _serviceName;
            set { _serviceName = value ?? string.Empty; OnPropertyChanged(); Recalculate(); }
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
            private set { _lineAmount = value; OnPropertyChanged(); }
        }

        public void Recalculate()
        {
            var gross = Quantity * UnitPrice;
            var discount = gross * (DiscountPercent / 100m);
            LineAmount = Math.Round(gross - discount, 2, MidpointRounding.AwayFromZero);
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
                vm.ServiceName = row.ServiceName;
                vm.Quantity = row.Quantity;
                vm.UnitName = row.UnitName;
                vm.UnitPrice = row.UnitPrice;
                vm.DiscountPercent = row.DiscountPercent;
                vm.LineAmount = row.LineAmount;
                if (row.WorkshopServiceId.HasValue && catalog != null)
                    vm.SelectedCatalog = catalog.FirstOrDefault(c => c.RowId == row.WorkshopServiceId.Value);
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
        private string _partName = string.Empty;
        private decimal _quantity = 1m;
        private string _unitName = "шт.";
        private decimal _unitPrice;
        private decimal _lineAmount;

        public IList<WorkshopPartItem> Catalog { get; set; }

        public WorkshopPartItem SelectedCatalog
        {
            get => _selectedCatalog;
            set
            {
                if (!Equals(_selectedCatalog, value))
                {
                    _selectedCatalog = value;
                    OnPropertyChanged();
                    if (value != null)
                    {
                        PartName = value.Name;
                        UnitName = string.IsNullOrWhiteSpace(value.UnitName) ? "шт." : value.UnitName;
                        UnitPrice = value.Price;
                        WorkshopPartId = value.RowId;
                    }
                }
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
            set { _quantity = value < 0 ? 0 : value; OnPropertyChanged(); Recalculate(); }
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
            private set { _lineAmount = value; OnPropertyChanged(); }
        }

        public void Recalculate() =>
            LineAmount = Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

        public TaskPartLineRow ToRow() => new TaskPartLineRow
        {
            WorkshopPartId = WorkshopPartId,
            PartName = PartName,
            Quantity = Quantity,
            UnitName = UnitName,
            UnitPrice = UnitPrice,
            LineAmount = LineAmount
        };

        public static TaskPartLineVm FromRow(TaskPartLineRow row, IList<WorkshopPartItem> catalog = null)
        {
            var vm = new TaskPartLineVm { Catalog = catalog };
            if (row != null)
            {
                vm.WorkshopPartId = row.WorkshopPartId;
                vm.PartName = row.PartName;
                vm.Quantity = row.Quantity;
                vm.UnitName = row.UnitName;
                vm.UnitPrice = row.UnitPrice;
                vm.LineAmount = row.LineAmount;
                if (row.WorkshopPartId.HasValue && catalog != null)
                    vm.SelectedCatalog = catalog.FirstOrDefault(c => c.RowId == row.WorkshopPartId.Value);
            }
            vm.Recalculate();
            return vm;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
