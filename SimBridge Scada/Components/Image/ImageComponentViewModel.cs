using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SimBridge_Scada.ViewModels.Components;
public sealed class ImageComponentViewModel : ComponentViewModel
{
    private string _imagePath = "";
    private double _opacity = 1;
    public override string Title => "Изображение";
    public string ImagePath { get => _imagePath; set { if (SetProperty(ref _imagePath, value)) { OnPropertyChanged(nameof(Source)); OnPropertyChanged(nameof(Status)); } } }
    public double Opacity { get => _opacity; set => SetProperty(ref _opacity, double.IsFinite(value) ? Math.Clamp(value,0,1) : 1); }
    public ImageSource? Source
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ImagePath)) return null;
            try
            {
                var path = ResolvePath();
                if (!File.Exists(path)) return null;
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.UriSource = new Uri(path); image.EndInit(); image.Freeze(); return image;
            }
            catch (Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or FormatException) { return null; }
        }
    }
    public string ResolvePath()
    {
        if(Path.IsPathRooted(ImagePath)) return Path.GetFullPath(ImagePath);
        var document=Owner?.OwnerWorkspace?.Document;
        if(document?.WorkingDirectory is { } working)
        {
            var local=Path.GetFullPath(Path.Combine(working,ImagePath));
            if(File.Exists(local)) return local;
        }
        var directory=Path.GetDirectoryName(document?.FilePath??document?.ProjectPath??"")??AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(directory,ImagePath));
    }
    public override void RefreshContext() { OnPropertyChanged(nameof(Source)); OnPropertyChanged(nameof(Status)); }
    public string Status => string.IsNullOrWhiteSpace(ImagePath) ? "Выберите изображение" : Source is null ? "Изображение недоступно" : "";
}


