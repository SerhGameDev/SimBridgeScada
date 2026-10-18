using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SimBridge_Scada.Infrastructure;

/// <summary>Loads optional local icons; absent or unreadable files use a white image.</summary>
public sealed class IconPathConverter : IValueConverter
{
    private static readonly ImageSource Fallback = CreateFallback();
    private static ImageSource CreateFallback()
    {
        var image = new DrawingImage(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(2, 2, 16, 16), 2, 2)));
        image.Freeze(); return image;
    }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path)) return Fallback;
        try
        {
            path = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
            if (!File.Exists(path)) return Fallback;
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute); bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FormatException or System.Security.SecurityException)
        { return Fallback; }
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
