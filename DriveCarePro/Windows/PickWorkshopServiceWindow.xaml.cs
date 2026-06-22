using DriveCarePro.Services.WorkshopServices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace DriveCarePro.Windows
{
    public partial class PickWorkshopServiceWindow : Window
    {
        private readonly ICollectionView _view;

        public WorkshopServiceItem SelectedItem { get; private set; }

        public PickWorkshopServiceWindow(IEnumerable<WorkshopServiceItem> items)
        {
            InitializeComponent();

            var list = (items ?? Enumerable.Empty<WorkshopServiceItem>())
                .Where(i => i != null && !string.IsNullOrWhiteSpace(i.Name))
                .OrderBy(i => i.Name)
                .ToList();

            _view = CollectionViewSource.GetDefaultView(list);
            _view.Filter = FilterItem;
            ItemsGrid.ItemsSource = _view;

            SearchBox.TextChanged += (_, __) => _view.Refresh();
            SearchBox.KeyDown += SearchBox_KeyDown;
            Loaded += (_, __) => SearchBox.Focus();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmSelection();
                e.Handled = true;
            }
        }

        private bool FilterItem(object obj)
        {
            if (!(obj is WorkshopServiceItem item))
                return false;

            var query = (SearchBox?.Text ?? string.Empty).Trim();
            if (query.Length == 0)
                return true;

            return (item.Name?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (item.Description?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        private void ItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ConfirmSelection();

        private void Confirm_Click(object sender, RoutedEventArgs e) => ConfirmSelection();

        private void ConfirmSelection()
        {
            if (!(ItemsGrid.SelectedItem is WorkshopServiceItem item))
            {
                MessageBox.Show("Выберите услугу из списка.", Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedItem = item;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
