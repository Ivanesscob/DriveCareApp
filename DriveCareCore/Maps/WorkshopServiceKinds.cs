using System;
using System.Collections.Generic;
using System.Linq;

namespace DriveCareCore.Maps
{
    public enum WorkshopServiceKindCode
    {
        AutoService = 0,
        Painting = 1,
        TireService = 2,
        Other = 99
    }

    public sealed class WorkshopServiceKindItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public WorkshopServiceKindCode Code { get; set; }
    }

    public static class WorkshopServiceKinds
    {
        public static readonly Guid AutoServiceId = new Guid("DC010001-0001-4001-8001-000000000001");
        public static readonly Guid PaintingId = new Guid("DC010002-0002-4002-8002-000000000002");
        public static readonly Guid TireServiceId = new Guid("DC010003-0003-4003-8003-000000000003");

        public static IReadOnlyList<WorkshopServiceKindItem> All { get; } = new List<WorkshopServiceKindItem>
        {
            new WorkshopServiceKindItem { Id = AutoServiceId, Name = "Автосервис", Code = WorkshopServiceKindCode.AutoService },
            new WorkshopServiceKindItem { Id = PaintingId, Name = "Покраска", Code = WorkshopServiceKindCode.Painting },
            new WorkshopServiceKindItem { Id = TireServiceId, Name = "Шиномонтаж", Code = WorkshopServiceKindCode.TireService }
        };

        public static WorkshopServiceKindCode ResolveCode(Guid? businessTypeId, string businessTypeName)
        {
            if (businessTypeId.HasValue)
            {
                if (businessTypeId.Value == PaintingId)
                    return WorkshopServiceKindCode.Painting;
                if (businessTypeId.Value == TireServiceId)
                    return WorkshopServiceKindCode.TireService;
                if (businessTypeId.Value == AutoServiceId)
                    return WorkshopServiceKindCode.AutoService;
            }

            var n = (businessTypeName ?? string.Empty).Trim().ToLowerInvariant();
            if (n.Contains("покрас") || n.Contains("кузов"))
                return WorkshopServiceKindCode.Painting;
            if (n.Contains("шин") || n.Contains("колес"))
                return WorkshopServiceKindCode.TireService;
            if (n.Contains("авто") || n.Contains("сервис") || n.Contains("сто"))
                return WorkshopServiceKindCode.AutoService;
            return WorkshopServiceKindCode.Other;
        }

        public static string GetDisplayName(Guid? businessTypeId, string businessTypeName)
        {
            var item = All.FirstOrDefault(x => businessTypeId.HasValue && x.Id == businessTypeId.Value);
            if (item != null)
                return item.Name;
            if (!string.IsNullOrWhiteSpace(businessTypeName))
                return businessTypeName.Trim();
            return "Автосервис";
        }

        public static string GetYandexIconPreset(WorkshopServiceKindCode code)
        {
            switch (code)
            {
                case WorkshopServiceKindCode.Painting:
                    return "islands#orangeAutoIcon";
                case WorkshopServiceKindCode.TireService:
                    return "islands#darkGreenAutoIcon";
                case WorkshopServiceKindCode.AutoService:
                    return "islands#blueAutoIcon";
                default:
                    return "islands#grayAutoIcon";
            }
        }
    }
}
