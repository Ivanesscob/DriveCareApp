using DriveCareCore.Data.BD;
using System;
using System.Data.Entity;
using System.Linq;

namespace DriveCareCore.Analytics
{
    public sealed class ActivityEventRequest
    {
        public string EventCode { get; set; }
        public byte ActorKind { get; set; } = ActivityActorKind.System;
        public Guid? UserId { get; set; }
        public Guid? EmployeeId { get; set; }
        public Guid? WorkshopId { get; set; }
        public Guid? CompanyId { get; set; }
        public string EntityType { get; set; }
        public Guid? EntityId { get; set; }
        public string PayloadJson { get; set; }
    }

    public static class ActivityEventService
    {
        private static bool? _tableExists;

        public static void LogFireAndForget(ActivityEventRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.EventCode))
                return;
            _ = TryLogAsync(request);
        }

        public static async System.Threading.Tasks.Task TryLogAsync(ActivityEventRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.EventCode))
                return;

            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    if (!await EnsureTableAsync(db).ConfigureAwait(false))
                        return;

                    var code = request.EventCode.Trim();
                    if (code.Length > 80)
                        code = code.Substring(0, 80);

                    var entityType = string.IsNullOrWhiteSpace(request.EntityType)
                        ? null
                        : request.EntityType.Trim();
                    if (entityType != null && entityType.Length > 60)
                        entityType = entityType.Substring(0, 60);

                    var payload = string.IsNullOrWhiteSpace(request.PayloadJson)
                        ? null
                        : request.PayloadJson;

                    await db.Database.ExecuteSqlCommandAsync(@"
INSERT INTO dbo.AppActivityEvents
    (RowId, EventCode, ActorKind, UserId, EmployeeId, WorkshopId, CompanyId, EntityType, EntityId, PayloadJson, CreatedAt)
VALUES
    (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, SYSUTCDATETIME())",
                        Guid.NewGuid(),
                        code,
                        request.ActorKind,
                        (object)request.UserId ?? DBNull.Value,
                        (object)request.EmployeeId ?? DBNull.Value,
                        (object)request.WorkshopId ?? DBNull.Value,
                        (object)request.CompanyId ?? DBNull.Value,
                        (object)entityType ?? DBNull.Value,
                        (object)request.EntityId ?? DBNull.Value,
                        (object)payload ?? DBNull.Value).ConfigureAwait(false);
                }
            }
            catch
            {
                // статистика не должна ломать основной сценарий
            }
        }

        private static async System.Threading.Tasks.Task<bool> EnsureTableAsync(DriveCareDBEntities db)
        {
            if (_tableExists == true)
                return true;
            if (_tableExists == false)
                return false;

            try
            {
                var exists = await db.Database.SqlQuery<int>(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.AppActivityEvents', N'U') IS NOT NULL THEN 1 ELSE 0 END")
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
                _tableExists = exists == 1;
            }
            catch
            {
                _tableExists = false;
            }

            return _tableExists == true;
        }
    }
}
