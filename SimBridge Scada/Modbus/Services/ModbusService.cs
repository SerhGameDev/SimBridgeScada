using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows.Threading;
using SimBridge_Scada.Infrastructure;
using SimBridge_Scada.Modbus.Models;
using SimBridge_Scada.Project.Services;
namespace SimBridge_Scada.Modbus.Services;
public sealed class ModbusService:ObservableObject,IModbusService,IProjectDataParticipant
{
    private readonly ObservableCollection<ModbusVariable> _variables=[];
    private readonly Dispatcher _dispatcher=Dispatcher.CurrentDispatcher;
    private string _host="127.0.0.1",_status="Отключено";
    private int _port=502,_unitId=1,_pollIntervalMs=500,_timeoutMs=2000,_generation;
    private bool _isPolling,_isTesting,_applying,_disposed;
    private CancellationTokenSource? _pollCancellation;
    private readonly CancellationTokenSource _lifetime=new();
    public ReadOnlyObservableCollection<ModbusVariable> Variables {get;}
    public string Host {get=>_host;set{if(SetProperty(ref _host,value))SettingsChanged();}}
    public int Port {get=>_port;set{if(SetProperty(ref _port,value))SettingsChanged();}}
    public int UnitId {get=>_unitId;set{if(SetProperty(ref _unitId,value))SettingsChanged();}}
    public int PollIntervalMs {get=>_pollIntervalMs;set{if(SetProperty(ref _pollIntervalMs,value))SettingsChanged();}}
    public int TimeoutMs {get=>_timeoutMs;set{if(SetProperty(ref _timeoutMs,value))SettingsChanged();}}
    [RuntimeState] public bool IsPolling {get=>_isPolling;private set=>SetProperty(ref _isPolling,value);}
    [RuntimeState] public bool IsTesting {get=>_isTesting;private set=>SetProperty(ref _isTesting,value);}
    [RuntimeState] public string Status {get=>_status;private set=>SetProperty(ref _status,value);}
    public event EventHandler? ValuesChanged;
    public event EventHandler? ConfigurationChanged;
    public string DataPath=>"Modbus/settings.json";
    public ModbusService()=>Variables=new(_variables);
    public ModbusVariable AddVariable()
    {
        Check();var index=_variables.Count==0?0:_variables.Max(v=>v.Index)+1;
        var variable=new ModbusVariable{Index=index};Attach(variable);_variables.Add(variable);SettingsChanged();return variable;
    }
    public bool RemoveVariable(ModbusVariable variable)
    {
        if(!_variables.Remove(variable))return false;
        variable.PropertyChanged-=VariableChanged;variable.IndexIsDuplicate=null;SettingsChanged();return true;
    }
    private void Attach(ModbusVariable variable)
    {variable.IndexIsDuplicate=index=>_variables.Any(v=>v!=variable&&v.Index==index);variable.PropertyChanged+=VariableChanged;}
    private void VariableChanged(object? sender,PropertyChangedEventArgs e)
    {
        if(e.PropertyName is nameof(ModbusVariable.Index) or nameof(ModbusVariable.Address) or nameof(ModbusVariable.DataType) or nameof(ModbusVariable.Area) or nameof(ModbusVariable.ByteOrder) or nameof(ModbusVariable.Comment))SettingsChanged();
    }
    private void SettingsChanged()
    {
        if(_applying)return;
        Interlocked.Increment(ref _generation);StopPolling();ClearValues("Настройки изменены");ConfigurationChanged?.Invoke(this,EventArgs.Empty);ValuesChanged?.Invoke(this,EventArgs.Empty);
    }
    private void ClearValues(string status)
    {foreach(var variable in _variables){variable.Value="—";variable.Status=status;}ValuesChanged?.Invoke(this,EventArgs.Empty);}
    private ModbusSettings Capture()
    {
        var settings=new ModbusSettings{Host=Host,Port=Port,UnitId=UnitId,PollIntervalMs=PollIntervalMs,TimeoutMs=TimeoutMs,
            Variables=_variables.Select(v=>new ModbusVariableConfig(v.Index,v.DataType,v.Area,v.Address,v.Comment,v.ByteOrder)).ToList()};
        Validate(settings);return settings;
    }
    private static void Validate(ModbusSettings settings)
    {
        if(settings.Version!=1 || !IPAddress.TryParse(settings.Host,out _)||settings.Port is <1 or >65535||settings.UnitId is <0 or >255
            ||settings.PollIntervalMs is <100 or >60000||settings.TimeoutMs is <100 or >30000||settings.Variables is null||settings.Variables.Count>10000)
            throw new InvalidDataException("Проверьте IP, порт (1–65535), Unit ID (0–255), период (100–60000 мс) и таймаут (100–30000 мс).");
        var ids=new HashSet<int>();
        foreach(var variable in settings.Variables)
        {
            if(variable is null||!ids.Add(variable.Index))throw new InvalidDataException("Индексы Modbus должны быть уникальными.");
            var error=ModbusVariable.Validate(variable.Index,variable.DataType,variable.Area,variable.Address,variable.ByteOrder);
            if(error.Length>0)throw new InvalidDataException(error);
        }
    }
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken=default)
    {
        Check();if(IsTesting)return false;IsTesting=true;Status="Проверка подключения…";
        var generation=_generation;
        try
        {
            var settings=Capture();using var token=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,_lifetime.Token);token.CancelAfter(settings.TimeoutMs);
            using var client=new ModbusTcpClient();await client.ConnectAsync(settings.Host,settings.Port,token.Token);
            if(settings.Variables.Count>0)await client.ReadAsync(settings.Variables[0],(byte)settings.UnitId,settings.TimeoutMs,token.Token);
            if(generation!=_generation||_disposed)return false;
            if(settings.Variables.Count>0)Status="Modbus TCP: подключение и чтение успешны";
            else Status="TCP-подключение успешно. Добавьте переменную для проверки Modbus.";
            return true;
        }
        catch(Exception e) when(e is IOException or InvalidDataException or SocketException or OperationCanceledException or ArgumentException)
        {if(generation==_generation&&!_disposed)Status=e is OperationCanceledException?"Проверка отменена или истёк таймаут":"Ошибка: "+e.Message;return false;}
        finally{IsTesting=false;}
    }
    public async Task<string> ReadValueAsync(int index,CancellationToken cancellationToken=default)
    {
        Check();var settings=Capture();var variable=settings.Variables.SingleOrDefault(v=>v.Index==index)??throw new KeyNotFoundException("Индекс Modbus не найден.");
        using var token=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,_lifetime.Token);token.CancelAfter(settings.TimeoutMs);
        using var client=new ModbusTcpClient();await client.ConnectAsync(settings.Host,settings.Port,token.Token);
        return await client.ReadAsync(variable,(byte)settings.UnitId,settings.TimeoutMs,token.Token);
    }
    public void StartPolling()
    {
        Check();if(IsPolling)return;
        try{Capture();}catch(InvalidDataException e){Status=e.Message;return;}
        var cancellation=CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);_pollCancellation=cancellation;IsPolling=true;Status="Подключение…";
        _=Task.Run(()=>PollAsync(cancellation));
    }
    public void StopPolling()
    {
        _pollCancellation?.Cancel();_pollCancellation=null;IsPolling=false;Status="Отключено";
        ClearValues("Отключено");
    }
    private async Task PollAsync(CancellationTokenSource cancellation)
    {
        var token=cancellation.Token;
        try
        {
            while(!token.IsCancellationRequested)
            {
                try
                {
                    var settings=await _dispatcher.InvokeAsync(Capture);var generation=Volatile.Read(ref _generation);
                    using var client=new ModbusTcpClient();using(var connect=CancellationTokenSource.CreateLinkedTokenSource(token)){connect.CancelAfter(settings.TimeoutMs);await client.ConnectAsync(settings.Host,settings.Port,connect.Token);}
                    await Publish(()=>Status="Modbus TCP: опрос выполняется",cancellation);
                    while(!token.IsCancellationRequested)
                    {
                        foreach(var variable in settings.Variables)
                        {
                            try
                            {
                                var value=await client.ReadAsync(variable,(byte)settings.UnitId,settings.TimeoutMs,token);
                                await Publish(()=>{if(generation!=_generation)return;var row=_variables.FirstOrDefault(v=>v.Index==variable.Index);if(row is null)return;row.Value=value;row.Status="OK";ValuesChanged?.Invoke(this,EventArgs.Empty);},cancellation);
                            }
                            catch(ModbusDeviceException e){await Publish(()=>{var row=_variables.FirstOrDefault(v=>v.Index==variable.Index);if(row is not null){row.Value="—";row.Status=e.Message;}ValuesChanged?.Invoke(this,EventArgs.Empty);},cancellation);}
                        }
                        await Task.Delay(settings.PollIntervalMs,token);
                    }
                }
                catch(Exception e) when(e is IOException or InvalidDataException or SocketException or OperationCanceledException)
                {
                    if(token.IsCancellationRequested)break;
                    await Publish(()=>{Status="Ошибка связи: "+e.Message+". Повторное подключение…";ClearValues("Ошибка связи");},cancellation);
                    await Task.Delay(1000,token);
                }
            }
        }
        catch(OperationCanceledException){ }
        finally{await Publish(()=>{IsPolling=false;Status="Отключено";},cancellation);cancellation.Dispose();}
    }
    private async Task Publish(Action action,CancellationTokenSource owner)
    {if(_dispatcher.HasShutdownStarted)return;await _dispatcher.InvokeAsync(()=>{if(!_disposed&&ReferenceEquals(_pollCancellation,owner))action();});}
    public byte[] CaptureProjectData()=>JsonSerializer.SerializeToUtf8Bytes(Capture(),new JsonSerializerOptions{WriteIndented=true});
    public PreparedProjectData PrepareProjectData(byte[]? data,bool merge)
    {
        var incoming=data is null?(merge?Capture():new ModbusSettings()):JsonSerializer.Deserialize<ModbusSettings>(data)??throw new InvalidDataException("Пустые настройки Modbus.");Validate(incoming);
        if(merge&&data is not null)
        {
            var current=Capture();
            if(current.Variables.Count>0)
            {
                if(current.Host!=incoming.Host||current.Port!=incoming.Port||current.UnitId!=incoming.UnitId)throw new InvalidDataException("Объединяемые проекты используют разные подключения Modbus.");
                foreach(var variable in current.Variables)
                {
                    var same=incoming.Variables.FirstOrDefault(v=>v.Index==variable.Index);
                    if(same is not null&&same!=variable)throw new InvalidDataException($"Конфликт переменной Modbus с индексом {variable.Index}.");
                    if(same is null)incoming.Variables.Add(variable);
                }
            }
        }
        return new(JsonSerializer.SerializeToUtf8Bytes(incoming,new JsonSerializerOptions{WriteIndented=true}),()=>Apply(incoming));
    }
    private void Apply(ModbusSettings settings)
    {
        StopPolling();Interlocked.Increment(ref _generation);_applying=true;
        try
        {
            Host=settings.Host;Port=settings.Port;UnitId=settings.UnitId;PollIntervalMs=settings.PollIntervalMs;TimeoutMs=settings.TimeoutMs;
            foreach(var old in _variables){old.PropertyChanged-=VariableChanged;old.IndexIsDuplicate=null;}_variables.Clear();
            foreach(var saved in settings.Variables){var variable=new ModbusVariable{Index=saved.Index,DataType=saved.DataType,Area=saved.Area,Address=saved.Address,Comment=saved.Comment,ByteOrder=saved.ByteOrder};Attach(variable);_variables.Add(variable);}
        }
        finally{_applying=false;}
        ValuesChanged?.Invoke(this,EventArgs.Empty);ConfigurationChanged?.Invoke(this,EventArgs.Empty);
    }
    private void Check()=>ObjectDisposedException.ThrowIf(_disposed,this);
    public void Dispose(){if(_disposed)return;StopPolling();_disposed=true;_lifetime.Cancel();_lifetime.Dispose();foreach(var row in _variables)row.PropertyChanged-=VariableChanged;}
}
