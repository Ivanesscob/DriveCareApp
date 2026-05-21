using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.Bookings
{
    public sealed class WorkshopOnlineBookingSettings
    {
        public Guid WorkshopId { get; set; }
        public int MaxBookingsPerDay { get; set; } = 5;
    }

    public sealed class WorkshopBookingDateOption
    {
        public DateTime Date { get; set; }
        public string DisplayLabel { get; set; }
        public int BookedCount { get; set; }
        public int MaxPerDay { get; set; }
        public int RemainingSlots => Math.Max(0, MaxPerDay - BookedCount);
    }

    public static class WorkshopOnlineBookingCapacity
    {
        public const int DefaultMaxPerDay = 5;
        public const int BookingHorizonDays = 60;

        static readonly string[] ShortDayNames =
        {
            null, "пн", "вт", "ср", "чт", "пт", "сб", "вс"
        };

        public static bool SettingsTableExists()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.WorkshopOnlineBookingSettings', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static Task<WorkshopOnlineBookingSettings> GetSettingsAsync(Guid workshopId) =>
            WithDb(db => GetSettingsAsync(db, workshopId));

        public static async Task<WorkshopOnlineBookingSettings> GetSettingsAsync(DriveCareDBEntities db, Guid workshopId)
        {
            if (workshopId == Guid.Empty)
                return new WorkshopOnlineBookingSettings { WorkshopId = workshopId, MaxBookingsPerDay = DefaultMaxPerDay };

            if (!SettingsTableExists())
                return new WorkshopOnlineBookingSettings { WorkshopId = workshopId, MaxBookingsPerDay = DefaultMaxPerDay };

            var max = await db.Database.SqlQuery<int?>(
                    "SELECT MaxBookingsPerDay FROM dbo.WorkshopOnlineBookingSettings WHERE WorkshopId = @w",
                    new SqlParameter("@w", workshopId))
                .FirstOrDefaultAsync().ConfigureAwait(false);

            return new WorkshopOnlineBookingSettings
            {
                WorkshopId = workshopId,
                MaxBookingsPerDay = max ?? DefaultMaxPerDay
            };
        }

        public static Task<(bool ok, string error)> SaveSettingsAsync(Guid workshopId, int maxBookingsPerDay) =>
            WithDb(db => SaveSettingsAsync(db, workshopId, maxBookingsPerDay));

        public static async Task<(bool ok, string error)> SaveSettingsAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            int maxBookingsPerDay)
        {
            if (!SettingsTableExists())
                return (false, "Таблица настроек не найдена. Выполните SQL WorkshopOnlineBooking_Capacity.sql.");
            if (workshopId == Guid.Empty)
                return (false, "Мастерская не выбрана.");
            if (maxBookingsPerDay < 1 || maxBookingsPerDay > 999)
                return (false, "Укажите лимит от 1 до 999 записей в день.");

            var exists = await db.Workshops.AnyAsync(w => w.RowId == workshopId).ConfigureAwait(false);
            if (!exists)
                return (false, "Мастерская не найдена.");

            await db.Database.ExecuteSqlCommandAsync(@"
IF EXISTS (SELECT 1 FROM dbo.WorkshopOnlineBookingSettings WHERE WorkshopId = @w)
    UPDATE dbo.WorkshopOnlineBookingSettings SET MaxBookingsPerDay = @m WHERE WorkshopId = @w;
ELSE
    INSERT INTO dbo.WorkshopOnlineBookingSettings (WorkshopId, MaxBookingsPerDay) VALUES (@w, @m);",
                new SqlParameter("@w", workshopId),
                new SqlParameter("@m", maxBookingsPerDay)).ConfigureAwait(false);

            return (true, null);
        }

        public static Task<List<WorkshopBookingDateOption>> GetAvailableDatesAsync(Guid workshopId) =>
            WithDb(db => GetAvailableDatesAsync(db, workshopId));

        public static async Task<List<WorkshopBookingDateOption>> GetAvailableDatesAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            DateTime? fromDate = null,
            int? horizonDays = null)
        {
            var result = new List<WorkshopBookingDateOption>();
            if (workshopId == Guid.Empty || !WorkshopOnlineBookingService.TablesExist())
                return result;

            var start = (fromDate ?? DateTime.Today).Date;
            var days = horizonDays ?? BookingHorizonDays;
            var end = start.AddDays(days - 1);

            var settings = await GetSettingsAsync(db, workshopId).ConfigureAwait(false);
            var maxPerDay = settings.MaxBookingsPerDay;

            var schedule = WorkshopWorkScheduleService.TablesExist()
                ? await WorkshopWorkScheduleService.GetScheduleAsync(db, workshopId).ConfigureAwait(false)
                : new List<WorkshopWorkScheduleDay>();
            var scheduleByDay = schedule.ToDictionary(d => d.DayOfWeek);

            var counts = await db.Database.SqlQuery<DayCountRow>(@"
SELECT CAST(b.PreferredDate AS DATE) AS BookingDate, COUNT(*) AS Cnt
FROM dbo.WorkshopOnlineBookings b
WHERE b.WorkshopId = @w
  AND b.Status IN (0, 1)
  AND b.PreferredDate IS NOT NULL
  AND CAST(b.PreferredDate AS DATE) >= @from
  AND CAST(b.PreferredDate AS DATE) <= @to
GROUP BY CAST(b.PreferredDate AS DATE)",
                    new SqlParameter("@w", workshopId),
                    new SqlParameter("@from", start),
                    new SqlParameter("@to", end))
                .ToListAsync().ConfigureAwait(false);

            var countByDate = counts.ToDictionary(
                c => c.BookingDate.Date,
                c => c.Cnt);

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                if (!IsWorkDayOpen(scheduleByDay, ToScheduleDayOfWeek(d)))
                    continue;

                var booked = countByDate.TryGetValue(d, out var c) ? c : 0;
                if (booked >= maxPerDay)
                    continue;

                var dow = ToScheduleDayOfWeek(d);
                var shortName = ShortDayNames[dow] ?? string.Empty;
                result.Add(new WorkshopBookingDateOption
                {
                    Date = d,
                    BookedCount = booked,
                    MaxPerDay = maxPerDay,
                    DisplayLabel = $"{shortName} {d:dd.MM.yyyy} (мест: {maxPerDay - booked})"
                });
            }

            return result;
        }

        public static Task<(bool ok, string error)> ValidateBookingDateAsync(Guid workshopId, DateTime preferredDate) =>
            WithDb(db => ValidateBookingDateAsync(db, workshopId, preferredDate));

        public static async Task<(bool ok, string error)> ValidateBookingDateAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            DateTime preferredDate)
        {
            if (workshopId == Guid.Empty)
                return (false, "Автосервис не указан.");

            var date = preferredDate.Date;
            if (date < DateTime.Today)
                return (false, "Нельзя записаться на прошедшую дату.");

            var available = await GetAvailableDatesAsync(db, workshopId, date, 1).ConfigureAwait(false);
            if (available.Any(a => a.Date == date))
                return (true, null);

            var settings = await GetSettingsAsync(db, workshopId).ConfigureAwait(false);
            if (!await IsWorkDayOpenForDate(db, workshopId, date).ConfigureAwait(false))
                return (false, "В этот день сервис не работает. Выберите другую дату.");

            var booked = await CountBookingsOnDateAsync(db, workshopId, date).ConfigureAwait(false);
            if (booked >= settings.MaxBookingsPerDay)
                return (false, "На выбранный день достигнут лимит записей. Выберите другую дату.");

            return (false, "Выбранная дата недоступна для записи.");
        }

        static async Task<bool> IsWorkDayOpenForDate(DriveCareDBEntities db, Guid workshopId, DateTime date)
        {
            if (!WorkshopWorkScheduleService.TablesExist())
                return ToScheduleDayOfWeek(date) < 6;

            var schedule = await WorkshopWorkScheduleService.GetScheduleAsync(db, workshopId).ConfigureAwait(false);
            return IsWorkDayOpen(schedule.ToDictionary(d => d.DayOfWeek), ToScheduleDayOfWeek(date));
        }

        static async Task<int> CountBookingsOnDateAsync(DriveCareDBEntities db, Guid workshopId, DateTime date)
        {
            return await db.Database.SqlQuery<int>(@"
SELECT COUNT(*) FROM dbo.WorkshopOnlineBookings
WHERE WorkshopId = @w AND Status IN (0, 1) AND CAST(PreferredDate AS DATE) = @d",
                    new SqlParameter("@w", workshopId),
                    new SqlParameter("@d", date.Date))
                .FirstOrDefaultAsync().ConfigureAwait(false);
        }

        public static int ToScheduleDayOfWeek(DateTime date) =>
            date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        static bool IsWorkDayOpen(IReadOnlyDictionary<int, WorkshopWorkScheduleDay> scheduleByDay, int dayOfWeek)
        {
            if (scheduleByDay.TryGetValue(dayOfWeek, out var day))
                return !day.IsClosed;
            return dayOfWeek < 6;
        }

        static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> work)
        {
            using (var db = new DriveCareDBEntities())
                return await work(db).ConfigureAwait(false);
        }

        sealed class DayCountRow
        {
            public DateTime BookingDate { get; set; }
            public int Cnt { get; set; }
        }
    }
}
