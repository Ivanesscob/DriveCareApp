using System;

namespace DriveCareCore.Data.BD
{
    public class WorkshopServiceItem
    {
        public Guid RowId { get; set; }
        public Guid WorkshopId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }

        public string DisplayLabel => (Name ?? string.Empty) + " — " + Price.ToString("N2") + " ₽";
    }
}
