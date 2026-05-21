using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.Messaging
{
    public static class WorkshopMessagingService
    {
        public static bool TablesExist()
        {
            try
            {
                const string sql = @"
SELECT CASE WHEN OBJECT_ID(N'dbo.WorkshopConversations', N'U') IS NOT NULL
             AND OBJECT_ID(N'dbo.WorkshopMessages', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static Task<List<ConversationListItem>> ListForUserAsync(Guid userId) =>
            WithDb(db => ListForUserAsync(db, userId));

        public static async Task<List<ConversationListItem>> ListForUserAsync(DriveCareDBEntities db, Guid userId)
        {
            if (!TablesExist() || userId == Guid.Empty)
                return new List<ConversationListItem>();

            const string sql = @"
SELECT c.RowId AS ConversationId, c.WorkshopId, c.UserId,
       w.Name AS WorkshopName, co.Name AS CompanyName,
       c.LastMessagePreview, c.LastMessageAt, c.UnreadForUser AS UnreadCount,
       COALESCE(NULLIF(LTRIM(RTRIM(u.Login)), N''), u.Email, N'Клиент') AS VisitorDisplayName
FROM dbo.WorkshopConversations c
INNER JOIN dbo.Workshops w ON w.RowId = c.WorkshopId
INNER JOIN dbo.Companies co ON co.RowId = w.CompanyId
INNER JOIN dbo.Users u ON u.RowId = c.UserId
WHERE c.UserId = @uid
ORDER BY c.LastMessageAt DESC;";

            return await db.Database.SqlQuery<ConversationListItem>(sql, new SqlParameter("@uid", userId))
                .ToListAsync().ConfigureAwait(false);
        }

        public static Task<List<ConversationListItem>> ListForWorkshopsAsync(IList<Guid> workshopIds) =>
            WithDb(db => ListForWorkshopsAsync(db, workshopIds));

        public static async Task<List<ConversationListItem>> ListForWorkshopsAsync(
            DriveCareDBEntities db,
            IList<Guid> workshopIds)
        {
            if (!TablesExist() || workshopIds == null || workshopIds.Count == 0)
                return new List<ConversationListItem>();

            var ids = workshopIds.Distinct().ToList();
            var paramNames = ids.Select((_, i) => "@w" + i).ToList();
            var sql = $@"
SELECT c.RowId AS ConversationId, c.WorkshopId, c.UserId,
       w.Name AS WorkshopName, co.Name AS CompanyName,
       c.LastMessagePreview, c.LastMessageAt, c.UnreadForWorkshop AS UnreadCount,
       COALESCE(NULLIF(LTRIM(RTRIM(u.Login)), N''), u.Email, N'Клиент') AS VisitorDisplayName
FROM dbo.WorkshopConversations c
INNER JOIN dbo.Workshops w ON w.RowId = c.WorkshopId
INNER JOIN dbo.Companies co ON co.RowId = w.CompanyId
INNER JOIN dbo.Users u ON u.RowId = c.UserId
WHERE c.WorkshopId IN ({string.Join(",", paramNames)})
ORDER BY c.LastMessageAt DESC;";

            var parameters = ids.Select((id, i) => new SqlParameter(paramNames[i], id)).ToArray();
            return await db.Database.SqlQuery<ConversationListItem>(sql, parameters)
                .ToListAsync().ConfigureAwait(false);
        }

        public static Task<List<ChatMessageItem>> LoadMessagesAsync(
            Guid conversationId,
            bool forUserSide,
            Guid currentUserId,
            Guid currentEmployeeId) =>
            WithDb(db => LoadMessagesAsync(db, conversationId, forUserSide, currentUserId, currentEmployeeId));

        public static async Task<List<ChatMessageItem>> LoadMessagesAsync(
            DriveCareDBEntities db,
            Guid conversationId,
            bool forUserSide,
            Guid currentUserId,
            Guid currentEmployeeId)
        {
            if (!TablesExist() || conversationId == Guid.Empty)
                return new List<ChatMessageItem>();

            const string sql = @"
SELECT m.RowId AS MessageId,
       m.SenderKind AS SenderKind,
       CASE WHEN m.SenderKind = 0 THEN
            COALESCE(NULLIF(LTRIM(RTRIM(u.Login)), N''), u.Email, N'Клиент')
       ELSE
            COALESCE(NULLIF(LTRIM(RTRIM(e.FirstName + N' ' + e.LastName)), N''), e.Login, N'Сотрудник')
       END AS SenderName,
       m.Body AS Body,
       m.CreatedAt AS CreatedAt
FROM dbo.WorkshopMessages m
LEFT JOIN dbo.Users u ON u.RowId = m.SenderUserId
LEFT JOIN dbo.Employees e ON e.RowId = m.SenderEmployeeId
WHERE m.ConversationId = @p_cid
ORDER BY m.CreatedAt ASC, m.RowId ASC;";

            var rows = await db.Database.SqlQuery<ChatMessageRow>(sql,
                    new SqlParameter("@p_cid", conversationId))
                .ToListAsync().ConfigureAwait(false);

            if (forUserSide)
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "UPDATE dbo.WorkshopConversations SET UnreadForUser = 0 WHERE RowId = @p0", conversationId)
                    .ConfigureAwait(false);
            }
            else
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "UPDATE dbo.WorkshopConversations SET UnreadForWorkshop = 0 WHERE RowId = @p0", conversationId)
                    .ConfigureAwait(false);
            }

            return rows.Select(r => new ChatMessageItem
            {
                MessageId = r.MessageId,
                SenderKind = (MessageSenderKind)r.SenderKind,
                SenderName = r.SenderName ?? "—",
                Body = r.Body ?? string.Empty,
                CreatedAt = r.CreatedAt,
                IsMine = forUserSide
                    ? (MessageSenderKind)r.SenderKind == MessageSenderKind.User
                    : (MessageSenderKind)r.SenderKind == MessageSenderKind.Employee
            }).ToList();
        }

        public static Task<(bool ok, string error, Guid? conversationId)> SendFromUserAsync(
            Guid userId,
            Guid conversationId,
            string body) =>
            WithDb(db => SendFromUserAsync(db, userId, conversationId, body));

        public static async Task<(bool ok, string error, Guid? conversationId)> SendFromUserAsync(
            DriveCareDBEntities db,
            Guid userId,
            Guid conversationId,
            string body)
        {
            if (!TablesExist())
                return (false, "Таблицы сообщений не найдены. Выполните SQL WorkshopMessaging_Tables.sql.", null);

            var text = (body ?? string.Empty).Trim();
            if (text.Length == 0)
                return (false, "Введите текст сообщения.", null);
            if (text.Length > 2000)
                text = text.Substring(0, 2000);

            if (conversationId == Guid.Empty)
                return (false, "Выберите диалог.", null);

            try
            {
                var conv = await db.Database.SqlQuery<ConvCheckRow>(
                    "SELECT RowId, UserId FROM dbo.WorkshopConversations WHERE RowId = @p0", conversationId)
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (conv == null || conv.UserId != userId)
                    return (false, "Диалог не найден.", null);

                await InsertMessageAsync(db, conversationId, MessageSenderKind.User, userId, null, text)
                    .ConfigureAwait(false);
                return (true, null, conversationId);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public static Task<(bool ok, string error)> SendFromEmployeeAsync(
            Guid employeeId,
            Guid conversationId,
            string body) =>
            WithDb(db => SendFromEmployeeAsync(db, employeeId, conversationId, body));

        public static async Task<(bool ok, string error)> SendFromEmployeeAsync(
            DriveCareDBEntities db,
            Guid employeeId,
            Guid conversationId,
            string body)
        {
            if (!TablesExist())
                return (false, "Таблицы сообщений не найдены. Выполните SQL WorkshopMessaging_Tables.sql.");

            var text = (body ?? string.Empty).Trim();
            if (text.Length == 0)
                return (false, "Введите текст сообщения.");
            if (text.Length > 2000)
                text = text.Substring(0, 2000);

            if (conversationId == Guid.Empty)
                return (false, "Выберите диалог.");

            try
            {
                await InsertMessageAsync(db, conversationId, MessageSenderKind.Employee, null, employeeId, text)
                    .ConfigureAwait(false);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static Task<(bool ok, string error, Guid? conversationId)> StartConversationAsync(
            Guid workshopId,
            Guid userId,
            Guid employeeId,
            string firstMessage) =>
            WithDb(db => StartConversationAsync(db, workshopId, userId, employeeId, firstMessage));

        public static async Task<(bool ok, string error, Guid? conversationId)> StartConversationAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            Guid userId,
            Guid employeeId,
            string firstMessage)
        {
            if (!TablesExist())
                return (false, "Таблицы сообщений не найдены. Выполните SQL WorkshopMessaging_Tables.sql.", null);
            if (workshopId == Guid.Empty || userId == Guid.Empty)
                return (false, "Не указана мастерская или клиент.", null);

            var text = (firstMessage ?? string.Empty).Trim();
            if (text.Length == 0)
                return (false, "Введите текст первого сообщения.", null);

            try
            {
                var existing = await db.Database.SqlQuery<Guid?>(
                    "SELECT RowId FROM dbo.WorkshopConversations WHERE WorkshopId = @w AND UserId = @u",
                    new SqlParameter("@w", workshopId),
                    new SqlParameter("@u", userId)).FirstOrDefaultAsync().ConfigureAwait(false);

                Guid convId;
                if (existing.HasValue && existing.Value != Guid.Empty)
                {
                    convId = existing.Value;
                }
                else
                {
                    convId = Guid.NewGuid();
                    var clientId = await TryResolveServiceClientIdAsync(db, workshopId, userId).ConfigureAwait(false);
                    var now = DateTime.Now;
                    var preview = BuildPreview(text);
                    await db.Database.ExecuteSqlCommandAsync(
                        @"INSERT INTO dbo.WorkshopConversations
                          (RowId, WorkshopId, UserId, WorkshopServiceClientId, Subject, LastMessageAt, LastMessagePreview,
                           UnreadForUser, UnreadForWorkshop, CreatedAt)
                          VALUES (@id, @w, @u, @sc, @sub, @dt, @pr, 0, 1, @dt)",
                        new SqlParameter("@id", convId),
                        new SqlParameter("@w", workshopId),
                        new SqlParameter("@u", userId),
                        new SqlParameter("@sc", (object)clientId ?? DBNull.Value),
                        new SqlParameter("@sub", "Сообщение от мастерской"),
                        new SqlParameter("@dt", now),
                        new SqlParameter("@pr", preview)).ConfigureAwait(false);
                }

                await InsertMessageAsync(db, convId, MessageSenderKind.Employee, null, employeeId, text)
                    .ConfigureAwait(false);
                return (true, null, convId);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public static Task<(bool ok, string error, Guid? conversationId)> GetOrCreateConversationForUserAsync(
            Guid workshopId,
            Guid userId) =>
            WithDb(db => GetOrCreateConversationForUserAsync(db, workshopId, userId));

        public static async Task<(bool ok, string error, Guid? conversationId)> GetOrCreateConversationForUserAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            Guid userId)
        {
            if (!TablesExist())
                return (false, "Таблицы сообщений не найдены.", null);
            if (workshopId == Guid.Empty || userId == Guid.Empty)
                return (false, "Не указана мастерская или пользователь.", null);

            try
            {
                var existing = await db.Database.SqlQuery<Guid?>(
                    "SELECT RowId FROM dbo.WorkshopConversations WHERE WorkshopId = @w AND UserId = @u",
                    new SqlParameter("@w", workshopId),
                    new SqlParameter("@u", userId)).FirstOrDefaultAsync().ConfigureAwait(false);

                if (existing.HasValue && existing.Value != Guid.Empty)
                    return (true, null, existing.Value);

                var convId = Guid.NewGuid();
                var clientId = await TryResolveServiceClientIdAsync(db, workshopId, userId).ConfigureAwait(false);
                var now = DateTime.Now;
                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO dbo.WorkshopConversations
                      (RowId, WorkshopId, UserId, WorkshopServiceClientId, Subject, LastMessageAt, LastMessagePreview,
                       UnreadForUser, UnreadForWorkshop, CreatedAt)
                      VALUES (@id, @w, @u, @sc, @sub, @dt, @pr, 0, 1, @dt)",
                    new SqlParameter("@id", convId),
                    new SqlParameter("@w", workshopId),
                    new SqlParameter("@u", userId),
                    new SqlParameter("@sc", (object)clientId ?? DBNull.Value),
                    new SqlParameter("@sub", "Обращение с карты автосервисов"),
                    new SqlParameter("@dt", now),
                    new SqlParameter("@pr", "Новый диалог")).ConfigureAwait(false);

                return (true, null, convId);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public static Task<int> CountUnreadForUserAsync(Guid userId) =>
            WithDb(async db =>
            {
                if (!TablesExist() || userId == Guid.Empty)
                    return 0;
                return await db.Database.SqlQuery<int>(
                    "SELECT ISNULL(SUM(UnreadForUser), 0) FROM dbo.WorkshopConversations WHERE UserId = @p0",
                    userId).FirstOrDefaultAsync().ConfigureAwait(false);
            });

        public static Task<int> CountUnreadForWorkshopsAsync(IList<Guid> workshopIds) =>
            WithDb(async db =>
            {
                if (!TablesExist() || workshopIds == null || workshopIds.Count == 0)
                    return 0;
                var ids = workshopIds.Distinct().ToList();
                var paramNames = ids.Select((_, i) => "@w" + i).ToList();
                var sql = $"SELECT ISNULL(SUM(UnreadForWorkshop), 0) FROM dbo.WorkshopConversations WHERE WorkshopId IN ({string.Join(",", paramNames)})";
                var parameters = ids.Select((id, i) => new SqlParameter(paramNames[i], id)).ToArray();
                return await db.Database.SqlQuery<int>(sql, parameters).FirstOrDefaultAsync().ConfigureAwait(false);
            });

        public static Task<List<VisitorPickItem>> ListVisitorsForWorkshopAsync(Guid workshopId) =>
            WithDb(db => ListVisitorsForWorkshopAsync(db, workshopId));

        public static async Task<List<VisitorPickItem>> ListVisitorsForWorkshopAsync(
            DriveCareDBEntities db,
            Guid workshopId)
        {
            var result = new List<VisitorPickItem>();
            if (workshopId == Guid.Empty)
                return result;

            try
            {
                if (await TableExistsAsync(db, "WorkshopServiceClients").ConfigureAwait(false))
                {
                    const string sqlClients = @"
SELECT DISTINCT sc.UserId, sc.FullName, sc.Phone, u.Login
FROM dbo.WorkshopServiceClients sc
LEFT JOIN dbo.Users u ON u.RowId = sc.UserId
WHERE sc.WorkshopId = @w AND sc.UserId IS NOT NULL;";
                    var rows = await db.Database.SqlQuery<VisitorPickRow>(sqlClients, new SqlParameter("@w", workshopId))
                        .ToListAsync().ConfigureAwait(false);
                    foreach (var r in rows.Where(x => x.UserId.HasValue && x.UserId.Value != Guid.Empty))
                    {
                        result.Add(new VisitorPickItem
                        {
                            UserId = r.UserId.Value,
                            DisplayName = FormatVisitorName(r.FullName, r.Login)
                        });
                    }
                }

                const string sqlTasks = @"
SELECT DISTINCT t.ClientUserId AS UserId,
       COALESCE(NULLIF(LTRIM(RTRIM(u.Login)), N''), u.Email, N'Клиент') AS DisplayName
FROM dbo.Tasks t
INNER JOIN dbo.Users u ON u.RowId = t.ClientUserId
WHERE t.ClientUserId IS NOT NULL
  AND EXISTS (SELECT 1 FROM dbo.Employees e WHERE e.RowId = t.EmployeeId AND e.WorkshopId = @w);";

                if (await ColumnExistsAsync(db, "Tasks", "ClientUserId").ConfigureAwait(false))
                {
                    foreach (var r in await db.Database.SqlQuery<VisitorPickItem>(sqlTasks, new SqlParameter("@w", workshopId))
                                 .ToListAsync().ConfigureAwait(false))
                    {
                        if (r.UserId == Guid.Empty || result.Any(x => x.UserId == r.UserId))
                            continue;
                        result.Add(r);
                    }
                }
            }
            catch
            {
            }

            return result.OrderBy(x => x.DisplayName).ToList();
        }

        private static async Task InsertMessageAsync(
            DriveCareDBEntities db,
            Guid conversationId,
            MessageSenderKind kind,
            Guid? userId,
            Guid? employeeId,
            string body)
        {
            var preview = BuildPreview(body);
            await db.Database.ExecuteSqlCommandAsync(
                @"INSERT INTO dbo.WorkshopMessages
                  (RowId, ConversationId, SenderKind, SenderUserId, SenderEmployeeId, Body, CreatedAt)
                  VALUES (@p_id, @p_cid, @p_kind, @p_uid, @p_eid, @p_body, GETDATE())",
                new SqlParameter("@p_id", Guid.NewGuid()),
                new SqlParameter("@p_cid", conversationId),
                new SqlParameter("@p_kind", (byte)kind),
                new SqlParameter("@p_uid", (object)userId ?? DBNull.Value),
                new SqlParameter("@p_eid", (object)employeeId ?? DBNull.Value),
                new SqlParameter("@p_body", body)).ConfigureAwait(false);

            if (kind == MessageSenderKind.User)
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"UPDATE dbo.WorkshopConversations
                      SET LastMessageAt = GETDATE(), LastMessagePreview = @p_pr, UnreadForWorkshop = UnreadForWorkshop + 1
                      WHERE RowId = @p_cid",
                    new SqlParameter("@p_pr", preview),
                    new SqlParameter("@p_cid", conversationId)).ConfigureAwait(false);
            }
            else
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"UPDATE dbo.WorkshopConversations
                      SET LastMessageAt = GETDATE(), LastMessagePreview = @p_pr, UnreadForUser = UnreadForUser + 1
                      WHERE RowId = @p_cid",
                    new SqlParameter("@p_pr", preview),
                    new SqlParameter("@p_cid", conversationId)).ConfigureAwait(false);
            }
        }

        private static async Task<Guid?> TryResolveServiceClientIdAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            Guid userId)
        {
            try
            {
                return await db.Database.SqlQuery<Guid?>(
                    "SELECT TOP 1 RowId FROM dbo.WorkshopServiceClients WHERE WorkshopId = @w AND UserId = @u ORDER BY CreatedAt DESC",
                    new SqlParameter("@w", workshopId),
                    new SqlParameter("@u", userId)).FirstOrDefaultAsync().ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        private static string BuildPreview(string body)
        {
            var t = (body ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            return t.Length <= 180 ? t : t.Substring(0, 177) + "…";
        }

        private static string FormatVisitorName(string fullName, string login)
        {
            var n = (fullName ?? string.Empty).Trim();
            if (n.Length > 0)
                return n;
            return string.IsNullOrWhiteSpace(login) ? "Клиент" : login.Trim();
        }

        private static async Task<bool> TableExistsAsync(DriveCareDBEntities db, string tableName)
        {
            var sql = "SELECT CASE WHEN OBJECT_ID(@n, N'U') IS NOT NULL THEN 1 ELSE 0 END";
            return await db.Database.SqlQuery<int>(sql, new SqlParameter("@n", "dbo." + tableName))
                .FirstOrDefaultAsync().ConfigureAwait(false) == 1;
        }

        private static async Task<bool> ColumnExistsAsync(DriveCareDBEntities db, string table, string column)
        {
            var sql = "SELECT CASE WHEN COL_LENGTH(@t, @c) IS NOT NULL THEN 1 ELSE 0 END";
            return await db.Database.SqlQuery<int>(sql,
                    new SqlParameter("@t", "dbo." + table),
                    new SqlParameter("@c", column))
                .FirstOrDefaultAsync().ConfigureAwait(false) == 1;
        }

        private static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> work)
        {
            using (var db = new DriveCareDBEntities())
                return await work(db).ConfigureAwait(false);
        }

        private sealed class ChatMessageRow
        {
            public Guid MessageId { get; set; }
            public byte SenderKind { get; set; }
            public string SenderName { get; set; }
            public string Body { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private sealed class ConvCheckRow
        {
            public Guid RowId { get; set; }
            public Guid UserId { get; set; }
        }

        private sealed class VisitorPickRow
        {
            public Guid? UserId { get; set; }
            public string FullName { get; set; }
            public string Phone { get; set; }
            public string Login { get; set; }
        }
    }

    public sealed class VisitorPickItem
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; }
    }
}
