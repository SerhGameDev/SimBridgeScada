using System.Collections.ObjectModel;
using SimBridge_Scada.Infrastructure;
namespace SimBridge_Scada.ViewModels;

/// <summary>Initializes configured buttons; Render publishes them to WPF. One active tool per sidebar.</summary>
public abstract class SidebarViewModel : ObservableObject
{
    private readonly SidebarButtonViewModel[] _configured;
    private readonly ObservableCollection<SidebarButtonViewModel> _buttons = [];
    private SidebarButtonViewModel? _activeButton;
    private double _width = 300;
    public ReadOnlyObservableCollection<SidebarButtonViewModel> Buttons { get; }
    public ToolPanelViewModel? ActivePanel => _activeButton?.Panel;
    public bool IsPanelOpen => ActivePanel is not null;
    public double Width { get => IsPanelOpen ? _width : 0; set { if (double.IsFinite(value) && value > 0) { _width = Math.Clamp(value, 240, 720); OnPropertyChanged(); } } }
    protected SidebarViewModel(IEnumerable<SidebarButtonViewModel> buttons)
    {
        _configured = buttons.ToArray();
        if (_configured.Distinct().Count() != _configured.Length) throw new ArgumentException("Duplicate sidebar buttons.", nameof(buttons));
        Buttons = new(_buttons);
        foreach (var button in _configured) button.Initialize(this);
        Render();
    }
    public void Render()
    {
        _buttons.Clear();
        foreach (var button in _configured) _buttons.Add(button);
    }
    internal void Toggle(SidebarButtonViewModel button)
    {
        if (!_buttons.Contains(button) || button.Panel is null) return;
        var previous = _activeButton;
        if (previous is not null) previous.IsActive = false;
        _activeButton = previous == button ? null : button;
        if (_activeButton is not null) _activeButton.IsActive = true;
        OnPropertyChanged(nameof(ActivePanel)); OnPropertyChanged(nameof(IsPanelOpen)); OnPropertyChanged(nameof(Width));
    }
}

