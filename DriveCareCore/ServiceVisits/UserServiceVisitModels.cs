using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DriveCareCore.ServiceVisits
{
    public sealed class UserServiceVisitItem
    {
        public Guid DocumentId { get; set; }
        public Guid RootTaskId { get; set; }
        public Guid? RepairHistoryId { get; set; }
        public Guid WorkshopId { get; set; }
        public Guid? CarId { get; set; }
        public string WorkshopName { get; set; }
        public string VisitReason { get; set; }
        public string ServiceKind { get; set; }
        public byte Status { get; set; }
        public byte ClientStage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? MileageKm { get; set; }
        public DateTime? RepairDate { get; set; }
        public string ServicesSummary { get; set; }

        public bool IsCompleted => Status == 1;
        public DateTime DisplayDate => CompletedAt ?? RepairDate ?? CreatedAt;
        public string DateLabel => DisplayDate.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
        public string TimeLabel => DisplayDate.ToString("HH:mm", CultureInfo.GetCultureInfo("ru-RU"));
        public string StatusLabel
        {
            get
            {
                if (IsCompleted)
                    return ServiceDocumentClientStageLabels.Completed;

                var stage = ServiceDocumentClientStageLabels.Normalize(ClientStage, Status);
                return ServiceDocumentClientStageLabels.ForUser(stage);
            }
        }
        public string ReasonLine => string.IsNullOrWhiteSpace(VisitReason) ? "Причина не указана" : VisitReason.Trim();
        public string MileageLine => MileageKm.HasValue
            ? $"{MileageKm.Value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"))} км"
            : "пробег не указан";
        public string KindLine => string.IsNullOrWhiteSpace(ServiceKind) ? "Ремонт" : ServiceKind.Trim();
        public string ServicesLine => string.IsNullOrWhiteSpace(ServicesSummary)
            ? "Услуги будут указаны после завершения ремонта"
            : ServicesSummary.Trim();
        public bool CanOpenWorkOrder => IsCompleted;
    }

    public sealed class UserWorkOrderLineVm
    {
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string UnitName { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineAmount { get; set; }
        public decimal DiscountPercent { get; set; }
    }

    public sealed class UserWorkOrderPreview
    {
        public Guid DocumentId { get; set; }
        public string Title { get; set; }
        public string WorkshopName { get; set; }
        public string VisitReason { get; set; }
        public string ServiceKind { get; set; }
        public string StatusLabel { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int? MileageKm { get; set; }
        public string ReportText { get; set; } = string.Empty;
        public List<UserWorkOrderLineVm> Services { get; set; } = new List<UserWorkOrderLineVm>();
        public List<UserWorkOrderLineVm> Parts { get; set; } = new List<UserWorkOrderLineVm>();
    }
}
