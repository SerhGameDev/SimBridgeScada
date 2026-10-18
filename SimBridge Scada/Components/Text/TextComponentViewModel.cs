using System.Collections.ObjectModel;
using SimBridge_Scada.Infrastructure;
using SimBridge_Scada.Services;
using SimBridge_Scada.Modbus.Models;
using SimBridge_Scada.Modbus.Services;
using System.Windows;
using System.Windows.Media;
namespace SimBridge_Scada.ViewModels.Components;
public sealed class TextComponentViewModel : ComponentViewModel
{
    private string _text = "Текст", _color = "#DFE1E5";
    private double _fontSize = 20;
    private bool _bold, _syncWithModbus;
    private int? _modbusVariableIndex;
    private readonly IModbusService? _modbus;
    [RuntimeState] public ReadOnlyObservableCollection<ModbusVariable>? ModbusVariables=>_modbus?.Variables;
    public bool SyncWithModbus {get=>_syncWithModbus;set{if(SetProperty(ref _syncWithModbus,value)){OnPropertyChanged(nameof(DisplayText));OnPropertyChanged(nameof(ModbusStatus));}}}
    public int? ModbusVariableIndex {get=>_modbusVariableIndex;set{if(SetProperty(ref _modbusVariableIndex,value)){OnPropertyChanged(nameof(DisplayText));OnPropertyChanged(nameof(ModbusStatus));}}}
    [RuntimeState] public string DisplayText=>!SyncWithModbus?Text:_modbus?.Variables.FirstOrDefault(v=>v.Index==ModbusVariableIndex)?.Value??"—";
    [RuntimeState] public string ModbusStatus=>!SyncWithModbus?"":ModbusVariableIndex is null?"Выберите индекс переменной":_modbus?.Variables.FirstOrDefault(v=>v.Index==ModbusVariableIndex)?.Status??"Индекс переменной не найден";
    public TextComponentViewModel()
    {
        if(ServiceLocator.Current.TryGet<IModbusService>(out var service))
        {
            _modbus=service;
            WeakEventManager<IModbusService,EventArgs>.AddHandler(service,nameof(IModbusService.ValuesChanged),ValuesUpdated);
        }
    }
    private void ValuesUpdated(object? sender,EventArgs e){OnPropertyChanged(nameof(DisplayText));OnPropertyChanged(nameof(ModbusStatus));}
    public override string Title => "Текст";
    public string Text { get => _text; set { if(SetProperty(ref _text,value)) OnPropertyChanged(nameof(DisplayText)); } }
    public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize,double.IsFinite(value) ? Math.Clamp(value,1,256) : 20); }
    public string Color { get => _color; set { if(SetProperty(ref _color,value)) OnPropertyChanged(nameof(Foreground)); } }
    public bool Bold { get => _bold; set { if(SetProperty(ref _bold,value)) OnPropertyChanged(nameof(Weight)); } }
    public FontWeight Weight => Bold ? FontWeights.Bold : FontWeights.Normal;
    public Brush Foreground
    {
        get { try { if(new BrushConverter().ConvertFromString(Color) is not Brush brush) return Brushes.White; brush.Freeze(); return brush; } catch(Exception e) when(e is FormatException or NotSupportedException or ArgumentException) { return Brushes.White; } }
    }
}


