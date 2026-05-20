using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DriveCarePro.Services.WorkshopServices
{
    public sealed class WorkshopPartItem : INotifyPropertyChanged
    {
        public const int MaxDescriptionLength = 500;

        private decimal _quantityOnHand;

        public Guid RowId { get; set; }
        public Guid WorkshopId { get; set; }
        public string Name { get; set; }
        public string Article { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string UnitName { get; set; } = "шт.";
        public decimal QuantityOnHand
        {
            get => _quantityOnHand;
            set
            {
                if (_quantityOnHand == value)
                    return;
                _quantityOnHand = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ListLabel));
            }
        }

        public string Category { get; set; } = "Accessories";
        public bool IsActive { get; set; } = true;

        public string ListLabel
        {
            get
            {
                var art = string.IsNullOrWhiteSpace(Article) ? string.Empty : $" ({Article.Trim()})";
                return (Name ?? string.Empty).Trim() + art;
            }
        }

        public string PriceLabel => $"{Price:N2} ₽";

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
