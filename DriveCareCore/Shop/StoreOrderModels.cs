using System;
using System.Collections.Generic;

namespace DriveCareCore.Shop
{
    public sealed class StoreOrderLineInput
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public sealed class StoreOrderListItem
    {
        public Guid RowId { get; set; }
        public string OrderNumber { get; set; }
        public byte Status { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public string PickupName { get; set; }
        public string PickupAddress { get; set; }
        public string QrPayload { get; set; }

        public string StatusLabel => StoreOrderStatusLabels.Get(Status);
        public string TotalLabel => $"{TotalAmount:0} ₽";
        public string DateLabel => CreatedAt.ToString("dd.MM.yyyy HH:mm");
        public bool CanPay => Status == (byte)StoreOrderStatus.AwaitingPayment;
        public bool IsPaid => Status >= (byte)StoreOrderStatus.Paid && Status != (byte)StoreOrderStatus.Cancelled;
    }

    public sealed class StoreOrderDetail
    {
        public StoreOrderListItem Header { get; set; }
        public IReadOnlyList<StoreOrderLineVm> Lines { get; set; } = Array.Empty<StoreOrderLineVm>();
    }

    public sealed class StoreOrderLineVm
    {
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
        public string Display => $"{ProductName} × {Quantity} — {LineTotal:0} ₽";
    }
}
