using DriveCare.Services;
using DriveCareCore.Painting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DriveCare.Windows
{
    public partial class PaintJobWindow : Window
    {
        private readonly CarPaintKind _kind;

        public Guid? SelectedColorId { get; private set; }
        public string CustomColorName { get; private set; }
        public string PartName { get; private set; }
        public string Notes { get; private set; }

        public PaintJobWindow(CarPaintKind kind, IEnumerable<CarPaintColorOption> colors)
        {
            InitializeComponent();
            _kind = kind;
            Title = CarPaintService.GetKindTitle(kind);
            TitleText.Text = Title;
            HeaderIcon.Kind = kind == CarPaintKind.Wheels
                ? MahApps.Metro.IconPacks.PackIconModernKind.Ring
                : kind == CarPaintKind.Part
                    ? MahApps.Metro.IconPacks.PackIconModernKind.Tools
                    : MahApps.Metro.IconPacks.PackIconModernKind.TransitCar;
            HintText.Text = kind == CarPaintKind.Part
                ? "Укажите деталь и цвет покраски."
                : "Выберите цвет из списка или введите свой.";

            if (kind == CarPaintKind.Part)
                PartPanel.Visibility = Visibility.Visible;

            var list = (colors ?? Enumerable.Empty<CarPaintColorOption>()).ToList();
            ColorCombo.ItemsSource = list;
            if (list.Count > 0)
                ColorCombo.SelectedIndex = 0;

            UpdateColorPreview();
        }

        private void ColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(CustomColorBox.Text))
                return;
            UpdateColorPreview();
        }

        private void CustomColorBox_TextChanged(object sender, TextChangedEventArgs e) =>
            UpdateColorPreview();

        private void UpdateColorPreview()
        {
            var name = GetEffectiveColorName();
            var color = PaintColorBrushHelper.ResolveColor(name);
            ColorPreviewInner.Background = PaintColorBrushHelper.BrushFromColorName(name);
            ColorPreviewInner.BorderBrush = PaintColorBrushHelper.ContrastBorderBrushFor(color);
            ColorPreviewOuter.BorderBrush = PaintColorBrushHelper.ContrastBorderBrushFor(color);
            PreviewColorNameText.Text = string.IsNullOrWhiteSpace(name) ? "—" : name;
        }

        private string GetEffectiveColorName()
        {
            var custom = (CustomColorBox.Text ?? string.Empty).Trim();
            if (custom.Length > 0)
                return custom;

            var selected = ColorCombo.SelectedItem as CarPaintColorOption;
            return selected?.Name?.Trim() ?? string.Empty;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (_kind == CarPaintKind.Part)
            {
                var part = (PartNameBox.Text ?? string.Empty).Trim();
                if (part.Length == 0)
                {
                    ShowError("Укажите название детали.");
                    return;
                }

                PartName = part;
            }

            var custom = (CustomColorBox.Text ?? string.Empty).Trim();
            var selected = ColorCombo.SelectedItem as CarPaintColorOption;
            if (custom.Length > 0)
            {
                SelectedColorId = null;
                CustomColorName = custom;
            }
            else if (selected != null && selected.ColorId != Guid.Empty)
            {
                SelectedColorId = selected.ColorId;
                CustomColorName = selected.Name;
            }
            else
            {
                ShowError("Выберите цвет из списка или введите название.");
                return;
            }

            Notes = (NotesBox.Text ?? string.Empty).Trim();
            DialogResult = true;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
