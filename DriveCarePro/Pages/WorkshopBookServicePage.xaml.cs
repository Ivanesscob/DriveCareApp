using DriveCarePro.Services;

using DriveCareCore.WorkOrders;
using DriveCarePro.Services.RepairWorkOrder;

using DriveCarePro.Services.ServiceBooking;

using Microsoft.Win32;

using System;

using System.Linq;

using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

using System.Windows;

using System.Windows.Controls;



namespace DriveCarePro.Pages

{

    public partial class WorkshopBookServicePage : Page

    {

        private readonly ServiceBookingContext _ctx;

        private OwnerOrganizationScope _scope;

        private RepairWorkOrderModel _companyContext = new RepairWorkOrderModel();



        public WorkshopBookServicePage(ServiceBookingContext ctx)

        {

            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));

            InitializeComponent();

            Loaded += WorkshopBookServicePage_Loaded;

        }



        private async void WorkshopBookServicePage_Loaded(object sender, RoutedEventArgs e)

        {

            ApplyKind();



            var resolved = await OwnerOrganizationScope.TryResolveAsync().ConfigureAwait(true);

            if (!resolved.ok)

            {

                HintText.Text = resolved.error;

                BtnSave.IsEnabled = BtnDownloadEmpty.IsEnabled = false;

                return;

            }



            _scope = resolved.scope;

            _ctx.Scope = _scope;

            if (AppState.CurrentEmployee?.WorkshopId != null)

                _ctx.WorkshopId = AppState.CurrentEmployee.WorkshopId.Value;



            PrefillFromContext();



            try

            {

                _companyContext = await RepairWorkOrderDataService.LoadCompanyContextAsync(_scope).ConfigureAwait(true);

                CompanyNameBox.Text = _companyContext.CompanyName;

                CompanyAddressBox.Text = _companyContext.CompanyLegalAddress;

                CompanyPhoneBox.Text = _companyContext.CompanyPhone;

            }

            catch (Exception ex) when (AppState.IsDatabaseConnectionError(ex))

            {

                HintText.Text = AppState.BuildConnectionErrorMessage(ex);

                BtnSave.IsEnabled = BtnDownloadEmpty.IsEnabled = false;

            }

        }



        private void ApplyKind()

        {

            if (_ctx.Kind == ServiceBookingKind.Painting)

            {

                Title = "Записать на покраску";

                TitleText.Text = "Записать машину на покраску";

            }

            else

            {

                Title = "Записать на ремонт";

                TitleText.Text = "Записать машину на ремонт";

            }



            HintText.Text = "Данные сохраняются в базу. Вам будет создано задание с указанием, что сделать с машиной. Заказ-наряд — при выдаче авто клиенту.";

        }



        private void PrefillFromContext()

        {

            if (!string.IsNullOrWhiteSpace(_ctx.ClientFullName))

                ClientNameBox.Text = _ctx.ClientFullName;

            if (!string.IsNullOrWhiteSpace(_ctx.ClientPhone))

                ClientPhoneBox.Text = _ctx.ClientPhone;

            if (!string.IsNullOrWhiteSpace(_ctx.ClientAddress))

                ClientAddressBox.Text = _ctx.ClientAddress;

            if (!string.IsNullOrWhiteSpace(_ctx.CarDescription))

                CarDescriptionBox.Text = _ctx.CarDescription;

            if (!string.IsNullOrWhiteSpace(_ctx.Vin))

                VinBox.Text = _ctx.Vin;

            if (!string.IsNullOrWhiteSpace(_ctx.PlateNumber))

                PlateNumberBox.Text = _ctx.PlateNumber;

            if (!string.IsNullOrWhiteSpace(_ctx.Year))

                YearBox.Text = _ctx.Year;

            if (!string.IsNullOrWhiteSpace(_ctx.Mileage))

                MileageBox.Text = _ctx.Mileage;

            if (!string.IsNullOrWhiteSpace(_ctx.Color))

                ColorBox.Text = _ctx.Color;

            if (!string.IsNullOrWhiteSpace(_ctx.VisitReason))
                VisitReasonBox.Text = _ctx.VisitReason;
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>

            AppState.Navigate(new WorkshopClientLookupPage(_ctx));



        private async void DownloadEmpty_Click(object sender, RoutedEventArgs e)

        {

            var path = AskSavePath("Zakaz-naryad-pustoy");

            if (path == null)

                return;



            try

            {

                var model = BuildModelFromForm(includeClientAndCar: false);

                await RunOnBackgroundAsync(() => RepairWorkOrderPrintService.GenerateEmptyForPrint(model, path)).ConfigureAwait(true);

                MessageBox.Show("Пустой заказ-наряд сохранён.", TitleText.Text, MessageBoxButton.OK, MessageBoxImage.Information);

                RepairWorkOrderPrintService.OpenDocument(path);

            }

            catch (Exception ex)

            {

                MessageBox.Show("Не удалось сформировать документ: " + ex.Message, TitleText.Text, MessageBoxButton.OK, MessageBoxImage.Warning);

            }

        }



        private async void Save_Click(object sender, RoutedEventArgs e)

        {

            SyncContextFromForm();
            EnsureClientPath();

            if (string.IsNullOrWhiteSpace(_ctx.ClientEmail) && !string.IsNullOrWhiteSpace(_ctx.SearchEmail))
                _ctx.ClientEmail = _ctx.SearchEmail;

            if (string.IsNullOrWhiteSpace(_ctx.ClientFullName))

            {

                MessageBox.Show("Укажите имя клиента.", TitleText.Text, MessageBoxButton.OK, MessageBoxImage.Information);

                return;

            }



            if (string.IsNullOrWhiteSpace(_ctx.CarDescription) && _ctx.ClientPath != ServiceClientPath.ExistingUserWithSelectedCar)

            {

                MessageBox.Show("Укажите автомобиль.", TitleText.Text, MessageBoxButton.OK, MessageBoxImage.Information);

                return;

            }



            if (string.IsNullOrWhiteSpace(VisitReasonBox.Text))
            {
                MessageBox.Show("Укажите причину обращения.", TitleText.Text,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            BtnSave.IsEnabled = false;

            try

            {

                var save = await ServiceBookingPersistenceService.SaveBookingAsync(_ctx).ConfigureAwait(true);

                if (!save.Success)

                {

                    MessageBox.Show(save.ErrorMessage, TitleText.Text, MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;

                }

                _ctx.CreatedRepairHistoryId = save.RepairHistoryId;
                _ctx.CreatedTaskId = save.TaskId;

                var msg = "Запись сохранена в базу.\nЗадание создано и назначено вам — см. список заданий на главной.";
                if (save.TaskId.HasValue)
                    msg += "\n\nОткрыть карточку задания?";

                if (save.TaskId.HasValue &&
                    MessageBox.Show(msg, TitleText.Text, MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    AppState.Navigate(new EmployeeTaskCardPage(save.TaskId.Value));
                    return;
                }

                MessageBox.Show("Запись сохранена. Задание добавлено в ваш список.", TitleText.Text,
                    MessageBoxButton.OK, MessageBoxImage.Information);

                AppState.Navigate(new ProHomePage());

            }

            catch (Exception ex)

            {

                MessageBox.Show("Ошибка: " + ex.Message, TitleText.Text, MessageBoxButton.OK, MessageBoxImage.Warning);

            }

            finally

            {

                BtnSave.IsEnabled = true;

            }

        }



        private void EnsureClientPath()
        {
            if (_ctx.ClientPath != ServiceClientPath.Unknown)
                return;

            if (_ctx.SelectedCarId.HasValue)
                _ctx.ClientPath = ServiceClientPath.ExistingUserWithSelectedCar;
            else if (_ctx.FoundUser != null)
                _ctx.ClientPath = ServiceClientPath.ExistingUserGuestCar;
            else
                _ctx.ClientPath = ServiceClientPath.ManualGuest;
        }

        private void SyncContextFromForm()

        {

            _ctx.ClientFullName = ClientNameBox.Text?.Trim() ?? _ctx.ClientFullName;

            _ctx.ClientPhone = ClientPhoneBox.Text?.Trim() ?? string.Empty;

            _ctx.ClientAddress = ClientAddressBox.Text?.Trim() ?? string.Empty;

            _ctx.CarDescription = CarDescriptionBox.Text?.Trim() ?? string.Empty;

            _ctx.Vin = VinBox.Text?.Trim() ?? string.Empty;

            _ctx.PlateNumber = PlateNumberBox.Text?.Trim() ?? string.Empty;

            _ctx.Year = YearBox.Text?.Trim() ?? string.Empty;

            _ctx.Mileage = MileageBox.Text?.Trim() ?? string.Empty;

            _ctx.Color = ColorBox.Text?.Trim() ?? string.Empty;

            _ctx.VisitReason = VisitReasonBox.Text?.Trim() ?? string.Empty;
            _ctx.SpecialNotes = SpecialNotesBox.Text?.Trim() ?? string.Empty;

        }



        private RepairWorkOrderModel BuildModelFromForm(bool includeClientAndCar)

        {

            var model = new RepairWorkOrderModel

            {

                CompanyName = CompanyNameBox.Text?.Trim() ?? _companyContext.CompanyName,

                CompanyLegalAddress = CompanyAddressBox.Text?.Trim() ?? _companyContext.CompanyLegalAddress,

                CompanyPhone = CompanyPhoneBox.Text?.Trim() ?? string.Empty,

                OrderDate = DateTime.Now.ToString("dd.MM.yyyy"),

                OrderTime = DateTime.Now.ToString("HH:mm"),

                RepairType = _ctx.RepairTypeDisplay

            };



            if (!includeClientAndCar)

                return model;



            model.ClientName = ClientNameBox.Text?.Trim() ?? string.Empty;

            model.ClientAddress = ClientAddressBox.Text?.Trim() ?? string.Empty;

            model.ClientPhone = ClientPhoneBox.Text?.Trim() ?? string.Empty;

            model.CarDescription = CarDescriptionBox.Text?.Trim() ?? string.Empty;

            model.Vin = VinBox.Text?.Trim() ?? string.Empty;

            model.PlateNumber = PlateNumberBox.Text?.Trim() ?? string.Empty;

            model.Year = YearBox.Text?.Trim() ?? string.Empty;

            model.Mileage = MileageBox.Text?.Trim() ?? string.Empty;

            model.Color = ColorBox.Text?.Trim() ?? string.Empty;

            model.VisitReason = VisitReasonBox.Text?.Trim() ?? string.Empty;
            model.SpecialNotes = SpecialNotesBox.Text?.Trim() ?? string.Empty;
            model.WorkLines = null;

            return model;

        }



        private static string AskSavePath(string defaultBaseName)

        {

            var dlg = new SaveFileDialog

            {

                Filter = "Документ Word (*.docx)|*.docx",

                FileName = defaultBaseName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".docx",

                AddExtension = true,

                DefaultExt = ".docx"

            };



            return dlg.ShowDialog() == true ? dlg.FileName : null;

        }



        private static async Task RunOnBackgroundAsync(Action action)

        {

            await Task.Run(action).ConfigureAwait(false);

        }

    }

}


