using System;

namespace DriveCare.Data
{
    /// <summary>Строка из dbo.UserCarMaintenanceHistory (SqlQuery).</summary>
    internal sealed class MaintenanceSqlRow
    {
        public Guid RowId { get; set; }
        public Guid UserCarRowId { get; set; }
        public DateTime ServiceDate { get; set; }
        public int? MileageKm { get; set; }
        public string Title { get; set; }
        public string Notes { get; set; }
        public string ComponentCode { get; set; }
        public string WorkshopName { get; set; }
        public byte? SeverityAfter { get; set; }
    }

    /// <summary>Строка из RepairHistory при подгрузке в экран обслуживания (SqlQuery).</summary>
    internal sealed class RepairHistoryMaintenanceSqlRow
    {
        public DateTime ServiceDate { get; set; }
        public int? MileageKm { get; set; }
        public string Title { get; set; }
        public string Notes { get; set; }
    }

    public sealed class MaintenanceHistoryItemVm
    {
        public DateTime ServiceDate { get; set; }
        public int? MileageKm { get; set; }
        public string Title { get; set; }
        public string Notes { get; set; }
        public string ComponentCode { get; set; }
        public string WorkshopName { get; set; }
        public byte? SeverityAfter { get; set; }
        public string DateLabel => ServiceDate.ToString("dd.MM.yyyy");
        public string MileageLabel => MileageKm.HasValue ? $"{MileageKm:N0} км" : "пробег не указан";
        public string WhenWhereLine => $"{DateLabel} · {MileageLabel}";
        public bool HasNotes => !string.IsNullOrWhiteSpace(Notes);
        public string ComponentLabel => string.IsNullOrWhiteSpace(ComponentCode) ? null : ComponentCode.Trim();
        public string WorkshopLine => string.IsNullOrWhiteSpace(WorkshopName) ? null : WorkshopName.Trim();
    }

    public sealed class MaintenanceRecommendationVm
    {
        public string PartName { get; set; }
        public string IntervalHint { get; set; }
        public string Summary { get; set; }
        /// <summary>Good, Watch, Due, Info.</summary>
        public string Urgency { get; set; }

        public string PartHeading =>
            string.IsNullOrWhiteSpace(IntervalHint) ? PartName : $"{PartName}  {IntervalHint}";
    }

    /// <summary>Сводка по категории работ из текста записей истории.</summary>
    public sealed class MaintenanceCategoryInsightVm
    {
        public string CategoryName { get; set; }
        public int OccurrenceCount { get; set; }
        public string LastDoneLabel { get; set; }
        public string NextApproxLabel { get; set; }
        public string DetailHint { get; set; }

        public string HeaderLine => $"{CategoryName}  ({OccurrenceCount} запис.)";
        public string LastCaption => $"Последний раз: {LastDoneLabel}";
        public string NextCaption => $"Ориентир следующего визита: {NextApproxLabel}";
    }

    /// <summary>Одна карточка в боковой колонке (ТО, тормоза, ГРМ, масло).</summary>
    public sealed class MaintenanceInfoSlotVm
    {
        public string DisplayTitle { get; set; }
        public string Line1 { get; set; }
        public string Line2 { get; set; }
        public string Detail { get; set; }
        public bool HasData { get; set; }
    }
}
