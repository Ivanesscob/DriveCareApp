using DriveCareCore.Data.BD;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class AddCarSalePage : Page, INotifyPropertyChanged
    {
        private string _saleTitle = string.Empty;
        private string _saleDescription = string.Empty;
        private string _priceText = string.Empty;
        private string _photoPath = string.Empty;
        private string _selectedPhotoPath = string.Empty;
        private UserCarOption _selectedUserCar;

        public ObservableCollection<UserCarOption> UserCars { get; } = new ObservableCollection<UserCarOption>();

        public string SaleTitle { get => _saleTitle; set { _saleTitle = value ?? string.Empty; OnPropertyChanged(); } }
        public string SaleDescription { get => _saleDescription; set { _saleDescription = value ?? string.Empty; OnPropertyChanged(); } }
        public string PriceText { get => _priceText; set { _priceText = value ?? string.Empty; OnPropertyChanged(); } }
        public string PhotoPath { get => _photoPath; set { _photoPath = value ?? string.Empty; OnPropertyChanged(); } }

        public UserCarOption SelectedUserCar
        {
            get => _selectedUserCar;
            set { _selectedUserCar = value; OnPropertyChanged(); }
        }

        public AddCarSalePage()
        {
            InitializeComponent();
            DataContext = this;
            LoadUserCars();
        }

        private void LoadUserCars()
        {
            UserCars.Clear();
            if (AppState.CurrentUserId == Guid.Empty)
                return;

            var rows = AppConnect.model1.UserCars
                .Include("Cars")
                .Include("Cars.Models")
                .Include("Cars.Models.Brands")
                .Where(uc => uc.UserId == AppState.CurrentUserId)
                .ToList();

            foreach (var uc in rows)
            {
                if (uc.Cars == null)
                    continue;
                var brand = uc.Cars.Models?.Brands?.Name?.Trim();
                var model = uc.Cars.Models?.Name?.Trim();
                var year = uc.Cars.Year.HasValue ? " " + uc.Cars.Year.Value : string.Empty;
                var display = (string.IsNullOrWhiteSpace(brand) ? "Марка" : brand) + " " +
                              (string.IsNullOrWhiteSpace(model) ? "Модель" : model) + year;

                UserCars.Add(new UserCarOption
                {
                    UserCarId = uc.RowId,
                    CarId = uc.CarId,
                    DisplayName = display.Trim()
                });
            }

            if (UserCars.Count > 0)
                SelectedUserCar = UserCars[0];
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedUserCar == null)
            {
                MessageBox.Show("Выберите автомобиль.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var title = (SaleTitle ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("Введите заголовок объявления.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseDecimal(PriceText, out var price) || price <= 0)
            {
                MessageBox.Show("Введите корректную цену.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saleId = Guid.NewGuid();
            var now = DateTime.Now;
            var savedPhotoPath = ResolvePhotoPathForStorage();
            var sale = new CarSales
            {
                RowId = saleId,
                CarId = SelectedUserCar.CarId,
                Title = title,
                Description = (SaleDescription ?? string.Empty).Trim(),
                PhotoPath = savedPhotoPath,
                CreatedAt = now
            };

            var salePrice = new CarSalePrices
            {
                RowId = Guid.NewGuid(),
                CarSaleId = saleId,
                Price = price,
                StartDate = now,
                EndDate = null,
                Description = "Стартовая цена"
            };

            var userSale = new UserCarSales
            {
                RowId = Guid.NewGuid(),
                UserId = AppState.CurrentUserId,
                CarSaleId = saleId,
                Description = "Создано пользователем"
            };

            try
            {
                AppConnect.model1.CarSales.Add(sale);
                AppConnect.model1.CarSalePrices.Add(salePrice);
                AppConnect.model1.UserCarSales.Add(userSale);
                AppConnect.model1.SaveChanges();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить объявление: " + ex.Message, "DriveCare", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show("Объявление добавлено.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
            AppState.SetFrame<BuyCarPage>();
        }

        private static bool TryParseDecimal(string text, out decimal value)
        {
            var t = (text ?? string.Empty).Trim();
            return decimal.TryParse(t, NumberStyles.Any, CultureInfo.CurrentCulture, out value)
                || decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private string ResolvePhotoPathForStorage()
        {
            if (string.IsNullOrWhiteSpace(_selectedPhotoPath))
                return null;

            string serverIp = "ivanessco.servebeer.com";
            int port = 5000;

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    client.Connect(serverIp, port);
                    using (NetworkStream stream = client.GetStream())
                    {
                        // --- подготовка данных ---
                        string originalFileName = Path.GetFileName(_selectedPhotoPath);
                        byte[] fileData = File.ReadAllBytes(_selectedPhotoPath);

                        // команда UPLOAD
                        string command = "UPLOAD";
                        byte[] cmdBytes = Encoding.UTF8.GetBytes(command);
                        byte[] cmdLength = BitConverter.GetBytes(cmdBytes.Length);
                        stream.Write(cmdLength, 0, 4);
                        stream.Write(cmdBytes, 0, cmdBytes.Length);

                        // длина имени файла
                        byte[] nameBytes = Encoding.UTF8.GetBytes(originalFileName);
                        byte[] nameLength = BitConverter.GetBytes(nameBytes.Length);
                        stream.Write(nameLength, 0, 4);

                        // имя файла
                        stream.Write(nameBytes, 0, nameBytes.Length);

                        // размер файла
                        byte[] fileSize = BitConverter.GetBytes((long)fileData.Length);
                        stream.Write(fileSize, 0, 8);

                        // сам файл
                        stream.Write(fileData, 0, fileData.Length);

                        // --- читаем сгенерированное имя от сервера ---
                        byte[] genNameLengthBytes = new byte[4];
                        stream.Read(genNameLengthBytes, 0, 4);
                        int genNameLength = BitConverter.ToInt32(genNameLengthBytes, 0);

                        byte[] genNameBytes = new byte[genNameLength];
                        int totalRead = 0;
                        while (totalRead < genNameLength)
                        {
                            int bytesRead = stream.Read(genNameBytes, totalRead, genNameLength - totalRead);
                            if (bytesRead == 0) break;
                            totalRead += bytesRead;
                        }

                        string generatedFileName = Encoding.UTF8.GetString(genNameBytes);
                        return generatedFileName;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка отправки: " + ex.Message);
                return null;
            }
        }

        private void SelectPhoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите фото автомобиля",
                Filter = "Изображения (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
                CheckFileExists = true,
                Multiselect = false
            };

            var result = dialog.ShowDialog();
            if (result != true)
                return;

            PhotoPath = dialog.FileName;
            _selectedPhotoPath = dialog.FileName;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<BuyCarPage>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public sealed class UserCarOption
    {
        public Guid UserCarId { get; set; }
        public Guid CarId { get; set; }
        public string DisplayName { get; set; }
    }
}
