using SimBridge_Scada.Infrastructure;
namespace SimBridge_Scada.ViewModels;
public sealed class WorkspaceExplorerViewModel : ToolPanelViewModel
{
    public WorkspaceManagerViewModel Manager { get; }
    public RelayCommand OpenFileCommand { get; }
    public RelayCommand SaveAllCommand { get; }
    public WorkspaceExplorerViewModel(WorkspaceManagerViewModel manager, Action openFile, Action saveAll) : base("Рабочие области")
    { Manager = manager; OpenFileCommand = new(openFile); SaveAllCommand = new(saveAll); }
}
