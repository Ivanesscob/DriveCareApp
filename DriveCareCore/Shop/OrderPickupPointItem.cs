using System;

namespace DriveCareCore.Shop
{
    public sealed class OrderPickupPointItem
    {
        public Guid RowId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string District { get; set; }
        public string AddressLine { get; set; }
        public string City { get; set; }
        public int SortOrder { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public string FullAddress => string.IsNullOrWhiteSpace(City)
            ? AddressLine
            : $"{City}, {AddressLine}";

        public string ListTitle => Name ?? District ?? "Пункт выдачи";
        public string ListSubtitle => $"{District} · {AddressLine}";
        public string FilterTag => District ?? string.Empty;

        public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;
    }
}
