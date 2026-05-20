using DriveCareCore.Maps;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace DriveCare.Controls
{
    public partial class YandexWorkshopMapView : UserControl
    {
        bool _coreReady;
        bool _webMessageHooked;

        public event EventHandler<Guid> WorkshopSelected;

        public YandexWorkshopMapView()
        {
            InitializeComponent();
            Loaded += async (_, __) => await EnsureBrowserAsync().ConfigureAwait(true);
        }

        public async Task EnsureBrowserAsync()
        {
            if (_coreReady)
                return;

            try
            {
                await MapWebView.EnsureCoreWebView2Async().ConfigureAwait(true);
                _coreReady = true;
                ErrorText.Visibility = Visibility.Collapsed;

                if (!_webMessageHooked)
                {
                    MapWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                    _webMessageHooked = true;
                }

                MapWebView.CoreWebView2.NavigationCompleted += (_, __) => HideLoading();
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Нужен Microsoft Edge WebView2 Runtime.\nСкачайте с сайта Microsoft и перезапустите приложение.\n\n" + ex.Message;
                ErrorText.Visibility = Visibility.Visible;
                HideLoading();
            }
        }

        public async Task LoadPinsAsync(IReadOnlyList<WorkshopMapPin> pins)
        {
            ShowLoading();
            await EnsureBrowserAsync().ConfigureAwait(true);
            if (!_coreReady)
                return;

            var html = YandexWorkshopMapHtmlBuilder.Build(pins, YandexMapsConfig.ApiKey);
            MapWebView.CoreWebView2.NavigateToString(html);
        }

        public async Task FocusWorkshopAsync(Guid workshopId)
        {
            if (!_coreReady || workshopId == Guid.Empty)
                return;

            var id = workshopId.ToString("D");
            var script = "if (window.driveCareFocusWorkshop) driveCareFocusWorkshop('" + id + "');";
            try
            {
                await MapWebView.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
            }
            catch
            {
            }
        }

        void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.TryGetWebMessageAsString();
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var obj = JObject.Parse(json);
                if ((string)obj["type"] != "select")
                    return;

                var idText = (string)obj["workshopId"];
                if (Guid.TryParse(idText, out var workshopId) && workshopId != Guid.Empty)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        WorkshopSelected?.Invoke(this, workshopId);
                    }));
                }
            }
            catch
            {
            }
        }

        void ShowLoading()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingOverlay.IsHitTestVisible = true;
        }

        void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingOverlay.IsHitTestVisible = false;
        }
    }
}
