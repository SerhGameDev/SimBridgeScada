using SimBridge_Scada.Infrastructure;
namespace SimBridge_Scada.ViewModels;
public sealed class ElementLibraryItemViewModel
{
    public string Title { get; }
    public string? IconPath { get; }
    public RelayCommand AddCommand { get; }
    public ElementLibraryItemViewModel(string title, Func<SchematicElementViewModel> factory, WorkspaceManagerViewModel manager, string? iconPath = null)
    {
        Title = title; IconPath = iconPath;
        AddCommand = new(() =>
        {
            if (manager.ActiveViewModel is not { } workspace) return;
            var offset = 80 + workspace.Elements.Count % 10 * 40;
            workspace.AddElement(factory(), offset, offset);
        }, () => manager.ActiveWorkspace is not null);
        manager.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(manager.ActiveWorkspace)) AddCommand.NotifyCanExecuteChanged(); };
    }
}
