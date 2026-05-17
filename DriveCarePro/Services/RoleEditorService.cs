using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Linq;
using System.Text;



namespace DriveCarePro.Services

{

    internal enum RoleScopeKind

    {

        Company,

        Workshop

    }



    internal static class RoleEditorService

    {

        public sealed class PermissionItem

        {

            public Guid PermissionId { get; set; }

            public string Code { get; set; }

            public string Name { get; set; }

            public string Description { get; set; }

            public string GroupName { get; set; }

            public string GroupCode { get; set; }

            public string Title => string.IsNullOrWhiteSpace(Name) ? Code : Name;

            public string Subtitle

            {

                get

                {

                    var parts = new List<string>();

                    if (!string.IsNullOrWhiteSpace(GroupName))

                        parts.Add(GroupName);

                    if (!string.IsNullOrWhiteSpace(Code))

                        parts.Add(Code);

                    if (!string.IsNullOrWhiteSpace(Description))

                        parts.Add(Description);

                    return parts.Count > 0 ? string.Join(" · ", parts) : "—";

                }

            }

        }



        public sealed class WorkshopItem

        {

            public Guid RowId { get; set; }

            public string Name { get; set; }

        }



        public sealed class RoleEditModel

        {

            public Guid? RoleId { get; set; }

            public string Name { get; set; }

            public string Description { get; set; }

            public RoleScopeKind ScopeKind { get; set; } = RoleScopeKind.Workshop;

            public Guid? WorkshopId { get; set; }

            public Guid? CompanyId { get; set; }

            public bool IsActive { get; set; } = true;

            public HashSet<Guid> PermissionIds { get; } = new HashSet<Guid>();

        }



        public sealed class SaveRoleResult

        {

            public bool Success { get; set; }

            public string ErrorMessage { get; set; }

            public Guid RoleId { get; set; }

        }



        public static List<PermissionItem> LoadPermissions(bool systemRolesMode)

        {

            var db = AppConnect.model1;



            var rows = (

                from p in db.Permissions

                join g in db.PermissionGroups on p.PermissionGroupId equals g.RowId into groups

                from g in groups.DefaultIfEmpty()

                select new

                {

                    Permission = p,

                    Group = g

                }).ToList();



            if (!systemRolesMode)

            {

                rows = rows

                    .Where(r => !IsAdminPermissionGroup(r.Group))

                    .Where(r => !IsAdminPermissionCode(r.Permission?.Code))

                    .ToList();

            }



            return rows

                .OrderBy(r => r.Group != null ? (r.Group.Name ?? r.Group.Code ?? "") : "")

                .ThenBy(r => r.Permission.Name)

                .ThenBy(r => r.Permission.Code)

                .Select(r => new PermissionItem

                {

                    PermissionId = r.Permission.RowId,

                    Code = (r.Permission.Code ?? string.Empty).Trim(),

                    Name = (r.Permission.Name ?? string.Empty).Trim(),

                    Description = (r.Permission.Description ?? string.Empty).Trim(),

                    GroupName = r.Group != null ? (r.Group.Name ?? string.Empty).Trim() : string.Empty,

                    GroupCode = r.Group != null ? (r.Group.Code ?? string.Empty).Trim() : string.Empty

                })

                .ToList();

        }



        public static List<WorkshopItem> LoadWorkshopsForOwner(OwnerOrganizationScope scope)

        {

            if (scope == null)

                return new List<WorkshopItem>();



            return AppConnect.model1.Workshops

                .Where(w => scope.WorkshopIds.Contains(w.RowId))

                .OrderBy(w => w.Name)

                .AsEnumerable()

                .Select(w => new WorkshopItem

                {

                    RowId = w.RowId,

                    Name = string.IsNullOrWhiteSpace(w.Name) ? "—" : w.Name.Trim()

                })

                .ToList();

        }



        public static RoleEditModel LoadRole(Guid roleId, bool systemRolesMode, OwnerOrganizationScope scope)

        {

            var db = AppConnect.model1;

            var role = db.Roles.FirstOrDefault(r => r.RowId == roleId);

            if (role == null || !CanEdit(role, systemRolesMode, scope))

                return null;



            var model = new RoleEditModel

            {

                RoleId = role.RowId,

                Name = role.Name ?? string.Empty,

                Description = role.Description ?? string.Empty,

                WorkshopId = role.WorkshopId,

                CompanyId = role.CompanyId,

                IsActive = role.IsActive != false,

                ScopeKind = ResolveScopeKind(role, scope)

            };



            foreach (var pid in db.RolePermissionsMaps.Where(rp => rp.RoleId == roleId).Select(rp => rp.PermissionId))

                model.PermissionIds.Add(pid);



            return model;

        }



