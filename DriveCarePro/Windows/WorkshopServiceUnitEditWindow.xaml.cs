using DriveCarePro.Services.WorkshopServices;
using System;
using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class WorkshopServiceUnitEditWindow : Window
    {
        private readonly WorkshopServiceUnitItem _item;
        private readonly bool _isNew;

        public WorkshopServiceUnitEditWindow(WorkshopServiceUnitItem item, bool isNew)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _isNew = isNew;
            InitializeComponent();
            HeaderText.Text = isNew ? "Новая единица" : "Редактирование единицы";
            NameBox.Text = item.Name ?? string.Empty;
            ActiveCheck.IsChecked = item.IsActive;
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
                MessageBox.Show("Укажите обозначение единицы.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _item.Name = NameBox.Text.Trim();
            _item.IsActive = ActiveCheck.IsChecked == true;

            var (ok, error) = await WorkshopServiceUnitsService.SaveAsync(_item, _isNew).ConfigureAwait(true);
            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось сохранить.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
