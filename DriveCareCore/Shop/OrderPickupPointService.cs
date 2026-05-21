using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DriveCareCore.Shop
{
    public static class OrderPickupPointService
    {
        public static bool TableExists()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.OrderPickupPoints', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static IReadOnlyList<OrderPickupPointItem> LoadActive()
        {
            if (!TableExists())
                return Array.Empty<OrderPickupPointItem>();

            try
            {
                const string sql = @"
SELECT RowId, Code, Name, District, AddressLine, City, SortOrder, Latitude, Longitude
FROM dbo.OrderPickupPoints
WHERE IsActive = 1
ORDER BY SortOrder, District, Name;";

                return AppConnect.model1.Database.SqlQuery<PickupSqlRow>(sql)
                    .Select(Map)
                    .ToList();
            }
            catch
            {
                return Array.Empty<OrderPickupPointItem>();
            }
        }

        public static IReadOnlyList<string> LoadDistricts(IReadOnlyList<OrderPickupPointItem> points)
        {
            return (points ?? Array.Empty<OrderPickupPointItem>())
                .Select(p => p.District)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static OrderPickupPointItem FindById(Guid rowId, IReadOnlyList<OrderPickupPointItem> points)
        {
            return (points ?? Array.Empty<OrderPickupPointItem>())
                .FirstOrDefault(p => p.RowId == rowId);
        }

        static OrderPickupPointItem Map(PickupSqlRow r)
        {
            return new OrderPickupPointItem
            {
                RowId = r.RowId,
                Code = r.Code?.Trim(),
                Name = r.Name?.Trim(),
                District = r.District?.Trim(),
                AddressLine = r.AddressLine?.Trim(),
                City = string.IsNullOrWhiteSpace(r.City) ? "Санкт-Петербург" : r.City.Trim(),
                SortOrder = r.SortOrder,
                Latitude = r.Latitude,
                Longitude = r.Longitude
            };
        }

        public static IReadOnlyList<Maps.WorkshopMapPin> ToMapPins(IEnumerable<OrderPickupPointItem> points)
        {
            const double defaultLat = 59.9343;
            const double defaultLon = 30.3351;
            return (points ?? Array.Empty<OrderPickupPointItem>())
                .Select(p => new Maps.WorkshopMapPin
                {
                    WorkshopId = p.RowId,
                    WorkshopName = p.ListTitle,
                    CompanyName = p.District,
                    AddressLine = p.FullAddress,
                    ServiceKindName = "Пункт выдачи",
                    ServiceKindsLabel = "Пункт выдачи",
                    Latitude = p.Latitude ?? defaultLat,
                    Longitude = p.Longitude ?? defaultLon
                })
                .ToList();
        }

        sealed class PickupSqlRow
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
        }
    }
}
