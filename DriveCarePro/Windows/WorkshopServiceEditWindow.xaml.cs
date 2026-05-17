using DriveCarePro.Services.WorkshopServices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Windows
{
    public partial class WorkshopServiceEditWindow : Window
    {
        private readonly WorkshopServiceItem _item;
        private readonly bool _isNew;
        private readonly Guid _workshopId;
        private List<WorkshopServiceUnitItem> _units = new List<WorkshopServiceUnitItem>();

        public WorkshopServiceEditWindow(WorkshopServiceItem item, bool isNew, Guid workshopId)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _isNew = isNew;
            _workshopId = workshopId;
            InitializeComponent();
            HeaderText.Text = isNew ? "Новая услуга" : "Редактирование услуги";
            NameBox.Text = item.Name ?? string.Empty;
            DescriptionBox.Text = item.Description ?? string.Empty;
            PriceBox.Text = item.Price.ToString("0.00", CultureInfo.InvariantCulture);
            ActiveCheck.IsChecked = item.IsActive;
            DescriptionBox.TextChanged += DescriptionBox_TextChanged;
            UpdateDescriptionCounter();
            Loaded += WorkshopServiceEditWindow_Loaded;
        }

        private async void WorkshopServiceEditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= WorkshopServiceEditWindow_Loaded;
            _units = await WorkshopServiceUnitsService.ListForWorkshopAsync(_workshopId).ConfigureAwait(true);
            UnitCombo.ItemsSource = _units;

            if (_item.UnitId.HasValue && _units.Any(u => u.RowId == _item.UnitId.Value))
                UnitCombo.SelectedValue = _item.UnitId.Value;
            else if (!string.IsNullOrWhiteSpace(_item.UnitName))
                UnitCombo.SelectedItem = _units.FirstOrDefault(u => string.Equals(u.Name, _item.UnitName, StringComparison.OrdinalIgnoreCase));
            else if (_units.Count > 0)
                UnitCombo.SelectedIndex = 0;
        }

        public WorkshopServiceItem SavedItem { get; private set; }

        private void DescriptionBox_TextChanged(object sender, TextChangedEventArgs e) =>
            UpdateDescriptionCounter();

        private void UpdateDescriptionCounter()
        {
            var len = (DescriptionBox.Text ?? string.Empty).Length;
            DescriptionCounter.Text = $"{len} / {WorkshopServiceItem.MaxDescriptionLength}";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("Укажите название услуги.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse((PriceBox.Text ?? string.Empty).Replace(',', '.'),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out var price) || price < 0)
            {
                MessageBox.Show("Укажите корректную цену.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!(UnitCombo.SelectedItem is WorkshopServiceUnitItem unit))
            {
                MessageBox.Show("Выберите единицу измерения.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _item.Name = NameBox.Text.Trim();
            _item.Description = DescriptionBox.Text?.Trim();
            _item.Price = price;
            _item.UnitId = unit.RowId;
            _item.UnitName = unit.Name;
            _item.IsActive = ActiveCheck.IsChecked == true;

            var (ok, error) = await WorkshopServiceCatalogService.SaveAsync(_item, _isNew).ConfigureAwait(true);
            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось сохранить.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SavedItem = _item;
            DialogResult = true;
            Close();
        }
    }
}
