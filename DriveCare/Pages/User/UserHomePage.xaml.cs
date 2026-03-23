using DriveCareCore;
using DriveCareCore.Data.BD;
using DriveCareCore.Services;
using DriveCare.Pages.User.ActionPages;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

                    var seenCarIds = new System.Collections.Generic.HashSet<Guid>();
                    foreach (var uc in rows.Where(uc => uc.Cars != null))
                    {
                        if (seenCarIds.Contains(uc.CarId))
                            continue;
                        seenCarIds.Add(uc.CarId);

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

        private void QuickActionButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;
            var action = btn.Tag as string ?? string.Empty;

            switch (action)
            {
                case "ToolsStore":
                    AppState.SetFrame<ToolsStorePage>();
                    break;
                case "ServiceSelect":
                    AppState.SetFrame<ServiceSelectPage>();
                    break;
                case "PaintCar":
                    AppState.SetFrame<PaintCarPage>();
                    break;
                case "ServiceCar":
                    AppState.SetFrame<ServiceCarPage>();
                    break;
                case "BuyCar":
                    AppState.SetFrame<BuyCarPage>();
                    break;
                default:
                    return;
            }
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<ProfilePage>();
        }

        private void HeroCar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            if (GarageCars.Count == 0) return;

            var idx = Math.Max(0, Math.Min(_index, GarageCars.Count - 1));
            OpenCarDetailsWindow(GarageCars[idx]);
            e.Handled = true;
        }

        private void GarageCars_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (GarageCars.Count == 0) return;
            var lb = sender as ListBox;
            if (lb == null) return;

            var container = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            var item = container?.Content as CarDisplayItem ?? lb.SelectedItem as CarDisplayItem;
            if (item == null) return;

            OpenCarDetailsWindow(item);
            e.Handled = true;
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                var match = current as T;
                if (match != null)
                    return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void OpenCarDetailsWindow(CarDisplayItem item)
        {
            try
            {
                var owner = Window.GetWindow(this);
                //var wnd = new CarDetailsWindow(item) { Owner = owner };
                //wnd.Show();
            }
            catch
            {
            }
        }
    }
}
