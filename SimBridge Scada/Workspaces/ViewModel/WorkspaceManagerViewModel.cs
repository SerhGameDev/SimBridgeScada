using SimBridge_Scada.Shared.FileDrop;
using System.Collections.ObjectModel;
using SimBridge_Scada.Infrastructure;
namespace SimBridge_Scada.ViewModels;

public sealed class WorkspaceManagerViewModel : ObservableObject, IFileDropTarget
{
    private readonly ObservableCollection<SchematicWorkspace> _all = [], _open = [];
    private readonly Func<string, SchematicWorkspace> _factory;
    private SchematicWorkspace? _activeWorkspace;
    private int _nextNumber = 1;
    public ReadOnlyObservableCollection<SchematicWorkspace> AllWorkspaces { get; }
    public ReadOnlyObservableCollection<SchematicWorkspace> OpenWorkspaces { get; }
    public RelayCommand CreateWorkspaceCommand { get; }
    public Action<IReadOnlyList<string>>? FileImporter { get; set; }
    public void OpenFiles(IReadOnlyList<string> paths) => FileImporter?.Invoke(paths);
    public WorkspaceViewModel? ActiveViewModel => ActiveWorkspace?.ViewModel;
    public bool HasOpenWorkspaces => _open.Count > 0;
    public SchematicWorkspace? ActiveWorkspace
    {
        get => _activeWorkspace;
        set
        {
            if (value is not null && !_open.Contains(value)) throw new ArgumentException("Workspace must be opened first.", nameof(value));
            if (_activeWorkspace == value) return;
            if (_activeWorkspace is not null) _activeWorkspace.IsActive = false;
            SetProperty(ref _activeWorkspace, value);
            if (value is not null) value.IsActive = true;
            OnPropertyChanged(nameof(ActiveViewModel));
        }
    }
    public WorkspaceManagerViewModel(Func<string, SchematicWorkspace>? factory = null)
    {
        _factory = factory ?? (title => new SchematicWorkspace(title));
        AllWorkspaces = new(_all); OpenWorkspaces = new(_open);
        CreateWorkspaceCommand = new(() => CreateWorkspace());
    }
    public SchematicWorkspace CreateWorkspace(string? title = null, bool open = true)
    {
        if (title is null)
        {
            do { title = $"Мнемосхема {_nextNumber++:00}"; } while (_all.Any(w => w.Title == title));
        }
        return Add(_factory(title), open);
    }
    public SchematicWorkspace Add(SchematicWorkspace workspace, bool open = true)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (_all.Any(w => w.Id == workspace.Id)) throw new InvalidOperationException("Workspace with this ID is already registered.");
        workspace.Attach(this); _all.Add(workspace);
        if (open) Open(workspace);
        return workspace;
    }
    public void Open(SchematicWorkspace workspace)
    {
        if (!_all.Contains(workspace)) throw new ArgumentException("Unknown workspace.", nameof(workspace));
        if (!_open.Contains(workspace)) { _open.Add(workspace); workspace.IsOpen = true; OnPropertyChanged(nameof(HasOpenWorkspaces)); }
        ActiveWorkspace = workspace;
    }
    // Closing only hides the tab. The document remains in AllWorkspaces, including its elements and viewport.
    public void Close(SchematicWorkspace workspace)
    {
        var index = _open.IndexOf(workspace);
        if (index < 0) return;
        var wasActive = ActiveWorkspace == workspace;
        _open.RemoveAt(index); workspace.IsOpen = false;
        if (wasActive) ActiveWorkspace = _open.Count == 0 ? null : _open[Math.Min(index, _open.Count - 1)];
        OnPropertyChanged(nameof(HasOpenWorkspaces));
    }
    public bool Remove(SchematicWorkspace workspace)
    {
        if (!_all.Contains(workspace)) return false;
        Close(workspace); _all.Remove(workspace); workspace.Detach(this); return true;
    }
    /// <summary>Discard the previous project's catalog, selection and tab state.</summary>
    public void Reset()
    {
        ActiveWorkspace=null;
        foreach(var workspace in _all.ToArray())
        {
            workspace.ViewModel.SelectedElement=null;
            Remove(workspace);
        }
        _nextNumber=1;
    }
    public void MoveTab(SchematicWorkspace workspace, int index)
    {
        var oldIndex = _open.IndexOf(workspace);
        if (oldIndex < 0 || _open.Count < 2) return;
        _open.Move(oldIndex, Math.Clamp(index, 0, _open.Count - 1));
    }
}



