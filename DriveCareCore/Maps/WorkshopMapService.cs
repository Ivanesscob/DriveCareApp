using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.Maps
{
    public sealed class WorkshopMapLoadResult
    {
        public List<WorkshopMapPin> Pins { get; set; } = new List<WorkshopMapPin>();
        public List<string> SkippedNoAddress { get; set; } = new List<string>();
        public List<string> SkippedGeocodeFailed { get; set; } = new List<string>();
    }

    public static class WorkshopMapService
    {
        public static bool CoordinatesColumnsExist()
        {
            try
            {
                const string sql = @"
SELECT CASE WHEN COL_LENGTH(N'dbo.Addresses', N'Latitude') IS NOT NULL
             AND COL_LENGTH(N'dbo.Addresses', N'Longitude') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static Task<WorkshopMapLoadResult> LoadPinsForMapAsync() =>
            WithDb(LoadPinsForMapAsync);

        public static async Task<WorkshopMapLoadResult> LoadPinsForMapAsync(DriveCareDBEntities db)
        {
            var result = new WorkshopMapLoadResult();
            var hasCoordCols = await ColumnExistsAsync(db, "Addresses", "Latitude").ConfigureAwait(false);

            var sql = @"
SELECT w.RowId AS WorkshopId, w.Name AS WorkshopName, co.Name AS CompanyName, w.Description,
       w.BusinessTypeId, bt.Name AS ServiceKindName,
       a.RowId AS AddressId, a.FullAddress, a.City, a.Street, a.House,
       (SELECT TOP 1 e.Phone FROM dbo.Employees e
        WHERE e.WorkshopId = w.RowId AND e.IsActive = 1 AND e.Phone IS NOT NULL AND LTRIM(RTRIM(e.Phone)) <> N''
        ORDER BY e.HireDate) AS Phone" +
                (hasCoordCols ? ", a.Latitude, a.Longitude" : ", CAST(NULL AS FLOAT) AS Latitude, CAST(NULL AS FLOAT) AS Longitude") + @"
FROM dbo.Workshops w
INNER JOIN dbo.Companies co ON co.RowId = w.CompanyId
LEFT JOIN dbo.BusinessTypes bt ON bt.RowId = w.BusinessTypeId
LEFT JOIN dbo.Addresses a ON a.RowId = w.AddressId
ORDER BY w.Name;";

            var rows = await db.Database.SqlQuery<WorkshopMapRow>(sql).ToListAsync().ConfigureAwait(false);

            foreach (var row in rows)
            {
                var name = string.IsNullOrWhiteSpace(row.WorkshopName) ? "Автосервис" : row.WorkshopName.Trim();
                var addressLine = BuildAddressLine(row);

                if (string.IsNullOrWhiteSpace(addressLine) && !HasCoords(row))
                {
                    result.SkippedNoAddress.Add(name);
                    continue;
                }

                double lat;
                double lon;
                if (HasCoords(row))
                {
                    lat = row.Latitude.Value;
                    lon = row.Longitude.Value;
                }
                else
                {
                    var geo = await YandexGeocoderService.TryGeocodeSpbAsync(addressLine).ConfigureAwait(false);
                    if (!geo.HasValue)
                    {
                        result.SkippedGeocodeFailed.Add(name + " — " + addressLine);
                        continue;
                    }

                    lat = geo.Value.lat;
                    lon = geo.Value.lon;

                    if (hasCoordCols && row.AddressId.HasValue && row.AddressId.Value != Guid.Empty)
                    {
                        await SaveCoordinatesAsync(db, row.AddressId.Value, lat, lon).ConfigureAwait(false);
                    }
                }

                result.Pins.Add(new WorkshopMapPin
                {
                    WorkshopId = row.WorkshopId,
                    WorkshopName = name,
                    CompanyName = row.CompanyName ?? string.Empty,
                    AddressLine = addressLine,
                    Phone = row.Phone ?? string.Empty,
                    Description = row.Description ?? string.Empty,
                    BusinessTypeId = row.BusinessTypeId,
                    ServiceKindName = row.ServiceKindName ?? string.Empty,
                    Latitude = lat,
                    Longitude = lon
                });
            }

            return result;
        }

        static bool HasCoords(WorkshopMapRow row) =>
            row.Latitude.HasValue && row.Longitude.HasValue &&
            Math.Abs(row.Latitude.Value) > 0.0001 && Math.Abs(row.Longitude.Value) > 0.0001;

        static string BuildAddressLine(WorkshopMapRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.FullAddress))
                return row.FullAddress.Trim();

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(row.City))
                parts.Add(row.City.Trim());
            if (!string.IsNullOrWhiteSpace(row.Street))
                parts.Add(row.Street.Trim());
            if (!string.IsNullOrWhiteSpace(row.House))
                parts.Add(row.House.Trim());

            var line = string.Join(", ", parts);
            if (line.Length == 0)
                return null;

            if (line.IndexOf("санкт", StringComparison.OrdinalIgnoreCase) < 0 &&
                line.IndexOf("спб", StringComparison.OrdinalIgnoreCase) < 0 &&
                line.IndexOf("петербург", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return "Санкт-Петербург, " + line;
            }

            return line;
        }

        static async Task SaveCoordinatesAsync(DriveCareDBEntities db, Guid addressId, double lat, double lon)
        {
            await db.Database.ExecuteSqlCommandAsync(
                "UPDATE dbo.Addresses SET Latitude = @lat, Longitude = @lon WHERE RowId = @id",
                new SqlParameter("@lat", lat),
                new SqlParameter("@lon", lon),
                new SqlParameter("@id", addressId)).ConfigureAwait(false);
        }

        static async Task<bool> ColumnExistsAsync(DriveCareDBEntities db, string table, string column)
        {
            var sql = "SELECT CASE WHEN COL_LENGTH(@t, @c) IS NOT NULL THEN 1 ELSE 0 END";
            return await db.Database.SqlQuery<int>(sql,
                    new SqlParameter("@t", "dbo." + table),
                    new SqlParameter("@c", column))
                .FirstOrDefaultAsync().ConfigureAwait(false) == 1;
        }

        static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> work)
        {
            using (var db = new DriveCareDBEntities())
                return await work(db).ConfigureAwait(false);
        }

        sealed class WorkshopMapRow
        {
            public Guid WorkshopId { get; set; }
            public string WorkshopName { get; set; }
            public string CompanyName { get; set; }
            public string Description { get; set; }
            public Guid? BusinessTypeId { get; set; }
            public string ServiceKindName { get; set; }
            public string Phone { get; set; }
            public Guid? AddressId { get; set; }
            public string FullAddress { get; set; }
            public string City { get; set; }
            public string Street { get; set; }
            public string House { get; set; }
            public double? Latitude { get; set; }
            public double? Longitude { get; set; }
        }
    }
}
