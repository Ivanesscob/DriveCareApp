namespace DriveCareCore.Shop
{
    public enum StoreOrderStatus : byte
    {
        AwaitingPayment = 0,
        Paid = 1,
        ReadyForPickup = 2,
        Completed = 3,
        Cancelled = 4
    }

    public static class StoreOrderStatusLabels
    {
        public static string Get(byte status)
        {
            switch ((StoreOrderStatus)status)
            {
                case StoreOrderStatus.AwaitingPayment: return "Ожидает оплаты";
                case StoreOrderStatus.Paid: return "Оплачен";
                case StoreOrderStatus.ReadyForPickup: return "Готов к выдаче";
                case StoreOrderStatus.Completed: return "Выдан";
                case StoreOrderStatus.Cancelled: return "Отменён";
                default: return "—";
            }
        }
    }
}
