using DriveCareCore.Painting;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Windows
{
    public partial class WorkshopPaintServiceEditWindow : Window
    {
        public CarPaintKind PaintKind { get; private set; }
        public string ServiceName { get; private set; }
        public string Description { get; private set; }
        public decimal? PriceFrom { get; private set; }

        public WorkshopPaintServiceEditWindow()
        {
            InitializeComponent();
            KindCombo.Items.Add(new KindItem(CarPaintKind.Wheels, "Покраска дисков"));
            KindCombo.Items.Add(new KindItem(CarPaintKind.FullCar, "Покраска всей машины"));
            KindCombo.Items.Add(new KindItem(CarPaintKind.Part, "Перекраска детали"));
            KindCombo.DisplayMemberPath = "Title";
            KindCombo.SelectedIndex = 1;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!(KindCombo.SelectedItem is KindItem kind))
            {
                MessageBox.Show("Выберите тип работы.", "Услуга",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var name = (NameBox.Text ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                MessageBox.Show("Введите название услуги.", "Услуга",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            decimal? price = null;
            var raw = (PriceBox.Text ?? string.Empty).Trim().Replace(" ", "");
            if (raw.Length > 0)
            {
                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) &&
                    !decimal.TryParse(raw, NumberStyles.Number, CultureInfo.GetCultureInfo("ru-RU"), out p))
                {
                    MessageBox.Show("Некорректная цена.", "Услуга",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                price = p;
            }

            PaintKind = kind.Kind;
            ServiceName = name;
            Description = (DescriptionBox.Text ?? string.Empty).Trim();
            PriceFrom = price;
            DialogResult = true;
            Close();
        }

        private sealed class KindItem
        {
            public KindItem(CarPaintKind kind, string title)
            {
                Kind = kind;
                Title = title;
            }

            public CarPaintKind Kind { get; }
            public string Title { get; }
        }
    }
}
