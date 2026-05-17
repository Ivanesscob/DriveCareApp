using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services
{
    internal static class OwnerEmployeesDataService
    {
        public sealed class EmployeeRowVm
        {
            public Guid EmployeeId { get; set; }
            public string FullName { get; set; }
            public string Login { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string WorkshopName { get; set; }
            public string RolesDisplay { get; set; }
            public string ActiveDisplay { get; set; }
            public bool IsOrganizationOwner { get; set; }
        }

        public static System.Threading.Tasks.Task<List<EmployeeRowVm>> LoadGridAsync(OwnerOrganizationScope scope) =>
            DatabaseExecutor.WithDbAsync(async db =>
            {
                var workshopNames = await db.Workshops
                    .Where(w => scope.WorkshopIds.Contains(w.RowId))
                    .ToListAsync()
                    .ConfigureAwait(false);

                var workshopDict = workshopNames.ToDictionary(
                    w => w.RowId,
                    w => (w.Name ?? string.Empty).Trim());

                var roleMaps = await db.EmployeeRolesMaps.ToListAsync().ConfigureAwait(false);
                var roles = await db.Roles.ToListAsync().ConfigureAwait(false);
                var roleDict = roles.ToDictionary(
                    r => r.RowId,
                    r => (r.Name ?? string.Empty).Trim());

                var employees = await scope.EmployeesInOrganization(db)
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return employees.Select(e =>
                {
                    var roleIds = roleMaps
                        .Where(m => m.EmployeeId == e.RowId)
                        .Select(m => m.RoleId)
                        .ToList();
                    var roleNames = roleIds
                        .Select(id => roleDict.TryGetValue(id, out var n) ? n : null)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                    var roleText = roleNames.Count == 0
                        ? "—"
                        : string.Join(", ", roleNames);
                    var isOwner = roleNames.Any(AppState.IsOwnerRoleName);
                    var ws = e.WorkshopId.HasValue && workshopDict.TryGetValue(e.WorkshopId.Value, out var wn)
                        ? wn
                        : "—";

                    return new EmployeeRowVm
                    {
                        EmployeeId = e.RowId,
                        FullName = AppState.FormatEmployeeDisplayName(e),
                        Login = string.IsNullOrWhiteSpace(e.Login) ? "—" : e.Login.Trim(),
                        Email = string.IsNullOrWhiteSpace(e.Email) ? "—" : e.Email.Trim(),
                        Phone = string.IsNullOrWhiteSpace(e.Phone) ? "—" : e.Phone.Trim(),
                        WorkshopName = string.IsNullOrWhiteSpace(ws) ? "—" : ws,
                        RolesDisplay = string.IsNullOrWhiteSpace(roleText) ? "—" : roleText,
                        ActiveDisplay = e.IsActive == false ? "Нет" : "Да",
                        IsOrganizationOwner = isOwner
                    };
                }).ToList();
            });
    }
}
