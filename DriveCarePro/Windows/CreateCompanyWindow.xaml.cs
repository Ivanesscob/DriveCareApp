using DriveCareCore.Data.BD;
using DriveCareCore.Dialogs;
using DriveCareCore.Maps;
using DriveCarePro.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DriveCarePro.Windows
{
    public partial class CreateCompanyWindow : Window
    {
        private const int MinOwnerAgeYears = 18;

        private readonly DispatcherTimer _searchDebounce = new DispatcherTimer();
        private CancellationTokenSource _searchCts;
        private bool _suppressSearch;
        private bool _addressPickedFromGeo;
        private List<Country> _countries = new List<Country>();

        // Разбор адреса для БД (не показываем отдельными полями)
        private string _parsedCity = string.Empty;
        private string _parsedStreet = string.Empty;
        private string _parsedHouse = string.Empty;
        private double? _parsedLatitude;
        private double? _parsedLongitude;

        public CreateCompanyWindow()
        {
            InitializeComponent();
            _searchDebounce.Interval = TimeSpan.FromMilliseconds(350);
            _searchDebounce.Tick += SearchDebounce_Tick;
            Loaded += CreateCompanyWindow_Loaded;
        }

        private void CreateCompanyWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCountries();

            var maxBirth = DateTime.Today.AddYears(-MinOwnerAgeYears);
            BirthDatePicker.DisplayDateEnd = maxBirth;
            BirthDatePicker.SelectedDate = maxBirth;
        }

        private void LoadCountries()
        {
            try
            {
                _countries = AppConnect.model1.Countries
                    .OrderBy(c => c.Name)
                    .ToList();
                CountryCombo.ItemsSource = _countries;
                if (_countries.Count > 0)
                    CountryCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                _countries = new List<Country>();
                CountryCombo.ItemsSource = _countries;
                AddressSearchStatus.Text = "Не удалось загрузить список стран: " + ex.Message;
            }
        }

        private void AddressLineBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressSearch)
                return;
            _addressPickedFromGeo = false;
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }

        private void AddressLineBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!AddressLineBox.IsKeyboardFocusWithin &&
                    !AddressSuggestionsList.IsKeyboardFocusWithin)
                    SuggestionsPopup.IsOpen = false;
            }), DispatcherPriority.Input);
        }

        private void SearchDebounce_Tick(object sender, EventArgs e)
        {
            _searchDebounce.Stop();
            RunAddressSearch();
        }

        private async void RunAddressSearch()
        {
            var query = AddressLineBox?.Text?.Trim() ?? string.Empty;
            if (query.Length < 3)
            {
                SuggestionsPopup.IsOpen = false;
                if (query.Length == 0)
                    AddressSearchStatus.Text = "Начните вводить адрес — появятся подсказки.";
                else
                    AddressSearchStatus.Text = "Введите ещё символы для поиска (минимум 3).";
                return;
            }

            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;
            AddressSearchStatus.Text = "Поиск адресов…";

            try
            {
                var items = await GeoapifyAutocompleteService.SearchAsync(query, token).ConfigureAwait(true);
                if (token.IsCancellationRequested)
                    return;

                AddressSuggestionsList.ItemsSource = items;
                var hasItems = items != null && items.Count > 0;
                SuggestionsPopup.IsOpen = hasItems;
                AddressSearchStatus.Text = hasItems
                    ? "Выберите адрес из списка."
                    : "Ничего не найдено. Уточните запрос.";
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    SuggestionsPopup.IsOpen = false;
                    AddressSearchStatus.Text = "Ошибка поиска: " + ex.Message;
                }
            }
        }

        private void AddressSuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!SuggestionsPopup.IsOpen)
                return;
            if (AddressSuggestionsList.SelectedItem is GeoapifyAddressSuggestion item)
                ApplyAddressSuggestion(item);
        }

        private void AddressSuggestionsList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && AddressSuggestionsList.SelectedItem is GeoapifyAddressSuggestion item)
            {
                ApplyAddressSuggestion(item);
                e.Handled = true;
            }
        }

        private void ApplyAddressSuggestion(GeoapifyAddressSuggestion item)
        {
            if (item == null)
                return;

            _suppressSearch = true;
            try
            {
                AddressLineBox.Text = item.Label;
                _parsedCity = item.City ?? string.Empty;
                _parsedStreet = item.Street ?? string.Empty;
                _parsedHouse = item.House ?? string.Empty;
                _parsedLatitude = item.Latitude;
                _parsedLongitude = item.Longitude;
                TrySelectCountry(item.CountryCode, item.CountryName);
                _addressPickedFromGeo = true;
                SuggestionsPopup.IsOpen = false;
                AddressSearchStatus.Text = item.HasHouseNumber
                    ? "Адрес выбран. При необходимости укажите квартиру."
                    : "Адрес выбран. Проверьте, что указан номер дома.";
            }
            finally
            {
                _suppressSearch = false;
            }
        }

        private void TrySelectCountry(string countryCode, string countryName)
        {
            if (_countries.Count == 0)
                return;

            Country match = null;
            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                var code = countryCode.Trim().ToUpperInvariant();
                match = _countries.FirstOrDefault(c =>
                    c.Code != null && c.Code.Trim().Equals(code, StringComparison.OrdinalIgnoreCase));
            }

            if (match == null && !string.IsNullOrWhiteSpace(countryName))
            {
                var name = countryName.Trim();
                match = _countries.FirstOrDefault(c =>
                    c.Name != null && c.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            if (match != null)
                CountryCombo.SelectedItem = match;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!TryValidate(out var errors))
            {
                AppMessageBox.Show(errors, "Проверьте данные", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var country = CountryCombo.SelectedItem as Country;
            if (country == null)
            {
                AppMessageBox.Show("Выберите страну.", "Проверьте данные", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var typeIds = new List<Guid>();
            if (ChkCreateAuto.IsChecked == true)
                typeIds.Add(WorkshopServiceKinds.AutoServiceId);
            if (ChkCreatePaint.IsChecked == true)
                typeIds.Add(WorkshopServiceKinds.PaintingId);
            if (ChkCreateTire.IsChecked == true)
                typeIds.Add(WorkshopServiceKinds.TireServiceId);

            if (typeIds.Count == 0)
            {
                AppMessageBox.Show("Отметьте хотя бы один тип: автосервис, покраска или шиномонтаж.",
                    "Проверьте данные", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var input = new CompanyCreationService.CreateCompanyInput
            {
                CompanyName = CompanyNameBox.Text.Trim(),
                CompanyDescription = CompanyDescriptionBox.Text.Trim(),
                WorkshopName = WorkshopNameBox.Text.Trim(),
                WorkshopDescription = WorkshopDescriptionBox.Text.Trim(),
                BusinessTypeId = typeIds[0],
                BusinessTypeIds = typeIds,
                CountryId = country.RowId,
                AddressLine = AddressLineBox.Text.Trim(),
                Latitude = _parsedLatitude,
                Longitude = _parsedLongitude,
                ParsedCity = _parsedCity,
                ParsedStreet = _parsedStreet,
                ParsedHouse = _parsedHouse,
                Apartment = ApartmentBox.Text.Trim(),
                LastName = LastNameBox.Text.Trim(),
                FirstName = FirstNameBox.Text.Trim(),
                MidName = MidNameBox.Text.Trim(),
                Login = LoginBox.Text.Trim(),
                Password = PasswordBox.Password,
                Email = EmailBox.Text.Trim(),
                Phone = PhoneBox.Text.Trim(),
                BirthDate = BirthDatePicker.SelectedDate.Value
            };

            var result = CompanyCreationService.Create(input);
            if (!result.Success)
            {
                AppMessageBox.Show(result.ErrorMessage, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AppMessageBox.Show(
                "Компания и мастерская созданы." + Environment.NewLine +
                "Владелец может войти в DriveCare Pro с указанным логином и паролем.",
                "Готово",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }

        private bool TryValidate(out string errors)
        {
            var list = new List<string>();

            RequireText(CompanyNameBox, "Название компании", list);
            RequireText(WorkshopNameBox, "Название мастерской", list);

            if (string.IsNullOrWhiteSpace(AddressLineBox?.Text))
                list.Add("• Адрес (одной строкой)");
            else if (!_addressPickedFromGeo)
                list.Add("• Выберите адрес из подсказок Geoapify");

            if (CountryCombo.SelectedItem == null)
                list.Add("• Страна");

            RequireText(LastNameBox, "Фамилия", list);
            RequireText(FirstNameBox, "Имя", list);
            RequireText(LoginBox, "Логин", list);
            if (string.IsNullOrWhiteSpace(PasswordBox?.Password))
                list.Add("• Пароль");
            if (string.IsNullOrWhiteSpace(EmailBox?.Text))
                list.Add("• Email");
            else if (!ContactValidation.IsValidEmail(EmailBox.Text))
                list.Add("• Email (некорректный формат, например user@mail.ru)");

            if (string.IsNullOrWhiteSpace(PhoneBox?.Text))
                list.Add("• Телефон");
            else if (!ContactValidation.IsValidPhone(PhoneBox.Text))
                list.Add("• Телефон (минимум 10 цифр; для РФ: +7 9XX… или 8 9XX…)");

            var login = LoginBox?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(login) &&
                AppConnect.model1.Employees.Any(emp => emp.Login == login))
                list.Add("• Логин уже занят другим сотрудником");

            if (!BirthDatePicker.SelectedDate.HasValue)
                list.Add("• Дата рождения");
            else if (!IsOwnerAtLeast18(BirthDatePicker.SelectedDate.Value))
                list.Add("• Владельцу должно быть не менее 18 лет");

            errors = list.Count == 0
                ? string.Empty
                : "Не заполнено или неверно:" + Environment.NewLine + string.Join(Environment.NewLine, list);
            return list.Count == 0;
        }

        private static bool IsOwnerAtLeast18(DateTime birthDate)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age))
                age--;
            return age >= MinOwnerAgeYears;
        }

        private static void RequireText(TextBox box, string caption, ICollection<string> errors)
        {
            if (box == null || string.IsNullOrWhiteSpace(box.Text))
                errors.Add("• " + caption);
        }

        protected override void OnClosed(EventArgs e)
        {
            _searchCts?.Cancel();
            _searchDebounce.Stop();
            SuggestionsPopup.IsOpen = false;
            base.OnClosed(e);
        }
    }
}
