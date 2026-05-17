using System;

namespace DriveCareCore.Data.BD
{
    public class WorkshopGuestCar
    {
        public Guid RowId { get; set; }
        public Guid WorkshopId { get; set; }
        public Guid ServiceClientId { get; set; }
        public Guid CarId { get; set; }
        public Guid? RepairHistoryId { get; set; }
        public string Vin { get; set; }
        public string PlateNumber { get; set; }
        public string BrandModelText { get; set; }
        public int? Year { get; set; }
        public string Color { get; set; }
        public int? Mileage { get; set; }
        public bool IsLinkedToUser { get; set; }
        public Guid? UserCarId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
