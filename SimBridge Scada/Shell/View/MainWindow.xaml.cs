using System.ComponentModel;
using System.Windows;
using SimBridge_Scada.Modes;
using SimBridge_Scada.Modes.Editing;
using SimBridge_Scada.Project.Services;
using SimBridge_Scada.ViewModels;
namespace SimBridge_Scada;

/// <summary>Application entry point. The selected mode configures services and editor behavior.</summary>
public partial class MainWindow : Window
{
    public IApplicationMode Mode { get; }
    public MainWindow() : this(new EditingMode()) { }
    public MainWindow(IApplicationMode mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        InitializeComponent();
        Mode=mode;
        try { Mode.Initialize(this); }
        catch { Mode.Dispose();throw; }
        Closed+=(_,_)=>Mode.Dispose();
    }
    private EditingMode Editor => Mode as EditingMode ?? throw new InvalidOperationException("Текущий режим не поддерживает редактирование проекта.");
    // Keep the public file API available to callers while its implementation belongs to the mode.
    public IProjectService ProjectService => Editor.ProjectService;
    public bool SaveProject(string? path=null,bool saveAs=false)=>Editor.SaveProject(path,saveAs);
    public SchematicWorkspace? LoadProject(string? path=null)=>Editor.LoadProject(path);
    public bool SaveWorkspace(SchematicWorkspace? document=null,string? path=null,bool saveAs=false)=>Editor.SaveWorkspace(document,path,saveAs);
    public SchematicWorkspace? LoadWorkspace(string? path=null)=>Editor.LoadWorkspace(path);
    public bool SaveAllWorkspaces(string? path=null)=>Editor.SaveAllWorkspaces(path);
    public bool RenameWorkspace(SchematicWorkspace? document=null)=>Editor.RenameWorkspace(document);
    public bool DeleteWorkspace(SchematicWorkspace? document=null,bool confirm=true)=>Editor.DeleteWorkspace(document,confirm);
    public void ShowModbusWindow()=>Editor.ShowModbusWindow();
    protected override void OnClosing(CancelEventArgs e)
    {
        if(!e.Cancel&&!Mode.CanClose())e.Cancel=true;
        base.OnClosing(e);
    }
}
