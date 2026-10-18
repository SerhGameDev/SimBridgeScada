using SimBridge_Scada.ViewModels.Components;
namespace SimBridge_Scada.ViewModels;
public sealed class TankElementViewModel : SchematicElementViewModel
{
    public override string TypeName => "Резервуар";
    public TankElementViewModel() { Name = "Резервуар"; AddComponent(new SymbolComponentViewModel { Kind = SymbolKind.Tank }); Height = 160; }
}


