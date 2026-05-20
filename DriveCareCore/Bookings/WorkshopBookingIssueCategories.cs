using System.Collections.Generic;

namespace DriveCareCore.Bookings
{
    public sealed class WorkshopBookingIssueCategoryItem
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public bool RequiresDetails { get; set; }
    }

    public static class WorkshopBookingIssueCategories
    {
        public const string OtherCode = "other";

        public static IReadOnlyList<WorkshopBookingIssueCategoryItem> All { get; } =
            new List<WorkshopBookingIssueCategoryItem>
            {
                new WorkshopBookingIssueCategoryItem { Code = "engine", Name = "Двигатель" },
                new WorkshopBookingIssueCategoryItem { Code = "brakes", Name = "Тормоза" },
                new WorkshopBookingIssueCategoryItem { Code = "suspension", Name = "Подвеска / ходовая" },
                new WorkshopBookingIssueCategoryItem { Code = "electrical", Name = "Электрика" },
                new WorkshopBookingIssueCategoryItem { Code = "body", Name = "Кузов / ЛКП" },
                new WorkshopBookingIssueCategoryItem { Code = "glass", Name = "Стёкла / фары" },
                new WorkshopBookingIssueCategoryItem { Code = "climate", Name = "Кондиционер / отопление" },
                new WorkshopBookingIssueCategoryItem { Code = "tires", Name = "Шины / диски" },
                new WorkshopBookingIssueCategoryItem { Code = "maintenance", Name = "ТО / регламент" },
                new WorkshopBookingIssueCategoryItem { Code = "diagnostics", Name = "Диагностика (неясная поломка)" },
                new WorkshopBookingIssueCategoryItem { Code = OtherCode, Name = "Другое", RequiresDetails = true }
            };

        public static string GetDisplayName(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return string.Empty;
            foreach (var item in All)
            {
                if (item.Code == code)
                    return item.Name;
            }
            return code;
        }
    }
}
