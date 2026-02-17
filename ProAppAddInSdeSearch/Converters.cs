using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ProAppAddInSdeSearch
{
    /// <summary>
    /// Converts GeometryIconType string to WPF Path geometry for vector icons.
    /// Point = circle, Polyline = zigzag line, Polygon = hexagon,
    /// Table = grid, Dataset = folder, Relationship = chain link
    /// </summary>
    public class GeometryIconPathConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> _paths = new()
        {
            // Circle for points
            ["Point"] = "M10,2 A8,8 0 1,1 10,18 A8,8 0 1,1 10,2 Z",

            // Zigzag polyline
            ["Polyline"] = "M2,16 L7,4 L13,14 L18,2",

            // Hexagon for polygons
            ["Polygon"] = "M10,1 L18,5.5 L18,14.5 L10,19 L2,14.5 L2,5.5 Z",

            // 3D box for multipatch
            ["Multipatch"] = "M10,1 L18,5 L18,14 L10,18 L2,14 L2,5 Z M10,1 L10,10 M10,10 L18,5 M10,10 L2,5",

            // Grid/table icon
            ["Table"] = "M2,3 L18,3 L18,17 L2,17 Z M2,8 L18,8 M2,13 L18,13 M9,3 L9,17",

            // Folder icon
            ["Dataset"] = "M1,5 L8,5 L10,3 L18,3 L18,17 L1,17 Z",

            // Chain link
            ["Relationship"] = "M5,8 A3,3 0 1,1 5,12 M15,8 A3,3 0 1,0 15,12 M7,10 L13,10",

            // Question mark
            ["Unknown"] = "M10,2 A8,8 0 1,1 10,18 A8,8 0 1,1 10,2 Z M8,7 Q8,4 10,4 Q12,4 12,7 Q12,9 10,10 L10,12 M10,14 L10,15"
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = value as string ?? "Unknown";
            if (!_paths.TryGetValue(key, out var path)) path = _paths["Unknown"];

            try { return Geometry.Parse(path); }
            catch { return Geometry.Parse(_paths["Unknown"]); }
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    /// <summary>
    /// Maps GeometryIconType to a distinct color for each dataset type.
    /// </summary>
    public class GeometryIconColorConverter : IValueConverter
    {
        private static readonly Dictionary<string, Color> _colors = new()
        {
            ["Point"]        = Color.FromRgb(0x4C, 0xAF, 0x50), // Green
            ["Polyline"]     = Color.FromRgb(0x21, 0x96, 0xF3), // Blue
            ["Polygon"]      = Color.FromRgb(0xFF, 0x98, 0x00), // Orange
            ["Multipatch"]   = Color.FromRgb(0x9C, 0x27, 0xB0), // Purple
            ["Table"]        = Color.FromRgb(0x78, 0x90, 0x9C), // Blue-gray
            ["Dataset"]      = Color.FromRgb(0xFF, 0xC1, 0x07), // Amber
            ["Relationship"] = Color.FromRgb(0x8D, 0x6E, 0x63), // Brown
            ["Unknown"]      = Color.FromRgb(0x9E, 0x9E, 0x9E), // Gray
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = value as string ?? "Unknown";
            if (!_colors.TryGetValue(key, out var color)) color = _colors["Unknown"];
            return new SolidColorBrush(color);
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) => v is bool b ? !b : v;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => v is bool b ? !b : v;
    }

    public class NonEmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) =>
            string.IsNullOrWhiteSpace(v as string) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}
