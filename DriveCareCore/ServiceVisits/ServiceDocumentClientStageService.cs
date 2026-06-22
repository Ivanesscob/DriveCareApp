using DriveCareCore.Data.BD;
using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.ServiceVisits
{
    public static class ServiceDocumentClientStageService
    {
        public static Task TrySetStageForRootTaskAsync(Guid rootTaskId, ServiceDocumentClientStage stage) =>
            WithDb(db => TrySetStageForRootTaskAsync(db, rootTaskId, stage));

        public static async Task TrySetStageForRootTaskAsync(
            DriveCareDBEntities db,
            Guid rootTaskId,
            ServiceDocumentClientStage stage)
        {
            if (rootTaskId == Guid.Empty || !await ColumnExistsAsync(db).ConfigureAwait(false))
                return;

            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"UPDATE dbo.ServiceDocuments SET ClientStage = @st
                      WHERE RootTaskId = @id AND Status = 0",
                    new SqlParameter("@st", (byte)stage),
                    new SqlParameter("@id", rootTaskId)).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
            }
        }

        public static Task TrySetStageForDocumentAsync(Guid documentId, ServiceDocumentClientStage stage) =>
            WithDb(db => TrySetStageForDocumentAsync(db, documentId, stage));

        public static async Task TrySetStageForDocumentAsync(
            DriveCareDBEntities db,
            Guid documentId,
            ServiceDocumentClientStage stage)
        {
            if (documentId == Guid.Empty || !await ColumnExistsAsync(db).ConfigureAwait(false))
                return;

            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"UPDATE dbo.ServiceDocuments SET ClientStage = @st WHERE RowId = @id",
                    new SqlParameter("@st", (byte)stage),
                    new SqlParameter("@id", documentId)).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
            }
        }

        public static Task TryMarkReadyForPickupAsync(Guid rootTaskId) =>
            TrySetStageForRootTaskAsync(rootTaskId, ServiceDocumentClientStage.ReadyForPickup);

        public static Task TryMarkInRepairAsync(Guid rootTaskId) =>
            TrySetStageForRootTaskAsync(rootTaskId, ServiceDocumentClientStage.InRepair);

        public static Task TryMarkAcceptedAsync(Guid rootTaskId) =>
            TrySetStageForRootTaskAsync(rootTaskId, ServiceDocumentClientStage.Accepted);

        public static Task TryMarkAcceptedAsync(DriveCareDBEntities db, Guid rootTaskId) =>
            TrySetStageForRootTaskAsync(db, rootTaskId, ServiceDocumentClientStage.Accepted);

        public static Task TryMarkInRepairAsync(DriveCareDBEntities db, Guid rootTaskId) =>
            TrySetStageForRootTaskAsync(db, rootTaskId, ServiceDocumentClientStage.InRepair);

        public static Task TryMarkReadyForPickupAsync(DriveCareDBEntities db, Guid rootTaskId) =>
            TrySetStageForRootTaskAsync(db, rootTaskId, ServiceDocumentClientStage.ReadyForPickup);

        public static Task<ServiceDocumentClientStage?> TryGetStageForRootTaskAsync(DriveCareDBEntities db, Guid rootTaskId)
        {
            if (rootTaskId == Guid.Empty)
                return Task.FromResult<ServiceDocumentClientStage?>(null);

            return GetStageCoreAsync(db, rootTaskId);
        }

        static async Task<ServiceDocumentClientStage?> GetStageCoreAsync(DriveCareDBEntities db, Guid rootTaskId)
        {
            if (!await ColumnExistsAsync(db).ConfigureAwait(false))
                return null;

            try
            {
                var row = await db.Database.SqlQuery<StageRow>(
                    @"SELECT TOP 1 Status, ClientStage FROM dbo.ServiceDocuments WHERE RootTaskId = @id ORDER BY CreatedAt DESC",
                    new SqlParameter("@id", rootTaskId)).FirstOrDefaultAsync().ConfigureAwait(false);

                if (row == null)
                    return null;

                return ServiceDocumentClientStageLabels.Normalize(row.ClientStage, row.Status);
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return null;
            }
        }

        sealed class StageRow
        {
            public byte Status { get; set; }
            public byte ClientStage { get; set; }
        }

        public static async Task TryFinalizeDocumentAsync(DriveCareDBEntities db, Guid rootTaskId)
        {
            if (rootTaskId == Guid.Empty)
                return;

            var hasStage = await ColumnExistsAsync(db).ConfigureAwait(false);
            try
            {
                if (hasStage)
                {
                    await db.Database.ExecuteSqlCommandAsync(
                        @"UPDATE dbo.ServiceDocuments SET Status = 1, CompletedAt = @dt, ClientStage = @st
                          WHERE RootTaskId = @id AND Status = 0",
                        new SqlParameter("@dt", DateTime.Now),
                        new SqlParameter("@st", (byte)ServiceDocumentClientStage.Completed),
                        new SqlParameter("@id", rootTaskId)).ConfigureAwait(false);
                }
                else
                {
                    await db.Database.ExecuteSqlCommandAsync(
                        @"UPDATE dbo.ServiceDocuments SET Status = 1, CompletedAt = @dt
                          WHERE RootTaskId = @id AND Status = 0",
                        new SqlParameter("@dt", DateTime.Now),
                        new SqlParameter("@id", rootTaskId)).ConfigureAwait(false);
                }
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
            }
        }

        public static Task TryFinalizeDocumentAsync(Guid rootTaskId) =>
            WithDb(db => TryFinalizeDocumentAsync(db, rootTaskId));

        static async Task<bool> ColumnExistsAsync(DriveCareDBEntities db)
        {
            try
            {
                const string sql = @"SELECT CASE WHEN COL_LENGTH(N'dbo.ServiceDocuments', N'ClientStage') IS NOT NULL THEN 1 ELSE 0 END;";
                return await db.Database.SqlQuery<int>(sql).FirstOrDefaultAsync().ConfigureAwait(false) == 1;
            }
            catch
            {
                return false;
            }
        }

        static async Task WithDb(Func<DriveCareDBEntities, Task> action)
        {
            using (var db = new DriveCareDBEntities())
                await action(db).ConfigureAwait(false);
        }
    }
}
