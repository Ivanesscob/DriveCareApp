using DriveCarePro.Pages;

namespace DriveCarePro
{
    internal static class ProNavigation
    {
        public static void GoHome() => AppState.SetFrame<ProHomePage>();
    }
}
