using System.Windows;
using SimBridge_Scada.ViewModels;
namespace SimBridge_Scada.Views;
public partial class RenameWorkspaceWindow : Window
{
    public RenameWorkspaceWindow(RenameWorkspaceViewModel viewModel)
    {
        InitializeComponent(); DataContext = viewModel;
        viewModel.CloseRequested += CloseDialog;
        Closed += (_, _) => viewModel.CloseRequested -= CloseDialog;
        Loaded += (_, _) => { NameField.Focus(); NameField.SelectAll(); };
    }
    private void CloseDialog(bool result) => DialogResult = result;
}
