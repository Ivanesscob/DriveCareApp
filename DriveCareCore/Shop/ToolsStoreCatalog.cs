using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DriveCareCore.Shop
{
    /// <summary>Общий каталог магазина (DriveCare и DriveCarePro).</summary>
    public static class ToolsStoreCatalog
    {
        public static readonly string[] Categories =
        {
            "Engine", "Transmission", "Body", "Tires", "Accessories"
        };

        private static readonly string[] TierNames =
        {
            "Стандарт", "Премиум", "Pro", "Sport", "Comfort", "Eco"
        };

        public static IReadOnlyList<ShopCatalogProduct> ListByCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return Array.Empty<ShopCatalogProduct>();

            var list = new List<ShopCatalogProduct>(TierNames.Length);
            for (var i = 0; i < TierNames.Length; i++)
            {
                var price = 3500m + i * 1200m + (category.Length * 100);
                list.Add(new ShopCatalogProduct
                {
                    ProductId = StableProductId(category, i),
                    Category = category,
                    Name = CategoryRu(category) + " " + TierNames[i],
                    Description = "Категория: " + CategoryRu(category),
                    Price = price,
                    UnitName = "шт."
                });
            }

            return list;
        }

        public static IReadOnlyList<ShopCatalogProduct> ListAll()
        {
            return Categories.SelectMany(ListByCategory).ToList();
        }

        public static ShopCatalogProduct FindById(Guid productId)
        {
            foreach (var cat in Categories)
            {
                var hit = ListByCategory(cat).FirstOrDefault(p => p.ProductId == productId);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        public static string CategoryRu(string category)
        {
            switch (category)
            {
                case "Engine": return "Деталь двигателя";
                case "Transmission": return "Деталь трансмиссии";
                case "Body": return "Кузовной элемент";
                case "Tires": return "Шина";
                case "Accessories": return "Аксессуар";
                default: return "Товар";
            }
        }

        public static Guid StableProductId(string category, int index)
        {
            var key = "DriveCare.ToolsStore." + (category ?? string.Empty) + "." + index;
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
                return new Guid(hash);
            }
        }
    }

    public sealed class ShopCatalogProduct
    {
        public Guid ProductId { get; set; }
        public string Category { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string UnitName { get; set; } = "шт.";

        public string PriceLabel => $"{Price:0} ₽";
    }
}
