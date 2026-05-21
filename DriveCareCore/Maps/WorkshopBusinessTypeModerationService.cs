using DriveCareCore.Data.BD;
using DriveCareCore.Data.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;

namespace DriveCareCore.Maps
{
    public enum WorkshopBusinessTypeChangeStatus : byte
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public sealed class WorkshopBusinessTypeChangeRequestInfo
    {
        public Guid RequestId { get; set; }
        public Guid WorkshopId { get; set; }
        public byte Status { get; set; }
        public string OwnerComment { get; set; }
        public string ModerationComment { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<Guid> RequestedTypeIds { get; set; } = new List<Guid>();
        public string RequestedTypesLabel { get; set; }
    }

    public sealed class WorkshopBusinessTypeModerationQueueRow
    {
        public Guid RequestId { get; set; }
        public Guid WorkshopId { get; set; }
        public string WorkshopName { get; set; }
        public string CompanyName { get; set; }
        public string CurrentTypesLabel { get; set; }
        public string RequestedTypesLabel { get; set; }
        public string RequesterName { get; set; }
        public string CreatedAtDisplay { get; set; }
        public string OwnerComment { get; set; }
    }

    public static class WorkshopBusinessTypeModerationService
    {
        public static bool TablesExist()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.WorkshopBusinessTypeChangeRequests', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static bool RequiresOwnerModeration() => TablesExist();

        public static (bool ok, string error) SubmitChangeRequest(
            Guid workshopId,
            Guid requestedByEmployeeId,
            IReadOnlyList<Guid> requestedTypeIds,
            string ownerComment = null)
        {
            if (!TablesExist())
                return (false, "Выполните SQL: DriveCareCore/Data/BD/Sql/WorkshopBusinessTypeChangeRequests_Tables.sql");

            if (workshopId == Guid.Empty)
                return (false, "Мастерская не указана.");
            if (requestedByEmployeeId == Guid.Empty)
                return (false, "Сотрудник не указан.");

            var ids = NormalizeTypeIds(requestedTypeIds);
            if (ids.Count == 0)
                return (false, "Выберите хотя бы один тип: автосервис, покраска или шиномонтаж.");

            var current = WorkshopBusinessTypesHelper.GetTypeIdsForWorkshop(workshopId);
            if (TypeSetsEqual(current, ids))
                return (false, "Типы не изменились по сравнению с действующими на карте.");

            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    var pendingId = db.Database.SqlQuery<Guid?>(@"
SELECT TOP 1 RowId FROM dbo.WorkshopBusinessTypeChangeRequests
WHERE WorkshopId = @p0 AND Status = @s
ORDER BY CreatedAt DESC;",
                        workshopId, (byte)WorkshopBusinessTypeChangeStatus.Pending).FirstOrDefault();

                    Guid requestId;
                    if (pendingId.HasValue && pendingId.Value != Guid.Empty)
                    {
                        requestId = pendingId.Value;
                        db.Database.ExecuteSqlCommand(@"
UPDATE dbo.WorkshopBusinessTypeChangeRequests
SET RequestedByEmployeeId = @e, OwnerComment = @c, CreatedAt = GETDATE(),
    ModerationComment = NULL, ModeratedByEmployeeId = NULL, ModeratedAt = NULL
WHERE RowId = @id;",
                            new SqlParameter("@e", requestedByEmployeeId),
                            new SqlParameter("@c", (object)(ownerComment ?? string.Empty) ?? DBNull.Value),
                            new SqlParameter("@id", requestId));

                        db.Database.ExecuteSqlCommand(
                            @"DELETE FROM dbo.WorkshopBusinessTypeChangeRequestTypes WHERE RequestId = @p0;",
                            requestId);
                    }
                    else
                    {
                        requestId = Guid.NewGuid();
                        db.Database.ExecuteSqlCommand(@"
INSERT INTO dbo.WorkshopBusinessTypeChangeRequests
    (RowId, WorkshopId, RequestedByEmployeeId, Status, OwnerComment, CreatedAt)
VALUES (@id, @w, @e, @s, @c, GETDATE());",
                            new SqlParameter("@id", requestId),
                            new SqlParameter("@w", workshopId),
                            new SqlParameter("@e", requestedByEmployeeId),
                            new SqlParameter("@s", (byte)WorkshopBusinessTypeChangeStatus.Pending),
                            new SqlParameter("@c", string.IsNullOrWhiteSpace(ownerComment) ? (object)DBNull.Value : ownerComment.Trim()));
                    }

                    foreach (var typeId in ids)
                    {
                        db.Database.ExecuteSqlCommand(@"
INSERT INTO dbo.WorkshopBusinessTypeChangeRequestTypes (RequestId, BusinessTypeId)
VALUES (@r, @t);",
                            new SqlParameter("@r", requestId),
                            new SqlParameter("@t", typeId));
                    }

                    db.SaveChanges();
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static WorkshopBusinessTypeChangeRequestInfo GetPendingForWorkshop(Guid workshopId)
        {
            if (!TablesExist() || workshopId == Guid.Empty)
                return null;

            try
            {
                var header = AppConnect.model1.Database.SqlQuery<RequestHeaderRow>(@"
SELECT TOP 1 RowId AS RequestId, WorkshopId, Status, OwnerComment, ModerationComment, CreatedAt
FROM dbo.WorkshopBusinessTypeChangeRequests
WHERE WorkshopId = @p0 AND Status = @s
ORDER BY CreatedAt DESC;",
                    workshopId, (byte)WorkshopBusinessTypeChangeStatus.Pending).FirstOrDefault();

                if (header == null || header.RequestId == Guid.Empty)
                    return null;

                var typeIds = LoadRequestedTypeIds(header.RequestId);
                return new WorkshopBusinessTypeChangeRequestInfo
                {
                    RequestId = header.RequestId,
                    WorkshopId = header.WorkshopId,
                    Status = header.Status,
                    OwnerComment = header.OwnerComment,
                    ModerationComment = header.ModerationComment,
                    CreatedAt = header.CreatedAt,
                    RequestedTypeIds = typeIds,
                    RequestedTypesLabel = WorkshopServiceKinds.BuildKindsLabel(typeIds)
                };
            }
            catch
            {
                return null;
            }
        }

        public static List<WorkshopBusinessTypeModerationQueueRow> ListPendingForAdmin()
        {
            if (!TablesExist())
                return new List<WorkshopBusinessTypeModerationQueueRow>();

            try
            {
                var headers = AppConnect.model1.Database.SqlQuery<QueueHeaderRow>(@"
SELECT TOP 200 r.RowId AS RequestId, r.WorkshopId, r.CreatedAt, r.OwnerComment,
       w.Name AS WorkshopName, c.Name AS CompanyName,
       e.LastName AS EmpLast, e.FirstName AS EmpFirst, e.MidName AS EmpMid
FROM dbo.WorkshopBusinessTypeChangeRequests r
INNER JOIN dbo.Workshops w ON w.RowId = r.WorkshopId
LEFT JOIN dbo.Companies c ON c.RowId = w.CompanyId
INNER JOIN dbo.Employees e ON e.RowId = r.RequestedByEmployeeId
WHERE r.Status = @p0
ORDER BY r.CreatedAt DESC;",
                    (byte)WorkshopBusinessTypeChangeStatus.Pending).ToList();

                var result = new List<WorkshopBusinessTypeModerationQueueRow>();
                foreach (var h in headers)
                {
                    var requestedIds = LoadRequestedTypeIds(h.RequestId);
                    var currentIds = WorkshopBusinessTypesHelper.GetTypeIdsForWorkshop(h.WorkshopId);
                    result.Add(new WorkshopBusinessTypeModerationQueueRow
                    {
                        RequestId = h.RequestId,
                        WorkshopId = h.WorkshopId,
                        WorkshopName = string.IsNullOrWhiteSpace(h.WorkshopName) ? "—" : h.WorkshopName.Trim(),
                        CompanyName = string.IsNullOrWhiteSpace(h.CompanyName) ? "—" : h.CompanyName.Trim(),
                        CurrentTypesLabel = WorkshopServiceKinds.BuildKindsLabel(currentIds),
                        RequestedTypesLabel = WorkshopServiceKinds.BuildKindsLabel(requestedIds),
                        RequesterName = FormatEmployeeName(h.EmpLast, h.EmpFirst, h.EmpMid),
                        CreatedAtDisplay = h.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                        OwnerComment = string.IsNullOrWhiteSpace(h.OwnerComment) ? "—" : h.OwnerComment.Trim()
                    });
                }

                return result;
            }
            catch
            {
                return new List<WorkshopBusinessTypeModerationQueueRow>();
            }
        }

        public static int CountPending()
        {
            if (!TablesExist())
                return 0;

            try
            {
                return AppConnect.model1.Database.SqlQuery<int>(@"
SELECT COUNT(1) FROM dbo.WorkshopBusinessTypeChangeRequests WHERE Status = @p0;",
                    (byte)WorkshopBusinessTypeChangeStatus.Pending).FirstOrDefault();
            }
            catch
            {
                return 0;
            }
        }

        public static (bool ok, string error) Approve(Guid requestId, Guid moderatorEmployeeId)
        {
            if (!TablesExist())
                return (false, "Таблицы заявок не созданы.");
            if (requestId == Guid.Empty)
                return (false, "Заявка не указана.");

            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    var header = db.Database.SqlQuery<RequestActionRow>(@"
SELECT RowId AS RequestId, WorkshopId, Status, RequestedByEmployeeId
FROM dbo.WorkshopBusinessTypeChangeRequests WHERE RowId = @p0;",
                        requestId).FirstOrDefault();

                    if (header == null)
                        return (false, "Заявка не найдена.");
                    if (header.Status != (byte)WorkshopBusinessTypeChangeStatus.Pending)
                        return (false, "Заявка уже обработана.");

                    var typeIds = LoadRequestedTypeIds(requestId);
                    var apply = WorkshopBusinessTypesHelper.SetTypeIdsForWorkshop(header.WorkshopId, typeIds);
                    if (!apply.ok)
                        return apply;

                    db.Database.ExecuteSqlCommand(@"
UPDATE dbo.WorkshopBusinessTypeChangeRequests
SET Status = @s, ModeratedByEmployeeId = @m, ModeratedAt = GETDATE()
WHERE RowId = @id;",
                        (byte)WorkshopBusinessTypeChangeStatus.Approved,
                        moderatorEmployeeId,
                        requestId);

                    db.SaveChanges();

                    WorkshopBusinessTypeModerationNotifier.NotifyRequester(
                        db,
                        header.RequestedByEmployeeId,
                        true,
                        header.WorkshopId,
                        WorkshopServiceKinds.BuildKindsLabel(typeIds),
                        null);
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static (bool ok, string error) Reject(Guid requestId, Guid moderatorEmployeeId, string moderationComment)
        {
            if (!TablesExist())
                return (false, "Таблицы заявок не созданы.");
            if (requestId == Guid.Empty)
                return (false, "Заявка не указана.");

            var comment = (moderationComment ?? string.Empty).Trim();
            if (comment.Length == 0)
                return (false, "Укажите причину отклонения.");

            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    var header = db.Database.SqlQuery<RequestActionRow>(@"
SELECT RowId AS RequestId, WorkshopId, Status, RequestedByEmployeeId
FROM dbo.WorkshopBusinessTypeChangeRequests WHERE RowId = @p0;",
                        requestId).FirstOrDefault();

                    if (header == null)
                        return (false, "Заявка не найдена.");
                    if (header.Status != (byte)WorkshopBusinessTypeChangeStatus.Pending)
                        return (false, "Заявка уже обработана.");

                    db.Database.ExecuteSqlCommand(@"
UPDATE dbo.WorkshopBusinessTypeChangeRequests
SET Status = @s, ModerationComment = @c, ModeratedByEmployeeId = @m, ModeratedAt = GETDATE()
WHERE RowId = @id;",
                        (byte)WorkshopBusinessTypeChangeStatus.Rejected,
                        comment,
                        moderatorEmployeeId,
                        requestId);

                    db.SaveChanges();

                    WorkshopBusinessTypeModerationNotifier.NotifyRequester(
                        db,
                        header.RequestedByEmployeeId,
                        false,
                        header.WorkshopId,
                        null,
                        comment);
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static (bool ok, string error) ApplyDirectly(Guid workshopId, IReadOnlyList<Guid> typeIds) =>
            WorkshopBusinessTypesHelper.SetTypeIdsForWorkshop(workshopId, typeIds);

        static List<Guid> LoadRequestedTypeIds(Guid requestId)
        {
            if (requestId == Guid.Empty)
                return new List<Guid>();

            return AppConnect.model1.Database.SqlQuery<Guid>(@"
SELECT BusinessTypeId FROM dbo.WorkshopBusinessTypeChangeRequestTypes
WHERE RequestId = @p0 ORDER BY BusinessTypeId;",
                requestId).ToList();
        }

        static List<Guid> NormalizeTypeIds(IReadOnlyList<Guid> typeIds) =>
            (typeIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();

        static bool TypeSetsEqual(IReadOnlyList<Guid> a, IReadOnlyList<Guid> b) =>
            NormalizeTypeIds(a).OrderBy(x => x).SequenceEqual(NormalizeTypeIds(b).OrderBy(x => x));

        static string FormatEmployeeName(string last, string first, string mid)
        {
            var parts = new[] { last, first, mid }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();
            return parts.Count == 0 ? "—" : string.Join(" ", parts);
        }

        sealed class RequestHeaderRow
        {
            public Guid RequestId { get; set; }
            public Guid WorkshopId { get; set; }
            public byte Status { get; set; }
            public Guid RequestedByEmployeeId { get; set; }
            public string OwnerComment { get; set; }
            public string ModerationComment { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        sealed class RequestActionRow
        {
            public Guid RequestId { get; set; }
            public Guid WorkshopId { get; set; }
            public byte Status { get; set; }
            public Guid RequestedByEmployeeId { get; set; }
        }

        sealed class QueueHeaderRow
        {
            public Guid RequestId { get; set; }
            public Guid WorkshopId { get; set; }
            public DateTime CreatedAt { get; set; }
            public string OwnerComment { get; set; }
            public string WorkshopName { get; set; }
            public string CompanyName { get; set; }
            public string EmpLast { get; set; }
            public string EmpFirst { get; set; }
            public string EmpMid { get; set; }
        }
    }
}
