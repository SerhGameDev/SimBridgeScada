using SimBridge_Scada.Shared.Menus;
using System.Collections.ObjectModel;
using SimBridge_Scada.Infrastructure;
using SimBridge_Scada.ViewModels.Components;
namespace SimBridge_Scada.ViewModels;

public abstract class SchematicElementViewModel : ObservableObject, ILeftClickMenuProvider
{
    private readonly ObservableCollection<ComponentViewModel> _components = [];
    private readonly ObservableCollection<SchematicElementViewModel> _children = [];
    private bool _isSelected, _isHierarchyExpanded = true;
    private SchematicElementViewModel? _parent;
    internal WorkspaceViewModel? OwnerWorkspace { get; set; }
    public Guid Id { get; internal set; } = Guid.NewGuid();
    public abstract string TypeName { get; }
    public ReadOnlyObservableCollection<ComponentViewModel> Components { get; }
    public ReadOnlyObservableCollection<SchematicElementViewModel> Children { get; }
    internal ObservableCollection<SchematicElementViewModel> MutableChildren => _children;
    public SchematicElementViewModel? Parent => _parent;
    public bool IsHierarchyExpanded { get => _isHierarchyExpanded; set => SetProperty(ref _isHierarchyExpanded, value); }
    public ObjectPropertiesComponentViewModel ObjectProperties { get; }
    public TransformComponentViewModel Transform { get; }
    protected SchematicElementViewModel()
    {
        Components = new(_components); Children = new(_children);
        ObjectProperties = AddComponent(new ObjectPropertiesComponentViewModel());
        Transform = AddComponent(new TransformComponentViewModel());
        ObjectProperties.PropertyChanged += (_, e) => { OnPropertyChanged(e.PropertyName); NotifyWorldState(); };
        Transform.PropertyChanged += (_, e) => { OnPropertyChanged(e.PropertyName); NotifyWorldState(); };
    }
    public T AddComponent<T>(T component) where T : ComponentViewModel
    {
        ArgumentNullException.ThrowIfNull(component);
        if (component.Owner is not null) throw new InvalidOperationException("Component already belongs to an object.");
        if (component is ObjectPropertiesComponentViewModel or TransformComponentViewModel && _components.Any(c => c.GetType() == component.GetType()))
            throw new InvalidOperationException("Required component already exists.");
        component.Owner = this; _components.Add(component); return component;
    }
    public bool RemoveComponent(ComponentViewModel component)
    {
        if (component == ObjectProperties || component == Transform) return false;
        if (!_components.Remove(component)) return false;
        component.Owner = null; return true;
    }
    public void MoveComponent(ComponentViewModel component, int index)
    {
        var old = _components.IndexOf(component);
        if (old >= 0) _components.Move(old, Math.Clamp(index, 0, _components.Count - 1));
    }
    internal void SetParentReference(SchematicElementViewModel? parent)
    { _parent = parent; OnPropertyChanged(nameof(Parent)); NotifyWorldState(); }
    public void NotifyWorldState()
    {
        OnPropertyChanged(nameof(WorldX)); OnPropertyChanged(nameof(WorldY)); OnPropertyChanged(nameof(WorldRotation));
        OnPropertyChanged(nameof(EffectiveIsVisible)); OnPropertyChanged(nameof(EffectiveIsEnabled));
        foreach (var child in _children) child.NotifyWorldState();
    }
    private (double X, double Y) WorldCenter
    {
        get
        {
            if (Parent is null) return (X + Width / 2, Y + Height / 2);
            var center = Parent.WorldCenter; var angle = Parent.WorldRotation * Math.PI / 180;
            var x = X + Width / 2 - Parent.Width / 2; var y = Y + Height / 2 - Parent.Height / 2;
            return (center.X + x * Math.Cos(angle) - y * Math.Sin(angle), center.Y + x * Math.Sin(angle) + y * Math.Cos(angle));
        }
    }
    public double WorldX => WorldCenter.X - Width / 2;
    public double WorldY => WorldCenter.Y - Height / 2;
    public double WorldRotation => Rotation + (Parent?.WorldRotation ?? 0);
    public bool EffectiveIsVisible => IsVisible && (Parent?.EffectiveIsVisible ?? true);
    public bool EffectiveIsEnabled => IsEnabled && (Parent?.EffectiveIsEnabled ?? true);
    internal void SetWorldPosition(double x, double y)
    {
        if (Parent is null) { X = x; Y = y; return; }
        var center = Parent.WorldCenter; var angle = -Parent.WorldRotation * Math.PI / 180;
        var dx = x + Width / 2 - center.X; var dy = y + Height / 2 - center.Y;
        X = dx * Math.Cos(angle) - dy * Math.Sin(angle) + Parent.Width / 2 - Width / 2;
        Y = dx * Math.Sin(angle) + dy * Math.Cos(angle) + Parent.Height / 2 - Height / 2;
    }
    public ActionMenu CreateMenu()
    {
        if (OwnerWorkspace is { } workspace) workspace.SelectedElement = this;
        return new ActionMenu([
            new("Создать дочерний объект", () => OwnerWorkspace?.CreateEmpty(this)),
            new("Сделать корневым", () => OwnerWorkspace?.SetParent(this, null), () => Parent is not null),
            new("Состояние", [
                new ActionMenuItem(IsEnabled ? "Выключить" : "Включить", () => IsEnabled = !IsEnabled),
                new ActionMenuItem(IsVisible ? "Скрыть" : "Показать", () => IsVisible = !IsVisible),
                new ActionMenuItem(IsLocked ? "Разблокировать перемещение" : "Заблокировать перемещение", () => IsLocked = !IsLocked)
            ]),
            ActionMenuItem.Separator(),
            new("Удалить объект", () => OwnerWorkspace?.RemoveElement(this), () => OwnerWorkspace is not null)
        ]);
    }
    public string Name { get => ObjectProperties.Name; set => ObjectProperties.Name = value; }
    public bool IsEnabled { get => ObjectProperties.IsEnabled; set => ObjectProperties.IsEnabled = value; }
    public bool IsVisible { get => ObjectProperties.IsVisible; set => ObjectProperties.IsVisible = value; }
    public double X { get => Transform.X; set => Transform.X = value; }
    public double Y { get => Transform.Y; set => Transform.Y = value; }
    public double Width { get => Transform.Width; set => Transform.Width = value; }
    public double Height { get => Transform.Height; set => Transform.Height = value; }
    public double Rotation { get => Transform.Rotation; set => Transform.Rotation = value; }
    public bool IsLocked { get => Transform.IsLocked; set => Transform.IsLocked = value; }
    public bool IsSelected { get => _isSelected; internal set => SetProperty(ref _isSelected, value); }
}
