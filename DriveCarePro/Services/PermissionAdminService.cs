using DriveCareCore.Data.BD;
using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace DriveCarePro.Services
{
    internal static class PermissionAdminService
    {
        public static bool IsServicePermissionGroup(PermissionGroup group)
        {
            if (group == null)
                return false;

            var name = (group.Name ?? string.Empty).Trim().ToLowerInvariant();
            var code = (group.Code ?? string.Empty).Trim().ToLowerInvariant();

            return name.Contains("сервис") || name.Contains("service")
                   || code.Contains("сервис") || code.Contains("service");
        }

        public static (bool ok, string error, int ownerGrants) TryCreate(
            DriveCareDBEntities db,
            string code,
            string name,
            string description,
            Guid permissionGroupId)
        {
            try
            {
                ClearStalePermissionTrackerEntries(db);

                code = (code ?? string.Empty).Trim();
                name = (name ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
                    return (false, "Укажите код и название.", 0);

                if (db.Permissions.AsNoTracking().Any(p => p.Code == code))
                    return (false, "Разрешение с таким кодом уже есть.", 0);

                var group = db.PermissionGroups.AsNoTracking()
                    .FirstOrDefault(g => g.RowId == permissionGroupId);
                if (group == null)
                    return (false, "Группа разрешений не найдена.", 0);

                var rowId = Guid.NewGuid();
                var desc = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

                var inserted = db.Database.ExecuteSqlCommand(
                    @"INSERT INTO dbo.Permissions (RowId, Code, Name, Description, PermissionGroupId)
                      VALUES (@p0, @p1, @p2, @p3, @p4)",
                    rowId, code, name, desc, permissionGroupId);

                if (inserted == 0)
                    return (false, "Не удалось сохранить разрешение.", 0);

                DetachPermissionFromContext(db, rowId);

                var ownerGrants = 0;
                if (IsServicePermissionGroup(group))
                    ownerGrants = GrantPermissionToOwnerRoles(db, rowId);

                return (true, null, ownerGrants);
            }
            catch (Exception ex)
            {
                return (false, FormatSaveError(ex), 0);
            }
        }

        public static string TryUpdate(
            DriveCareDBEntities db,
            Guid permissionId,
            string code,
            string name,
            string description,
            Guid permissionGroupId)
        {
            try
            {
                ClearStalePermissionTrackerEntries(db);

                if (!db.Permissions.AsNoTracking().Any(p => p.RowId == permissionId))
                    return "Разрешение не найдено.";

                code = (code ?? string.Empty).Trim();
                name = (name ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
                    return "Укажите код и название.";

                if (db.Permissions.AsNoTracking().Any(p => p.Code == code && p.RowId != permissionId))
                    return "Разрешение с таким кодом уже есть.";

                var group = db.PermissionGroups.AsNoTracking()
                    .FirstOrDefault(g => g.RowId == permissionGroupId);
                if (group == null)
                    return "Группа разрешений не найдена.";

                var desc = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

                var updated = db.Database.ExecuteSqlCommand(
                    @"UPDATE dbo.Permissions
                      SET Code = @p1, Name = @p2, Description = @p3, PermissionGroupId = @p4
                      WHERE RowId = @p0",
                    permissionId, code, name, desc, permissionGroupId);

                if (updated == 0)
                    return "Разрешение не найдено.";

                DetachPermissionFromContext(db, permissionId);

                if (IsServicePermissionGroup(group))
                    GrantPermissionToOwnerRoles(db, permissionId);

                return null;
            }
            catch (Exception ex)
            {
                return FormatSaveError(ex);
            }
        }

        /// <summary>Выдаёт разрешение всем ролям владельца (только SQL).</summary>
        public static int GrantPermissionToOwnerRoles(DriveCareDBEntities db, Guid permissionId)
        {
            var ownerRoles = db.Roles.AsNoTracking()
                .Where(r => r.IsActive != false)
                .ToList()
                .Where(r => AppState.IsOwnerRoleName(r.Name))
                .ToList();

            var added = 0;
            foreach (var role in ownerRoles)
            {
                if (RolePermissionMapExists(db, role.RowId, permissionId))
                    continue;

                if (InsertRolePermissionMap(db, role.RowId, permissionId))
                    added++;
            }

            return added;
        }

        public static string TryDelete(DriveCareDBEntities db, Guid permissionId)
        {
            try
            {
                ClearStalePermissionTrackerEntries(db);

                if (!db.Permissions.AsNoTracking().Any(p => p.RowId == permissionId))
                    return "Разрешение не найдено.";

                DeleteRolePermissionLinks(db, permissionId);

                var deleted = db.Database.ExecuteSqlCommand(
                    "DELETE FROM dbo.Permissions WHERE RowId = @p0", permissionId);

                if (deleted == 0)
                    return "Разрешение не найдено.";

                DetachPermissionFromContext(db, permissionId);
                return null;
            }
            catch (Exception ex)
            {
                return FormatDeleteError(ex);
            }
        }

        /// <summary>Сбрасывает «зависшие» удаления EF после SQL DELETE в общем контексте.</summary>
        private static void ClearStalePermissionTrackerEntries(DriveCareDBEntities db)
        {
            foreach (var entry in db.ChangeTracker.Entries<RolePermissionsMap>()
                .Where(e => e.State == EntityState.Deleted).ToList())
            {
                entry.State = EntityState.Detached;
            }

            foreach (var entry in db.ChangeTracker.Entries<Permission>()
                .Where(e => e.State == EntityState.Deleted).ToList())
            {
                entry.State = EntityState.Detached;
            }
        }

        private static bool RolePermissionMapExists(DriveCareDBEntities db, Guid roleId, Guid permissionId)
        {
            foreach (var sql in new[]
            {
                "SELECT COUNT(1) FROM dbo.RolePermissionsMap WHERE RoleId = @p0 AND PermissionId = @p1",
                "SELECT COUNT(1) FROM dbo.RolePermissions WHERE RoleId = @p0 AND PermissionId = @p1"
            })
            {
                try
                {
                    var count = db.Database.SqlQuery<int>(sql, roleId, permissionId).FirstOrDefault();
                    if (count > 0)
                        return true;
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                }
            }

            return db.RolePermissionsMaps.AsNoTracking()
                .Any(m => m.RoleId == roleId && m.PermissionId == permissionId);
        }

        private static bool InsertRolePermissionMap(DriveCareDBEntities db, Guid roleId, Guid permissionId)
        {
            var mapId = Guid.NewGuid();
            foreach (var sql in new[]
            {
                "INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId) VALUES (@p0, @p1, @p2)",
                "INSERT INTO dbo.RolePermissions (RowId, RoleId, PermissionId) VALUES (@p0, @p1, @p2)"
            })
            {
                try
                {
                    db.Database.ExecuteSqlCommand(sql, mapId, roleId, permissionId);
                    return true;
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                }
                catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DeleteRolePermissionLinks(DriveCareDBEntities db, Guid permissionId)
        {
            foreach (var sql in new[]
            {
                "DELETE FROM dbo.RolePermissionsMap WHERE PermissionId = @p0",
                "DELETE FROM dbo.RolePermissions WHERE PermissionId = @p0"
            })
            {
                try
                {
                    db.Database.ExecuteSqlCommand(sql, permissionId);
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                }
            }

            foreach (var map in db.RolePermissionsMaps.Local.Where(m => m.PermissionId == permissionId).ToList())
            {
                var entry = db.Entry(map);
                if (entry != null && entry.State != EntityState.Detached)
                    entry.State = EntityState.Detached;
            }
        }

        private static void DetachPermissionFromContext(DriveCareDBEntities db, Guid permissionId)
        {
            foreach (var map in db.RolePermissionsMaps.Local.Where(m => m.PermissionId == permissionId).ToList())
            {
                var entry = db.Entry(map);
                if (entry != null)
                    entry.State = EntityState.Detached;
            }

            var permission = db.Permissions.Local.FirstOrDefault(p => p.RowId == permissionId);
            if (permission != null)
            {
                var entry = db.Entry(permission);
                if (entry != null)
                    entry.State = EntityState.Detached;
                return;
            }

            try
            {
                var tracked = db.Permissions.Find(permissionId);
                if (tracked != null)
                {
                    var entry = ((IObjectContextAdapter)db).ObjectContext.ObjectStateManager
                        .GetObjectStateEntry(tracked);
                    if (entry != null)
                        entry.ChangeState(EntityState.Detached);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static string FormatSaveError(Exception ex)
        {
            var sb = new StringBuilder();
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    if (sb.Length > 0)
                        sb.Append(' ');
                    sb.Append(current.Message.Trim());
                }
            }

            return sb.Length > 0 ? sb.ToString() : "Не удалось сохранить разрешение.";
        }

        private static string FormatDeleteError(Exception ex)
        {
            var text = FormatSaveError(ex);

            if (text.IndexOf("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("REFERENCE", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                text += " Сначала снимите разрешение со всех ролей в конструкторе ролей.";
            }

            return text;
        }
    }
}
