using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.Shop
{
    /// <summary>Каталог магазина из dbo.Parts (статус «Активна»).</summary>
    public static class UserPartsCatalogService
    {
        public static Task<IReadOnlyList<ShopCatalogProduct>> ListByCategoryAsync(string category) =>
            WithDb(db => ListByCategoryAsync(db, category));

        public static async Task<IReadOnlyList<ShopCatalogProduct>> ListByCategoryAsync(
            DriveCareDBEntities db,
            string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return Array.Empty<ShopCatalogProduct>();

            try
            {
                var storeCat = MapUiCategoryToStore(category);
                var hasPrice = await ColumnExistsAsync(db, "Parts", "Price").ConfigureAwait(false);
                var hasStoreCat = await ColumnExistsAsync(db, "Parts", "StoreCategory").ConfigureAwait(false);

                var priceSql = hasPrice ? "ISNULL(p.Price, 0)" : "(1200 + (ABS(CHECKSUM(p.RowId)) % 18000))";
                var catSql = hasStoreCat
                    ? "ISNULL(NULLIF(LTRIM(RTRIM(p.StoreCategory)), N''), N'Accessories')"
                    : @"CASE
                        WHEN p.Description LIKE N'%Двигатель%' THEN N'Engine'
                        WHEN p.Description LIKE N'%Кузов%' THEN N'Body'
                        WHEN p.Description LIKE N'%Трансмисс%' THEN N'Transmission'
                        WHEN p.Description LIKE N'%Шин%' THEN N'Tires'
                        ELSE N'Accessories' END";

                var sql = $@"
SELECT p.RowId AS ProductId, p.Name, p.Article, p.Description,
       CAST({priceSql} AS DECIMAL(18,2)) AS Price,
       {catSql} AS StoreCategory
FROM dbo.Parts p
LEFT JOIN dbo.Statuses s ON s.RowId = p.StatusId
WHERE (s.Name = N'Активна' OR p.StatusId IS NULL)
  AND ({catSql}) = @cat
ORDER BY p.Name";

                var rows = await db.Database.SqlQuery<PartStoreRow>(sql, new SqlParameter("@cat", storeCat))
                    .ToListAsync().ConfigureAwait(false);

                if (rows.Count == 0)
                    return ToolsStoreCatalog.ListByCategory(category);

                return rows.Select(r => new ShopCatalogProduct
                {
                    ProductId = r.ProductId,
                    Category = category,
                    Name = string.IsNullOrWhiteSpace(r.Name) ? "Запчасть" : r.Name.Trim(),
                    Description = BuildDescription(r),
                    Price = r.Price > 0 ? r.Price : 1200m,
                    UnitName = "шт."
                }).ToList();
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return ToolsStoreCatalog.ListByCategory(category);
            }
        }

        public static Task<ShopCatalogProduct> FindByIdAsync(Guid productId) =>
            WithDb(db => FindByIdAsync(db, productId));

        public static async Task<ShopCatalogProduct> FindByIdAsync(DriveCareDBEntities db, Guid productId)
        {
            if (productId == Guid.Empty)
                return null;

            foreach (var cat in ToolsStoreCatalog.Categories)
            {
                var list = await ListByCategoryAsync(db, cat).ConfigureAwait(false);
                var hit = list.FirstOrDefault(p => p.ProductId == productId);
                if (hit != null)
                    return hit;
            }

            return ToolsStoreCatalog.FindById(productId);
        }

        static string BuildDescription(PartStoreRow r)
        {
            var article = string.IsNullOrWhiteSpace(r.Article) ? null : "Арт. " + r.Article.Trim();
            var desc = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
            if (!string.IsNullOrEmpty(article) && !string.IsNullOrEmpty(desc))
                return article + " · " + desc;
            return article ?? desc ?? ToolsStoreCatalog.CategoryRu(MapStoreToUi(r.StoreCategory));
        }

        static string MapUiCategoryToStore(string uiCategory)
        {
            switch (uiCategory)
            {
                case "Engine": return "Engine";
                case "Transmission": return "Transmission";
                case "Body": return "Body";
                case "Tires": return "Tires";
                case "Accessories": return "Accessories";
                default: return uiCategory;
            }
        }

        static string MapStoreToUi(string storeCategory)
        {
            switch (storeCategory)
            {
                case "Engine": return "Engine";
                case "Transmission": return "Transmission";
                case "Body": return "Body";
                case "Tires": return "Tires";
                default: return "Accessories";
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

        sealed class PartStoreRow
        {
            public Guid ProductId { get; set; }
            public string Name { get; set; }
            public string Article { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public string StoreCategory { get; set; }
        }
    }
}
