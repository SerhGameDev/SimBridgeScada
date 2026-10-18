namespace SimBridge_Scada.Shared.Menus;
public sealed class MenuButtonViewModel(Func<ActionMenu> factory) : ILeftClickMenuProvider
{
    public ActionMenu CreateMenu() => factory();
}

