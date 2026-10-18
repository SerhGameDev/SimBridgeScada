using System.Collections.ObjectModel;
using SimBridge_Scada.Infrastructure;

namespace SimBridge_Scada.ViewModels;

/// <summary>Workspace API. Call mutation methods on the UI dispatcher thread.</summary>
public sealed class WorkspaceViewModel : ObservableObject
{
    private readonly ObservableCollection<SchematicElementViewModel> _elements = [], _roots = [];
    private SchematicElementViewModel? _selectedElement;
    private double _zoom = 1, _offsetX, _offsetY;
    public SchematicWorkspace? Document { get; internal set; }
    public double OffsetX { get => _offsetX; set => SetProperty(ref _offsetX, double.IsFinite(value) ? Math.Max(0, value) : 0); }
    public double OffsetY { get => _offsetY; set => SetProperty(ref _offsetY, double.IsFinite(value) ? Math.Max(0, value) : 0); }
    private bool _snapToGrid = true;
    public const double GridSize = 20;
    public double CanvasWidth => 2400;
    public double CanvasHeight => 1600;
    public ReadOnlyObservableCollection<SchematicElementViewModel> Elements { get; }
    public ReadOnlyObservableCollection<SchematicElementViewModel> RootElements { get; }
    public RelayCommand CreateEmptyCommand { get; }
    public RelayCommand RemoveSelectedCommand { get; }
    public RelayCommand ResetZoomCommand { get; }
    public bool SnapToGrid { get => _snapToGrid; set => SetProperty(ref _snapToGrid, value); }
    public double Zoom
    {
        get => _zoom;
        set { if (SetProperty(ref _zoom, double.IsFinite(value) ? Math.Clamp(value, .25, 2) : 1)) OnPropertyChanged(nameof(ZoomText)); }
    }
    public string ZoomText => $"{Zoom:P0}";
    public SchematicElementViewModel? SelectedElement
    {
        get => _selectedElement;
        set
        {
            if (value is not null && !_elements.Contains(value)) throw new ArgumentException("Element does not belong to this workspace.", nameof(value));
            if (_selectedElement == value) return;
            if (_selectedElement is not null) _selectedElement.IsSelected = false;
            SetProperty(ref _selectedElement, value);
            if (value is not null) { value.IsSelected = true; for (var parent = value.Parent; parent is not null; parent = parent.Parent) parent.IsHierarchyExpanded = true; }
            RemoveSelectedCommand.NotifyCanExecuteChanged();
        }
    }
    public WorkspaceViewModel()
    {
        Elements = new(_elements); RootElements = new(_roots);
        CreateEmptyCommand = new(() => CreateEmpty());
        RemoveSelectedCommand = new(() => { if (SelectedElement is { } element) RemoveElement(element); }, () => SelectedElement is not null);
        ResetZoomCommand = new(() => Zoom = 1);
    }
    public T AddElement<T>(T element, double x = 80, double y = 80) where T : SchematicElementViewModel
    {
        ArgumentNullException.ThrowIfNull(element);
        if (element.OwnerWorkspace is not null) throw new InvalidOperationException("Element already belongs to a workspace.");
        element.X = x; element.Y = y;
        element.OwnerWorkspace = this;
        _elements.Add(element); _roots.Add(element);
        SelectedElement = element;
        return element;
    }
    public bool RemoveElement(SchematicElementViewModel element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!_elements.Contains(element)) return false;
        foreach (var child in element.Children.ToArray()) RemoveElement(child);
        if (SelectedElement == element) SelectedElement = null;
        if (element.Parent is { } parent) parent.MutableChildren.Remove(element); else _roots.Remove(element);
        element.SetParentReference(null); element.OwnerWorkspace = null;
        return _elements.Remove(element);
    }
    public bool RemoveElement(Guid id)
    {
        var element = _elements.FirstOrDefault(e => e.Id == id);
        return element is not null && RemoveElement(element);
    }
    public void ClearElements() { foreach (var root in _roots.ToArray()) RemoveElement(root); }
    public void MoveElement(SchematicElementViewModel element, double x, double y)
    {
        if (!_elements.Contains(element) || element.IsLocked) return;
        if (SnapToGrid) { x = Math.Round(x / GridSize) * GridSize; y = Math.Round(y / GridSize) * GridSize; }
        element.SetWorldPosition(Math.Clamp(x, 0, Math.Max(0, CanvasWidth - element.Width)), Math.Clamp(y, 0, Math.Max(0, CanvasHeight - element.Height)));
    }
    public SchematicElementViewModel CreateEmpty(SchematicElementViewModel? parent = null)
    {
        var element = AddElement(new EmptyElementViewModel());
        if (parent is not null) { SetParent(element, parent, false); element.X = 20; element.Y = 20; parent.IsHierarchyExpanded = true; }
        return element;
    }
    public bool CanSetParent(SchematicElementViewModel element, SchematicElementViewModel? parent)
    {
        if (element.OwnerWorkspace != this || parent is not null && parent.OwnerWorkspace != this) return false;
        for (var ancestor = parent; ancestor is not null; ancestor = ancestor.Parent) if (ancestor == element) return false;
        return true;
    }
    public void SetParent(SchematicElementViewModel element, SchematicElementViewModel? parent, bool keepWorldPosition = true, int? siblingIndex = null)
    {
        if (!CanSetParent(element, parent)) throw new InvalidOperationException("Нельзя создать цикл или перенести объект в другую схему.");
        var x = element.WorldX; var y = element.WorldY; var rotation = element.WorldRotation;
        var oldList = element.Parent?.MutableChildren ?? _roots;
        oldList.Remove(element);
        element.SetParentReference(parent);
        var list = parent?.MutableChildren ?? _roots;
        list.Insert(Math.Clamp(siblingIndex ?? list.Count, 0, list.Count), element);
        if (keepWorldPosition) { element.SetWorldPosition(x, y); element.Rotation = rotation - (parent?.WorldRotation ?? 0); }
        Document?.MarkDirty();
    }
    public int GetSiblingIndex(SchematicElementViewModel element) => (element.Parent?.MutableChildren ?? _roots).IndexOf(element);
}

