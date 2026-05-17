using System;

namespace DriveCarePro.Services.WorkshopServices
{
    public sealed class WorkshopServiceItem
    {
        public const int MaxDescriptionLength = 500;

        public Guid RowId { get; set; }
        public Guid WorkshopId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public Guid? UnitId { get; set; }
        public string UnitName { get; set; }
        public bool IsActive { get; set; }

        public string PriceDisplay => Price.ToString("N2");
        public string ActiveDisplay => IsActive ? "Да" : "Нет";
    }

    public sealed class TaskServiceLineRow
    {
        public Guid? RowId { get; set; }
        public Guid? WorkshopServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1m;
        public string UnitName { get; set; } = "усл.";
        public decimal UnitPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal LineAmount { get; set; }

        public void RecalculateAmount()
        {
            var gross = Quantity * UnitPrice;
            var discount = gross * (DiscountPercent / 100m);
            LineAmount = Math.Round(gross - discount, 2, MidpointRounding.AwayFromZero);
        }
    }

    public sealed class TaskPartLineRow
    {
        public Guid? RowId { get; set; }
        public Guid? WorkshopPartId { get; set; }
        public string PartName { get; set; } = string.Empty;
        public decimal Quantity { get; set; } = 1m;
        public string UnitName { get; set; } = "шт.";
        public decimal UnitPrice { get; set; }
        public decimal LineAmount { get; set; }

        public void RecalculateAmount() =>
            LineAmount = Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);
    }
}
