using System;

namespace DriveCareCore.Painting
{
    public sealed class WorkshopPaintServiceOffer
    {
        public Guid RowId { get; set; }
        public Guid WorkshopId { get; set; }
        public CarPaintKind PaintKind { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal? PriceFrom { get; set; }
        public string PriceDisplay { get; set; }
        public bool HasPrice => !string.IsNullOrWhiteSpace(PriceDisplay);
    }

    public sealed class WorkshopPaintColorOffer
    {
        public Guid RowId { get; set; }
        public Guid? ColorId { get; set; }
        public string ColorName { get; set; }
    }

    public sealed class WorkshopPaintShopDetail
    {
        public Guid WorkshopId { get; set; }
        public string WorkshopName { get; set; }
        public string CompanyName { get; set; }
        public string AddressLine { get; set; }
        public string Phone { get; set; }
        public string Description { get; set; }
    }
}
