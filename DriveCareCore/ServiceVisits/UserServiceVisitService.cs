using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.ServiceVisits
{
    public static class UserServiceVisitService
    {
        public static Task<List<UserServiceVisitItem>> LoadVisitsAsync(Guid userId, Guid? userCarRowId = null) =>
            WithDb(db => LoadVisitsAsync(db, userId, userCarRowId));

        public static async Task<List<UserServiceVisitItem>> LoadVisitsAsync(
            DriveCareDBEntities db,
            Guid userId,
            Guid? userCarRowId = null)
        {
            if (userId == Guid.Empty || !await TableExistsAsync(db, "ServiceDocuments").ConfigureAwait(false))
                return new List<UserServiceVisitItem>();

            Guid? filterCarId = null;
            if (userCarRowId.HasValue && userCarRowId.Value != Guid.Empty)
            {
                filterCarId = await db.UserCars.AsNoTracking()
                    .Where(uc => uc.RowId == userCarRowId.Value && uc.UserId == userId)
                    .Select(uc => (Guid?)uc.CarId)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
                if (!filterCarId.HasValue || filterCarId.Value == Guid.Empty)
                    return new List<UserServiceVisitItem>();
            }

            var hasDocLines = await TableExistsAsync(db, "ServiceDocumentServiceLines").ConfigureAwait(false);
            var hasTaskLines = await TableExistsAsync(db, "TaskServiceLines").ConfigureAwait(false);

            var servicesExpr = hasDocLines
                ? @"ISNULL((
    SELECT STUFF((
        SELECT N', ' + LEFT(sdl.ServiceName, 80)
        FROM dbo.ServiceDocumentServiceLines sdl
        WHERE sdl.DocumentId = sd.RowId
        ORDER BY sdl.SortOrder
        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'')
), N'')"
                : "CAST(N'' AS NVARCHAR(MAX))";

            if (hasTaskLines)
            {
                servicesExpr = $@"COALESCE(NULLIF({servicesExpr}, N''), (
    SELECT STUFF((
        SELECT DISTINCT N', ' + LEFT(tsl.ServiceName, 80)
        FROM dbo.Tasks t
        INNER JOIN dbo.TaskServiceLines tsl ON tsl.TaskId = t.RowId
        WHERE t.RowId = sd.RootTaskId OR t.DocumentId = sd.RowId
        FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'')
))";
            }

            var hasStage = await ColumnExistsAsync(db, "ServiceDocuments", "ClientStage").ConfigureAwait(false);
            var stageSel = hasStage ? "sd.ClientStage" : "CAST(2 AS TINYINT) AS ClientStage";

            var sql = $@"
SELECT sd.RowId AS DocumentId, sd.RootTaskId, sd.RepairHistoryId, sd.WorkshopId, sd.CarId,
       w.Name AS WorkshopName, sd.VisitReason, sd.ServiceKind, sd.Status,
       {stageSel}, sd.CreatedAt, sd.CompletedAt, rh.Mileage AS MileageKm, rh.RepairDate,
       {servicesExpr} AS ServicesSummary,
       CAST(ISNULL(rt.IsCompleted, 0) AS bit) AS RootTaskCompleted
FROM dbo.ServiceDocuments sd
INNER JOIN dbo.Workshops w ON w.RowId = sd.WorkshopId
LEFT JOIN dbo.RepairHistory rh ON rh.RowId = sd.RepairHistoryId
LEFT JOIN dbo.Tasks rt ON rt.RowId = sd.RootTaskId
WHERE sd.ClientUserId = @userId" +
                (filterCarId.HasValue ? " AND sd.CarId = @carId" : string.Empty) + @"
ORDER BY COALESCE(sd.CompletedAt, sd.CreatedAt) DESC;";

            var parms = new List<SqlParameter> { new SqlParameter("@userId", userId) };
            if (filterCarId.HasValue)
                parms.Add(new SqlParameter("@carId", filterCarId.Value));

            try
            {
                var rows = await db.Database.SqlQuery<UserServiceVisitItem>(sql, parms.ToArray())
                    .ToListAsync()
                    .ConfigureAwait(false);
                return rows ?? new List<UserServiceVisitItem>();
            }
            catch (SqlException)
            {
                return new List<UserServiceVisitItem>();
            }
        }

        public static Task<UserWorkOrderPreview> TryLoadWorkOrderAsync(Guid userId, Guid documentId) =>
            WithDb(db => TryLoadWorkOrderAsync(db, userId, documentId));

        public static async Task<UserWorkOrderPreview> TryLoadWorkOrderAsync(
            DriveCareDBEntities db,
            Guid userId,
            Guid documentId)
        {
            if (userId == Guid.Empty || documentId == Guid.Empty
                || !await TableExistsAsync(db, "ServiceDocuments").ConfigureAwait(false))
                return null;

            DocumentHeaderRow header;
            try
            {
                var hasStage = await ColumnExistsAsync(db, "ServiceDocuments", "ClientStage").ConfigureAwait(false);
                var stageSel = hasStage
                    ? "ISNULL(sd.ClientStage, CAST(2 AS TINYINT)) AS ClientStage"
                    : "CAST(2 AS TINYINT) AS ClientStage";
                var sql = $@"
SELECT sd.RowId AS DocumentId, sd.RootTaskId, sd.Title, sd.VisitReason, sd.ServiceKind, sd.Status,
       sd.CompletedAt, w.Name AS WorkshopName, rh.Mileage AS MileageKm, {stageSel}
FROM dbo.ServiceDocuments sd
INNER JOIN dbo.Workshops w ON w.RowId = sd.WorkshopId
LEFT JOIN dbo.RepairHistory rh ON rh.RowId = sd.RepairHistoryId
WHERE sd.RowId = @docId AND sd.ClientUserId = @userId";
                header = await db.Database.SqlQuery<DocumentHeaderRow>(sql,
                    new SqlParameter("@docId", documentId),
                    new SqlParameter("@userId", userId)).FirstOrDefaultAsync().ConfigureAwait(false);
            }
            catch (SqlException)
            {
                return null;
            }

            if (header == null)
                return null;

            var preview = new UserWorkOrderPreview
            {
                DocumentId = header.DocumentId,
                Title = header.Title ?? "Заказ-наряд",
                WorkshopName = header.WorkshopName ?? string.Empty,
                VisitReason = header.VisitReason ?? string.Empty,
                ServiceKind = header.ServiceKind ?? string.Empty,
                StatusLabel = header.Status == 1
                    ? ServiceDocumentClientStageLabels.Completed
                    : ServiceDocumentClientStageLabels.ForUser(
                        ServiceDocumentClientStageLabels.Normalize(header.ClientStage, header.Status)),
                CompletedAt = header.CompletedAt,
                MileageKm = header.MileageKm
            };

            preview.Services = await LoadServiceLinesAsync(db, documentId, header.RootTaskId).ConfigureAwait(false);
            preview.Parts = await LoadPartLinesAsync(db, documentId, header.RootTaskId).ConfigureAwait(false);

            try
            {
                preview.ReportText = await db.Database.SqlQuery<string>(
                    "SELECT ReportText FROM dbo.ServiceDocuments WHERE RowId = @p0", documentId)
                    .FirstOrDefaultAsync().ConfigureAwait(false) ?? string.Empty;
            }
            catch (SqlException)
            {
            }

            return preview;
        }

        static async Task<List<UserWorkOrderLineVm>> LoadServiceLinesAsync(
            DriveCareDBEntities db,
            Guid documentId,
            Guid rootTaskId)
        {
            if (await TableExistsAsync(db, "ServiceDocumentServiceLines").ConfigureAwait(false))
            {
                try
                {
                    var fromDoc = await db.Database.SqlQuery<UserWorkOrderLineVm>(@"
SELECT ServiceName AS Name, Quantity, UnitName, UnitPrice, LineAmount,
       ISNULL(DiscountPercent, 0) AS DiscountPercent
FROM dbo.ServiceDocumentServiceLines WHERE DocumentId = @p0 ORDER BY SortOrder", documentId)
                        .ToListAsync().ConfigureAwait(false);
                    if (fromDoc != null && fromDoc.Count > 0)
                        return fromDoc;
                }
                catch (SqlException)
                {
                }
            }

            if (!await TableExistsAsync(db, "TaskServiceLines").ConfigureAwait(false))
                return new List<UserWorkOrderLineVm>();

            try
            {
                return await db.Database.SqlQuery<UserWorkOrderLineVm>(@"
SELECT tsl.ServiceName AS Name, tsl.Quantity, tsl.UnitName, tsl.UnitPrice, tsl.LineAmount,
       ISNULL(tsl.DiscountPercent, 0) AS DiscountPercent
FROM dbo.Tasks t
INNER JOIN dbo.TaskServiceLines tsl ON tsl.TaskId = t.RowId
WHERE t.DocumentId = @doc OR t.RowId = @root
ORDER BY tsl.SortOrder",
                    new SqlParameter("@doc", documentId),
                    new SqlParameter("@root", rootTaskId)).ToListAsync().ConfigureAwait(false)
                    ?? new List<UserWorkOrderLineVm>();
            }
            catch (SqlException)
            {
                return new List<UserWorkOrderLineVm>();
            }
        }

        static async Task<List<UserWorkOrderLineVm>> LoadPartLinesAsync(
            DriveCareDBEntities db,
            Guid documentId,
            Guid rootTaskId)
        {
            if (await TableExistsAsync(db, "ServiceDocumentPartLines").ConfigureAwait(false))
            {
                try
                {
                    var fromDoc = await db.Database.SqlQuery<UserWorkOrderLineVm>(@"
SELECT PartName AS Name, Quantity, UnitName, UnitPrice, LineAmount, CAST(0 AS DECIMAL(9,2)) AS DiscountPercent
FROM dbo.ServiceDocumentPartLines WHERE DocumentId = @p0 ORDER BY SortOrder", documentId)
                        .ToListAsync().ConfigureAwait(false);
                    if (fromDoc != null && fromDoc.Count > 0)
                        return fromDoc;
                }
                catch (SqlException)
                {
                }
            }

            if (!await TableExistsAsync(db, "TaskPartLines").ConfigureAwait(false))
                return new List<UserWorkOrderLineVm>();

            try
            {
                return await db.Database.SqlQuery<UserWorkOrderLineVm>(@"
SELECT tpl.PartName AS Name, tpl.Quantity, tpl.UnitName, tpl.UnitPrice, tpl.LineAmount,
       CAST(0 AS DECIMAL(9,2)) AS DiscountPercent
FROM dbo.Tasks t
INNER JOIN dbo.TaskPartLines tpl ON tpl.TaskId = t.RowId
WHERE t.DocumentId = @doc OR t.RowId = @root
ORDER BY tpl.SortOrder",
                    new SqlParameter("@doc", documentId),
                    new SqlParameter("@root", rootTaskId)).ToListAsync().ConfigureAwait(false)
                    ?? new List<UserWorkOrderLineVm>();
            }
            catch (SqlException)
            {
                return new List<UserWorkOrderLineVm>();
            }
        }

        static async Task<bool> ColumnExistsAsync(DriveCareDBEntities db, string table, string column)
        {
            try
            {
                const string sql = @"SELECT CASE WHEN COL_LENGTH(@t, @c) IS NOT NULL THEN 1 ELSE 0 END;";
                return await db.Database.SqlQuery<int>(sql,
                    new SqlParameter("@t", "dbo." + table),
                    new SqlParameter("@c", column)).FirstOrDefaultAsync().ConfigureAwait(false) == 1;
            }
            catch
            {
                return false;
            }
        }

        static async Task<bool> TableExistsAsync(DriveCareDBEntities db, string tableName)
        {
            try
            {
                var sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo." + tableName + @"', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return await db.Database.SqlQuery<int>(sql).FirstOrDefaultAsync().ConfigureAwait(false) == 1;
            }
            catch
            {
                return false;
            }
        }

        static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> action)
        {
            using (var db = new DriveCareDBEntities())
                return await action(db).ConfigureAwait(false);
        }

        sealed class DocumentHeaderRow
        {
            public Guid DocumentId { get; set; }
            public Guid RootTaskId { get; set; }
            public string Title { get; set; }
            public string VisitReason { get; set; }
            public string ServiceKind { get; set; }
            public byte Status { get; set; }
            public byte ClientStage { get; set; }
            public DateTime? CompletedAt { get; set; }
            public string WorkshopName { get; set; }
            public int? MileageKm { get; set; }
        }
    }
}
