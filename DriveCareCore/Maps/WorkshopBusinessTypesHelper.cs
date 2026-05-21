using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using DriveCareCore.Data.BD;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.Maps
{
    public static class WorkshopBusinessTypesHelper
    {
        public static bool JunctionTableExists()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.WorkshopBusinessTypes', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static Task<Dictionary<Guid, List<Guid>>> LoadTypeIdsByWorkshopAsync() =>
            WithDb(LoadTypeIdsByWorkshopAsync);

        public static async Task<Dictionary<Guid, List<Guid>>> LoadTypeIdsByWorkshopAsync(DriveCareDBEntities db)
        {
            var map = new Dictionary<Guid, List<Guid>>();
            if (!JunctionTableExists())
                return map;

            var rows = await db.Database.SqlQuery<TypeMapRow>(@"
SELECT WorkshopId, BusinessTypeId
FROM dbo.WorkshopBusinessTypes
ORDER BY WorkshopId;").ToListAsync().ConfigureAwait(false);

            foreach (var row in rows)
            {
                if (row.WorkshopId == Guid.Empty || row.BusinessTypeId == Guid.Empty)
                    continue;
                if (!map.TryGetValue(row.WorkshopId, out var list))
                {
                    list = new List<Guid>();
                    map[row.WorkshopId] = list;
                }
                if (!list.Contains(row.BusinessTypeId))
                    list.Add(row.BusinessTypeId);
            }

            return map;
        }

        public static async Task EnsurePrimaryTypeAsync(Guid workshopId, Guid businessTypeId)
        {
            using (var db = new DriveCareDBEntities())
                await EnsurePrimaryTypeAsync(db, workshopId, businessTypeId).ConfigureAwait(false);
        }

        public static async Task EnsurePrimaryTypeAsync(DriveCareDBEntities db, Guid workshopId, Guid businessTypeId)
        {
            if (workshopId == Guid.Empty || businessTypeId == Guid.Empty)
                return;
            if (!JunctionTableExists())
                return;

            await db.Database.ExecuteSqlCommandAsync(@"
IF NOT EXISTS (SELECT 1 FROM dbo.WorkshopBusinessTypes WHERE WorkshopId = @w AND BusinessTypeId = @t)
    INSERT INTO dbo.WorkshopBusinessTypes (RowId, WorkshopId, BusinessTypeId) VALUES (NEWID(), @w, @t);",
                new SqlParameter("@w", workshopId),
                new SqlParameter("@t", businessTypeId)).ConfigureAwait(false);
        }

        public static List<Guid> GetTypeIdsForWorkshop(Guid workshopId)
        {
            if (workshopId == Guid.Empty)
                return new List<Guid>();

            if (!JunctionTableExists())
            {
                try
                {
                    const string sql = @"SELECT BusinessTypeId FROM dbo.Workshops WHERE RowId = @p0 AND BusinessTypeId IS NOT NULL;";
                    var id = AppConnect.model1.Database.SqlQuery<Guid?>(sql, workshopId).FirstOrDefault();
                    return id.HasValue && id.Value != Guid.Empty ? new List<Guid> { id.Value } : new List<Guid>();
                }
                catch
                {
                    return new List<Guid>();
                }
            }

            try
            {
                return AppConnect.model1.Database.SqlQuery<Guid>(@"
SELECT BusinessTypeId FROM dbo.WorkshopBusinessTypes WHERE WorkshopId = @p0 ORDER BY BusinessTypeId;",
                    workshopId).ToList();
            }
            catch
            {
                return new List<Guid>();
            }
        }

        public static (bool ok, string error) SetTypeIdsForWorkshop(Guid workshopId, IReadOnlyList<Guid> typeIds)
        {
            if (workshopId == Guid.Empty)
                return (false, "Мастерская не указана.");

            var ids = (typeIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
            if (ids.Count == 0)
                return (false, "Выберите хотя бы один тип: автосервис, покраска или шиномонтаж.");

            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    if (JunctionTableExists())
                    {
                        db.Database.ExecuteSqlCommand(
                            @"DELETE FROM dbo.WorkshopBusinessTypes WHERE WorkshopId = @p0;",
                            new SqlParameter("@p0", workshopId));

                        foreach (var typeId in ids)
                        {
                            db.Database.ExecuteSqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.WorkshopBusinessTypes WHERE WorkshopId = @w AND BusinessTypeId = @t)
    INSERT INTO dbo.WorkshopBusinessTypes (RowId, WorkshopId, BusinessTypeId) VALUES (NEWID(), @w, @t);",
                                new SqlParameter("@w", workshopId),
                                new SqlParameter("@t", typeId));
                        }
                    }

                    db.Database.ExecuteSqlCommand(
                        @"UPDATE dbo.Workshops SET BusinessTypeId = @p0 WHERE RowId = @p1;",
                        ids[0],
                        workshopId);
                    db.SaveChanges();
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> work)
        {
            using (var db = new DriveCareDBEntities())
                return await work(db).ConfigureAwait(false);
        }

        sealed class TypeMapRow
        {
            public Guid WorkshopId { get; set; }
            public Guid BusinessTypeId { get; set; }
        }
    }
}
