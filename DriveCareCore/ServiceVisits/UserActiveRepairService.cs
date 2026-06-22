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
    public sealed class UserCarRepairStatusVm
    {
        public Guid DocumentId { get; set; }
        public Guid CarId { get; set; }
        public string WorkshopName { get; set; }
        public string StageLabel { get; set; }
        public int StageStep { get; set; }
        public ServiceDocumentClientStage Stage { get; set; }
        public DateTime UpdatedAt { get; set; }

        public bool IsActive => Stage != ServiceDocumentClientStage.Completed;
        public string Step1State => StageStep >= 1 ? "done" : "pending";
        public string Step2State => StageStep >= 2 ? "done" : "pending";
        public string Step3State => StageStep >= 3 ? "done" : "pending";
    }

    public static class UserActiveRepairService
    {
        public static Task<UserCarRepairStatusVm> TryLoadForCarAsync(Guid userId, Guid carId) =>
            WithDb(db => TryLoadForCarAsync(db, userId, carId));

        public static async Task<UserCarRepairStatusVm> TryLoadForCarAsync(
            DriveCareDBEntities db,
            Guid userId,
            Guid carId)
        {
            if (userId == Guid.Empty || carId == Guid.Empty)
                return null;

            if (!await TableExistsAsync(db, "ServiceDocuments").ConfigureAwait(false))
                return null;

            var hasStage = await ColumnExistsAsync(db, "ServiceDocuments", "ClientStage").ConfigureAwait(false);

            try
            {
                var sql = hasStage
                    ? @"SELECT TOP 1 sd.RowId AS DocumentId, sd.CarId, sd.Status, sd.ClientStage,
                              sd.CreatedAt AS UpdatedAt, w.Name AS WorkshopName
                       FROM dbo.ServiceDocuments sd
                       INNER JOIN dbo.Workshops w ON w.RowId = sd.WorkshopId
                       WHERE sd.ClientUserId = @u AND sd.CarId = @c AND sd.Status = 0
                       ORDER BY sd.CreatedAt DESC"
                    : @"SELECT TOP 1 sd.RowId AS DocumentId, sd.CarId, sd.Status,
                              CAST(2 AS TINYINT) AS ClientStage,
                              sd.CreatedAt AS UpdatedAt, w.Name AS WorkshopName
                       FROM dbo.ServiceDocuments sd
                       INNER JOIN dbo.Workshops w ON w.RowId = sd.WorkshopId
                       WHERE sd.ClientUserId = @u AND sd.CarId = @c AND sd.Status = 0
                       ORDER BY sd.CreatedAt DESC";

                var row = await db.Database.SqlQuery<RepairRow>(sql,
                    new SqlParameter("@u", userId),
                    new SqlParameter("@c", carId)).FirstOrDefaultAsync().ConfigureAwait(false);

                if (row == null || row.DocumentId == Guid.Empty)
                    return null;

                var stage = ServiceDocumentClientStageLabels.Normalize(row.ClientStage, row.Status);
                if (stage == ServiceDocumentClientStage.Completed)
                    return null;

                return new UserCarRepairStatusVm
                {
                    DocumentId = row.DocumentId,
                    CarId = row.CarId,
                    WorkshopName = row.WorkshopName ?? "Автосервис",
                    Stage = stage,
                    StageLabel = ServiceDocumentClientStageLabels.ForUser(stage),
                    StageStep = ServiceDocumentClientStageLabels.StepIndex(stage),
                    UpdatedAt = row.UpdatedAt
                };
            }
            catch (SqlException)
            {
                return null;
            }
        }

        public static Task<List<UserCarRepairStatusVm>> ListActiveAsync(Guid userId) =>
            WithDb(async db =>
            {
                if (userId == Guid.Empty)
                    return new List<UserCarRepairStatusVm>();

                var cars = await db.UserCars.AsNoTracking()
                    .Where(uc => uc.UserId == userId)
                    .Select(uc => uc.CarId)
                    .Distinct()
                    .ToListAsync().ConfigureAwait(false);

                var list = new List<UserCarRepairStatusVm>();
                foreach (var carId in cars)
                {
                    var item = await TryLoadForCarAsync(db, userId, carId).ConfigureAwait(false);
                    if (item != null)
                        list.Add(item);
                }

                return list;
            });

        static async Task<bool> TableExistsAsync(DriveCareDBEntities db, string table)
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(@t, N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return await db.Database.SqlQuery<int>(sql, new SqlParameter("@t", "dbo." + table))
                    .FirstOrDefaultAsync().ConfigureAwait(false) == 1;
            }
            catch
            {
                return false;
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

        static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> action)
        {
            using (var db = new DriveCareDBEntities())
                return await action(db).ConfigureAwait(false);
        }

        sealed class RepairRow
        {
            public Guid DocumentId { get; set; }
            public Guid CarId { get; set; }
            public byte Status { get; set; }
            public byte ClientStage { get; set; }
            public DateTime UpdatedAt { get; set; }
            public string WorkshopName { get; set; }
        }
    }
}
