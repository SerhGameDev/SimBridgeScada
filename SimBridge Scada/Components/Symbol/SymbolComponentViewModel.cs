namespace SimBridge_Scada.ViewModels.Components;
public enum SymbolKind { Pump, Valve, Tank }
public sealed class SymbolComponentViewModel : ComponentViewModel
{
    private SymbolKind _kind;
    public override string Title => "Символ оборудования";
    public SymbolKind Kind { get => _kind; set => SetProperty(ref _kind,value); }
}
