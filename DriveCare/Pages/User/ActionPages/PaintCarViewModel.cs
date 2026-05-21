using DriveCare;
using DriveCare.Data;
using DriveCare.Services;
using DriveCareCore.Data.BD;
using DriveCareCore.Services;
using DriveCareCore.Painting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace DriveCare.Pages.User.ActionPages
{
    public sealed class PaintCarViewModel : INotifyPropertyChanged
    {
        private CarDisplayItem _selectedCar;
        private string _currentColorText = "—";
        private string _currentColorSinceText = string.Empty;
        private ImageSource _centerCarImage;
        private Brush _currentColorSwatch = Brushes.SlateGray;
        private Brush _currentColorBorderBrush = Brushes.White;
        private string _historyHeaderText = "История покраски";

        public PaintCarViewModel()
        {
            GarageCars = new ObservableCollection<CarDisplayItem>();
            HistoryItems = new ObservableCollection<CarPaintHistoryItemVm>();
        }

        public ObservableCollection<CarDisplayItem> GarageCars { get; }
        public ObservableCollection<CarPaintHistoryItemVm> HistoryItems { get; }

        public CarDisplayItem SelectedCar
        {
            get => _selectedCar;
            set
            {
                if (_selectedCar == value)
                    return;
                _selectedCar = value;
                OnPropertyChanged();
                NotifyShell();
                ReloadForSelectedCar();
            }
        }

        public string CurrentColorText
        {
            get => _currentColorText;
            private set { _currentColorText = value ?? "—"; OnPropertyChanged(); }
        }

        public string CurrentColorSinceText
        {
            get => _currentColorSinceText;
            private set
            {
                _currentColorSinceText = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCurrentColorSince));
            }
        }

        public bool HasCurrentColorSince => !string.IsNullOrWhiteSpace(CurrentColorSinceText);

        public ImageSource CenterCarImage
        {
            get => _centerCarImage;
            private set { _centerCarImage = value; OnPropertyChanged(); }
        }

        public Brush CurrentColorSwatch
        {
            get => _currentColorSwatch;
            private set { _currentColorSwatch = value ?? Brushes.SlateGray; OnPropertyChanged(); }
        }

        public Brush CurrentColorBorderBrush
        {
            get => _currentColorBorderBrush;
            private set { _currentColorBorderBrush = value ?? Brushes.White; OnPropertyChanged(); }
        }

        public string HistoryHeaderText
        {
            get => _historyHeaderText;
            private set { _historyHeaderText = value ?? "История"; OnPropertyChanged(); }
        }

        public Visibility NoCarsVisibility =>
            GarageCars.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CarPanelVisibility =>
            GarageCars.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptyHistoryVisibility =>
            GarageCars.Count > 0 && HistoryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HistoryListVisibility =>
            HistoryItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public void Refresh()
        {
            var keepId = _selectedCar?.UserCarId ?? Guid.Empty;
            GarageCars.Clear();

            if (AppState.CurrentUserId != Guid.Empty)
            {
                try
                {
                    var uid = AppState.CurrentUserId;
                    var rows = AppConnect.model1.UserCars
                        .Include("Car")
                        .Include("Car.CarType")
                        .Include("Car.Model")
                        .Include("Car.Model.Brand")
                        .Where(uc => uc.UserId == uid)
                        .ToList();

                    var seenCarIds = new HashSet<Guid>();
                    foreach (var uc in rows.Where(uc => uc.Car != null))
                    {
                        if (!seenCarIds.Add(uc.CarId))
                            continue;

                        var car = uc.Car;
                        var photo = CarTypeImageHelper.GetImageForCarTypeName(car.CarType?.Name);
                        var brand = car.Model?.Brand?.Name?.Trim();
                        var model = car.Model?.Name?.Trim();
                        string name;
                        if (!string.IsNullOrEmpty(brand) && !string.IsNullOrEmpty(model))
                            name = $"{brand} {model}";
                        else if (!string.IsNullOrEmpty(model))
                            name = model;
                        else if (!string.IsNullOrEmpty(brand))
                            name = brand;
                        else
                            name = "Автомобиль";

                        GarageCars.Add(new CarDisplayItem
                        {
                            Photo = photo,
                            Name = name,
                            CarId = uc.CarId,
                            UserCarId = uc.RowId
                        });
                    }
                }
                catch
                {
                }
            }

            CarDisplayItem pick = null;
            if (keepId != Guid.Empty)
                pick = GarageCars.FirstOrDefault(c => c.UserCarId == keepId);
            if (pick == null && GarageCars.Count > 0)
                pick = GarageCars[0];

            _selectedCar = pick;
            OnPropertyChanged(nameof(SelectedCar));
            NotifyShell();
            ReloadForSelectedCar();
        }

        public (bool ok, string error) TryRecordPaint(
            CarPaintKind kind,
            Guid? colorId,
            string colorName,
            string partName,
            string notes)
        {
            if (_selectedCar == null)
                return (false, "Выберите автомобиль.");

            var result = CarPaintService.RecordPaint(
                _selectedCar.CarId,
                kind,
                colorId,
                colorName,
                partName,
                notes);

            if (result.ok)
                ReloadForSelectedCar();

            return result;
        }

        private void ReloadForSelectedCar()
        {
            HistoryItems.Clear();
            CurrentColorText = "—";
            CurrentColorSinceText = string.Empty;
            ApplyCurrentColorPreview(null);
            CenterCarImage = null;
            HistoryHeaderText = "История покраски";

            if (_selectedCar == null)
            {
                NotifyShell();
                return;
            }

            CenterCarImage = _selectedCar.Photo;

            var current = CarPaintService.GetCurrentBodyColor(_selectedCar.CarId);
            var colorName = current.ColorName ?? "—";
            CurrentColorText = colorName;
            CurrentColorSwatch = PaintColorBrushHelper.BrushFromColorName(colorName);
            if (current.Since.HasValue)
            {
                var culture = CultureInfo.GetCultureInfo("ru-RU");
                CurrentColorSinceText = "Активен с " + current.Since.Value.ToLocalTime().ToString("d MMMM yyyy", culture);
            }

            foreach (var item in CarPaintService.LoadHistory(_selectedCar.CarId))
                HistoryItems.Add(CarPaintHistoryItemVm.From(item));

            HistoryHeaderText = HistoryItems.Count == 0
                ? "История покраски"
                : $"История · {HistoryItems.Count} {PluralizeRecords(HistoryItems.Count)}";

            NotifyShell();
        }

        private void ApplyCurrentColorPreview(string colorName)
        {
            var c = PaintColorBrushHelper.ResolveColor(colorName);
            CurrentColorSwatch = PaintColorBrushHelper.BrushFromColorName(colorName);
            CurrentColorBorderBrush = PaintColorBrushHelper.ContrastBorderBrushFor(c);
        }

        private static string PluralizeRecords(int n)
        {
            var mod10 = n % 10;
            var mod100 = n % 100;
            if (mod10 == 1 && mod100 != 11) return "запись";
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return "записи";
            return "записей";
        }

        private void NotifyShell()
        {
            OnPropertyChanged(nameof(NoCarsVisibility));
            OnPropertyChanged(nameof(CarPanelVisibility));
            OnPropertyChanged(nameof(EmptyHistoryVisibility));
            OnPropertyChanged(nameof(HistoryListVisibility));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
