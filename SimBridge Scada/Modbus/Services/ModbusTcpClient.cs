using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using SimBridge_Scada.Modbus.Models;
namespace SimBridge_Scada.Modbus.Services;
public sealed class ModbusDeviceException(byte code):IOException($"Modbus: код исключения {code}.");

/// <summary>Serialized, read-only Modbus TCP session. No write function codes are issued.</summary>
public sealed class ModbusTcpClient:IDisposable
{
    private readonly TcpClient _client=new();
    private ushort _transaction;
    public async Task ConnectAsync(string host,int port,CancellationToken cancellationToken)=>await _client.ConnectAsync(IPAddress.Parse(host),port,cancellationToken).ConfigureAwait(false);
    public async Task<string> ReadAsync(ModbusVariableConfig variable,byte unit,int timeoutMs,CancellationToken cancellationToken)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(timeoutMs);
        var token=timeout.Token;
        var count=variable.DataType is ModbusDataType.UInt32 or ModbusDataType.Int32 or ModbusDataType.Float32?2:variable.DataType==ModbusDataType.Float64?4:1;
        var function=(byte)variable.Area;
        var request=new byte[12];var transaction=unchecked(++_transaction);
        BinaryPrimitives.WriteUInt16BigEndian(request,transaction);BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(4),6);
        request[6]=unit;request[7]=function;BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(8),(ushort)variable.Address);BinaryPrimitives.WriteUInt16BigEndian(request.AsSpan(10),(ushort)count);
        var stream=_client.GetStream();await stream.WriteAsync(request,token).ConfigureAwait(false);
        var header=new byte[7];await stream.ReadExactlyAsync(header,token).ConfigureAwait(false);
        var length=BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
        if(BinaryPrimitives.ReadUInt16BigEndian(header)!=transaction||BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2))!=0||header[6]!=unit||length<2||length>254)throw new InvalidDataException("Некорректный MBAP-заголовок Modbus.");
        var pdu=new byte[length-1];await stream.ReadExactlyAsync(pdu,token).ConfigureAwait(false);
        if(pdu[0]==(function|0x80)){if(pdu.Length!=2)throw new InvalidDataException("Некорректный ответ исключения Modbus.");throw new ModbusDeviceException(pdu[1]);}
        var bytes=function is 1 or 2?1:count*2;
        if(pdu[0]!=function||pdu.Length!=bytes+2||pdu[1]!=bytes)throw new InvalidDataException("Некорректный размер ответа Modbus.");
        if(function is 1 or 2)return (pdu[2]&1)!=0?"True":"False";
        return Decode(pdu.AsSpan(2).ToArray(),variable.DataType,variable.ByteOrder);
    }
    public static string Decode(byte[] bytes,ModbusDataType type,ModbusByteOrder order)
    {
        bytes=(byte[])bytes.Clone();
        if(order is ModbusByteOrder.WordSwap or ModbusByteOrder.LittleEndian)
            for(var left=0;left<bytes.Length/2/2;left++){var right=bytes.Length/2-1-left;(bytes[left*2],bytes[right*2])=(bytes[right*2],bytes[left*2]);(bytes[left*2+1],bytes[right*2+1])=(bytes[right*2+1],bytes[left*2+1]);}
        if(order is ModbusByteOrder.ByteSwap or ModbusByteOrder.LittleEndian)
            for(var i=0;i<bytes.Length;i+=2)(bytes[i],bytes[i+1])=(bytes[i+1],bytes[i]);
        object value=type switch
        {
            ModbusDataType.Boolean=>BinaryPrimitives.ReadUInt16BigEndian(bytes)!=0,
            ModbusDataType.UInt16=>BinaryPrimitives.ReadUInt16BigEndian(bytes),
            ModbusDataType.Int16=>BinaryPrimitives.ReadInt16BigEndian(bytes),
            ModbusDataType.UInt32=>BinaryPrimitives.ReadUInt32BigEndian(bytes),
            ModbusDataType.Int32=>BinaryPrimitives.ReadInt32BigEndian(bytes),
            ModbusDataType.Float32=>BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(bytes)),
            ModbusDataType.Float64=>BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(bytes)),
            _=>throw new InvalidDataException("Неподдерживаемый тип Modbus.")
        };
        return value is IFormattable formatted?formatted.ToString(null,CultureInfo.InvariantCulture):value.ToString()!;
    }
    public void Dispose()=>_client.Dispose();
}
