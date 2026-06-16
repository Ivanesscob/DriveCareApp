using DriveCareCore.Data.BD;
using System;

namespace DriveCarePro.Services
{
    /// <summary>
    /// Какие разрешения выдаются владельцу организации (Pro): все рабочие, без админских и клиентских.
    /// </summary>
    internal static class OrganizationPermissionRules
    {
        public static bool IsGrantableToWorkshopOwner(Permission permission, PermissionGroup group)
        {
            if (permission == null)
                return false;

            var code = (permission.Code ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(code))
                return false;

            if (IsAdminPermissionGroup(group) || IsAdminPermissionCode(code) || ProPermissions.IsAdminPanelCode(code))
                return false;

            if (IsUserPermissionGroup(group) || IsUserPermissionCode(code))
                return false;

            return true;
        }

        public static bool IsAdminPermissionGroup(PermissionGroup group)
        {
            if (group == null)
                return false;
            return MatchesAdminToken(group.Code) || MatchesAdminToken(group.Name);
        }

        public static bool IsAdminPermissionCode(string code) => MatchesAdminToken(code);

        public static bool IsUserPermissionGroup(PermissionGroup group)
        {
            if (group == null)
                return false;
            return MatchesUserToken(group.Code) || MatchesUserToken(group.Name);
        }

        public static bool IsUserPermissionCode(string code) => MatchesUserToken(code);

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

        private static bool MatchesUserToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var n = value.Trim().ToLowerInvariant();

            if (n.Contains("сотрудник") || n.Contains("employee") || n.Contains("организац")
                || n.Contains("сервис") || n.Contains("service") || n.Contains("мастерск")
                || n.Contains("workshop") || n.Contains("pro."))
                return false;

            if (n.Contains("пользовател") || n.Contains("пользователя"))
                return true;
            if (n.Contains("клиент") || n.Contains("client"))
                return true;
            if (n == "user" || n == "users" || n.StartsWith("user.") || n.StartsWith("users."))
                return true;
            if (n.Contains("drivecare") && !n.Contains("pro"))
                return true;
            if (n.Contains("consumer") || n.Contains("потребител"))
                return true;

            return false;
        }
    }
}
