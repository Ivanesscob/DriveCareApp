using System;
using System.Globalization;
using System.Windows;

namespace DriveCare.Windows
{
    public partial class EnterRealMileageWindow : Window
    {
        private readonly int _minimumKm;

        public int? EnteredMileageKm { get; private set; }

        public EnterRealMileageWindow(int minimumKm)
        {
            InitializeComponent();
            _minimumKm = Math.Max(0, minimumKm);
            HintText.Text = _minimumKm > 0
                ? $"Не меньше {_minimumKm.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"))} км (последний пробег или примерный ориентир)."
                : "Укажите текущий пробег по одометру.";
            if (_minimumKm > 0)
                MileageBox.Text = _minimumKm.ToString(CultureInfo.InvariantCulture);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var raw = (MileageBox.Text ?? string.Empty).Trim().Replace(" ", "").Replace("\u00A0", "");
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var km) &&
                !int.TryParse(raw, NumberStyles.Integer, CultureInfo.GetCultureInfo("ru-RU"), out km))
            {
                ShowError("Введите целое число километров.");
                return;
            }

            if (km < _minimumKm)
            {
                ShowError($"Пробег не может быть меньше {_minimumKm:N0} км.");
                return;
            }

            if (km > 9_999_999)
            {
                ShowError("Слишком большое значение.");
                return;
            }

            EnteredMileageKm = km;
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
