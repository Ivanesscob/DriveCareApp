using System;

namespace DriveCarePro.Services
{
    /// <summary>Коды разрешений Pro (совпадают с Permissions.Code в БД).</summary>
    public static class ProPermissions
    {
        public static readonly Guid ModerateSalesId = new Guid("32A54FDC-2A14-4C90-A715-0016DAE61E19");
        public const string ModerateSales = "MODERATE_SALES";

        public static readonly Guid EditTasksId = new Guid("28CFDDE0-2E89-4953-BB2F-05D31A80AC1B");
        public const string EditTasks = "EDIT_TASKS";

        public static readonly Guid ViewAnalyticsId = new Guid("03F24D58-7616-4E5E-8671-1484322AF40E");
        public const string ViewAnalytics = "VIEW_ANALYTICS";

        public static readonly Guid AdminPanelId = new Guid("CEA68AB4-2741-4982-B729-166ED196BC9C");
        public const string AdminPanel = "ADMIN_PANEL";

        public static readonly Guid ViewNotificationsId = new Guid("EF2671E5-6A8F-4CBE-85A7-187A94D47C21");
        public const string ViewNotifications = "VIEW_NOTIFICATIONS";

        public static readonly Guid CreateRolesId = new Guid("82198DF6-1639-41BA-A4A9-1E4DAA3B2E26");
        public const string CreateRoles = "CREATE_ROLES";

        public static readonly Guid EditRepairsId = new Guid("9C173222-6BC6-4A5E-88AC-4A220B7CE3AF");
        public const string EditRepairs = "EDIT_REPAIRS";

        public static readonly Guid DeleteCarsId = new Guid("547361ED-FC2F-4E2B-A62D-4CE187ACA642");
        public const string DeleteCars = "DELETE_CARS";

        public static readonly Guid ViewCarsId = new Guid("B74C53F4-DA47-4056-A17B-4F33D4FC2729");
        public const string ViewCars = "VIEW_CARS";

        public static readonly Guid DeleteTasksId = new Guid("D22C667E-C639-4D20-BB08-5451AC04789C");
        public const string DeleteTasks = "DELETE_TASKS";

        public static readonly Guid CreateSalesId = new Guid("49EEE6AE-148D-4EE2-BE97-577C80CC0D49");
        public const string CreateSales = "CREATE_SALES";

        public static readonly Guid ViewSalesId = new Guid("DFF0B938-22B4-40AE-8FC1-589020E77E1C");
        public const string ViewSales = "VIEW_SALES";

        public static readonly Guid EditEmployeesId = new Guid("45618BBC-E8F0-4F4C-A89C-80BA4998A67E");
        public const string EditEmployees = "EDIT_EMPLOYEES";

        public static readonly Guid ViewEmployeesId = new Guid("5F25F657-D110-4344-9AAE-81AA1C346E1E");
        public const string ViewEmployees = "VIEW_EMPLOYEES";

        public static readonly Guid CreateEmployeesId = new Guid("663B84F9-7A73-4B24-9B2D-8D816ED6B229");
        public const string CreateEmployees = "CREATE_EMPLOYEES";

        public static readonly Guid DeleteEmployeesId = new Guid("41ABB304-77C5-41F6-9BDF-C0F982D856F7");
        public const string DeleteEmployees = "DELETE_EMPLOYEES";

        public static readonly Guid ViewRepairsId = new Guid("A91C811F-360B-4849-A89B-C2E78516EA8A");
        public const string ViewRepairs = "VIEW_REPAIRS";

        public static readonly Guid EditCarsId = new Guid("28002FDA-0391-4846-8CC9-C880FE636E3C");
        public const string EditCars = "EDIT_CARS";

        public static readonly Guid CreateRepairsId = new Guid("5039480B-D5CE-4A33-981D-CC678E38A9E8");
        public const string CreateRepairs = "CREATE_REPAIRS";

        public static readonly Guid ViewTasksId = new Guid("6D51012E-BEC0-41E5-841B-CF3437CC724B");
        public const string ViewTasks = "VIEW_TASKS";

        public static readonly Guid CreateTasksId = new Guid("6F8DF7A3-5CE6-4243-BBD5-E8060374ADDC");
        public const string CreateTasks = "CREATE_TASKS";

        public const string PurchaseParts = "PURCHASE_PARTS";

        public static readonly Guid ConfirmWorkshopBookingId = new Guid("B8E2F4A1-3C5D-4E9F-A2B1-7D8E9F0A1B2C");
        public const string ConfirmWorkshopBooking = "CONFIRM_WORKSHOP_BOOKING";

        public static readonly Guid ManageWorkshopScheduleId = new Guid("DC030001-0001-4001-8001-000000000001");
        public const string ManageWorkshopSchedule = "MANAGE_WORKSHOP_SCHEDULE";

        /// <summary>Разрешения платформенного администратора.</summary>
        public static readonly string[] AdminPanelCodes =
        {
            AdminPanel,
            ModerateSales,
            ViewNotifications
        };

        /// <summary>Разрешения рабочей панели сотрудника / владельца / сервиса.</summary>
        public static readonly string[] WorkspaceCodes =
        {
            ViewEmployees,
            EditEmployees,
            CreateEmployees,
            DeleteEmployees,
            CreateRoles,
            ViewRepairs,
            EditRepairs,
            CreateRepairs,
            ViewTasks,
            EditTasks,
            CreateTasks,
            ViewCars,
            EditCars,
            DeleteCars,
            ViewSales,
            CreateSales,
            ViewAnalytics,
            DeleteTasks,
            ConfirmWorkshopBooking,
            ManageWorkshopSchedule
        };

        public static bool IsAdminPanelCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return false;
            foreach (var c in AdminPanelCodes)
            {
                if (string.Equals(c, code, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
