namespace SimBridge_Scada.ViewModels;
public sealed class EmptyElementViewModel : SchematicElementViewModel
{
    public override string TypeName => "Объект";
    public EmptyElementViewModel() => Name = "Объект";
}
