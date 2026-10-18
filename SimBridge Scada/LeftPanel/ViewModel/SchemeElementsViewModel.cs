namespace SimBridge_Scada.ViewModels;
public sealed class SchemeElementsViewModel : ToolPanelViewModel
{
    private readonly WorkspaceManagerViewModel _manager;
    public WorkspaceViewModel? Workspace => _manager.ActiveViewModel;
    public SchemeElementsViewModel(WorkspaceManagerViewModel manager) : base("Элементы схемы")
    {
        _manager = manager;
        manager.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(manager.ActiveViewModel)) OnPropertyChanged(nameof(Workspace)); };
    }
}
