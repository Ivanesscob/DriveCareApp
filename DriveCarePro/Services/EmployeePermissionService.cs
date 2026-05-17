using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace DriveCarePro.Services
{
    internal static class EmployeePermissionService
    {
        private static HashSet<string> _codes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> CurrentCodes => _codes;

        public static void RefreshForEmployee(Employee employee, IList<Role> roles)
        {
            RefreshForEmployeeAsync(employee, roles).GetAwaiter().GetResult();
        }

        public static async System.Threading.Tasks.Task RefreshForEmployeeAsync(Employee employee, IList<Role> roles)
        {
            _codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (employee == null || roles == null || roles.Count == 0)
                return;

            try
            {
                var roleIds = roles.Where(r => r != null).Select(r => r.RowId).Distinct().ToList();
                if (roleIds.Count == 0)
                    return;

                var codes = await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var list = await (
                            from map in db.RolePermissionsMaps
                            where roleIds.Contains(map.RoleId)
                            join p in db.Permissions on map.PermissionId equals p.RowId
                            where p.Code != null && p.Code != ""
                            select p.Code.Trim()
                        )
                        .Distinct()
                        .ToListAsync()
                        .ConfigureAwait(false);

                    return list;
                }).ConfigureAwait(false);

                _codes = new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                _codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public static void Clear()
        {
            _codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
