using System;

namespace DriveCarePro.Services.WorkshopServices
{
    public sealed class WorkshopPartItem
    {
        public const int MaxDescriptionLength = 500;

        public Guid RowId { get; set; }
        public Guid WorkshopId { get; set; }
        public string Name { get; set; }
        public string Article { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string UnitName { get; set; } = "шт.";
        public decimal QuantityOnHand { get; set; }
        public string Category { get; set; } = "Accessories";
        public bool IsActive { get; set; } = true;

        public string ListLabel
        {
            get
            {
                var art = string.IsNullOrWhiteSpace(Article) ? string.Empty : $" ({Article.Trim()})";
                var stock = QuantityOnHand > 0
                    ? $" — остаток {QuantityOnHand:0.###} {UnitName ?? "шт."}"
                    : string.Empty;
                return (Name ?? string.Empty).Trim() + art + stock;
            }
        }

        public string PriceLabel => $"{Price:N2} ₽";
    }
}
