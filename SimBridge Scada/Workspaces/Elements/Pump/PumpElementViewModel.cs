using SimBridge_Scada.ViewModels.Components;
namespace SimBridge_Scada.ViewModels;
public sealed class PumpElementViewModel : SchematicElementViewModel
{
    public override string TypeName => "Насос";
    public PumpElementViewModel() { Name = "Насос"; AddComponent(new SymbolComponentViewModel { Kind = SymbolKind.Pump }); }
}


