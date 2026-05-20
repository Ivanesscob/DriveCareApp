using System;

namespace DriveCareCore.Maps
{
    public sealed class WorkshopMapPin
    {
        public Guid WorkshopId { get; set; }
        public string WorkshopName { get; set; }
        public string CompanyName { get; set; }
        public string AddressLine { get; set; }
        public string Phone { get; set; }
        public string Description { get; set; }
        public Guid? BusinessTypeId { get; set; }
        public string ServiceKindName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
