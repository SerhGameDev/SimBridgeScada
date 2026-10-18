using SimBridge_Scada.Infrastructure;
namespace SimBridge_Scada.ViewModels;

public sealed class RenameWorkspaceViewModel : ObservableObject
{
    private readonly SchematicWorkspace _workspace;
    private string _name;
    public string Name
    {
        get => _name;
        set { if (SetProperty(ref _name, value)) { OnPropertyChanged(nameof(Error)); ConfirmCommand.NotifyCanExecuteChanged(); } }
    }
    public string Error => string.IsNullOrWhiteSpace(Name) ? "Введите название мнемосхемы." : Name.Trim().Length > 128 ? "Название не должно превышать 128 символов." : "";
    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }
    public event Action<bool>? CloseRequested;
    public RenameWorkspaceViewModel(SchematicWorkspace workspace)
    {
        _workspace = workspace; _name = workspace.Title;
        ConfirmCommand = new(() => { _workspace.Title = Name.Trim(); CloseRequested?.Invoke(true); }, () => Error.Length == 0);
        CancelCommand = new(() => CloseRequested?.Invoke(false));
    }
}
