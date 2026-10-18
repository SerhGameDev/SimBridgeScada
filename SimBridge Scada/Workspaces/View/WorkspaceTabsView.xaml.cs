using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimBridge_Scada.ViewModels;
namespace SimBridge_Scada.Views;
public partial class WorkspaceTabsView : UserControl
{
    private Point _start;
    private SchematicWorkspace? _dragCandidate;
    public WorkspaceTabsView() => InitializeComponent();
    private void TabDown(object sender, MouseButtonEventArgs e)
    {
        if (IsButton(e.OriginalSource as DependencyObject)) { _dragCandidate = null; return; }
        if (sender is not FrameworkElement { DataContext: SchematicWorkspace workspace }) return;
        _start = e.GetPosition(this); _dragCandidate = workspace;
        workspace.OpenCommand.Execute(null);
    }
    private void TabMove(object sender, MouseEventArgs e)
    {
        if (_dragCandidate is null || e.LeftButton != MouseButtonState.Pressed) return;
        var delta = e.GetPosition(this) - _start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var workspace = _dragCandidate; _dragCandidate = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(SchematicWorkspace), workspace), DragDropEffects.Move);
    }
    private void TabsWheel(object sender, MouseWheelEventArgs e)
    { if (sender is ScrollViewer scroll) { scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta); e.Handled = true; } }
    private void TabsDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(SchematicWorkspace)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }
    private void TabsDrop(object sender, DragEventArgs e)
    {
        if (DataContext is not WorkspaceManagerViewModel manager || e.Data.GetData(typeof(SchematicWorkspace)) is not SchematicWorkspace workspace) return;
        var source = e.OriginalSource as DependencyObject;
        SchematicWorkspace? target = null;
        while (source is not null && source != sender)
        {
            if (source is FrameworkElement { DataContext: SchematicWorkspace item }) { target = item; break; }
            source = FindParent(source);
        }
        var index = target is null ? manager.OpenWorkspaces.Count - 1 : manager.OpenWorkspaces.IndexOf(target);
        manager.MoveTab(workspace, index); e.Handled = true;
    }
    private static bool IsButton(DependencyObject? node)
    {
        while (node is not null) { if (node is Button) return true; node = FindParent(node); }
        return false;
    }
    private static DependencyObject? FindParent(DependencyObject node) => node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
}



