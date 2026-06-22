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
            var hasReviews = await TableExistsAsync(db, "WorkshopReviews").ConfigureAwait(false);

            var ratingSql = hasReviews
                ? @",
       (SELECT AVG(CAST(rv.Rating AS DECIMAL(4,2))) FROM dbo.WorkshopReviews rv
        WHERE rv.WorkshopId = w.RowId AND rv.Status = 1) AS AvgRating,
       (SELECT COUNT(1) FROM dbo.WorkshopReviews rv
        WHERE rv.WorkshopId = w.RowId AND rv.Status = 1) AS ReviewCount"
                : ", CAST(NULL AS DECIMAL(4,2)) AS AvgRating, CAST(0 AS INT) AS ReviewCount";

            var sql = @"
SELECT w.RowId AS WorkshopId, w.Name AS WorkshopName, co.Name AS CompanyName, w.Description,
       w.BusinessTypeId, bt.Name AS ServiceKindName,
       a.RowId AS AddressId, a.FullAddress, a.City, a.Street, a.House,
       (SELECT TOP 1 e.Phone FROM dbo.Employees e
        WHERE e.WorkshopId = w.RowId AND e.IsActive = 1 AND e.Phone IS NOT NULL AND LTRIM(RTRIM(e.Phone)) <> N''
        ORDER BY e.HireDate) AS Phone" + ratingSql +
                (hasCoordCols ? ", a.Latitude, a.Longitude" : ", CAST(NULL AS FLOAT) AS Latitude, CAST(NULL AS FLOAT) AS Longitude") + @"
FROM dbo.Workshops w
INNER JOIN dbo.Companies co ON co.RowId = w.CompanyId
LEFT JOIN dbo.BusinessTypes bt ON bt.RowId = w.BusinessTypeId
LEFT JOIN dbo.Addresses a ON a.RowId = w.AddressId
ORDER BY w.Name;";

            var rows = await db.Database.SqlQuery<WorkshopMapRow>(sql).ToListAsync().ConfigureAwait(false);
            var typesByWorkshop = await WorkshopBusinessTypesHelper.LoadTypeIdsByWorkshopAsync(db).ConfigureAwait(false);

            var siteGroups = new Dictionary<string, List<WorkshopMapPin>>();

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

                var typeIds = ResolveTypeIds(row.WorkshopId, row.BusinessTypeId, typesByWorkshop);
                var pin = new WorkshopMapPin
                {
                    WorkshopId = row.WorkshopId,
                    WorkshopIds = new List<Guid> { row.WorkshopId },
                    WorkshopName = name,
                    CompanyName = row.CompanyName ?? string.Empty,
                    AddressLine = addressLine,
                    Phone = row.Phone ?? string.Empty,
                    Description = row.Description ?? string.Empty,
                    BusinessTypeId = typeIds.Count > 0 ? typeIds[0] : row.BusinessTypeId,
                    BusinessTypeIds = typeIds,
                    ServiceKindName = row.ServiceKindName ?? string.Empty,
                    ServiceKindsLabel = WorkshopServiceKinds.BuildKindsLabel(typeIds),
                    AddressId = row.AddressId,
                    Latitude = lat,
                    Longitude = lon,
                    AvgRating = row.AvgRating,
                    ReviewCount = row.ReviewCount
                };

                var siteKey = BuildSiteKey(row.AddressId, lat, lon);
                if (!siteGroups.TryGetValue(siteKey, out var group))
                {
                    group = new List<WorkshopMapPin>();
                    siteGroups[siteKey] = group;
                }
                group.Add(pin);
            }

            foreach (var group in siteGroups.Values)
                result.Pins.Add(MergeSitePins(group));

            return result;
        }

        static List<Guid> ResolveTypeIds(
            Guid workshopId,
            Guid? primaryTypeId,
            Dictionary<Guid, List<Guid>> typesByWorkshop)
        {
            if (typesByWorkshop != null && typesByWorkshop.TryGetValue(workshopId, out var list) && list.Count > 0)
                return new List<Guid>(list);
            if (primaryTypeId.HasValue && primaryTypeId.Value != Guid.Empty)
                return new List<Guid> { primaryTypeId.Value };
            return new List<Guid>();
        }

        static string BuildSiteKey(Guid? addressId, double lat, double lon)
        {
            if (addressId.HasValue && addressId.Value != Guid.Empty)
                return "A:" + addressId.Value.ToString("D");
            return "G:" + lat.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)
                   + ":" + lon.ToString("F5", System.Globalization.CultureInfo.InvariantCulture);
        }

        static WorkshopMapPin MergeSitePins(List<WorkshopMapPin> group)
        {
            if (group == null || group.Count == 0)
                return null;
            if (group.Count == 1)
                return group[0];

            var ordered = group.OrderBy(p => p.WorkshopName).ToList();
            var primary = ordered[0];
            var workshopIds = ordered.Select(p => p.WorkshopId).Distinct().ToList();
            var typeIds = ordered.SelectMany(p => p.BusinessTypeIds ?? new List<Guid>()).Distinct().ToList();
            if (typeIds.Count == 0 && primary.BusinessTypeId.HasValue)
                typeIds.Add(primary.BusinessTypeId.Value);

            var names = ordered.Select(p => p.WorkshopName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            var companies = ordered.Select(p => p.CompanyName).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
            var phones = ordered.Select(p => p.Phone).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? string.Empty;
            var descriptions = ordered.Select(p => p.Description).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToList();

            var totalReviews = ordered.Sum(p => p.ReviewCount);
            decimal? avgRating = null;
            if (totalReviews > 0)
            {
                var weighted = ordered
                    .Where(p => p.ReviewCount > 0 && p.AvgRating.HasValue)
                    .Sum(p => p.AvgRating.Value * p.ReviewCount);
                if (weighted > 0)
                    avgRating = weighted / totalReviews;
            }

            return new WorkshopMapPin
            {
                WorkshopId = primary.WorkshopId,
                WorkshopIds = workshopIds,
                WorkshopName = names.Count == 1 ? names[0] : string.Join(" / ", names),
                CompanyName = companies.Count == 1 ? companies[0] : string.Join(" · ", companies),
                AddressLine = primary.AddressLine,
                Phone = phones,
                Description = descriptions.Count > 0 ? string.Join("\n", descriptions) : primary.Description,
                BusinessTypeId = typeIds.Count > 0 ? typeIds[0] : primary.BusinessTypeId,
                BusinessTypeIds = typeIds,
                ServiceKindName = WorkshopServiceKinds.BuildKindsLabel(typeIds),
                ServiceKindsLabel = WorkshopServiceKinds.BuildKindsLabel(typeIds),
                AddressId = primary.AddressId,
                Latitude = primary.Latitude,
                Longitude = primary.Longitude,
                AvgRating = avgRating ?? primary.AvgRating,
                ReviewCount = totalReviews
            };
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

        static async Task<bool> TableExistsAsync(DriveCareDBEntities db, string tableName)
        {
            try
            {
                var sql = @"SELECT CASE WHEN OBJECT_ID(@t, N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return await db.Database.SqlQuery<int>(sql, new SqlParameter("@t", "dbo." + tableName))
                    .FirstOrDefaultAsync().ConfigureAwait(false) == 1;
            }
            catch
            {
                return false;
            }
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
            public decimal? AvgRating { get; set; }
            public int ReviewCount { get; set; }
        }
    }
}
