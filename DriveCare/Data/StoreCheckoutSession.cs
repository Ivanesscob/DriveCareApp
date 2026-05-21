using DriveCare.Pages.User.ActionPages;
using DriveCareCore.Shop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DriveCare
{
    /// <summary>Данные корзины для экрана оформления (после «Оформить заказ»).</summary>
    public static class StoreCheckoutSession
    {
        public static IReadOnlyList<StoreOrderLineInput> Lines { get; private set; } = Array.Empty<StoreOrderLineInput>();
        public static decimal Total { get; private set; }

        public static bool HasItems => Lines != null && Lines.Count > 0;

        public static void SetFromCart()
        {
            Lines = StoreCartService.Items.Select(i => new StoreOrderLineInput
            {
                ProductId = i.ProductId,
                ProductName = i.Name,
                Category = i.Category,
                Quantity = i.Quantity,
                UnitPrice = i.Price
            }).ToList();
            Total = StoreCartService.Items.Sum(i => i.LineTotal);
        }

        public static void Clear()
        {
            Lines = Array.Empty<StoreOrderLineInput>();
            Total = 0;
        }

        public static string BuildQrPayload(string orderNumber, Guid pickupPointId)
        {
            return $"DRIVECARE|{orderNumber ?? "PENDING"}|{Total:0}|{pickupPointId:N}";
        }
    }
}
