using DriveCarePro.Services;
using DriveCarePro.Services.WorkshopServices;
using DriveCarePro.Windows;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Pages
{
    public partial class ProTaskShopPage : Page, INotifyPropertyChanged
    {
        private readonly Guid _taskId;
        private readonly Guid _workshopId;
        private string _activeCategory = "Engine";
        private decimal _cartTotal;

        private readonly ObservableCollection<WorkshopPartItem> _products = new ObservableCollection<WorkshopPartItem>();
        private readonly ObservableCollection<ProShopCartItemVm> _cart = new ObservableCollection<ProShopCartItemVm>();

        public ProTaskShopPage(Guid taskId, Guid workshopId)
        {
            _taskId = taskId;
            _workshopId = workshopId;
            InitializeComponent();
            DataContext = this;
            ProductsList.ItemsSource = _products;
            CartList.ItemsSource = _cart;
            Loaded += ProTaskShopPage_Loaded;
            _cart.CollectionChanged += (_, __) => RefreshCartUi();
        }

        private async void ProTaskShopPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ProTaskShopPage_Loaded;
            await LoadCategoryAsync(_activeCategory).ConfigureAwait(true);
        }

        private async Task LoadCategoryAsync(string category)
        {
            try
            {
                var items = await WorkshopPartCatalogService.ListShopForWorkshopAsync(_workshopId, category)
                    .ConfigureAwait(true);
                _products.Clear();
                foreach (var item in items)
                    _products.Add(item);

                if (_products.Count == 0)
                    ShopHintText.Text = "В этой категории нет позиций.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить каталог: " + ex.Message, "Магазин",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new EmployeeTaskCardPage(_taskId));

        private async void Category_Click(object sender, RoutedEventArgs e)
        {
            var category = (sender as Button)?.Tag as string;
            if (string.IsNullOrWhiteSpace(category))
                return;
            _activeCategory = category;
            await LoadCategoryAsync(_activeCategory).ConfigureAwait(true);
        }

        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            var part = (sender as FrameworkElement)?.Tag as WorkshopPartItem;
            if (part == null)
                return;

            var item = _cart.FirstOrDefault(c => c.PartId == part.RowId);
            if (item == null)
            {
                _cart.Add(new ProShopCartItemVm
                {
                    PartId = part.RowId,
                    Name = part.Name,
                    UnitName = part.UnitName,
                    Price = part.Price,
                    Quantity = 1
                });
            }
            else
            {
                item.Quantity++;
            }
            RefreshCartUi();
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.Tag as ProShopCartItemVm;
            if (item == null)
                return;
            item.Quantity--;
            if (item.Quantity <= 0)
                _cart.Remove(item);
            RefreshCartUi();
        }

        private async void RequestPurchase_Click(object sender, RoutedEventArgs e)
        {
            if (_cart.Count == 0)
            {
                MessageBox.Show("Добавьте позиции в корзину.", "Магазин", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var emp = AppState.CurrentEmployee;
            if (emp == null)
                return;

            var purchasers = await TaskPurchaseRequestService.ListAuthorizedPurchasersAsync(emp.RowId)
                .ConfigureAwait(true);
            if (purchasers.Count == 0)
            {
                MessageBox.Show(
                    "Нет уполномоченных сотрудников на закупку.\n\n" +
                    "Назначьте роль с правом PURCHASE_PARTS или роль «закуп» / «снабжение» / владелец.",
                    "Магазин", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new RequestPurchaseWindow(purchasers) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true || win.SelectedEmployee == null)
                return;

            var lines = _cart.Select(c => new TaskPartLineRow
            {
                WorkshopPartId = c.PartId,
                PartName = c.Name,
                Quantity = c.Quantity,
                UnitName = c.UnitName ?? "шт.",
                UnitPrice = c.Price
            }).ToList();

            var (ok, error, _) = await TaskPurchaseRequestService.CreatePurchaseRequestAsync(
                _taskId, emp.RowId, win.SelectedEmployee.EmployeeId, lines).ConfigureAwait(true);

            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось создать запрос.", "Магазин",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                "Запрос на покупку отправлен " + win.SelectedEmployee.DisplayName + ".\n" +
                "После завершения закупки детали появятся в отчёте вашего задания.",
                "Магазин", MessageBoxButton.OK, MessageBoxImage.Information);

            _cart.Clear();
            RefreshCartUi();
            AppState.Navigate(new EmployeeTaskCardPage(_taskId));
        }

        private void RefreshCartUi()
        {
            _cartTotal = _cart.Sum(i => i.LineTotal);
            CartTitleText.Text = $"Корзина ({_cart.Sum(i => i.Quantity)} шт.)";
            CartTotalText.Text = $"Итого: {_cartTotal:N2} ₽";
            OnPropertyChanged(nameof(CartTotalText));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string prop = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public sealed class ProShopCartItemVm : INotifyPropertyChanged
    {
        public Guid PartId { get; set; }
        public string Name { get; set; }
        public string UnitName { get; set; }
        public decimal Price { get; set; }

        private int _quantity = 1;

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value < 1 ? 1 : value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LineTotal)));
            }
        }

        public decimal LineTotal => Price * Quantity;
        public string DisplayText => $"{Name} × {Quantity} — {LineTotal:N2} ₽";

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
