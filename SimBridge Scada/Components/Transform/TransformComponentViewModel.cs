namespace SimBridge_Scada.ViewModels.Components;

public sealed class TransformComponentViewModel : ComponentViewModel
{
    private double _x, _y, _width = 144, _height = 120, _rotation;
    private bool _isLocked;
    public override string Title => "Transform";
    public double X { get => _x; set => SetProperty(ref _x, double.IsFinite(value) ? value : 0); }
    public double Y { get => _y; set => SetProperty(ref _y, double.IsFinite(value) ? value : 0); }
    public double Width { get => _width; set => SetProperty(ref _width, Finite(value, 64)); }
    public double Height { get => _height; set => SetProperty(ref _height, Finite(value, 64)); }
    public double Rotation { get => _rotation; set => SetProperty(ref _rotation, double.IsFinite(value) ? value % 360 : 0); }
    public bool IsLocked { get => _isLocked; set => SetProperty(ref _isLocked, value); }
    private static double Finite(double value, double minimum) => double.IsFinite(value) ? Math.Max(minimum, value) : minimum;
}

