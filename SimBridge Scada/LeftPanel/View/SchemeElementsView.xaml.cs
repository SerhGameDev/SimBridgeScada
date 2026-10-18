using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SimBridge_Scada.ViewModels;
namespace SimBridge_Scada.Views;
public partial class SchemeElementsView : UserControl
{
    private Point _start;
    private SchematicElementViewModel? _candidate;
    public SchemeElementsView() => InitializeComponent();
    private void SelectionChanged(object sender,RoutedPropertyChangedEventArgs<object> e)
    { if(Hierarchy.DataContext is WorkspaceViewModel workspace && e.NewValue is SchematicElementViewModel element && workspace.Elements.Contains(element)) workspace.SelectedElement=element; }
    private void DragStart(object sender,MouseButtonEventArgs e)
    {
        _candidate=null;
        var source=e.OriginalSource as DependencyObject;
        for(var node=source;node is not null && node!=Hierarchy;node=ParentOf(node)) if(node is ToggleButton) return;
        var item=FindItem(source); if(item?.DataContext is not SchematicElementViewModel element) return;
        _candidate=element; _start=e.GetPosition(Hierarchy);
    }
    private void DragMove(object sender,MouseEventArgs e)
    {
        if(_candidate is null || e.LeftButton!=MouseButtonState.Pressed) return;
        var delta=e.GetPosition(Hierarchy)-_start;
        if(Math.Abs(delta.X)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y)<SystemParameters.MinimumVerticalDragDistance) return;
        var element=_candidate; _candidate=null;
        DragDrop.DoDragDrop(Hierarchy,new DataObject("SimBridge.Hierarchy",element),DragDropEffects.Move);
    }
    private void DragOverNode(object sender,DragEventArgs e)
    {
        if(e.Data.GetData("SimBridge.Hierarchy") is not SchematicElementViewModel element || Hierarchy.DataContext is not WorkspaceViewModel workspace) return;
        var target=FindItem(e.OriginalSource as DependencyObject)?.DataContext as SchematicElementViewModel;
        e.Effects=workspace.CanSetParent(element,target) ? DragDropEffects.Move : DragDropEffects.None; e.Handled=true;
    }
    private void DropNode(object sender,DragEventArgs e)
    {
        if(e.Data.GetData("SimBridge.Hierarchy") is not SchematicElementViewModel element || Hierarchy.DataContext is not WorkspaceViewModel workspace) return;
        var item=FindItem(e.OriginalSource as DependencyObject);
        var target=item?.DataContext as SchematicElementViewModel;
        var parent=target; int? index=null;
        if(item is not null && target is not null)
        {
            var y=e.GetPosition(item).Y;
            if(y<6 || y>24 && y<=30) { parent=target.Parent; index=workspace.GetSiblingIndex(target)+(y>24 ? 1 : 0); }
        }
        if(parent==element.Parent && index is { } next && workspace.GetSiblingIndex(element)<next) index=next-1;
        if(workspace.CanSetParent(element,parent)) { workspace.SetParent(element,parent,true,index); if(parent is not null) parent.IsHierarchyExpanded=true; workspace.SelectedElement=element; }
        e.Handled=true;
    }
    private static TreeViewItem? FindItem(DependencyObject? node)
    { for(;node is not null;node=ParentOf(node)) if(node is TreeViewItem item) return item; return null; }
    private static DependencyObject? ParentOf(DependencyObject node)=>node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
}

