using System;
using System.Collections.Generic;

namespace DriveCareCore.Analytics
{
    public static class ActivityEventCodes
    {
        public const string UserLogin = "USER_LOGIN";
        public const string UserRegister = "USER_REGISTER";
        public const string ProEmployeeLogin = "PRO_EMPLOYEE_LOGIN";

        public const string CarSaleCatalogView = "CAR_SALE_CATALOG_VIEW";
        public const string CarSaleDetailView = "CAR_SALE_DETAIL_VIEW";
        public const string CarSaleCreate = "CAR_SALE_CREATE";
        public const string CarSaleModerateApprove = "CAR_SALE_MODERATE_APPROVE";
        public const string CarSaleModerateReject = "CAR_SALE_MODERATE_REJECT";

        public const string WorkshopDetailView = "WORKSHOP_DETAIL_VIEW";
        public const string WorkshopMapView = "WORKSHOP_MAP_VIEW";
        public const string WorkshopOnlineBookingCreate = "WORKSHOP_ONLINE_BOOKING_CREATE";
        public const string WorkshopOnlineBookingConfirm = "WORKSHOP_ONLINE_BOOKING_CONFIRM";
        public const string WorkshopOnlineBookingReject = "WORKSHOP_ONLINE_BOOKING_REJECT";
        public const string ProServiceBookingCreate = "PRO_SERVICE_BOOKING_CREATE";

        public const string TaskCreate = "TASK_CREATE";
        public const string TaskComplete = "TASK_COMPLETE";
        public const string TaskDelegate = "TASK_DELEGATE";

        public const string StoreOrderCreate = "STORE_ORDER_CREATE";
        public const string PaintInquiryCreate = "PAINT_INQUIRY_CREATE";
        public const string ChatMessageSend = "CHAT_MESSAGE_SEND";
        public const string CompanyCreate = "COMPANY_CREATE";
        public const string PurchaseRequestCreate = "PURCHASE_REQUEST_CREATE";
        public const string PurchaseRequestFulfill = "PURCHASE_REQUEST_FULFILL";

        private static readonly IReadOnlyDictionary<string, string> Titles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [UserLogin] = "Вход пользователя",
                [UserRegister] = "Регистрация пользователя",
                [ProEmployeeLogin] = "Вход сотрудника Pro",
                [CarSaleCatalogView] = "Просмотр каталога объявлений",
                [CarSaleDetailView] = "Просмотр объявления",
                [CarSaleCreate] = "Создание объявления",
                [CarSaleModerateApprove] = "Одобрение объявления",
                [CarSaleModerateReject] = "Отклонение объявления",
                [WorkshopDetailView] = "Просмотр мастерской",
                [WorkshopMapView] = "Просмотр карты мастерских",
                [WorkshopOnlineBookingCreate] = "Онлайн-запись на сервис",
                [WorkshopOnlineBookingConfirm] = "Подтверждение онлайн-записи",
                [WorkshopOnlineBookingReject] = "Отклонение онлайн-записи",
                [ProServiceBookingCreate] = "Запись на сервис (Pro)",
                [TaskCreate] = "Создание задания",
                [TaskComplete] = "Завершение задания",
                [TaskDelegate] = "Делегирование задания",
                [StoreOrderCreate] = "Заказ в магазине",
                [PaintInquiryCreate] = "Заявка на покраску",
                [ChatMessageSend] = "Сообщение в чате",
                [CompanyCreate] = "Создание компании",
                [PurchaseRequestCreate] = "Заявка на закупку",
                [PurchaseRequestFulfill] = "Выполнение закупки"
            };

        public static string GetTitle(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return "—";
            return Titles.TryGetValue(code.Trim(), out var title) ? title : code.Trim();
        }
    }

    public static class ActivityActorKind
    {
        public const byte User = 0;
        public const byte Employee = 1;
        public const byte System = 2;
    }
}
