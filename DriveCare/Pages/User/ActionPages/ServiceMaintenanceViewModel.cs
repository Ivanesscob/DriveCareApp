using DriveCare;
using DriveCare.Data;
using DriveCare.Services;
using DriveCareCore.Data.BD;
using DriveCareCore.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
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
using System.Windows.Media.Imaging;

namespace DriveCare.Pages.User.ActionPages
{
    /// <summary>Данные и команды экрана «Обслуживание».</summary>
    public sealed class ServiceMaintenanceViewModel : INotifyPropertyChanged
    {
        private CarDisplayItem _selectedCar;
        private int? _approximateCurrentMileageKm;
        private string _approximateMileageDisplayText = "—";
        private ImageSource _centerCarImage;
        private IReadOnlyList<MaintenanceHistoryItemVm> _historyNewestFirst = Array.Empty<MaintenanceHistoryItemVm>();
        private PlotModel _mileagePlotModel = new PlotModel();
        private string _mileagePredictedSummary = string.Empty;
        private Visibility _mileagePredictedLineVisibility = Visibility.Collapsed;
        private string _overallHint = string.Empty;
        private int _countGood;
        private int _countWatch;
        private int _countDue;

        public ServiceMaintenanceViewModel()
        {
            GarageCars = new ObservableCollection<CarDisplayItem>();
            HistoryItems = new ObservableCollection<MaintenanceHistoryItemVm>();
            ComponentStatuses = new ObservableCollection<VehicleComponentStatusVm>();
            KmRecommendations = new ObservableCollection<MaintenanceRecommendationVm>();
            ThemeService.ThemeChanged += OnAppThemeChanged;
        }

        private void OnAppThemeChanged(object sender, EventArgs e)
        {
            if (_selectedCar == null)
                return;
            RebuildMileageChart();
        }

        public PlotModel MileagePlotModel
        {
            get => _mileagePlotModel;
            private set { _mileagePlotModel = value ?? new PlotModel(); OnPropertyChanged(); }
        }

        public Visibility MileageHintEmptyHistoryVisibility =>
            HistoryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility MileageHintNoOdometerVisibility =>
            HistoryItems.Count > 0 && !HasMileagePoints ? Visibility.Visible : Visibility.Collapsed;

        public Visibility MileagePlotHostVisibility =>
            HasMileagePoints ? Visibility.Visible : Visibility.Collapsed;

        public string ApproximateMileageDisplayText
        {
            get => _approximateMileageDisplayText;
            private set { _approximateMileageDisplayText = value ?? "—"; OnPropertyChanged(); }
        }

        public string MileagePredictedSummary
        {
            get => _mileagePredictedSummary;
            private set { _mileagePredictedSummary = value ?? string.Empty; OnPropertyChanged(); }
        }

        public Visibility MileagePredictedLineVisibility
        {
            get => _mileagePredictedLineVisibility;
            private set { _mileagePredictedLineVisibility = value; OnPropertyChanged(); }
        }

        public Visibility MileageInsightPanelVisibility =>
            HasMileagePoints ? Visibility.Visible : Visibility.Collapsed;

        private bool HasMileagePoints => _historyNewestFirst.Any(h => h.MileageKm.HasValue);

        /// <summary>Минимально допустимый реальный пробег: не ниже истории и примерного ориентира.</summary>
        public int GetMinimumAllowedMileageKm()
        {
            var maxHistory = _historyNewestFirst
                .Where(h => h.MileageKm.HasValue)
                .Select(h => h.MileageKm.Value)
                .DefaultIfEmpty(0)
                .Max();
            var approx = _approximateCurrentMileageKm ?? 0;
            return Math.Max(maxHistory, approx);
        }

        public (bool ok, string error) TryAddRealMileage(int mileageKm)
        {
            if (_selectedCar == null)
                return (false, "Выберите автомобиль.");

            var minKm = GetMinimumAllowedMileageKm();
            if (mileageKm < minKm)
                return (false, $"Пробег не может быть меньше {minKm:N0} км.");

            var result = ServiceMaintenanceRepository.TryInsertOdometerReading(_selectedCar.UserCarId, mileageKm);
            if (!result.ok)
                return result;

            ReloadForSelectedCar();
            return (true, null);
        }

        public ObservableCollection<CarDisplayItem> GarageCars { get; }
        public ObservableCollection<MaintenanceHistoryItemVm> HistoryItems { get; }
        public ObservableCollection<VehicleComponentStatusVm> ComponentStatuses { get; }
        public ObservableCollection<MaintenanceRecommendationVm> KmRecommendations { get; }

