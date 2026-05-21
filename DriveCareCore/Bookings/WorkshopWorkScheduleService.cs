using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.Bookings
{
    public static class WorkshopWorkScheduleService
    {
        static readonly string[] DayNames =
        {
            null,
            "Понедельник",
            "Вторник",
            "Среда",
            "Четверг",
            "Пятница",
            "Суббота",
            "Воскресенье"
        };

        public static bool TablesExist()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.WorkshopWorkSchedules', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static Task<List<WorkshopWorkScheduleDay>> GetScheduleAsync(Guid workshopId) =>
            WithDb(db => GetScheduleAsync(db, workshopId));

        public static async Task<List<WorkshopWorkScheduleDay>> GetScheduleAsync(DriveCareDBEntities db, Guid workshopId)
        {
            var result = new List<WorkshopWorkScheduleDay>();
            if (workshopId == Guid.Empty)
                return result;

            var rows = TablesExist()
                ? await db.Database.SqlQuery<ScheduleRow>(@"
SELECT RowId, WorkshopId, DayOfWeek, IsClosed, OpenTime, CloseTime
FROM dbo.WorkshopWorkSchedules
WHERE WorkshopId = @w", new SqlParameter("@w", workshopId)).ToListAsync().ConfigureAwait(false)
                : new List<ScheduleRow>();

            var byDay = rows.ToDictionary(r => r.DayOfWeek);

            for (var day = 1; day <= 7; day++)
            {
                if (byDay.TryGetValue(day, out var row))
                {
                    result.Add(new WorkshopWorkScheduleDay
                    {
                        RowId = row.RowId,
                        WorkshopId = row.WorkshopId,
                        DayOfWeek = day,
                        DayName = DayNames[day],
                        IsClosed = row.IsClosed,
                        OpenTime = ParseSqlTime(row.OpenTime),
                        CloseTime = ParseSqlTime(row.CloseTime)
                    });
                }
                else
                {
                    result.Add(CreateDefaultDay(workshopId, day));
                }
            }

            return result;
        }

        static WorkshopWorkScheduleDay CreateDefaultDay(Guid workshopId, int day)
        {
            var closed = day >= 6;
            return new WorkshopWorkScheduleDay
            {
                RowId = Guid.Empty,
                WorkshopId = workshopId,
                DayOfWeek = day,
                DayName = DayNames[day],
                IsClosed = closed,
                OpenTime = closed ? (TimeSpan?)null : new TimeSpan(9, 0, 0),
                CloseTime = closed ? (TimeSpan?)null : new TimeSpan(18, 0, 0)
            };
        }

        public static Task<(bool ok, string error)> SaveScheduleAsync(Guid workshopId, IList<WorkshopWorkScheduleDayInput> days) =>
            WithDb(db => SaveScheduleAsync(db, workshopId, days));

        public static async Task<(bool ok, string error)> SaveScheduleAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            IList<WorkshopWorkScheduleDayInput> days)
        {
            if (!TablesExist())
                return (false, "Таблица расписания не найдена. Выполните SQL WorkshopWorkSchedule_Tables.sql.");
            if (workshopId == Guid.Empty)
                return (false, "Мастерская не выбрана.");
            if (days == null || days.Count == 0)
                return (false, "Нет данных расписания.");

            var exists = await db.Workshops.AnyAsync(w => w.RowId == workshopId).ConfigureAwait(false);
            if (!exists)
                return (false, "Мастерская не найдена.");

            foreach (var day in days)
            {
                if (day.DayOfWeek < 1 || day.DayOfWeek > 7)
                    return (false, "Некорректный день недели.");

                if (!day.IsClosed)
                {
                    if (!TryParseTime(day.OpenTimeText, out var open))
                        return (false, DayNames[day.DayOfWeek] + ": укажите время открытия (например 09:00).");
                    if (!TryParseTime(day.CloseTimeText, out var close))
                        return (false, DayNames[day.DayOfWeek] + ": укажите время закрытия (например 18:00).");
                    if (close <= open)
                        return (false, DayNames[day.DayOfWeek] + ": время закрытия должно быть позже открытия.");
                }
            }

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    await db.Database.ExecuteSqlCommandAsync(
                        "DELETE FROM dbo.WorkshopWorkSchedules WHERE WorkshopId = @w",
                        new SqlParameter("@w", workshopId)).ConfigureAwait(false);

                    foreach (var day in days.OrderBy(d => d.DayOfWeek))
                    {
                        TimeSpan? open = null;
                        TimeSpan? close = null;
                        if (!day.IsClosed)
                        {
                            TryParseTime(day.OpenTimeText, out var o);
                            TryParseTime(day.CloseTimeText, out var c);
                            open = o;
                            close = c;
                        }

                        await db.Database.ExecuteSqlCommandAsync(@"
INSERT INTO dbo.WorkshopWorkSchedules (RowId, WorkshopId, DayOfWeek, IsClosed, OpenTime, CloseTime)
VALUES (@id, @w, @d, @closed, @open, @close)",
                            new SqlParameter("@id", Guid.NewGuid()),
                            new SqlParameter("@w", workshopId),
                            new SqlParameter("@d", day.DayOfWeek),
                            new SqlParameter("@closed", day.IsClosed),
                            new SqlParameter("@open", (object)open ?? DBNull.Value),
                            new SqlParameter("@close", (object)close ?? DBNull.Value)).ConfigureAwait(false);
                    }

                    tx.Commit();
                    return (true, null);
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    return (false, ex.Message);
                }
            }
        }

        public static bool TryParseTime(string text, out TimeSpan value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var s = text.Trim().Replace('.', ':');
            if (TimeSpan.TryParseExact(s, @"h\:mm", CultureInfo.InvariantCulture, out value))
                return true;
            if (TimeSpan.TryParseExact(s, @"hh\:mm", CultureInfo.InvariantCulture, out value))
                return true;
            return TimeSpan.TryParse(s, out value);
        }

        static TimeSpan? ParseSqlTime(object sqlValue)
        {
            if (sqlValue == null || sqlValue == DBNull.Value)
                return null;
            if (sqlValue is TimeSpan ts)
                return ts;
            if (sqlValue is DateTime dt)
                return dt.TimeOfDay;
            return TimeSpan.TryParse(sqlValue.ToString(), out var parsed) ? parsed : (TimeSpan?)null;
        }

        static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> work)
        {
            using (var db = new DriveCareDBEntities())
                return await work(db).ConfigureAwait(false);
        }

        sealed class ScheduleRow
        {
            public Guid RowId { get; set; }
            public Guid WorkshopId { get; set; }
            public int DayOfWeek { get; set; }
            public bool IsClosed { get; set; }
            public object OpenTime { get; set; }
            public object CloseTime { get; set; }
        }
    }
}
