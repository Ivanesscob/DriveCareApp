using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace DriveCareCore.Shop
{
    public static class StoreOrderService
    {
        public static bool TablesExist()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.StoreOrders', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static (bool ok, string error, Guid orderId, string orderNumber) TryCreateOrder(
            Guid userId,
            Guid pickupPointId,
            decimal totalAmount,
            IReadOnlyList<StoreOrderLineInput> lines)
        {
            if (!TablesExist())
                return (false, "Таблицы заказов не найдены. Выполните OrderPickupPoints_Tables.sql на сервере.", Guid.Empty, null);
            if (userId == Guid.Empty)
                return (false, "Войдите в аккаунт.", Guid.Empty, null);
            if (pickupPointId == Guid.Empty)
                return (false, "Выберите пункт выдачи на карте.", Guid.Empty, null);
            if (lines == null || lines.Count == 0)
                return (false, "Корзина пуста.", Guid.Empty, null);

            var orderId = Guid.NewGuid();
            var orderNumber = BuildOrderNumber();
            var qrPayload = $"DRIVECARE|{orderNumber}|{totalAmount:0}|{pickupPointId:N}";
            var statusAwaiting = (byte)StoreOrderStatus.AwaitingPayment;

            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    db.Database.ExecuteSqlCommand(@"
INSERT INTO dbo.StoreOrders (RowId, UserId, PickupPointId, OrderNumber, Status, TotalAmount, QrPayload, CreatedAt)
VALUES (@p_id, @p_uid, @p_pp, @p_num, @p_status, @p_tot, @p_qr, GETDATE());",
                        new SqlParameter("@p_id", SqlDbType.UniqueIdentifier) { Value = orderId },
                        new SqlParameter("@p_uid", SqlDbType.UniqueIdentifier) { Value = userId },
                        new SqlParameter("@p_pp", SqlDbType.UniqueIdentifier) { Value = pickupPointId },
                        new SqlParameter("@p_num", SqlDbType.NVarChar, 32) { Value = orderNumber },
                        new SqlParameter("@p_status", SqlDbType.TinyInt) { Value = statusAwaiting },
                        new SqlParameter("@p_tot", SqlDbType.Decimal) { Value = totalAmount, Precision = 18, Scale = 2 },
                        new SqlParameter("@p_qr", SqlDbType.NVarChar, 500) { Value = qrPayload });

                    var sort = 0;
                    foreach (var line in lines)
                    {
                        db.Database.ExecuteSqlCommand(@"
INSERT INTO dbo.StoreOrderLines (RowId, OrderId, ProductId, ProductName, Category, Quantity, UnitPrice, SortOrder)
VALUES (@p_lid, @p_oid, @p_pid, @p_name, @p_cat, @p_qty, @p_price, @p_sort);",
                            new SqlParameter("@p_lid", SqlDbType.UniqueIdentifier) { Value = Guid.NewGuid() },
                            new SqlParameter("@p_oid", SqlDbType.UniqueIdentifier) { Value = orderId },
                            new SqlParameter("@p_pid", SqlDbType.UniqueIdentifier) { Value = line.ProductId },
                            new SqlParameter("@p_name", SqlDbType.NVarChar, 200) { Value = line.ProductName ?? string.Empty },
                            new SqlParameter("@p_cat", SqlDbType.NVarChar, 40) { Value = (object)line.Category ?? DBNull.Value },
                            new SqlParameter("@p_qty", SqlDbType.Int) { Value = line.Quantity },
                            new SqlParameter("@p_price", SqlDbType.Decimal) { Value = line.UnitPrice, Precision = 18, Scale = 2 },
                            new SqlParameter("@p_sort", SqlDbType.Int) { Value = sort++ });
                    }
                }

                return (true, null, orderId, orderNumber);
            }
            catch (Exception ex)
            {
                return (false, FormatSqlError(ex), Guid.Empty, null);
            }
        }

        public static (bool ok, string error) TryMarkPaid(Guid orderId, Guid userId)
        {
            if (!TablesExist() || orderId == Guid.Empty || userId == Guid.Empty)
                return (false, "Некорректный заказ.");

            try
            {
                var statusPaid = (byte)StoreOrderStatus.Paid;
                var statusAwaiting = (byte)StoreOrderStatus.AwaitingPayment;
                var rows = AppConnect.model1.Database.ExecuteSqlCommand(@"
UPDATE dbo.StoreOrders
SET Status = @p_newStatus, PaidAt = GETDATE()
WHERE RowId = @p_id AND UserId = @p_uid AND Status = @p_oldStatus;",
                    new SqlParameter("@p_newStatus", SqlDbType.TinyInt) { Value = statusPaid },
                    new SqlParameter("@p_id", SqlDbType.UniqueIdentifier) { Value = orderId },
                    new SqlParameter("@p_uid", SqlDbType.UniqueIdentifier) { Value = userId },
                    new SqlParameter("@p_oldStatus", SqlDbType.TinyInt) { Value = statusAwaiting });

                return rows > 0 ? (true, null) : (false, "Заказ не найден или уже оплачен.");
            }
            catch (Exception ex)
            {
                return (false, FormatSqlError(ex));
            }
        }

        public static IReadOnlyList<StoreOrderListItem> LoadForUser(Guid userId)
        {
            if (!TablesExist() || userId == Guid.Empty)
                return Array.Empty<StoreOrderListItem>();

            try
            {
                const string sql = @"
SELECT o.RowId, o.OrderNumber, o.Status, o.TotalAmount, o.CreatedAt, o.PaidAt, o.QrPayload,
       p.Name AS PickupName,
       CASE WHEN p.City IS NULL OR p.City = N'' THEN p.AddressLine ELSE p.City + N', ' + p.AddressLine END AS PickupAddress
FROM dbo.StoreOrders o
INNER JOIN dbo.OrderPickupPoints p ON p.RowId = o.PickupPointId
WHERE o.UserId = @p_uid
ORDER BY o.CreatedAt DESC;";

                return AppConnect.model1.Database.SqlQuery<OrderListSqlRow>(sql,
                        new SqlParameter("@p_uid", SqlDbType.UniqueIdentifier) { Value = userId })
                    .Select(r => new StoreOrderListItem
                    {
                        RowId = r.RowId,
                        OrderNumber = r.OrderNumber,
                        Status = r.Status,
                        TotalAmount = r.TotalAmount,
                        CreatedAt = r.CreatedAt,
                        PaidAt = r.PaidAt,
                        PickupName = r.PickupName,
                        PickupAddress = r.PickupAddress,
                        QrPayload = r.QrPayload
                    })
                    .ToList();
            }
            catch
            {
                return Array.Empty<StoreOrderListItem>();
            }
        }

        public static StoreOrderDetail LoadDetail(Guid orderId, Guid userId)
        {
            var list = LoadForUser(userId);
            var header = list.FirstOrDefault(o => o.RowId == orderId);
            if (header == null)
                return null;

            var lines = Array.Empty<StoreOrderLineVm>();
            try
            {
                const string sql = @"
SELECT ProductName, Quantity, UnitPrice
FROM dbo.StoreOrderLines WHERE OrderId = @p_oid ORDER BY SortOrder;";
                lines = AppConnect.model1.Database.SqlQuery<LineSqlRow>(sql,
                        new SqlParameter("@p_oid", SqlDbType.UniqueIdentifier) { Value = orderId })
                    .Select(l => new StoreOrderLineVm
                    {
                        ProductName = l.ProductName,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice
                    })
                    .ToArray();
            }
            catch
            {
            }

            return new StoreOrderDetail { Header = header, Lines = lines };
        }

        static string BuildOrderNumber()
        {
            return "DC-" + DateTime.Now.ToString("yyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        }

        static string FormatSqlError(Exception ex)
        {
            var msg = ex?.Message ?? "Ошибка базы данных.";
            if (msg.IndexOf("StoreOrders", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("Invalid object name", StringComparison.OrdinalIgnoreCase) >= 0)
                return msg + "\n\nВыполните на сервере: OrderPickupPoints_Tables.sql и OrderPickupPoints_SpbSeed.sql";
            return msg;
        }

        sealed class OrderListSqlRow
        {
            public Guid RowId { get; set; }
            public string OrderNumber { get; set; }
            public byte Status { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? PaidAt { get; set; }
            public string QrPayload { get; set; }
            public string PickupName { get; set; }
            public string PickupAddress { get; set; }
        }

        sealed class LineSqlRow
        {
            public string ProductName { get; set; }
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
        }
    }
}
