using DriveCareCore.Data.BD;
using DriveCareCore.Shop;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.WorkshopServices
{
    internal static class WorkshopStockService
    {
        public static async Task<(bool ok, string error)> ReceivePurchaseLinesAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            IList<TaskPartLineRow> lines)
        {
            if (workshopId == Guid.Empty)
                return (false, "Не указана мастерская для приёмки на склад.");

            if (!await TableExistsAsync(db).ConfigureAwait(false))
                return (false, "Таблица WorkshopParts не найдена. Выполните скрипт WorkshopParts_Tables.sql на DriveCareDB.");

            var incoming = (lines ?? Array.Empty<TaskPartLineRow>())
                .Where(l => !string.IsNullOrWhiteSpace(l.PartName) && l.Quantity > 0)
                .ToList();

            if (incoming.Count == 0)
                return (true, null);

            foreach (var line in incoming)
            {
                var partId = await ReceiveLineAsync(db, workshopId, line).ConfigureAwait(false);
                line.WorkshopPartId = partId;
            }

            return (true, null);
        }

        /// <summary>Списание со склада при использовании в задании (ремонт или списание после закупки).</summary>
        public static async Task<(bool ok, string error)> ConsumeStockAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            IList<TaskPartLineRow> lines)
        {
            if (workshopId == Guid.Empty)
                return (false, "Не указана мастерская.");

            if (!await TableExistsAsync(db).ConfigureAwait(false))
                return (false, "Таблица WorkshopParts не найдена.");

            var usage = (lines ?? Array.Empty<TaskPartLineRow>())
                .Where(l => !string.IsNullOrWhiteSpace(l.PartName) && l.Quantity > 0)
                .ToList();

            if (usage.Count == 0)
                return (true, null);

            foreach (var line in usage)
            {
                if (!line.WorkshopPartId.HasValue || line.WorkshopPartId.Value == Guid.Empty)
                    return (false, "Все детали в отчёте должны быть выбраны со склада.");
            }

            var grouped = usage
                .GroupBy(l => l.WorkshopPartId.Value)
                .Select(g => new { PartId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToList();

            foreach (var g in grouped)
            {
                var part = await db.WorkshopParts.FirstOrDefaultAsync(p =>
                    p.RowId == g.PartId && p.WorkshopId == workshopId).ConfigureAwait(false);

                if (part == null)
                    return (false, "Деталь не найдена на складе мастерской.");

                if (part.QuantityOnHand < g.Qty)
                    return (false,
                        $"Недостаточно на складе: «{part.Name}» (есть {part.QuantityOnHand:0.###}, нужно {g.Qty:0.###}).");
            }

            foreach (var g in grouped)
            {
                var part = await db.WorkshopParts.FirstAsync(p => p.RowId == g.PartId).ConfigureAwait(false);
                part.QuantityOnHand -= g.Qty;
            }

            await db.SaveChangesAsync().ConfigureAwait(false);
            return (true, null);
        }

        /// <summary>Возврат на склад (удаление строки из задания или уменьшение количества).</summary>
        public static async Task<(bool ok, string error)> ReturnStockAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            IList<TaskPartLineRow> lines)
        {
            if (workshopId == Guid.Empty)
                return (false, "Не указана мастерская.");

            if (!await TableExistsAsync(db).ConfigureAwait(false))
                return (false, "Таблица WorkshopParts не найдена.");

            var returns = (lines ?? Array.Empty<TaskPartLineRow>())
                .Where(l => !string.IsNullOrWhiteSpace(l.PartName) && l.Quantity > 0)
                .ToList();

            if (returns.Count == 0)
                return (true, null);

            foreach (var line in returns)
            {
                if (!line.WorkshopPartId.HasValue || line.WorkshopPartId.Value == Guid.Empty)
                    return (false, "Не указана деталь склада для возврата.");
            }

            var grouped = returns
                .GroupBy(l => l.WorkshopPartId.Value)
                .Select(g => new { PartId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToList();

            foreach (var g in grouped)
            {
                var part = await db.WorkshopParts.FirstOrDefaultAsync(p =>
                    p.RowId == g.PartId && p.WorkshopId == workshopId).ConfigureAwait(false);

                if (part == null)
                    return (false, "Деталь не найдена на складе мастерской.");

                part.QuantityOnHand += g.Qty;
            }

            await db.SaveChangesAsync().ConfigureAwait(false);
            return (true, null);
        }

        public static async Task<Guid> ResolveWorkshopIdForPurchaseAsync(
            DriveCareDBEntities db,
            Guid purchaserEmployeeId,
            Guid sourceTaskId)
        {
            var purchaser = await db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.RowId == purchaserEmployeeId).ConfigureAwait(false);
            if (purchaser?.WorkshopId.HasValue == true && purchaser.WorkshopId.Value != Guid.Empty)
                return purchaser.WorkshopId.Value;

            var sourceTask = await db.Tasks.AsNoTracking()
                .FirstOrDefaultAsync(t => t.RowId == sourceTaskId).ConfigureAwait(false);
            if (sourceTask == null)
                return Guid.Empty;

            var requester = await db.Employees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.RowId == sourceTask.EmployeeId).ConfigureAwait(false);
            if (requester?.WorkshopId.HasValue == true)
                return requester.WorkshopId.Value;

            return Guid.Empty;
        }

        private static async Task<Guid> ReceiveLineAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            TaskPartLineRow line)
        {
            var qty = line.Quantity;
            var name = (line.PartName ?? string.Empty).Trim();
            var unit = string.IsNullOrWhiteSpace(line.UnitName) ? "шт." : line.UnitName.Trim();

            WorkshopPart existing = null;

            if (line.WorkshopPartId.HasValue && line.WorkshopPartId.Value != Guid.Empty)
            {
                existing = await db.WorkshopParts.FirstOrDefaultAsync(p =>
                    p.RowId == line.WorkshopPartId.Value && p.WorkshopId == workshopId).ConfigureAwait(false);
            }

            if (existing == null)
            {
                var candidates = await db.WorkshopParts
                    .Where(p => p.WorkshopId == workshopId)
                    .ToListAsync().ConfigureAwait(false);

                existing = candidates.FirstOrDefault(p =>
                    string.Equals((p.Name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
            }

            if (existing != null)
            {
                existing.QuantityOnHand += qty;
                if (line.UnitPrice > 0)
                    existing.Price = line.UnitPrice;
                if (!existing.IsActive)
                    existing.IsActive = true;
                if (!string.IsNullOrWhiteSpace(unit))
                    existing.UnitName = unit;

                await db.SaveChangesAsync().ConfigureAwait(false);
                return existing.RowId;
            }

            var catalog = line.WorkshopPartId.HasValue
                ? ToolsStoreCatalog.FindById(line.WorkshopPartId.Value)
                : null;

            var newId = Guid.NewGuid();
            if (line.WorkshopPartId.HasValue && line.WorkshopPartId.Value != Guid.Empty)
            {
                var idTaken = await db.WorkshopParts.AsNoTracking()
                    .AnyAsync(p => p.RowId == line.WorkshopPartId.Value).ConfigureAwait(false);
                if (!idTaken)
                    newId = line.WorkshopPartId.Value;
            }

            var category = catalog?.Category ?? "Accessories";
            var part = new WorkshopPart
            {
                RowId = newId,
                WorkshopId = workshopId,
                Name = catalog?.Name ?? name,
                Article = catalog?.Category,
                Description = catalog?.Description,
                Price = line.UnitPrice > 0 ? line.UnitPrice : (catalog?.Price ?? 0),
                UnitName = unit,
                QuantityOnHand = qty,
                Category = category,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            db.WorkshopParts.Add(part);
            await db.SaveChangesAsync().ConfigureAwait(false);
            return part.RowId;
        }

        private static async Task<bool> TableExistsAsync(DriveCareDBEntities db)
        {
            try
            {
                var id = await db.Database.SqlQuery<int?>(
                    "SELECT OBJECT_ID(N'dbo.WorkshopParts', N'U')").FirstOrDefaultAsync().ConfigureAwait(false);
                return id.HasValue && id.Value > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
