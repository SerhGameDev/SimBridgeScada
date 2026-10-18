using System.Collections.ObjectModel;
namespace SimBridge_Scada.ViewModels;
public sealed class ElementLibraryViewModel : ToolPanelViewModel
{
    public ReadOnlyObservableCollection<ElementLibraryItemViewModel> Items { get; }
    public ElementLibraryViewModel(IEnumerable<ElementLibraryItemViewModel> items) : base("Библиотека элементов") => Items = new(new(items));
}
