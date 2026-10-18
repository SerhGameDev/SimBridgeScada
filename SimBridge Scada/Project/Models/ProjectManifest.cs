namespace SimBridge_Scada.Project.Models;
public sealed class ProjectManifest
{
    public string Format { get; set; } = "SimBridge.ScadaProject";
    public int Version { get; set; } = 1;
    public List<Guid> Schemes { get; set; } = [];
    public List<Guid> OpenSchemes { get; set; } = [];
    public Guid? ActiveScheme { get; set; }
}
public sealed record SchemeInfo(Guid Id, string Name, int ObjectCount, bool IsOpen, bool HasChanges, string WorkingFile);
