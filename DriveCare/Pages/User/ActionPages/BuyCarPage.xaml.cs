using DriveCareCore.Data.BD;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class BuyCarPage : Page
    {
        public ObservableCollection<BuyCarSaleItemVm> SalesItems { get; } = new ObservableCollection<BuyCarSaleItemVm>();

        public BuyCarPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += (_, __) => LoadSales();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<UserHomePage>();
        }

        private void LoadSales()
        {
            SalesItems.Clear();
            try
            {
                const string sql = @"
SELECT
    b.Name AS BrandName,
    m.Name AS ModelName,
    clr.Name AS ColorName,
    ISNULL(lastPrice.Price, 0) AS LastPrice,
    cs.PhotoPath
FROM CarSales cs
INNER JOIN Cars c ON c.RowId = cs.CarId
LEFT JOIN Models m ON m.RowId = c.ModelId
LEFT JOIN Brands b ON b.RowId = m.BrandId

OUTER APPLY (
    SELECT TOP 1 col.Name
    FROM CarColors cc
    LEFT JOIN Colors col ON col.RowId = cc.ColorId
    WHERE cc.CarId = c.RowId
    ORDER BY cc.StartDate DESC
) clr

OUTER APPLY (
    SELECT TOP 1 p.Price
    FROM CarSalePrices p
    WHERE p.CarSaleId = cs.RowId
      AND p.EndDate IS NULL
    ORDER BY p.StartDate DESC
) lastPrice;";

                var rows = AppConnect.model1.Database.SqlQuery<BuyCarSqlRow>(sql).ToList();
                foreach (var row in rows)
                {
                    var brand = Safe(row.BrandName, "Марка");
                    var model = Safe(row.ModelName, "Модель");
                    var color = Safe(row.ColorName, "Без цвета");

                    SalesItems.Add(new BuyCarSaleItemVm
                    {
                        CarLabel = $"{brand} {model} · {color}",
                        PriceLabel = $"{row.LastPrice:0} ₽",
                        SellerLabel = "Продавец: плашка (временное поле)"
                    });
                }
            }
            catch
            {
                // Если таблицы/поля пока не совпадают с БД, просто оставляем пустой список.
            }
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private void SaleDetails_Click(object sender, RoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            var item = fe?.DataContext as BuyCarSaleItemVm;
            OpenSaleDetails(item);
        }

        private void SaleCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            var fe = sender as FrameworkElement;
            var item = fe?.DataContext as BuyCarSaleItemVm;
            OpenSaleDetails(item);
            e.Handled = true;
        }

        private void OpenSaleDetails(BuyCarSaleItemVm item)
        {
            // Пока пустой общий метод для кнопки "Подробнее" и двойного клика по карточке.
        }
    }

    public sealed class BuyCarSaleItemVm
    {
        public string CarLabel { get; set; }
        public string PriceLabel { get; set; }
        public string SellerLabel { get; set; }
    }

    internal sealed class BuyCarSqlRow
    {
        public string BrandName { get; set; }
        public string ModelName { get; set; }
        public string ColorName { get; set; }
        public decimal LastPrice { get; set; }
    }
}