        public static bool CanEdit(Role role, bool systemRolesMode, OwnerOrganizationScope scope)

        {

            if (role == null)

                return false;

            if (systemRolesMode)

                return scope == null && !role.WorkshopId.HasValue && !role.CompanyId.HasValue;

            return scope != null && scope.CanOwnerManageRole(role);

        }



        public static SaveRoleResult Save(RoleEditModel model, bool systemRolesMode, OwnerOrganizationScope scope)

        {

            if (model == null)

                return Fail("Нет данных для сохранения.");



            var name = (model.Name ?? string.Empty).Trim();

            if (name.Length == 0)

                return Fail("Введите название роли.");



            var db = AppConnect.model1;

            var desc = (model.Description ?? string.Empty).Trim();

            Role role;



            if (!model.RoleId.HasValue)

            {

                if (systemRolesMode)

                {

                    if (NameConflictSystem(db, name, null))

                        return Fail("Системная роль с таким названием уже существует.");

                    role = new Role

                    {

                        RowId = Guid.NewGuid(),

                        Name = name,

                        Description = desc.Length == 0 ? null : desc,

                        WorkshopId = null,

                        CompanyId = null,

                        IsActive = model.IsActive

                    };

                }

                else

                {

                    role = new Role

                    {

                        RowId = Guid.NewGuid(),

                        Name = name,

                        Description = desc.Length == 0 ? null : desc,

                        IsActive = model.IsActive

                    };

                    var scopeErr = ApplyOrganizationScope(role, model, scope);

                    if (scopeErr != null)

                        return Fail(scopeErr);

                    if (NameConflictOrganization(db, name, role, scope))

                        return Fail("Роль с таким названием уже существует в выбранной области.");

                }

                db.Roles.Add(role);

            }

            else

            {

                role = db.Roles.FirstOrDefault(r => r.RowId == model.RoleId.Value);

                if (role == null || !CanEdit(role, systemRolesMode, scope))

                    return Fail("Эту роль нельзя изменить.");



                if (systemRolesMode)

                {

                    if (NameConflictSystem(db, name, role.RowId))

                        return Fail("Системная роль с таким названием уже существует.");

                    role.Name = name;

                    role.Description = desc.Length == 0 ? null : desc;

                    role.WorkshopId = null;

                    role.CompanyId = null;

                    role.IsActive = model.IsActive;

                }

                else

                {

                    var apply = ApplyOrganizationScope(role, model, scope);

                    if (apply != null)

                        return Fail(apply);

                    if (NameConflictOrganization(db, name, role, scope))

                        return Fail("Роль с таким названием уже существует в выбранной области.");

                    role.Name = name;

                    role.Description = desc.Length == 0 ? null : desc;

                    role.IsActive = model.IsActive;

                }

            }



            try

            {

                var allowed = new HashSet<Guid>(LoadPermissions(systemRolesMode).Select(p => p.PermissionId));

                var selected = model.PermissionIds.Where(allowed.Contains).ToList();

                SyncPermissions(db, role.RowId, selected);

                db.SaveChanges();

                return new SaveRoleResult { Success = true, RoleId = role.RowId };

            }

            catch (Exception ex)

            {

                return Fail(FormatSaveError(ex));

            }

        }



        private static string FormatSaveError(Exception ex)

