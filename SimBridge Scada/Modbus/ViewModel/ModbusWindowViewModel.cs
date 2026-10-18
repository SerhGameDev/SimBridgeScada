using System.ComponentModel;
using SimBridge_Scada.Infrastructure;
using SimBridge_Scada.Modbus.Models;
using SimBridge_Scada.Modbus.Services;
namespace SimBridge_Scada.Modbus.ViewModel;
public static class ModbusChoices
{
    public static Array DataTypes=>Enum.GetValues<ModbusDataType>();
    public static Array Areas=>Enum.GetValues<ModbusArea>();
    public static Array ByteOrders=>Enum.GetValues<ModbusByteOrder>();
}
public sealed class ModbusWindowViewModel:ObservableObject,IDisposable
{
    private readonly CancellationTokenSource _closed=new();
    private ModbusVariable? _selectedVariable;
    private bool _hasValidationErrors;
    private bool _disposed;
    public IModbusService Service {get;}
    public ModbusVariable? SelectedVariable {get=>_selectedVariable;set{if(SetProperty(ref _selectedVariable,value))RemoveCommand.NotifyCanExecuteChanged();}}
    public bool HasValidationErrors {get=>_hasValidationErrors;set{if(SetProperty(ref _hasValidationErrors,value))RefreshCommands();}}
    public RelayCommand AddCommand {get;}
    public RelayCommand RemoveCommand {get;}
    public RelayCommand TestCommand {get;}
    public RelayCommand StartCommand {get;}
    public RelayCommand StopCommand {get;}
    public ModbusWindowViewModel(IModbusService service)
    {
        Service=service;
        AddCommand=new(()=>SelectedVariable=service.AddVariable());
        RemoveCommand=new(()=>{if(SelectedVariable is {} row){service.RemoveVariable(row);SelectedVariable=null;}},()=>SelectedVariable is not null);
        TestCommand=new(async()=>await service.TestConnectionAsync(_closed.Token),()=>!service.IsTesting&&!HasValidationErrors);
        StartCommand=new(service.StartPolling,()=>!service.IsPolling&&!HasValidationErrors);
        StopCommand=new(service.StopPolling,()=>service.IsPolling);
        Service.PropertyChanged+=Changed;
    }
    private void Changed(object? sender,PropertyChangedEventArgs e){if(e.PropertyName is nameof(Service.IsPolling) or nameof(Service.IsTesting))RefreshCommands();}
    private void RefreshCommands(){TestCommand.NotifyCanExecuteChanged();StartCommand.NotifyCanExecuteChanged();StopCommand.NotifyCanExecuteChanged();}
    public void Dispose(){if(_disposed)return;_disposed=true;_closed.Cancel();_closed.Dispose();Service.PropertyChanged-=Changed;}
}