        public string OverallHint
        {
            get => _overallHint;
            private set { _overallHint = value ?? string.Empty; OnPropertyChanged(); }
        }

        public int CountGood
        {
            get => _countGood;
            private set { _countGood = value; OnPropertyChanged(); }
        }

        public int CountWatch
        {
            get => _countWatch;
            private set { _countWatch = value; OnPropertyChanged(); }
        }

        public int CountDue
        {
            get => _countDue;
            private set { _countDue = value; OnPropertyChanged(); }
        }

        public Visibility ComponentPanelVisibility =>
            ComponentStatuses.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public CarDisplayItem SelectedCar
        {
            get => _selectedCar;
            set
            {
                if (_selectedCar == value)
                    return;
                _selectedCar = value;
                OnPropertyChanged();
                ReloadForSelectedCar();
            }
        }

        public ImageSource CenterCarImage
        {
            get => _centerCarImage;
            private set { _centerCarImage = value; OnPropertyChanged(); }
        }

        public Visibility NoCarsVisibility =>
            GarageCars.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CarPanelVisibility =>
            GarageCars.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptyHistoryVisibility =>
            GarageCars.Count > 0 && HistoryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public void Refresh()
        {
            var keepId = _selectedCar?.UserCarId ?? Guid.Empty;
            GarageCars.Clear();

            if (AppState.CurrentUserId != Guid.Empty && AppState.CurrentUser != null)
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
                        if (seenCarIds.Contains(uc.CarId))
                            continue;
                        seenCarIds.Add(uc.CarId);

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

        private void ReloadForSelectedCar()
        {
            HistoryItems.Clear();
            ComponentStatuses.Clear();
            KmRecommendations.Clear();
            _historyNewestFirst = Array.Empty<MaintenanceHistoryItemVm>();
            CountGood = CountWatch = CountDue = 0;
            OverallHint = string.Empty;

            if (_selectedCar == null)
            {
                CenterCarImage = LoadDefaultCarPackImage();
                MileagePlotModel = new PlotModel();
                ClearMileageInsight();
                NotifyMileageUi();
                NotifyShell();
                NotifyComponentUi();
                return;
            }

            CenterCarImage = _selectedCar.Photo ?? LoadDefaultCarPackImage();

            try
            {
                _historyNewestFirst = ServiceMaintenanceRepository.LoadHistory(_selectedCar.UserCarId);
            }
            catch
            {
                _historyNewestFirst = Array.Empty<MaintenanceHistoryItemVm>();
            }

            foreach (var h in _historyNewestFirst)
                HistoryItems.Add(h);

            UpdateApproximateCurrentMileage();
            RebuildComponentStatuses();
            RebuildKmRecommendations();
            RebuildMileageChart();
            NotifyShell();
            NotifyComponentUi();
        }

        private void RebuildComponentStatuses()
        {
            ComponentStatuses.Clear();
            if (_selectedCar == null)
                return;

            var items = VehicleComponentStatusService.Build(
                _selectedCar.UserCarId,
                _historyNewestFirst,
                _approximateCurrentMileageKm);

            foreach (var item in items.OrderBy(i => i.SortOrder))
                ComponentStatuses.Add(item);

            VehicleComponentStatusService.PersistComputed(_selectedCar.UserCarId, items);

            var counts = VehicleComponentStatusService.CountByLevel(items);
            CountGood = counts.good;
            CountWatch = counts.watch;
            CountDue = counts.due;
            OverallHint = VehicleComponentStatusService.BuildOverallHint(
                counts.good, counts.watch, counts.due, counts.unknown);
        }

        private void NotifyComponentUi()
        {
            OnPropertyChanged(nameof(ComponentPanelVisibility));
            OnPropertyChanged(nameof(OverallHint));
            OnPropertyChanged(nameof(CountGood));
            OnPropertyChanged(nameof(CountWatch));
            OnPropertyChanged(nameof(CountDue));
        }

        private void UpdateApproximateCurrentMileage()
        {
            var ru = CultureInfo.GetCultureInfo("ru-RU");
            var samples = BuildDailyMileageSamples(_historyNewestFirst);
            var today = DateTime.Today;
            if (samples.Count >= 2 &&
                MileageTrendEstimator.TryPredictToday(samples, today, out var pred, out _) &&
                pred >= 0)
            {
                _approximateCurrentMileageKm = pred;
                ApproximateMileageDisplayText = $"≈ {pred.ToString("N0", ru)} км";
                return;
            }

            if (samples.Count >= 1)
            {
                var last = samples[samples.Count - 1];
                _approximateCurrentMileageKm = last.Km;
                var d = last.Day.ToString("dd.MM.yyyy", ru);
                ApproximateMileageDisplayText = $"≈ {last.Km.ToString("N0", ru)} км (визит {d})";
                return;
            }

            _approximateCurrentMileageKm = null;
            ApproximateMileageDisplayText = "—";
        }

        private void RebuildMileageChart()
        {
            var samples = BuildDailyMileageSamples(_historyNewestFirst);
            var pts = samples
                .Select(s => DateTimeAxis.CreateDataPoint(s.Day, s.Km))
                .ToList();

            if (pts.Count == 0)
            {
                MileagePlotModel = new PlotModel();
                ClearMileageInsight();
                NotifyMileageUi();
                return;
            }

            var today = DateTime.Today;
            var hasRegression = MileageTrendEstimator.TryPredictToday(samples, today, out var predictedKm, out var kmPerDay);

            if (hasRegression && kmPerDay >= 0)
            {
                var dayLabel = today.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));
                var rate = kmPerDay < 0.05
                    ? "почти без движения по данным записей"
                    : $"~{kmPerDay.ToString("N1", CultureInfo.GetCultureInfo("ru-RU"))} км/день по тренду";
                MileagePredictedSummary =
                    $"Ориентир на сегодня ({dayLabel}): ≈ {predictedKm.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"))} км ({rate}).";
                MileagePredictedLineVisibility = Visibility.Visible;
            }
            else if (samples.Count >= 2)
            {
                MileagePredictedSummary =
                    "По истории не удаётся оценить рост пробега (слишком мало разнесённых по времени отметок или тренд нестабилен).";
                MileagePredictedLineVisibility = Visibility.Visible;
            }
            else
            {
                MileagePredictedSummary =
                    "Для прогноза на сегодня нужны минимум две записи с пробегом и разными датами.";
                MileagePredictedLineVisibility = Visibility.Visible;
            }

            var palette = OxyPlotThemeHelper.GetCurrentPalette();

            var model = new PlotModel { PlotMargins = new OxyThickness(48, 8, 12, 40) };
            OxyPlotThemeHelper.ApplyToPlotModel(model, palette);

            var dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Дата",
                StringFormat = "dd.MM.yy",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.None
            };
            OxyPlotThemeHelper.ApplyToAxis(dateAxis, palette);
            model.Axes.Add(dateAxis);

            var kmAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Пробег, км",
                MinimumPadding = 0.05,
                MaximumPadding = 0.12,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.None
            };
            OxyPlotThemeHelper.ApplyToAxis(kmAxis, palette);
            model.Axes.Add(kmAxis);

            var line = new LineSeries
            {
                Color = palette.LinePrimary,
                StrokeThickness = 2.5,
                MarkerType = MarkerType.Circle,
                MarkerSize = 4.5,
                MarkerFill = palette.LinePrimary,
                MarkerStroke = palette.MarkerStroke,
                MarkerStrokeThickness = 1
            };
            foreach (var p in pts)
                line.Points.Add(p);
            model.Series.Add(line);

            var last = samples[samples.Count - 1];
            if (hasRegression && kmPerDay >= 0 && today >= last.Day.Date)
            {
                var trend = new LineSeries
                {
                    Color = OxyColor.FromAColor(200, palette.LineTrend),
                    LineStyle = LineStyle.Dash,
                    StrokeThickness = 2,
                    MarkerType = MarkerType.None
                };
                trend.Points.Add(DateTimeAxis.CreateDataPoint(last.Day, last.Km));
                trend.Points.Add(DateTimeAxis.CreateDataPoint(today, predictedKm));
                model.Series.Add(trend);
            }

            if (hasRegression && kmPerDay >= 0 && today >= last.Day.Date)
            {
                var predPt = new ScatterSeries
                {
                    MarkerType = MarkerType.Square,
                    MarkerSize = 5.5,
                    MarkerFill = palette.LineTrend,
                    MarkerStroke = palette.MarkerStroke,
                    MarkerStrokeThickness = 1
                };
                predPt.Points.Add(new ScatterPoint(DateTimeAxis.ToDouble(today), predictedKm));
                model.Series.Add(predPt);
            }

            MileagePlotModel = model;
            NotifyMileageUi();
        }

        private static List<(DateTime Day, int Km)> BuildDailyMileageSamples(IReadOnlyList<MaintenanceHistoryItemVm> history)
        {
            return history
                .Where(h => h.MileageKm.HasValue)
                .GroupBy(h => h.ServiceDate.Date)
                .Select(g => (g.Key, g.Max(x => x.MileageKm.GetValueOrDefault())))
                .OrderBy(x => x.Key)
                .ToList();
        }

        private void ClearMileageInsight()
        {
            _approximateCurrentMileageKm = null;
            ApproximateMileageDisplayText = "—";
            MileagePredictedSummary = string.Empty;
            MileagePredictedLineVisibility = Visibility.Collapsed;
        }

        private void NotifyMileageUi()
        {
            OnPropertyChanged(nameof(MileagePlotModel));
            OnPropertyChanged(nameof(MileageHintEmptyHistoryVisibility));
            OnPropertyChanged(nameof(MileageHintNoOdometerVisibility));
            OnPropertyChanged(nameof(MileagePlotHostVisibility));
            OnPropertyChanged(nameof(MileageInsightPanelVisibility));
            OnPropertyChanged(nameof(ApproximateMileageDisplayText));
            OnPropertyChanged(nameof(MileagePredictedSummary));
            OnPropertyChanged(nameof(MileagePredictedLineVisibility));
        }

        private void RebuildKmRecommendations()
        {
            KmRecommendations.Clear();
            foreach (var row in MaintenanceRecommendationEngine.Build(_historyNewestFirst, _approximateCurrentMileageKm))
                KmRecommendations.Add(row);
        }

        private void NotifyShell()
        {
            OnPropertyChanged(nameof(NoCarsVisibility));
            OnPropertyChanged(nameof(CarPanelVisibility));
            OnPropertyChanged(nameof(EmptyHistoryVisibility));
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

    /// <summary>Линейный тренд пробега по дням (для ориентировочного значения на сегодня).</summary>
    internal static class MileageTrendEstimator
    {
        public static bool TryPredictToday(
            IReadOnlyList<(DateTime Day, int Km)> samples,
            DateTime today,
            out int predictedKm,
            out double kmPerDay)
        {
            predictedKm = 0;
            kmPerDay = 0;
            if (samples == null || samples.Count < 2)
                return false;

            var t0 = samples[0].Day.Date;
            var n = samples.Count;
            var tList = new List<double>(n);
            var yList = new List<double>(n);
            for (var i = 0; i < n; i++)
            {
                tList.Add((samples[i].Day.Date - t0).TotalDays);
                yList.Add(samples[i].Km);
            }

            var span = tList[n - 1];
            double b;
            double a;

            if (span < 1e-3)
                return false;

            var meanT = tList.Average();
            var meanY = yList.Average();
            double sxy = 0, sxx = 0;
            for (var i = 0; i < n; i++)
            {
                var dt = tList[i] - meanT;
                sxy += dt * (yList[i] - meanY);
                sxx += dt * dt;
            }

            if (sxx < 1e-9)
                return false;

            b = sxy / sxx;
            a = meanY - b * meanT;
            if (b < -0.01)
                return false;

            if (b > 500)
            {
                var lo = samples[n - 2];
                var hi = samples[n - 1];
                var dd = (hi.Day.Date - lo.Day.Date).TotalDays;
                if (dd < 1e-6)
                    return false;
                b = (hi.Km - lo.Km) / dd;
                if (b < 0)
                    return false;
                a = hi.Km - b * tList[n - 1];
            }

            kmPerDay = b;
            var tToday = (today.Date - t0).TotalDays;
            var pred = a + b * tToday;
            predictedKm = (int)Math.Round(pred);

            var firstKm = samples[0].Km;
            var lastKm = samples[n - 1].Km;
            var lastDay = samples[n - 1].Day.Date;

            if (today.Date >= lastDay && predictedKm < lastKm)
                predictedKm = lastKm;
            if (predictedKm < firstKm)
                predictedKm = firstKm;

            var daysSinceLast = Math.Max(0, (today.Date - lastDay).TotalDays);
            var maxReasonable = lastKm + (int)Math.Round(daysSinceLast * 800.0);
            if (predictedKm > maxReasonable)
                predictedKm = maxReasonable;

            return true;
        }
    }

}
