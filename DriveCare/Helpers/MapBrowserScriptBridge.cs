using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace DriveCare.Helpers
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public sealed class MapBrowserScriptBridge
    {
        readonly Action<string> _onSelectWorkshop;

        public MapBrowserScriptBridge(Action<string> onSelectWorkshop)
        {
            _onSelectWorkshop = onSelectWorkshop;
        }

        public void SelectWorkshop(string workshopId)
        {
            if (string.IsNullOrWhiteSpace(workshopId))
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                _onSelectWorkshop?.Invoke(workshopId);
                return;
            }

            dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                _onSelectWorkshop?.Invoke(workshopId);
            }));
        }
    }
}
