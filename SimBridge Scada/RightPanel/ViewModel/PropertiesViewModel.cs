using SimBridge_Scada.Shared.Menus;
using SimBridge_Scada.ViewModels.Components;
namespace SimBridge_Scada.ViewModels;
public sealed record ComponentDescriptor(string Title, Func<ComponentViewModel> Factory);
public sealed class PropertiesViewModel : ToolPanelViewModel
{
    private readonly WorkspaceManagerViewModel _manager;
    public WorkspaceViewModel? Workspace => _manager.ActiveViewModel;
    public MenuButtonViewModel AddComponentMenu { get; }
    public PropertiesViewModel(WorkspaceManagerViewModel manager, IEnumerable<ComponentDescriptor>? components = null) : base("Inspector")
    {
        _manager = manager;
        var catalog = (components ?? []).ToArray();
        AddComponentMenu = new(() => new ActionMenu(catalog.Select(entry => new ActionMenuItem(entry.Title,
            () => Workspace?.SelectedElement?.AddComponent(entry.Factory()), () => Workspace?.SelectedElement is not null))));
        manager.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(manager.ActiveViewModel)) OnPropertyChanged(nameof(Workspace)); };
    }
}
