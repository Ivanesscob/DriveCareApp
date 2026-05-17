using DriveCareCore.Shop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.WorkshopServices
{
    internal static class WorkshopPartCatalogService
    {
        /// <summary>Склад в задании — тот же каталог, что в пользовательском магазине (все категории).</summary>
        public static Task<List<WorkshopPartItem>> ListStockForWorkshopAsync(Guid workshopId)
        {
            _ = workshopId;
            var items = ToolsStoreCatalog.ListAll()
                .Select(ToWorkshopPart)
                .Select(p =>
                {
                    p.QuantityOnHand = 99m;
                    return p;
                })
                .OrderBy(p => p.Name)
                .ToList();
            return Task.FromResult(items);
        }

        /// <summary>Магазин — категория как у пользователя DriveCare.</summary>
        public static Task<List<WorkshopPartItem>> ListShopForWorkshopAsync(Guid workshopId, string category)
        {
            _ = workshopId;
            var items = ToolsStoreCatalog.ListByCategory(category)
                .Select(ToWorkshopPart)
                .Select(p =>
                {
                    p.QuantityOnHand = 0m;
                    return p;
                })
                .ToList();
            return Task.FromResult(items);
        }

        public static string CategoryRu(string category) => ToolsStoreCatalog.CategoryRu(category);

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
