using DriveCareCore;
using DriveCareCore.Data.BD;
using DriveCareCore.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DriveCare.Pages.User
{
    public partial class UserHomePage : Page, INotifyPropertyChanged
    {
        private int _index;
        private ImageSource _carImage;
        private string _carTitle = string.Empty;
        private string _positionText = string.Empty;

        public UserHomePage()
        {
            GarageCars = new ObservableCollection<CarDisplayItem>();
            InitializeComponent();
            DataContext = this;

            PrevCommand = new DelegateCommand(_ => Shift(-1), _ => GarageCars.Count > 1);
            NextCommand = new DelegateCommand(_ => Shift(1), _ => GarageCars.Count > 1);
            Loaded += (_, __) => ReloadCars();
        }

        public ObservableCollection<CarDisplayItem> GarageCars { get; }

        public DelegateCommand PrevCommand { get; }
        public DelegateCommand NextCommand { get; }

        public int SelectedCarIndex
        {
            get => _index;
            set
            {
                if (GarageCars.Count == 0)
                {
                    if (_index != 0) { _index = 0; OnPropertyChanged(); }
                    return;
                }
                var n = Math.Max(0, Math.Min(value, GarageCars.Count - 1));
                if (_index == n) return;
                _index = n;
                OnPropertyChanged();
                ApplyHeroFromIndex();
            }
        }

        public ImageSource CarImage
        {
            get => _carImage;
            private set { _carImage = value; OnPropertyChanged(); }
        }

        public string CarTitle
        {
            get => _carTitle;
            private set { _carTitle = value ?? string.Empty; OnPropertyChanged(); }
        }

        public string PositionText
        {
            get => _positionText;
            private set { _positionText = value ?? string.Empty; OnPropertyChanged(); }
        }

        public bool ShowNavChrome => GarageCars.Count > 1;

        private bool IsLoggedIn =>
            AppState.CurrentUserId != Guid.Empty && AppState.CurrentUser != null;

        public Visibility EmptyHintVisibility =>
            IsLoggedIn && GarageCars.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility AuthHintVisibility =>
            !IsLoggedIn ? Visibility.Visible : Visibility.Collapsed;

        public Visibility HeroVisibility =>
            GarageCars.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility StripVisibility =>
            GarageCars.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptyStatePanelVisibility =>
            GarageCars.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

        private void ReloadCars()
        {
            GarageCars.Clear();
            _index = 0;

            if (IsLoggedIn)
            {
                try
                {
                    var uid = AppState.CurrentUserId;
                    var rows = AppConnect.model1.UserCars
                        .Include("Cars")
                        .Include("Cars.CarTypes")
                        .Include("Cars.Models")
                        .Include("Cars.Models.Brands")
                        .Where(uc => uc.UserId == uid)
                        .ToList();

                    foreach (var uc in rows)
                    {
                        if (uc.Cars == null)
                            continue;
                        var car = uc.Cars;
                        var photo = CarTypeImageHelper.GetImageForCarTypeName(car.CarTypes?.Name);
                        var brand = car.Models?.Brands?.Name?.Trim();
                        var model = car.Models?.Name?.Trim();
                        string name;
                        if (!string.IsNullOrEmpty(brand) && !string.IsNullOrEmpty(model))
                            name = $"{brand} {model}";
                        else if (!string.IsNullOrEmpty(model))
                            name = model;
                        else if (!string.IsNullOrEmpty(brand))
                            name = brand;
                        else
                            name = "Автомобиль";

                        GarageCars.Add(new CarDisplayItem { Photo = photo, Name = name });
                    }
                }
                catch
                {
                }
            }

            OnPropertyChanged(nameof(SelectedCarIndex));
            NotifyChrome();
            ApplyHeroFromIndex();
        }

        private void Shift(int delta)
        {
            if (GarageCars.Count <= 1)
                return;
            _index = (_index + delta + GarageCars.Count) % GarageCars.Count;
            OnPropertyChanged(nameof(SelectedCarIndex));
            ApplyHeroFromIndex();
        }

        private void ApplyHeroFromIndex()
        {
            if (GarageCars.Count == 0)
            {
                CarImage = LoadDefaultCarPackImage();
                CarTitle = string.Empty;
                PositionText = string.Empty;
            }
            else
            {
                _index = Math.Min(_index, GarageCars.Count - 1);
                var item = GarageCars[_index];
                CarImage = item.Photo ?? LoadDefaultCarPackImage();
                CarTitle = string.IsNullOrWhiteSpace(item.Name) ? "Автомобиль" : item.Name;
                PositionText = GarageCars.Count > 1
                    ? $"{_index + 1} из {GarageCars.Count}"
                    : string.Empty;
            }

            NotifyChrome();
        }

        private void NotifyChrome()
        {
            OnPropertyChanged(nameof(ShowNavChrome));
            OnPropertyChanged(nameof(EmptyHintVisibility));
            OnPropertyChanged(nameof(AuthHintVisibility));
            OnPropertyChanged(nameof(HeroVisibility));
            OnPropertyChanged(nameof(StripVisibility));
            OnPropertyChanged(nameof(EmptyStatePanelVisibility));
            PrevCommand.RaiseCanExecuteChanged();
            NextCommand.RaiseCanExecuteChanged();
        }

        private static ImageSource LoadDefaultCarPackImage()
        {
            try
            {
                var uri = new Uri(
                    "pack://application:,,,/DriveCareCore;component/Data/Pics/TypeCarPics/SedanPic.png",
                    UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
