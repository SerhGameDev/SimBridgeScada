using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SimBridge_Scada.ViewModels;
namespace SimBridge_Scada.Views;
public partial class WorkspaceView : UserControl
{
    public WorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => InitializeShortcuts();
    }
    private void InitializeShortcuts()
    {
        InputBindings.Clear();
        if (DataContext is not WorkspaceViewModel { Document: { } document }) return;
        foreach (var shortcut in document.Shortcuts) InputBindings.Add(new KeyBinding(shortcut.Command, shortcut.Gesture));
    }
}
