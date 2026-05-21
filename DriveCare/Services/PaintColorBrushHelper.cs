using System;
using System.Windows.Media;

namespace DriveCare.Services
{
    /// <summary>Приблизительный цвет по названию из справочника (для превью на экране покраски).</summary>
    public static class PaintColorBrushHelper
    {
        public static Brush BrushFromColorName(string colorName)
        {
            var c = ResolveColor(colorName);
            var brush = new SolidColorBrush(c);
            if (brush.CanFreeze)
                brush.Freeze();
            return brush;
        }

        public static Brush ContrastBorderBrushFor(Color fill)
        {
            var lum = (0.299 * fill.R + 0.587 * fill.G + 0.114 * fill.B) / 255.0;
            var border = lum > 0.62
                ? Color.FromRgb(0x33, 0x41, 0x55)
                : Color.FromRgb(0xF8, 0xFA, 0xFC);
            var brush = new SolidColorBrush(border);
            if (brush.CanFreeze)
                brush.Freeze();
            return brush;
        }

        public static Color ResolveColor(string colorName)
        {
            var key = (colorName ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0 || key == "—" || key == "не указан")
                return Color.FromRgb(0x94, 0xA3, 0xB8);

            if (Contains(key, "черн", "black", "graphite", "антрац", "night"))
                return Color.FromRgb(0x1E, 0x29, 0x3B);
            if (Contains(key, "бел", "white", "снеж", "pearl", "перл", "ivory", "слон"))
                return Color.FromRgb(0xF8, 0xFA, 0xFC);
            if (Contains(key, "сер", "silver", "сереб", "grey", "gray", "сталь", "metal", "металл", "титан"))
                return Color.FromRgb(0x94, 0xA3, 0xB8);
            if (Contains(key, "крас", "red", "борд", "вишн", "ruby", "cherry", "коралл", "coral"))
                return Color.FromRgb(0xDC, 0x26, 0x26);
            if (Contains(key, "син", "blue", "navy", "голуб", "azure", "indigo", "лазур"))
                return Color.FromRgb(0x25, 0x63, 0xEB);
            if (Contains(key, "зел", "green", "изумруд", "olive", "хаки"))
                return Color.FromRgb(0x16, 0xA3, 0x4A);
            if (Contains(key, "желт", "yellow", "gold", "золот", "песоч", "беж", "sand"))
                return Color.FromRgb(0xCA, 0x8A, 0x04);
            if (Contains(key, "оранж", "orange", "мед", "copper", "бронз", "террак"))
                return Color.FromRgb(0xEA, 0x58, 0x0C);
            if (Contains(key, "фиол", "purple", "violet", "сирен", "plum"))
                return Color.FromRgb(0x7C, 0x3A, 0xED);
            if (Contains(key, "роз", "pink", "magenta", "фукс", "малин"))
                return Color.FromRgb(0xDB, 0x27, 0x77);
            if (Contains(key, "корич", "brown", "шокол", "кофе", "мокко", "каштан", "шоколад"))
                return Color.FromRgb(0x78, 0x4A, 0x2E);
            if (Contains(key, "бирюз", "teal", "cyan", "мят"))
                return Color.FromRgb(0x0D, 0x94, 0x88);

            return Color.FromRgb(0x64, 0x74, 0x8B);
        }

        private static bool Contains(string haystack, params string[] needles)
        {
            foreach (var n in needles)
            {
                if (haystack.IndexOf(n, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }
    }
}
