using System.Windows;
using System.Windows.Controls;
using SimBridge_Scada.Modbus.ViewModel;
namespace SimBridge_Scada.Modbus.View;
public partial class ModbusWindow:Window
{
    private int _validationErrors;
    public ModbusWindow(ModbusWindowViewModel viewModel)
    {InitializeComponent();DataContext=viewModel;Closed+=(_,_)=>viewModel.Dispose();}
    private void ValidationError(object sender,ValidationErrorEventArgs e)
    {
        _validationErrors=Math.Max(0,_validationErrors+(e.Action==ValidationErrorEventAction.Added?1:-1));
        if(DataContext is ModbusWindowViewModel vm)vm.HasValidationErrors=_validationErrors>0;
    }
}
