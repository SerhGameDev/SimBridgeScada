using System.ComponentModel;
using SimBridge_Scada.Infrastructure;
namespace SimBridge_Scada.Modbus.Models;
public enum ModbusDataType { Boolean, UInt16, Int16, UInt32, Int32, Float32, Float64 }
public enum ModbusArea { Coils=1, DiscreteInputs=2, HoldingRegisters=3, InputRegisters=4 }
public enum ModbusByteOrder { BigEndian, WordSwap, ByteSwap, LittleEndian }
public sealed class ModbusVariable : ObservableObject, IDataErrorInfo
{
    private int _index,_address;
    private ModbusDataType _dataType=ModbusDataType.UInt16;
    private ModbusArea _area=ModbusArea.HoldingRegisters;
    private ModbusByteOrder _byteOrder;
    private string _comment="",_value="—",_status="Нет данных";
    internal Func<int,bool>? IndexIsDuplicate {get;set;}
    public int Index {get=>_index;set{if(SetProperty(ref _index,value))OnPropertyChanged(nameof(DisplayLabel));}}
    public ModbusDataType DataType {get=>_dataType;set=>SetProperty(ref _dataType,value);}
    public ModbusArea Area {get=>_area;set=>SetProperty(ref _area,value);}
    public int Address {get=>_address;set{if(SetProperty(ref _address,value))OnPropertyChanged(nameof(DisplayLabel));}}
    public ModbusByteOrder ByteOrder {get=>_byteOrder;set=>SetProperty(ref _byteOrder,value);}
    public string Comment {get=>_comment;set{if(SetProperty(ref _comment,value))OnPropertyChanged(nameof(DisplayLabel));}}
    [RuntimeState] public string Value {get=>_value;internal set=>SetProperty(ref _value,value);}
    [RuntimeState] public string Status {get=>_status;internal set=>SetProperty(ref _status,value);}
    [RuntimeState] public string DisplayLabel=>$"{Index} · {Address} · {Comment}";
    public int RegisterCount=>DataType is ModbusDataType.UInt32 or ModbusDataType.Int32 or ModbusDataType.Float32?2:DataType==ModbusDataType.Float64?4:1;
    public string Error=>Validate(Index,DataType,Area,Address,ByteOrder,IndexIsDuplicate);
    public string this[string columnName]=>Error;
    public static string Validate(int index,ModbusDataType type,ModbusArea area,int address,ModbusByteOrder order,Func<int,bool>? duplicate=null)
    {
        if(index<0 || duplicate?.Invoke(index)==true)return "Индекс должен быть уникальным и неотрицательным.";
        if(!Enum.IsDefined(type)||!Enum.IsDefined(area)||!Enum.IsDefined(order))return "Неизвестный тип или область Modbus.";
        var count=type is ModbusDataType.UInt32 or ModbusDataType.Int32 or ModbusDataType.Float32?2:type==ModbusDataType.Float64?4:1;
        if(address<0 || address>65535 || (long)address+count>65536)return "Адрес должен находиться в диапазоне 0–65535 с учётом размера значения.";
        if(area is ModbusArea.Coils or ModbusArea.DiscreteInputs && type!=ModbusDataType.Boolean)return "Для битовой области выберите Boolean.";
        return "";
    }
}
public sealed record ModbusVariableConfig(int Index,ModbusDataType DataType,ModbusArea Area,int Address,string Comment,ModbusByteOrder ByteOrder);
public sealed class ModbusSettings
{
    public int Version {get;set;}=1;
    public string Host {get;set;}="127.0.0.1";
    public int Port {get;set;}=502;
    public int UnitId {get;set;}=1;
    public int PollIntervalMs {get;set;}=500;
    public int TimeoutMs {get;set;}=2000;
    public List<ModbusVariableConfig> Variables {get;set;}=[];
}
