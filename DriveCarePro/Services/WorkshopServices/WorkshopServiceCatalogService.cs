using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.WorkshopServices
{
    internal static class WorkshopServiceCatalogService
    {
        public static async Task<List<WorkshopServiceItem>> ListForWorkshopAsync(Guid workshopId, bool activeOnly = true)
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var sql = activeOnly
                        ? @"SELECT s.RowId, s.WorkshopId, s.Name, s.Description, s.Price, s.UnitId,
                                   COALESCE(u.Name, s.UnitName, N'усл.') AS UnitName, s.IsActive
                            FROM WorkshopServices s
                            LEFT JOIN WorkshopServiceUnits u ON u.RowId = s.UnitId
                            WHERE s.WorkshopId = @p0 AND s.IsActive = 1
                            ORDER BY s.Name"
                        : @"SELECT s.RowId, s.WorkshopId, s.Name, s.Description, s.Price, s.UnitId,
                                   COALESCE(u.Name, s.UnitName, N'усл.') AS UnitName, s.IsActive
                            FROM WorkshopServices s
                            LEFT JOIN WorkshopServiceUnits u ON u.RowId = s.UnitId
                            WHERE s.WorkshopId = @p0
                            ORDER BY s.Name";
                    return await db.Database.SqlQuery<WorkshopServiceItem>(sql, workshopId).ToListAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return await ListLegacyAsync(workshopId, activeOnly).ConfigureAwait(false);
            }
        }

        private static async Task<List<WorkshopServiceItem>> ListLegacyAsync(Guid workshopId, bool activeOnly)
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var sql = activeOnly
                        ? @"SELECT RowId, WorkshopId, Name, Description, Price, UnitName, IsActive
                            FROM WorkshopServices WHERE WorkshopId = @p0 AND IsActive = 1 ORDER BY Name"
                        : @"SELECT RowId, WorkshopId, Name, Description, Price, UnitName, IsActive
                            FROM WorkshopServices WHERE WorkshopId = @p0 ORDER BY Name";
                    return await db.Database.SqlQuery<WorkshopServiceItem>(sql, workshopId).ToListAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return new List<WorkshopServiceItem>();
            }
        }

        public static async Task<(bool ok, string error)> SaveAsync(WorkshopServiceItem item, bool isNew)
        {
            if (item == null || item.WorkshopId == Guid.Empty)
                return (false, "Не указана мастерская.");
            if (string.IsNullOrWhiteSpace(item.Name))
                return (false, "Укажите название услуги.");

            item.RowId = isNew ? Guid.NewGuid() : item.RowId;
            item.Name = item.Name.Trim();
            if (item.Name.Length > 300)
                item.Name = item.Name.Substring(0, 300);

            item.Description = TruncateDescription(item.Description);
            await ResolveUnitNameAsync(item).ConfigureAwait(false);

            try
            {
                await DatabaseExecutor.WithDbAsync(async db =>
                {
                    if (isNew)
                    {
                        await TryInsertAsync(db, item).ConfigureAwait(false);
                    }
                    else
                    {
                        await TryUpdateWithUnitIdAsync(db, item).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
                return (true, null);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return (false, "Таблица WorkshopServices не найдена. Выполните SQL-скрипт WorkshopServices_Tables.sql");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static async Task TryInsertAsync(DriveCareDBEntities db, WorkshopServiceItem item)
        {
            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO WorkshopServices (RowId, WorkshopId, Name, Description, Price, UnitName, UnitId, IsActive, SortOrder, CreatedAt)
                      VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, 0, @p8)",
                    item.RowId, item.WorkshopId, item.Name,
                    (object)item.Description ?? DBNull.Value,
                    item.Price, item.UnitName,
                    (object)item.UnitId ?? DBNull.Value,
                    item.IsActive, DateTime.Now).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO WorkshopServices (RowId, WorkshopId, Name, Description, Price, UnitName, IsActive, SortOrder, CreatedAt)
                      VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, 0, @p7)",
                    item.RowId, item.WorkshopId, item.Name,
                    (object)item.Description ?? DBNull.Value,
                    item.Price, item.UnitName, item.IsActive, DateTime.Now).ConfigureAwait(false);
            }
        }

        private static async Task TryUpdateWithUnitIdAsync(DriveCareDBEntities db, WorkshopServiceItem item)
        {
            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"UPDATE WorkshopServices SET Name=@p1, Description=@p2, Price=@p3, UnitName=@p4, UnitId=@p5, IsActive=@p6 WHERE RowId=@p0",
                    item.RowId, item.Name, (object)item.Description ?? DBNull.Value,
                    item.Price, item.UnitName, (object)item.UnitId ?? DBNull.Value, item.IsActive).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"UPDATE WorkshopServices SET Name=@p1, Description=@p2, Price=@p3, UnitName=@p4, IsActive=@p5 WHERE RowId=@p0",
                    item.RowId, item.Name, (object)item.Description ?? DBNull.Value,
                    item.Price, item.UnitName, item.IsActive).ConfigureAwait(false);
            }
        }

        private static async Task ResolveUnitNameAsync(WorkshopServiceItem item)
        {
            if (item.UnitId.HasValue && item.UnitId.Value != Guid.Empty)
            {
                var unit = await DatabaseExecutor.WithDbAsync(db =>
                    db.Database.SqlQuery<WorkshopServiceUnitItem>(
                        "SELECT RowId, WorkshopId, Name, IsActive FROM WorkshopServiceUnits WHERE RowId = @p0",
                        item.UnitId.Value).FirstOrDefaultAsync()).ConfigureAwait(false);

                if (unit != null && !string.IsNullOrWhiteSpace(unit.Name))
                {
                    item.UnitName = unit.Name.Trim();
                    return;
                }
            }

            item.UnitName = string.IsNullOrWhiteSpace(item.UnitName) ? "усл." : item.UnitName.Trim();
            if (item.UnitName.Length > 30)
                item.UnitName = item.UnitName.Substring(0, 30);
        }

        private static string TruncateDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return null;
            var text = description.Trim();
            return text.Length <= WorkshopServiceItem.MaxDescriptionLength
                ? text
                : text.Substring(0, WorkshopServiceItem.MaxDescriptionLength);
        }

        public static async Task<(bool ok, string error)> DeleteAsync(Guid serviceId)
        {
            try
            {
                await DatabaseExecutor.WithDbAsync(db =>
                    db.Database.ExecuteSqlCommandAsync(
                        "DELETE FROM WorkshopServices WHERE RowId = @p0", serviceId))
                    .ConfigureAwait(false);
                return (true, null);
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                return (false, "Услугу нельзя удалить: она уже используется в отчётах. Можно отключить через редактирование.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
