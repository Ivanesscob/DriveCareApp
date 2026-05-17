using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.WorkshopServices
{
    public sealed class WorkshopServiceUnitItem
    {
        public Guid RowId { get; set; }
        public Guid WorkshopId { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; } = true;

        public string ActiveDisplay => IsActive ? "Да" : "Нет";
    }

    internal static class WorkshopServiceUnitsService
    {
        private static readonly string[] DefaultUnits = { "усл.", "ч.", "шт.", "компл.", "л." };

        public static async Task<List<WorkshopServiceUnitItem>> ListForWorkshopAsync(Guid workshopId, bool activeOnly = true)
        {
            try
            {
                await EnsureDefaultsAsync(workshopId).ConfigureAwait(false);

                return await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var sql = activeOnly
                        ? @"SELECT RowId, WorkshopId, Name, IsActive FROM WorkshopServiceUnits
                            WHERE WorkshopId = @p0 AND IsActive = 1 ORDER BY Name"
                        : @"SELECT RowId, WorkshopId, Name, IsActive FROM WorkshopServiceUnits
                            WHERE WorkshopId = @p0 ORDER BY Name";
                    return await db.Database.SqlQuery<WorkshopServiceUnitItem>(sql, workshopId).ToListAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return DefaultUnits.Select(n => new WorkshopServiceUnitItem { Name = n, IsActive = true }).ToList();
            }
        }

        public static async Task EnsureDefaultsAsync(Guid workshopId)
        {
            if (workshopId == Guid.Empty)
                return;

            try
            {
                await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var count = await db.Database.SqlQuery<int>(
                        "SELECT COUNT(*) FROM WorkshopServiceUnits WHERE WorkshopId = @p0", workshopId)
                        .FirstOrDefaultAsync().ConfigureAwait(false);

                    if (count > 0)
                        return;

                    foreach (var name in DefaultUnits)
                    {
                        await db.Database.ExecuteSqlCommandAsync(
                            @"INSERT INTO WorkshopServiceUnits (RowId, WorkshopId, Name, IsActive, CreatedAt)
                              VALUES (@p0, @p1, @p2, 1, @p3)",
                            Guid.NewGuid(), workshopId, name, DateTime.Now).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // table not created yet
            }
        }

        public static async Task<(bool ok, string error)> SaveAsync(WorkshopServiceUnitItem item, bool isNew)
        {
            if (item == null || item.WorkshopId == Guid.Empty)
                return (false, "Не указана мастерская.");
            if (string.IsNullOrWhiteSpace(item.Name))
                return (false, "Укажите название единицы.");

            item.RowId = isNew ? Guid.NewGuid() : item.RowId;
            item.Name = item.Name.Trim();
            if (item.Name.Length > 30)
                item.Name = item.Name.Substring(0, 30);

            try
            {
                await DatabaseExecutor.WithDbAsync(async db =>
                {
                    if (isNew)
                    {
                        await db.Database.ExecuteSqlCommandAsync(
                            @"INSERT INTO WorkshopServiceUnits (RowId, WorkshopId, Name, IsActive, CreatedAt)
                              VALUES (@p0, @p1, @p2, @p3, @p4)",
                            item.RowId, item.WorkshopId, item.Name, item.IsActive, DateTime.Now).ConfigureAwait(false);
                    }
                    else
                    {
                        await db.Database.ExecuteSqlCommandAsync(
                            @"UPDATE WorkshopServiceUnits SET Name = @p1, IsActive = @p2 WHERE RowId = @p0",
                            item.RowId, item.Name, item.IsActive).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
                return (true, null);
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return (false, "Такая единица измерения уже есть в справочнике.");
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return (false, "Таблица WorkshopServiceUnits не найдена. Выполните SQL-скрипт WorkshopServiceUnits_Tables.sql");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(bool ok, string error)> DeleteAsync(Guid unitId)
        {
            try
            {
                await DatabaseExecutor.WithDbAsync(db =>
                    db.Database.ExecuteSqlCommandAsync(
                        "DELETE FROM WorkshopServiceUnits WHERE RowId = @p0", unitId))
                    .ConfigureAwait(false);
                return (true, null);
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                return (false, "Единицу нельзя удалить: она используется в услугах. Отключите её вместо удаления.");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
