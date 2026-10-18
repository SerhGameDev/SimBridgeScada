using System.Windows;
using System.Windows.Controls;
namespace SimBridge_Scada.Controls;
public sealed class ComponentVisualTemplateSelector : DataTemplateSelector
{
    private readonly DataTemplate _empty=new();
    public override DataTemplate SelectTemplate(object item, DependencyObject container) =>
        item is null ? _empty : (container as FrameworkElement)?.TryFindResource("Visual."+item.GetType().Name) as DataTemplate ?? _empty;
}
