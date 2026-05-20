using OxyPlot;
using OxyPlot.Axes;
using System;
using System.Windows;
using System.Windows.Media;

namespace DriveCare.Services
{
    /// <summary>Палитра OxyPlot в цветах App.* из активной темы.</summary>
    public sealed class OxyPlotPalette
    {
        public OxyColor PlotAreaBackground { get; set; }
        public OxyColor PlotBorder { get; set; }
        public OxyColor Label { get; set; }
        public OxyColor Title { get; set; }
        public OxyColor Grid { get; set; }
        public OxyColor LinePrimary { get; set; }
        public OxyColor LineTrend { get; set; }
        public OxyColor MarkerStroke { get; set; }
    }

    public static class OxyPlotThemeHelper
    {
        public static OxyPlotPalette GetCurrentPalette()
        {
            var plotBg = BrushToOxy("App.Brush.Surface2");
            var border = BrushToOxy("App.Brush.Border");
            var label = BrushToOxy("App.Brush.Muted");
            var title = BrushToOxy("App.Brush.Foreground");
            var accent = BrushToOxy("App.Brush.Accent");
            var markerStroke = ThemeService.Current == AppUiTheme.Dark
                ? BrushToOxy("App.Brush.SurfaceCard")
                : OxyColors.White;

            return new OxyPlotPalette
            {
                PlotAreaBackground = plotBg,
                PlotBorder = border,
                Label = label,
                Title = title,
                Grid = OxyColor.FromAColor(80, border),
                LinePrimary = accent,
                LineTrend = ThemeService.Current == AppUiTheme.Dark
                    ? OxyColor.FromRgb(147, 112, 219)
                    : OxyColor.FromRgb(99, 102, 241),
                MarkerStroke = markerStroke
            };
        }

        public static void ApplyToPlotModel(PlotModel model, OxyPlotPalette palette)
        {
            if (model == null || palette == null)
                return;

            model.Background = OxyColors.Transparent;
            model.PlotAreaBackground = palette.PlotAreaBackground;
            model.PlotAreaBorderColor = palette.PlotBorder;
            model.TextColor = palette.Title;
        }

        public static void ApplyToAxis(Axis axis, OxyPlotPalette palette)
        {
            if (axis == null || palette == null)
                return;

            axis.MajorGridlineColor = palette.Grid;
            axis.TicklineColor = palette.Label;
            axis.TextColor = palette.Label;
            axis.TitleColor = palette.Title;
        }

        private static OxyColor BrushToOxy(string resourceKey)
        {
            try
            {
                if (Application.Current?.TryFindResource(resourceKey) is SolidColorBrush brush)
                {
                    var c = brush.Color;
                    return OxyColor.FromArgb(c.A, c.R, c.G, c.B);
                }
            }
            catch
            {
            }

            return ThemeService.Current == AppUiTheme.Dark
                ? OxyColor.FromRgb(26, 26, 40)
                : OxyColor.FromRgb(232, 238, 245);
        }
    }
}
