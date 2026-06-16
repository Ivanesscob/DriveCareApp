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
        private readonly Guid? _taskId;
        private readonly Guid _workshopId;
        private readonly bool _fromTask;
        private string _activeCategory = "Engine";
        private decimal _cartTotal;

        private readonly ObservableCollection<WorkshopPartItem> _products = new ObservableCollection<WorkshopPartItem>();
        private readonly ObservableCollection<ProShopCartItemVm> _cart = new ObservableCollection<ProShopCartItemVm>();

        /// <summary>Каталог для закупщика с главной (без привязки к заданию).</summary>
        public ProTaskShopPage(Guid workshopId)
            : this(null, workshopId)
        {
        }

        public ProTaskShopPage(Guid taskId, Guid workshopId)
            : this((Guid?)taskId, workshopId)
        {
        }

        private ProTaskShopPage(Guid? taskId, Guid workshopId)
        {
            _taskId = taskId;
            _workshopId = workshopId;
            _fromTask = taskId.HasValue && taskId.Value != Guid.Empty;
            InitializeComponent();
            DataContext = this;
            ProductsList.ItemsSource = _products;
            CartList.ItemsSource = _cart;
            Loaded += ProTaskShopPage_Loaded;
            _cart.CollectionChanged += (_, __) => RefreshCartUi();

            if (_fromTask)
            {
                ShopHintText.Text =
                    "Каталог запчастей для задания. Добавьте позиции в корзину и отправьте запрос закупщику.";
                PurchaseButton.Content = "Запросить покупку";
                BackButton.Content = "← Назад к заданию";
            }
            else
            {
                ShopHintText.Text =
                    "Выберите запчасти и добавьте в корзину. Кнопка «Купить» добавит их на склад вашей мастерской.";
                PurchaseButton.Content = "Купить";
                BackButton.Content = "← На главную";
            }

            RefreshCartUi();
        }

        private async void ProTaskShopPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ProTaskShopPage_Loaded;

            if (!_fromTask && !AppState.CanAccessPurchaserShop)
            {
                MessageBox.Show(
                    "Магазин доступен только сотрудникам с ролью закупки (закуп, снабжение, склад, владелец) или правом PURCHASE_PARTS.",
                    "Магазин",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                AppState.Navigate(new ProHomePage());
                return;
            }

            if (_workshopId == Guid.Empty)
            {
                MessageBox.Show(
                    "Не определена мастерская для склада.\n\nПривяжите сотрудника к мастерской в карточке персонала.",
                    "Магазин",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                AppState.Navigate(_fromTask && _taskId.HasValue
                    ? (Page)new EmployeeTaskCardPage(_taskId.Value)
                    : new ProHomePage());
                return;
            }

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

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_fromTask && _taskId.HasValue)
                AppState.Navigate(new EmployeeTaskCardPage(_taskId.Value));
            else
                AppState.Navigate(new ProHomePage());
        }

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
            e.Handled = true;

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
                    Category = part.Category,
                    Quantity = 1
                });
            }
            else
            {
                item.Quantity++;
            }

            CartPanel.Visibility = Visibility.Visible;
            RefreshCartUi();
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var item = (sender as FrameworkElement)?.Tag as ProShopCartItemVm;
            if (item == null)
                return;
            item.Quantity--;
            if (item.Quantity <= 0)
                _cart.Remove(item);
            RefreshCartUi();
        }

        private async void Purchase_Click(object sender, RoutedEventArgs e)
        {
            if (_cart.Count == 0)
            {
                MessageBox.Show("Добавьте позиции в корзину.", "Магазин", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_fromTask && _taskId.HasValue)
                await RequestPurchaseForTaskAsync().ConfigureAwait(true);
            else
                await BuyToWorkshopStockAsync().ConfigureAwait(true);
        }

        private async Task BuyToWorkshopStockAsync()
        {
            var lines = BuildCartLines();
            var totalQty = lines.Sum(l => l.Quantity);
            var totalSum = _cart.Sum(c => c.LineTotal);

            if (MessageBox.Show(
                    $"Оформить покупку?\n\nПозиций: {lines.Count}, количество: {totalQty:0.###}\nСумма: {totalSum:N2} ₽\n\nЗапчасти будут добавлены на склад мастерской.",
                    "Магазин",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                var (ok, error) = await WorkshopStockService.ReceiveShopCartAsync(_workshopId, lines)
                    .ConfigureAwait(true);

                if (!ok)
                {
                    MessageBox.Show(error ?? "Не удалось добавить запчасти на склад.", "Магазин",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show(
                    $"Покупка оформлена.\n\n{lines.Count} поз. ({totalQty:0.###} шт.) записаны на склад мастерской.",
                    "Магазин",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                _cart.Clear();
                RefreshCartUi();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при покупке: " + ex.Message, "Магазин",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RequestPurchaseForTaskAsync()
        {
            if (!_taskId.HasValue)
                return;

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

            var lines = BuildCartLines();

            var (ok, error, _) = await TaskPurchaseRequestService.CreatePurchaseRequestAsync(
                _taskId.Value, emp.RowId, win.SelectedEmployee.EmployeeId, lines).ConfigureAwait(true);

            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось создать запрос.", "Магазин",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                "Запрос на покупку отправлен " + win.SelectedEmployee.DisplayName + ".\n" +
                "После завершения задания закупщиком детали поступят на склад и в отчёт вашего задания.",
                "Магазин", MessageBoxButton.OK, MessageBoxImage.Information);

            _cart.Clear();
            RefreshCartUi();
            AppState.Navigate(new EmployeeTaskCardPage(_taskId.Value));
        }

        private System.Collections.Generic.List<TaskPartLineRow> BuildCartLines() =>
            _cart.Select(c => new TaskPartLineRow
            {
                WorkshopPartId = c.PartId,
                PartName = c.Name,
                Quantity = c.Quantity,
                UnitName = c.UnitName ?? "шт.",
                UnitPrice = c.Price
            }).ToList();

        private void RefreshCartUi()
        {
            _cartTotal = _cart.Sum(i => i.LineTotal);
            var qty = _cart.Sum(i => i.Quantity);
            CartTitleText.Text = qty > 0 ? $"Корзина ({qty} шт.)" : "Корзина";
            CartTotalText.Text = qty > 0 ? $"Итого: {_cartTotal:N2} ₽" : string.Empty;
            CartTotalText.Visibility = qty > 0 ? Visibility.Visible : Visibility.Collapsed;
            CartList.Visibility = qty > 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyCartText.Visibility = qty > 0 ? Visibility.Collapsed : Visibility.Visible;
            PurchaseButton.IsEnabled = qty > 0;
            CartPanel.Visibility = Visibility.Visible;
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
        public string Category { get; set; }
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
