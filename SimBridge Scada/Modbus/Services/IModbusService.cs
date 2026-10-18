using System.Collections.ObjectModel;
using System.ComponentModel;
using SimBridge_Scada.Modbus.Models;
namespace SimBridge_Scada.Modbus.Services;
public interface IModbusService:INotifyPropertyChanged,IDisposable
{
    ReadOnlyObservableCollection<ModbusVariable> Variables {get;}
    string Host {get;set;}
    int Port {get;set;}
    int UnitId {get;set;}
    int PollIntervalMs {get;set;}
    int TimeoutMs {get;set;}
    bool IsPolling {get;}
    bool IsTesting {get;}
    string Status {get;}
    event EventHandler? ValuesChanged;
    ModbusVariable AddVariable();
    bool RemoveVariable(ModbusVariable variable);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken=default);
    Task<string> ReadValueAsync(int index,CancellationToken cancellationToken=default);
    void StartPolling();
    void StopPolling();
}
