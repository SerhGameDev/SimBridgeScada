using SimBridge_Scada.ViewModels.Components;
namespace SimBridge_Scada.ViewModels;
public sealed class ValveElementViewModel : SchematicElementViewModel
{
    public override string TypeName => "Задвижка";
    public ValveElementViewModel() { Name = "Задвижка"; AddComponent(new SymbolComponentViewModel { Kind = SymbolKind.Valve }); }
}


