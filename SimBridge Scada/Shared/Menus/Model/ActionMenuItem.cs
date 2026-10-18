using SimBridge_Scada.Infrastructure;
namespace SimBridge_Scada.Shared.Menus;
public sealed class ActionMenuItem
{
    public string Title { get; }
    public string? IconPath { get; init; }
    public string GestureText { get; init; } = "";
    public bool IsSeparator { get; }
    public RelayCommand? Command { get; }
    public IReadOnlyList<ActionMenuItem> Children { get; }
    public ActionMenuItem(string title, Action action, Func<bool>? canExecute = null)
    { Title = title; Command = new(action, canExecute); Children = []; }
    public ActionMenuItem(string title, IEnumerable<ActionMenuItem> children)
    { Title = title; Children = children.ToArray(); }
    private ActionMenuItem() { Title = ""; IsSeparator = true; Children = []; }
    public static ActionMenuItem Separator() => new();
}

