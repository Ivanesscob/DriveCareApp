using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace DriveCarePro.Services
{
    public static class ModerationBadgeHelper
    {
        public static void Apply(Border badge, TextBlock text, int count)
        {
            if (badge == null || text == null)
                return;

            if (count <= 0)
            {
                badge.Visibility = Visibility.Collapsed;
                return;
            }

            text.Text = count > 99 ? "99+" : count.ToString();
            badge.Visibility = Visibility.Visible;

            if (badge.Effect == null)
            {
                badge.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(229, 57, 53),
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.85
                };
            }
        }
    }
}
