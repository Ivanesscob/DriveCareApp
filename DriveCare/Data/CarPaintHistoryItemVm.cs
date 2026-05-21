using DriveCare.Services;
using DriveCareCore.Painting;
using System;
using System.Globalization;
using System.Windows.Media;

namespace DriveCare.Data
{
    public sealed class CarPaintHistoryItemVm
    {
        public DateTime StartDate { get; set; }
        public string KindTitle { get; set; }
        public string KindSubtitle { get; set; }
        public string ColorName { get; set; }
        public string PartLine { get; set; }
        public string NotesLine { get; set; }
        public string DateDisplay { get; set; }
        public bool HasPartLine { get; set; }
        public bool HasNotesLine { get; set; }
        public Brush ColorSwatchBrush { get; set; }
        public Brush ColorSwatchBorderBrush { get; set; }
        public Brush KindAccentBrush { get; set; }

        public static CarPaintHistoryItemVm From(CarPaintHistoryItem item)
        {
            var culture = CultureInfo.GetCultureInfo("ru-RU");
            var kind = item.PaintKind;
            var kindTitle = kind.HasValue
                ? CarPaintService.GetKindTitle(kind.Value)
                : "Смена цвета";

            var kindSubtitle = kind.HasValue
                ? GetKindSubtitle(kind.Value)
                : "Запись в журнале";

            var part = (item.PartName ?? string.Empty).Trim();
            var notes = (item.Description ?? string.Empty).Trim();
            var colorName = item.ColorName ?? "—";
            var color = PaintColorBrushHelper.ResolveColor(colorName);

            return new CarPaintHistoryItemVm
            {
                StartDate = item.StartDate,
                KindTitle = kindTitle,
                KindSubtitle = kindSubtitle,
                ColorName = colorName,
                PartLine = string.IsNullOrEmpty(part) ? null : part,
                NotesLine = string.IsNullOrEmpty(notes) ? null : notes,
                DateDisplay = item.StartDate.ToLocalTime().ToString("d MMM yyyy · HH:mm", culture),
                HasPartLine = !string.IsNullOrEmpty(part),
                HasNotesLine = !string.IsNullOrEmpty(notes),
                ColorSwatchBrush = PaintColorBrushHelper.BrushFromColorName(colorName),
                ColorSwatchBorderBrush = PaintColorBrushHelper.ContrastBorderBrushFor(color),
                KindAccentBrush = GetKindAccentBrush(kind)
            };
        }

        private static string GetKindSubtitle(CarPaintKind kind)
        {
            switch (kind)
            {
                case CarPaintKind.Wheels: return "Колёса и диски";
                case CarPaintKind.FullCar: return "Кузов целиком";
                case CarPaintKind.Part: return "Локальная работа";
                default: return string.Empty;
            }
        }

        private static Brush GetKindAccentBrush(CarPaintKind? kind)
        {
            Color c;
            switch (kind)
            {
                case CarPaintKind.Wheels:
                    c = Color.FromRgb(0x0E, 0x94, 0x88);
                    break;
                case CarPaintKind.FullCar:
                    c = Color.FromRgb(0x25, 0x63, 0xEB);
                    break;
                case CarPaintKind.Part:
                    c = Color.FromRgb(0xEA, 0x58, 0x0C);
                    break;
                default:
                    c = Color.FromRgb(0x64, 0x74, 0x8B);
                    break;
            }

            var brush = new SolidColorBrush(c);
            if (brush.CanFreeze)
                brush.Freeze();
            return brush;
        }
    }
}
