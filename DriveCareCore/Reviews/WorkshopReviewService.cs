using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.Reviews
{
    public static class WorkshopReviewService
    {
        public const string NotificationDescriptionPrefix = "WorkshopReview";

        public static Task<bool> TableExistsAsync() =>
            WithDb(async db =>
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.WorkshopReviews', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return await db.Database.SqlQuery<int>(sql).FirstOrDefaultAsync().ConfigureAwait(false) == 1;
            });

        public static Task<bool> HasReviewForDocumentAsync(Guid userId, Guid documentId) =>
            WithDb(db => HasReviewForDocumentAsync(db, userId, documentId));

        public static async Task<bool> HasReviewForDocumentAsync(DriveCareDBEntities db, Guid userId, Guid documentId)
        {
            if (!await TableExistsAsync(db).ConfigureAwait(false))
                return false;
            if (userId == Guid.Empty || documentId == Guid.Empty)
                return false;

            try
            {
                var count = await db.Database.SqlQuery<int>(
                    @"SELECT COUNT(1) FROM dbo.WorkshopReviews WHERE UserId = @u AND DocumentId = @d",
                    new SqlParameter("@u", userId),
                    new SqlParameter("@d", documentId)).FirstOrDefaultAsync().ConfigureAwait(false);
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        public static Task<(bool ok, string error)> TrySubmitAsync(WorkshopReviewSubmit submit) =>
            WithDb(async db =>
            {
                if (submit == null)
                    return (false, "Нет данных.");
                if (submit.UserId == Guid.Empty || submit.WorkshopId == Guid.Empty)
                    return (false, "Некорректные данные.");
                if (submit.Rating < 1 || submit.Rating > 5)
                    return (false, "Выберите оценку от 1 до 5 звёзд.");
                if (!await TableExistsAsync(db).ConfigureAwait(false))
                    return (false, "Таблица отзывов не найдена. Выполните SQL WorkshopReviews_Tables.sql.");

                if (submit.DocumentId.HasValue && submit.DocumentId.Value != Guid.Empty)
                {
                    if (await HasReviewForDocumentAsync(db, submit.UserId, submit.DocumentId.Value).ConfigureAwait(false))
                        return (false, "Вы уже оставили отзыв по этому визиту.");
                }

                try
                {
                    await db.Database.ExecuteSqlCommandAsync(@"
INSERT INTO dbo.WorkshopReviews
    (RowId, WorkshopId, UserId, DocumentId, RepairHistoryId, Rating, Comment, Pros, Cons, Status, CreatedAt)
VALUES (@id, @ws, @u, @doc, @rh, @rating, @comment, @pros, @cons, 1, @dt)",
                        new SqlParameter("@id", Guid.NewGuid()),
                        new SqlParameter("@ws", submit.WorkshopId),
                        new SqlParameter("@u", submit.UserId),
                        new SqlParameter("@doc", (object)submit.DocumentId ?? DBNull.Value),
                        new SqlParameter("@rh", (object)submit.RepairHistoryId ?? DBNull.Value),
                        new SqlParameter("@rating", submit.Rating),
                        new SqlParameter("@comment", (object)(submit.Comment ?? string.Empty) ?? DBNull.Value),
                        new SqlParameter("@pros", (object)(submit.Pros ?? string.Empty) ?? DBNull.Value),
                        new SqlParameter("@cons", (object)(submit.Cons ?? string.Empty) ?? DBNull.Value),
                        new SqlParameter("@dt", DateTime.Now)).ConfigureAwait(false);

                    await TryUpdateWorkshopAggregateAsync(db, submit.WorkshopId).ConfigureAwait(false);
                    return (true, null);
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            });

        public static Task<WorkshopReviewRequest> TryParseNotificationDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description) || !description.StartsWith(NotificationDescriptionPrefix + "|"))
                return Task.FromResult<WorkshopReviewRequest>(null);

            var parts = description.Split('|');
            if (parts.Length < 3)
                return Task.FromResult<WorkshopReviewRequest>(null);

            if (!Guid.TryParse(parts[1], out var documentId) || !Guid.TryParse(parts[2], out var workshopId))
                return Task.FromResult<WorkshopReviewRequest>(null);

            Guid? repairId = null;
            if (parts.Length > 3 && Guid.TryParse(parts[3], out var rh) && rh != Guid.Empty)
                repairId = rh;

            return WithDb(async db =>
            {
                var name = await db.Workshops.AsNoTracking()
                    .Where(w => w.RowId == workshopId)
                    .Select(w => w.Name)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                return new WorkshopReviewRequest
                {
                    DocumentId = documentId,
                    WorkshopId = workshopId,
                    RepairHistoryId = repairId,
                    WorkshopName = name ?? "Автосервис"
                };
            });
        }

        public static Task<WorkshopRatingSummary> GetRatingSummaryAsync(Guid workshopId) =>
            WithDb(db => GetRatingSummaryAsync(db, workshopId));

        public static async Task<WorkshopRatingSummary> GetRatingSummaryAsync(DriveCareDBEntities db, Guid workshopId)
        {
            if (workshopId == Guid.Empty || !await TableExistsAsync(db).ConfigureAwait(false))
                return new WorkshopRatingSummary();

            try
            {
                var row = await db.Database.SqlQuery<RatingAggRow>(@"
SELECT AVG(CAST(Rating AS DECIMAL(4,2))) AS AvgRating, COUNT(1) AS ReviewCount
FROM dbo.WorkshopReviews WHERE WorkshopId = @ws AND Status = 1",
                    new SqlParameter("@ws", workshopId)).FirstOrDefaultAsync().ConfigureAwait(false);

                if (row == null || row.ReviewCount <= 0)
                    return new WorkshopRatingSummary();

                return new WorkshopRatingSummary
                {
                    AvgRating = row.AvgRating,
                    ReviewCount = row.ReviewCount
                };
            }
            catch
            {
                return new WorkshopRatingSummary();
            }
        }

        public static Task<List<WorkshopReviewDisplay>> ListForWorkshopAsync(Guid workshopId, int take = 50) =>
            WithDb(db => ListForWorkshopAsync(db, workshopId, take));

        public static async Task<List<WorkshopReviewDisplay>> ListForWorkshopAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            int take = 50)
        {
            if (workshopId == Guid.Empty || !await TableExistsAsync(db).ConfigureAwait(false))
                return new List<WorkshopReviewDisplay>();

            take = Math.Max(1, Math.Min(take, 200));
            try
            {
                var rows = await db.Database.SqlQuery<WorkshopReviewDisplayRow>(@"
SELECT r.Rating, r.Comment, r.Pros, r.Cons, r.CreatedAt,
       COALESCE(NULLIF(LTRIM(RTRIM(u.Description)), N''), NULLIF(LTRIM(RTRIM(u.Login)), N''), u.Email, N'Клиент') AS AuthorName
FROM dbo.WorkshopReviews r
LEFT JOIN dbo.Users u ON u.RowId = r.UserId
WHERE r.WorkshopId = @ws AND r.Status = 1
ORDER BY r.CreatedAt DESC",
                    new SqlParameter("@ws", workshopId)).ToListAsync().ConfigureAwait(false);

                return rows.Select(r => new WorkshopReviewDisplay
                {
                    Rating = r.Rating,
                    Comment = r.Comment ?? string.Empty,
                    Pros = r.Pros ?? string.Empty,
                    Cons = r.Cons ?? string.Empty,
                    AuthorName = r.AuthorName ?? "Клиент",
                    CreatedAt = r.CreatedAt
                }).ToList();
            }
            catch
            {
                return new List<WorkshopReviewDisplay>();
            }
        }

        public static string BuildNotificationDescription(Guid documentId, Guid workshopId, Guid? repairHistoryId)
        {
            var rh = repairHistoryId.HasValue && repairHistoryId.Value != Guid.Empty
                ? repairHistoryId.Value.ToString("D")
                : string.Empty;
            return $"{NotificationDescriptionPrefix}|{documentId:D}|{workshopId:D}|{rh}";
        }

        static async Task TryUpdateWorkshopAggregateAsync(DriveCareDBEntities db, Guid workshopId)
        {
            try
            {
                if (await db.Database.SqlQuery<int>(
                    @"SELECT CASE WHEN COL_LENGTH(N'dbo.Workshops', N'AvgRating') IS NOT NULL THEN 1 ELSE 0 END")
                    .FirstOrDefaultAsync().ConfigureAwait(false) != 1)
                    return;

                await db.Database.ExecuteSqlCommandAsync(@"
UPDATE w SET
    AvgRating = agg.AvgRating,
    ReviewCount = agg.Cnt,
    RatingUpdatedAt = GETDATE()
FROM dbo.Workshops w
INNER JOIN (
    SELECT WorkshopId, AVG(CAST(Rating AS DECIMAL(3,2))) AS AvgRating, COUNT(1) AS Cnt
    FROM dbo.WorkshopReviews WHERE Status = 1 AND WorkshopId = @ws
    GROUP BY WorkshopId
) agg ON agg.WorkshopId = w.RowId
WHERE w.RowId = @ws", new SqlParameter("@ws", workshopId)).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        static async Task<bool> TableExistsAsync(DriveCareDBEntities db)
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.WorkshopReviews', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return await db.Database.SqlQuery<int>(sql).FirstOrDefaultAsync().ConfigureAwait(false) == 1;
            }
            catch
            {
                return false;
            }
        }

        sealed class RatingAggRow
        {
            public decimal? AvgRating { get; set; }
            public int ReviewCount { get; set; }
        }

        sealed class WorkshopReviewDisplayRow
        {
            public byte Rating { get; set; }
            public string Comment { get; set; }
            public string Pros { get; set; }
            public string Cons { get; set; }
            public string AuthorName { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> action)
        {
            using (var db = new DriveCareDBEntities())
                return await action(db).ConfigureAwait(false);
        }
    }
}
