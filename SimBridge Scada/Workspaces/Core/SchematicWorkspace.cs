using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using SimBridge_Scada.Infrastructure;
using SimBridge_Scada.Shared.Menus;
using SimBridge_Scada.ViewModels.Components;
namespace SimBridge_Scada.ViewModels;

public sealed record WorkspaceShortcut(KeyGesture Gesture, ICommand Command, string Description);

public sealed class SchematicWorkspace : ObservableObject, ILeftClickMenuProvider
{
    private string _title;
    private bool _isOpen, _isActive, _isDirty = true;
    private string? _filePath, _projectPath, _workingDirectory;
    private WorkspaceManagerViewModel? _manager;
    private Action<SchematicWorkspace>? _save, _delete;
    private Func<SchematicWorkspace, ActionMenu>? _menuFactory;
    private readonly List<WorkspaceShortcut> _shortcuts = [];
    private readonly HashSet<SchematicElementViewModel> _trackedElements = [];
    private readonly HashSet<ComponentViewModel> _trackedComponents = [];
    public Guid Id { get; internal set; } = Guid.NewGuid();
    public WorkspaceViewModel ViewModel { get; }
    public ReadOnlyObservableCollection<SchematicElementViewModel> Elements => ViewModel.Elements;
    public IReadOnlyList<WorkspaceShortcut> Shortcuts => _shortcuts.AsReadOnly();
    public string Title
    {
        get => _title;
        set { if (SetProperty(ref _title, value)) { MarkDirty(); OnPropertyChanged(nameof(DisplayTitle)); } }
    }
    public string DisplayTitle => (IsDirty ? "* " : "") + Title;
    public bool IsDirty => _isDirty;
    public string? FilePath => _filePath;
    public string? ProjectPath => _projectPath;
    public string? WorkingDirectory
    {
        get => _workingDirectory;
        internal set { if(SetProperty(ref _workingDirectory,value)) foreach(var component in Elements.SelectMany(e=>e.Components)) component.RefreshContext(); }
    }
    public bool IsOpen { get => _isOpen; internal set => SetProperty(ref _isOpen, value); }
    public bool IsActive { get => _isActive; internal set => SetProperty(ref _isActive, value); }
    public RelayCommand OpenCommand { get; }
    public RelayCommand CloseCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public SchematicWorkspace(string title, Action<SchematicWorkspace>? initialize = null)
    {
        _title = title; ViewModel = new WorkspaceViewModel { Document = this };
        OpenCommand = new(() => _manager?.Open(this)); CloseCommand = new(() => _manager?.Close(this));
        SaveCommand = new(() => _save?.Invoke(this), () => _save is not null);
        DeleteCommand = new(() => _delete?.Invoke(this), () => _delete is not null);
        RegisterShortcut(new(Key.Delete), ViewModel.RemoveSelectedCommand, "Удалить выбранный объект");
        RegisterShortcut(new(Key.Escape), new RelayCommand(() => ViewModel.SelectedElement = null), "Снять выделение");
        RegisterShortcut(new(Key.D0, ModifierKeys.Control), ViewModel.ResetZoomCommand, "Масштаб 100%");
        RegisterShortcut(new(Key.W, ModifierKeys.Control), CloseCommand, "Закрыть вкладку");
        RegisterShortcut(new(Key.S, ModifierKeys.Control), SaveCommand, "Сохранить схему");
        ((INotifyCollectionChanged)Elements).CollectionChanged += (_, _) => { ReconcileSubscriptions(); MarkDirty(); };
        ViewModel.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ViewModel.SnapToGrid)) MarkDirty(); };
        initialize?.Invoke(this);
    }
    public void ConfigureActions(Action<SchematicWorkspace> save, Action<SchematicWorkspace> delete, Func<SchematicWorkspace, ActionMenu>? menuFactory = null)
    {
        _save = save; _delete = delete; _menuFactory = menuFactory;
        SaveCommand.NotifyCanExecuteChanged(); DeleteCommand.NotifyCanExecuteChanged();
    }
    public ActionMenu CreateMenu() => _menuFactory?.Invoke(this) ?? new ActionMenu([
        new("Открыть", () => OpenCommand.Execute(null)),
        new("Сохранить", () => SaveCommand.Execute(null), () => SaveCommand.CanExecute(null)),
        new("Удалить из списка…", () => DeleteCommand.Execute(null), () => DeleteCommand.CanExecute(null))
    ]);
    public void MarkDirty()
    {
        if (_isDirty) return;
        _isDirty = true; OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(DisplayTitle));
    }
    public void MarkSaved(string path)
    {
        _filePath = System.IO.Path.GetFullPath(path); _projectPath = null;
        foreach (var component in Elements.SelectMany(e => e.Components)) component.RefreshContext();
        _isDirty = false;
        OnPropertyChanged(nameof(FilePath)); OnPropertyChanged(nameof(ProjectPath)); OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(DisplayTitle));
    }
    public void MarkProjectSaved(string path)
    {
        _projectPath = System.IO.Path.GetFullPath(path);
        foreach (var component in Elements.SelectMany(e => e.Components)) component.RefreshContext();
        _isDirty = false;
        OnPropertyChanged(nameof(ProjectPath)); OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(DisplayTitle));
    }
    private void ReconcileSubscriptions()
    {
        foreach (var old in _trackedElements.Where(e => !Elements.Contains(e)).ToArray())
        {
            old.PropertyChanged -= ElementChanged;
            ((INotifyCollectionChanged)old.Components).CollectionChanged -= ComponentsChanged;
            _trackedElements.Remove(old);
        }
        foreach (var added in Elements.Where(e => !_trackedElements.Contains(e)))
        {
            added.PropertyChanged += ElementChanged;
            ((INotifyCollectionChanged)added.Components).CollectionChanged += ComponentsChanged;
            _trackedElements.Add(added);
        }
        var current = Elements.SelectMany(e => e.Components).ToHashSet();
        foreach (var old in _trackedComponents.Where(c => !current.Contains(c)).ToArray())
        { old.PropertyChanged -= ComponentChanged; _trackedComponents.Remove(old); }
        foreach (var added in current.Where(c => !_trackedComponents.Contains(c)))
        { added.PropertyChanged += ComponentChanged; _trackedComponents.Add(added); }
    }
    private void ElementChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName is not nameof(SchematicElementViewModel.IsSelected) and not nameof(ComponentViewModel.IsExpanded) and not nameof(SchematicElementViewModel.IsHierarchyExpanded) and not nameof(SchematicElementViewModel.WorldX) and not nameof(SchematicElementViewModel.WorldY) and not nameof(SchematicElementViewModel.WorldRotation) and not nameof(SchematicElementViewModel.EffectiveIsVisible) and not nameof(SchematicElementViewModel.EffectiveIsEnabled)) MarkDirty(); }
    private void ComponentChanged(object? sender, PropertyChangedEventArgs e)
    { if(sender?.GetType().GetProperty(e.PropertyName??"")?.IsDefined(typeof(SimBridge_Scada.Infrastructure.RuntimeStateAttribute),true)==true)return; if (e.PropertyName != nameof(ComponentViewModel.IsExpanded) && !(sender is ImageComponentViewModel && e.PropertyName is nameof(ImageComponentViewModel.Source) or nameof(ImageComponentViewModel.Status))) MarkDirty(); }
    private void ComponentsChanged(object? sender, NotifyCollectionChangedEventArgs e) { ReconcileSubscriptions(); MarkDirty(); }
    public void RegisterShortcut(KeyGesture gesture, ICommand command, string description)
    {
        _shortcuts.RemoveAll(s => s.Gesture.Key == gesture.Key && s.Gesture.Modifiers == gesture.Modifiers);
        _shortcuts.Add(new(gesture, command, description));
    }
    internal void Attach(WorkspaceManagerViewModel manager)
    {
        if (_manager is not null && _manager != manager) throw new InvalidOperationException("Workspace already belongs to another manager.");
        _manager = manager;
    }
    internal void Detach(WorkspaceManagerViewModel manager) { if (_manager == manager) _manager = null; }
}





