using System;

namespace DriveCareCore.Bookings
{
    public sealed class WorkshopWorkScheduleDay
    {
        public Guid RowId { get; set; }
        public Guid WorkshopId { get; set; }
        public int DayOfWeek { get; set; }
        public string DayName { get; set; }
        public bool IsClosed { get; set; }
        public TimeSpan? OpenTime { get; set; }
        public TimeSpan? CloseTime { get; set; }

        public string OpenTimeText => FormatTime(OpenTime) ?? "09:00";
        public string CloseTimeText => FormatTime(CloseTime) ?? "18:00";

        static string FormatTime(TimeSpan? t) =>
            t.HasValue ? $"{t.Value.Hours:D2}:{t.Value.Minutes:D2}" : null;
    }

    public sealed class WorkshopWorkScheduleDayInput
    {
        public int DayOfWeek { get; set; }
        public bool IsClosed { get; set; }
        public string OpenTimeText { get; set; }
        public string CloseTimeText { get; set; }
    }
}
