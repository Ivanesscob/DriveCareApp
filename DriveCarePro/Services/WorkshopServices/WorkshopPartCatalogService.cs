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
    internal static class WorkshopPartCatalogService
    {
        /// <summary>Склад: позиции с остатком &gt; 0.</summary>
        public static Task<List<WorkshopPartItem>> ListStockForWorkshopAsync(Guid workshopId) =>
            ListPickerForWorkshopAsync(workshopId, null);

        /// <summary>
        /// Список для выбора в задании: остаток &gt; 0 и/или детали, уже указанные в отчёте задания
        /// (чтобы после удаления строки можно было снова выбрать ту же позицию).
        /// </summary>
        public static async Task<List<WorkshopPartItem>> ListPickerForWorkshopAsync(
            Guid workshopId,
            IEnumerable<Guid> alsoIncludePartIds)
        {
            if (workshopId == Guid.Empty)
                return new List<WorkshopPartItem>();

            var extraIds = (alsoIncludePartIds ?? Enumerable.Empty<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            try
            {
                var fromDb = await DatabaseExecutor.WithDbAsync(db =>
                {
                    var query = db.WorkshopParts.AsNoTracking()
                        .Where(p => p.WorkshopId == workshopId && p.IsActive);

                    if (extraIds.Count > 0)
                    {
                        return query
                            .Where(p => p.QuantityOnHand > 0 || extraIds.Contains(p.RowId))
                            .OrderBy(p => p.Name)
                            .ToListAsync();
                    }

                    return query
                        .Where(p => p.QuantityOnHand > 0)
                        .OrderBy(p => p.Name)
                        .ToListAsync();
                }).ConfigureAwait(false);

                return fromDb.Select(MapFromEntity).ToList();
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return new List<WorkshopPartItem>();
            }
        }

        /// <summary>Магазин — категория как у пользователя DriveCare.</summary>
        public static async Task<List<WorkshopPartItem>> ListShopForWorkshopAsync(Guid workshopId, string category)
        {
            _ = workshopId;
            var catalog = await UserPartsCatalogService.ListByCategoryAsync(category).ConfigureAwait(false);
            return catalog
                .Select(ToWorkshopPart)
                .Select(p =>
                {
                    p.QuantityOnHand = 0m;
                    return p;
                })
                .ToList();
        }

        public static string CategoryRu(string category) => ToolsStoreCatalog.CategoryRu(category);

        private static WorkshopPartItem MapFromEntity(WorkshopPart p) => new WorkshopPartItem
        {
            RowId = p.RowId,
            WorkshopId = p.WorkshopId,
            Name = p.Name,
            Article = p.Article,
            Description = p.Description,
            Price = p.Price,
            UnitName = p.UnitName ?? "шт.",
            QuantityOnHand = p.QuantityOnHand,
            Category = p.Category ?? "Accessories",
            IsActive = p.IsActive
        };

        private static WorkshopPartItem ToWorkshopPart(ShopCatalogProduct p) => new WorkshopPartItem
        {
            RowId = p.ProductId,
            WorkshopId = Guid.Empty,
            Name = p.Name,
            Article = p.Category,
            Description = p.Description,
            Price = p.Price,
            UnitName = p.UnitName ?? "шт.",
            QuantityOnHand = 0,
            Category = p.Category,
            IsActive = true
        };
    }
}
