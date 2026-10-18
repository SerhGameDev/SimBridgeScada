namespace SimBridge_Scada.ViewModels.Components;

public sealed class ObjectPropertiesComponentViewModel : ComponentViewModel
{
    private string _name = "Объект";
    private bool _isEnabled = true, _isVisible = true;
    public override string Title => "Объект";
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
    public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
}
