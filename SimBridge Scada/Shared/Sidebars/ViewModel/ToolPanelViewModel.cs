using SimBridge_Scada.Infrastructure;
namespace SimBridge_Scada.ViewModels;
public abstract class ToolPanelViewModel(string title) : ObservableObject
{
    public string Title { get; } = title;
}
