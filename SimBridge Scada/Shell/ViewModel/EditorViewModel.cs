using SimBridge_Scada.Shared.Menus;
namespace SimBridge_Scada.ViewModels;
public sealed class EditorViewModel(WorkspaceManagerViewModel workspaces, LeftSidebarViewModel leftSidebar, RightSidebarViewModel rightSidebar, MenuButtonViewModel fileMenu)
{
    public WorkspaceManagerViewModel Workspaces { get; } = workspaces;
    public LeftSidebarViewModel LeftSidebar { get; } = leftSidebar;
    public RightSidebarViewModel RightSidebar { get; } = rightSidebar;
    public MenuButtonViewModel FileMenu { get; } = fileMenu;
}
