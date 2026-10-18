using System.Text.Json;
namespace SimBridge_Scada.Workspaces.Persistence;

public sealed class WorkspaceFile
{
    public int Version { get; set; } = 2;
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public double Zoom { get; set; } = 1;
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public bool SnapToGrid { get; set; } = true;
    public List<ElementFile> Elements { get; set; } = [];
}
public sealed class ElementFile
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public int SiblingIndex { get; set; }
    public string Kind { get; set; } = "";
    public Dictionary<string, JsonElement> Data { get; set; } = [];
    public List<ComponentFile> Components { get; set; } = [];
}
public sealed class ComponentFile
{
    public string Kind { get; set; } = "";
    public Dictionary<string, JsonElement> Data { get; set; } = [];
}

