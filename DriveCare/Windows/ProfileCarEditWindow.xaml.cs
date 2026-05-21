using DriveCare.Services;
using DriveCareCore.Data.BD;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Windows
{
    public partial class ProfileCarEditWindow : Window
    {
        private readonly Guid _userId;
        private readonly UserGarageCarItem _editItem;

        public ProfileCarEditWindow(Guid userId, UserGarageCarItem editItem = null)
        {
            _userId = userId;
            _editItem = editItem;
            InitializeComponent();
            TitleText.Text = editItem == null ? "Добавить автомобиль" : "Редактировать автомобиль";
            Title = TitleText.Text;
            Loaded += ProfileCarEditWindow_Loaded;
        }

        private void ProfileCarEditWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ProfileCarEditWindow_Loaded;
            BrandCombo.ItemsSource = UserGarageService.LoadBrands();
            CarTypeCombo.ItemsSource = UserGarageService.LoadCarTypes();
            FuelTypeCombo.ItemsSource = UserGarageService.LoadFuelTypes();

            if (_editItem == null)
            {
                if (BrandCombo.Items.Count > 0)
                    BrandCombo.SelectedIndex = 0;
                if (CarTypeCombo.Items.Count > 0)
                    CarTypeCombo.SelectedIndex = 0;
                if (FuelTypeCombo.Items.Count > 0)
                    FuelTypeCombo.SelectedIndex = 0;
                return;
            }

            YearBox.Text = _editItem.Year?.ToString() ?? string.Empty;
            PlateBox.Text = _editItem.PlateNumber ?? string.Empty;
            VinBox.Text = _editItem.Vin ?? string.Empty;
            NoteBox.Text = _editItem.UserCarNote ?? string.Empty;

            SelectComboById(BrandCombo, ResolveBrandId(_editItem.ModelId));
            BrandCombo_SelectionChanged(null, null);
            SelectComboById(ModelCombo, _editItem.ModelId);
            SelectComboById(CarTypeCombo, _editItem.CarTypeId);
            SelectComboById(FuelTypeCombo, _editItem.FuelTypeId);
        }

        private static Guid? ResolveBrandId(Guid modelId)
        {
            if (modelId == Guid.Empty)
                return null;
            try
            {
                return AppConnect.model1.Models
                    .Where(m => m.RowId == modelId)
                    .Select(m => (Guid?)m.BrandId)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private void BrandCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var brand = BrandCombo.SelectedItem as CatalogEntry;
            ModelCombo.ItemsSource = UserGarageService.LoadModels(brand?.Id);
            if (ModelCombo.Items.Count > 0 && ModelCombo.SelectedItem == null)
                ModelCombo.SelectedIndex = 0;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var model = ModelCombo.SelectedItem as CatalogEntry;
            var carType = CarTypeCombo.SelectedItem as CatalogEntry;
            var fuel = FuelTypeCombo.SelectedItem as CatalogEntry;

            if (model == null || carType == null || fuel == null)
            {
                MessageBox.Show("Заполните марку, модель, тип кузова и топливо.", Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int? year = null;
            if (int.TryParse((YearBox.Text ?? string.Empty).Trim(), out var y) && y >= 1950 && y <= DateTime.Today.Year + 1)
                year = y;

            var input = new UserGarageSaveInput
            {
                UserCarId = _editItem?.UserCarId,
                UserId = _userId,
                ModelId = model.Id,
                CarTypeId = carType.Id,
                FuelTypeId = fuel.Id,
                Year = year,
                Vin = VinBox.Text,
                PlateNumber = PlateBox.Text,
                UserCarNote = NoteBox.Text
            };

            var (ok, error, _) = UserGarageService.Save(input);
            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось сохранить.", Title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private static void SelectComboById(ComboBox combo, Guid? id)
        {
            if (!id.HasValue || id.Value == Guid.Empty)
                return;
            foreach (CatalogEntry item in combo.Items)
            {
                if (item.Id == id.Value)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
        }
    }
}
