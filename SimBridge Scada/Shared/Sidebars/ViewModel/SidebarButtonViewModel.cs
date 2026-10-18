using SimBridge_Scada.Infrastructure;
namespace SimBridge_Scada.ViewModels;
public sealed class SidebarButtonViewModel : ObservableObject
{
    private bool _isActive;
    private string? _iconPath;
    private SidebarViewModel? _owner;
    private readonly Action? _action;
    public string Title { get; }
    public string? IconPath { get => _iconPath; set => SetProperty(ref _iconPath, value); }
    public ToolPanelViewModel? Panel { get; }
    public bool IsActive { get => _isActive; internal set => SetProperty(ref _isActive, value); }
    public RelayCommand ClickCommand { get; }
    public SidebarButtonViewModel(string title, string? iconPath, ToolPanelViewModel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);
        Title = title; _iconPath = iconPath; Panel = panel;
        ClickCommand = new(() => _owner?.Toggle(this));
    }
    public SidebarButtonViewModel(string title, string? iconPath, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Title = title; _iconPath = iconPath; _action = action;
        ClickCommand = new(() => _action());
    }
    internal void Initialize(SidebarViewModel owner)
    {
        if (_owner is not null && _owner != owner) throw new InvalidOperationException("Button already belongs to another sidebar.");
        _owner = owner;
    }
}


