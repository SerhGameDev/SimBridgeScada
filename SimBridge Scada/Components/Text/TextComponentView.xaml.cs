using System.Windows.Controls;
namespace SimBridge_Scada.Views;
public partial class TextComponentView : UserControl
{
 public TextComponentView()=>InitializeComponent();
 private void VariableSelected(object sender,SelectionChangedEventArgs e)
 {
  // Clearing/reloading the catalog must not erase the component's saved reference.
  if(sender is ComboBox { IsKeyboardFocusWithin:true,SelectedValue:int index } && e.AddedItems.Count>0 && DataContext is SimBridge_Scada.ViewModels.Components.TextComponentViewModel text)text.ModbusVariableIndex=index;
 }
}
