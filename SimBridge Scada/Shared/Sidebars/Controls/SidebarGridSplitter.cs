using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SimBridge_Scada.Infrastructure;
using SimBridge_Scada.ViewModels;
namespace SimBridge_Scada.Controls;

/// <summary>Preserves sidebar width bindings after GridSplitter changes column widths.</summary>
public sealed class SidebarGridSplitter : GridSplitter
{
    public static readonly DependencyProperty SidebarProperty = DependencyProperty.Register(nameof(Sidebar), typeof(SidebarViewModel), typeof(SidebarGridSplitter));
    public SidebarViewModel? Sidebar { get => (SidebarViewModel?)GetValue(SidebarProperty); set => SetValue(SidebarProperty, value); }
    public int PanelColumnIndex { get; set; }
    public SidebarGridSplitter() => DragCompleted += (_, _) => CommitWidth();
    public void CommitWidth()
    {
        if (Sidebar is null || Parent is not Grid grid || PanelColumnIndex >= grid.ColumnDefinitions.Count) return;
        var column = grid.ColumnDefinitions[PanelColumnIndex];
        Sidebar.Width = column.ActualWidth;
        BindingOperations.SetBinding(column, ColumnDefinition.WidthProperty, new Binding(nameof(SidebarViewModel.Width))
        { Source = Sidebar, Mode = BindingMode.TwoWay, Converter = new SidebarWidthConverter() });
    }
}