        {

            var msg = new StringBuilder();

            msg.Append("Не удалось сохранить.");

            for (var e = ex; e != null; e = e.InnerException)

            {

                if (!string.IsNullOrWhiteSpace(e.Message))

                    msg.Append(" ").Append(e.Message.Trim());

            }

            var text = msg.ToString();

            if (text.IndexOf("FK_RolePermissionsMap_Permissions", StringComparison.OrdinalIgnoreCase) >= 0

                || (text.IndexOf("RolePermissionsMap", StringComparison.OrdinalIgnoreCase) >= 0

                    && text.IndexOf("Permissions", StringComparison.OrdinalIgnoreCase) >= 0

                    && text.IndexOf("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) >= 0))

            {

                text += " Проверьте в БД: FK_RolePermissionsMap_Permissions должен ссылаться на колонку PermissionId → Permissions.RowId (не RoleId).";

            }

            return text;

        }



        public static string TryDelete(Guid roleId, bool systemRolesMode, OwnerOrganizationScope scope)

        {

            var db = AppConnect.model1;

            var role = db.Roles.FirstOrDefault(r => r.RowId == roleId);

            if (role == null || !CanEdit(role, systemRolesMode, scope))

                return systemRolesMode

                    ? "Удалить можно только системную роль."

                    : "Эту роль нельзя удалить.";



            List<EmployeeRolesMap> empMaps;

            if (systemRolesMode)

                empMaps = db.EmployeeRolesMaps.Where(m => m.RoleId == role.RowId).ToList();

            else

            {

                var orgIds = scope.EmployeesInOrganization(db).Select(emp => emp.RowId).ToList();

                empMaps = db.EmployeeRolesMaps

                    .Where(m => m.RoleId == role.RowId && orgIds.Contains(m.EmployeeId))

                    .ToList();

            }



            var userMaps = db.UserRoles.Where(m => m.RoleId == role.RowId).ToList();

            if (empMaps.Count > 0 || userMaps.Count > 0)

                return $"Нельзя удалить: роль назначена сотрудникам ({empMaps.Count}) или пользователям ({userMaps.Count}).";



            foreach (var rp in db.RolePermissionsMaps.Where(r => r.RoleId == role.RowId).ToList())

                db.RolePermissionsMaps.Remove(rp);

            db.Roles.Remove(role);



            try

            {

                db.SaveChanges();

                return null;

            }

            catch (Exception ex)

            {

                return "Не удалось удалить: " + ex.Message;

            }

        }



        private static bool IsAdminPermissionGroup(PermissionGroup group)

        {

            if (group == null)

                return false;

            return MatchesAdminToken(group.Code) || MatchesAdminToken(group.Name);

        }



        private static bool IsAdminPermissionCode(string code) =>

            MatchesAdminToken(code);



        private static bool MatchesAdminToken(string value)

        {

            if (string.IsNullOrWhiteSpace(value))

                return false;

            var n = value.Trim().ToLowerInvariant();

            if (n.Contains("админ") || n.Contains("admin"))

                return true;

            if (n.Contains("модерац") || n.Contains("moderation"))

                return true;

            if (n.StartsWith("pro.") && (n.Contains("admin") || n.Contains("moderation")))

                return true;

            return n == "platform" || n == "proadmin" || n == "pro_admin";

        }



        private static RoleScopeKind ResolveScopeKind(Role role, OwnerOrganizationScope scope)

        {

            if (role.WorkshopId.HasValue)

                return RoleScopeKind.Workshop;

            if (scope != null && role.CompanyId == scope.CompanyId)

                return RoleScopeKind.Company;

            return RoleScopeKind.Workshop;

        }



        private static string ApplyOrganizationScope(Role role, RoleEditModel model, OwnerOrganizationScope scope)

        {

            if (scope == null)

                return "Организация не определена.";



            if (model.ScopeKind == RoleScopeKind.Company)

            {

                role.CompanyId = scope.CompanyId;

                role.WorkshopId = null;

                return null;

            }



            if (!model.WorkshopId.HasValue)

                return "Выберите салон (мастерскую) для роли.";



            if (!scope.WorkshopIds.Contains(model.WorkshopId.Value))

                return "Выбран салон не из вашей компании.";



            role.WorkshopId = model.WorkshopId.Value;

            role.CompanyId = null;

            return null;

        }



        private static bool NameConflictSystem(DriveCareDBEntities db, string name, Guid? excludeId) =>

            db.Roles.Any(r =>

                r.RowId != excludeId &&

                !r.WorkshopId.HasValue && !r.CompanyId.HasValue &&

                r.Name != null && r.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));



        private static bool NameConflictOrganization(DriveCareDBEntities db, string name, Role role, OwnerOrganizationScope scope)

        {

            if (role.WorkshopId.HasValue)

            {

                return db.Roles.Any(r =>

                    r.RowId != role.RowId &&

                    r.WorkshopId == role.WorkshopId &&

                    r.Name != null && r.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

            }



            return db.Roles.Any(r =>

                r.RowId != role.RowId &&

                r.CompanyId == scope.CompanyId && !r.WorkshopId.HasValue &&

                r.Name != null && r.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

        }



        private static void SyncPermissions(DriveCareDBEntities db, Guid roleId, ICollection<Guid> selected)

        {

            var existing = db.RolePermissionsMaps.Where(rp => rp.RoleId == roleId).ToList();

            foreach (var row in existing.Where(r => !selected.Contains(r.PermissionId)))

                db.RolePermissionsMaps.Remove(row);



            var existingIds = existing.Select(r => r.PermissionId).ToHashSet();

            foreach (var pid in selected.Where(id => !existingIds.Contains(id)))

            {

                db.RolePermissionsMaps.Add(new RolePermissionsMap

                {

                    RowId = Guid.NewGuid(),

                    RoleId = roleId,

                    PermissionId = pid

                });

            }

        }



        private static SaveRoleResult Fail(string message) =>

            new SaveRoleResult { Success = false, ErrorMessage = message };

    }

}


