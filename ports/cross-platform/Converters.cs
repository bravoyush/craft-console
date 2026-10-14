using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CraftConsole.CrossPlatform
{
    public static class Converters
    {
        public static readonly IValueConverter FavoriteStarConverter = new FuncValueConverter<bool, string>(b => b ? "★" : "☆");
        public static readonly IValueConverter FavoriteColorConverter = new FuncValueConverter<bool, IBrush>(b => b ? new SolidColorBrush(Color.Parse("#FFD700")) : new SolidColorBrush(Color.Parse("#5F5F69")));
    }

    public class CategoryIconConverter : IValueConverter
    {
        public static readonly CategoryIconConverter Instance = new CategoryIconConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string cat = (value as string ?? "").Trim().ToLowerInvariant();
            if (cat.Contains("blur")) return "BL";
            if (cat.Contains("color") || cat.Contains("curves") || cat.Contains("levels")) return "CC";
            if (cat.Contains("distort") || cat.Contains("warp")) return "DS";
            if (cat.Contains("stylize") || cat.Contains("glow")) return "ST";
            if (cat.Contains("generate") || cat.Contains("render")) return "GN";
            if (cat.Contains("matte") || cat.Contains("key")) return "KY";
            if (cat.Contains("noise") || cat.Contains("grain")) return "NS";
            if (cat.Contains("layer") || cat.Contains("solid") || cat.Contains("null")) return "LY";
            if (cat.Contains("preset") || cat.Contains("text")) return "PR";
            if (cat.Contains("command") || cat.Contains("edit") || cat.Contains("file")) return "⌘";
            return "FX";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
