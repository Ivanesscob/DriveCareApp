using DriveCareCore.Data.BD;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.WorkshopServices
{
    /// <summary>Поддержка TaskPartLines: новая схема (UnitName, LineAmount) и старая (Unit, Amount).</summary>
    internal static class TaskPartLineSql
    {
        private static bool? _legacyUnitAmountColumns;

        public static async Task<bool> TableExistsAsync(DriveCareDBEntities db)
        {
            try
            {
                var id = await db.Database.SqlQuery<int?>(
                    "SELECT OBJECT_ID(N'dbo.TaskPartLines', N'U')").FirstOrDefaultAsync().ConfigureAwait(false);
                return id.HasValue && id > 0;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> UsesLegacyUnitAmountColumnsAsync(DriveCareDBEntities db)
        {
            if (_legacyUnitAmountColumns.HasValue)
                return _legacyUnitAmountColumns.Value;

            try
            {
                if (!await TableExistsAsync(db).ConfigureAwait(false))
                {
                    _legacyUnitAmountColumns = false;
                    return false;
                }

                var unitNameLen = await db.Database.SqlQuery<int?>(
                    "SELECT COL_LENGTH(N'dbo.TaskPartLines', N'UnitName')").FirstOrDefaultAsync().ConfigureAwait(false);
                _legacyUnitAmountColumns = !unitNameLen.HasValue || unitNameLen.Value <= 0;
            }
            catch
            {
                _legacyUnitAmountColumns = false;
            }

            return _legacyUnitAmountColumns.Value;
        }

        public static async Task<System.Collections.Generic.List<TaskPartLineRow>> LoadAsync(
            DriveCareDBEntities db,
            Guid taskId)
        {
            if (!await TableExistsAsync(db).ConfigureAwait(false))
                return new System.Collections.Generic.List<TaskPartLineRow>();

            if (await UsesLegacyUnitAmountColumnsAsync(db).ConfigureAwait(false))
            {
                return await db.Database.SqlQuery<TaskPartLineRow>(
                    @"SELECT RowId, PartName, Quantity, Unit AS UnitName, UnitPrice, Amount AS LineAmount
                      FROM TaskPartLines WHERE TaskId = @p0 ORDER BY SortOrder",
                    taskId).ToListAsync().ConfigureAwait(false);
            }

            try
            {
                return await db.Database.SqlQuery<TaskPartLineRow>(
                    @"SELECT RowId, WorkshopPartId, PartName, Quantity, UnitName, UnitPrice, LineAmount
                      FROM TaskPartLines WHERE TaskId = @p0 ORDER BY SortOrder",
                    taskId).ToListAsync().ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                return await db.Database.SqlQuery<TaskPartLineRow>(
                    @"SELECT RowId, PartName, Quantity, UnitName, UnitPrice, LineAmount
                      FROM TaskPartLines WHERE TaskId = @p0 ORDER BY SortOrder",
                    taskId).ToListAsync().ConfigureAwait(false);
            }
        }

        public static async Task InsertAsync(DriveCareDBEntities db, Guid taskId, TaskPartLineRow p, int sortOrder)
        {
            var unit = p.UnitName ?? "шт.";
            var amount = p.LineAmount;

            if (await UsesLegacyUnitAmountColumnsAsync(db).ConfigureAwait(false))
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO TaskPartLines (RowId, TaskId, PartName, Quantity, Unit, UnitPrice, Amount, SortOrder)
                      VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)",
                    Guid.NewGuid(), taskId, p.PartName.Trim(), p.Quantity, unit, p.UnitPrice, amount, sortOrder)
                    .ConfigureAwait(false);
                return;
            }

            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO TaskPartLines (RowId, TaskId, WorkshopPartId, PartName, Quantity, UnitName, UnitPrice, LineAmount, SortOrder)
                      VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8)",
                    Guid.NewGuid(), taskId,
                    (object)p.WorkshopPartId ?? DBNull.Value,
                    p.PartName.Trim(), p.Quantity, unit, p.UnitPrice, amount, sortOrder)
                    .ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO TaskPartLines (RowId, TaskId, PartName, Quantity, UnitName, UnitPrice, LineAmount, SortOrder)
                      VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)",
                    Guid.NewGuid(), taskId, p.PartName.Trim(), p.Quantity, unit, p.UnitPrice, amount, sortOrder)
                    .ConfigureAwait(false);
            }
        }
    }
}
