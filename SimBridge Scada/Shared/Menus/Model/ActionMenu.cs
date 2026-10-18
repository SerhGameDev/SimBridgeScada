namespace SimBridge_Scada.Shared.Menus;
public sealed class ActionMenu(IEnumerable<ActionMenuItem> items)
{
    public IReadOnlyList<ActionMenuItem> Items { get; } = items.ToArray();
}

