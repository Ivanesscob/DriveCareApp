using System;
using System.Linq;

namespace DriveCarePro.Pages.Admin
{
    internal static class AdminReferenceTables
    {
        public static readonly string[] Allowed =
        {
            "Users", "Employees", "Companies", "Workshops", "Cars", "CarSales", "Parts",
            "Roles", "Notifications", "UserRoles", "EmployeeRolesMap", "UserCars",
            "Addresses", "Brands", "Models", "CarTypes", "Countries", "UserCarSales",
            "CarSalePrices", "RepairHistory", "Tasks", "CarColors", "Colors", "FuelTypes",
            "PartManufacturers", "WarehouseManagers", "RepairCategories", "Statuses",
            "UserNotifications",
            "PermissionGroups", "Permissions", "RolePermissionsMap"
        };

        public static string[] Sorted() =>
            Allowed.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
