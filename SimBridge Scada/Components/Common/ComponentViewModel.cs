using SimBridge_Scada.Infrastructure;
using SimBridge_Scada.Shared.Menus;
namespace SimBridge_Scada.ViewModels.Components;
public abstract class ComponentViewModel : ObservableObject, ILeftClickMenuProvider
{
    private bool _isExpanded = true;
    public abstract string Title { get; }
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
    public SchematicElementViewModel? Owner { get; internal set; }
    public virtual void RefreshContext() { }
    public ActionMenu CreateMenu() => new([
        new("Переместить вверх", () => { if (Owner is { } owner) owner.MoveComponent(this, owner.Components.IndexOf(this)-1); }, () => Owner is { } owner && owner.Components.IndexOf(this)>0),
        new("Переместить вниз", () => { if (Owner is { } owner) owner.MoveComponent(this, owner.Components.IndexOf(this)+1); }, () => Owner is { } owner && owner.Components.IndexOf(this)<owner.Components.Count-1),
        ActionMenuItem.Separator(),
        new("Удалить компонент", () => Owner?.RemoveComponent(this), () => Owner is { } owner && this != owner.Transform && this != owner.ObjectProperties)
    ]);
}

