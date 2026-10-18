using System.IO;
using System.Text.Json;
using SimBridge_Scada.ViewModels;
namespace SimBridge_Scada.Workspaces.Persistence;

public sealed class WorkspaceProjectFile
{
    public string Format { get; set; } = "SimBridge.Project";
    public int Version { get; set; } = 1;
    public List<WorkspaceFile> Workspaces { get; set; } = [];
    public List<Guid> OpenWorkspaceIds { get; set; } = [];
    public Guid? ActiveWorkspaceId { get; set; }
}
public sealed record LoadedWorkspaceProject(IReadOnlyList<SchematicWorkspace> Workspaces, IReadOnlyList<Guid> OpenIds, Guid? ActiveId);

public sealed class WorkspaceProjectStorage(WorkspaceStorage storage)
{
    public const string FileExtension = ".simbridge-project";
    public void Save(WorkspaceManagerViewModel manager, string path)
    {
        var model = new WorkspaceProjectFile
        {
            Workspaces = manager.AllWorkspaces.Select(storage.Capture).ToList(),
            OpenWorkspaceIds = manager.OpenWorkspaces.Select(w => w.Id).ToList(), ActiveWorkspaceId = manager.ActiveWorkspace?.Id
        };
        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        path = Path.GetFullPath(path);
        var temp = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            { writer.Write(json); writer.Flush(); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            foreach (var document in manager.AllWorkspaces) document.MarkProjectSaved(path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public LoadedWorkspaceProject Load(string path, Func<string, SchematicWorkspace>? factory = null)
    {
        if (new FileInfo(path).Length > 128 * 1024 * 1024) throw new InvalidDataException("Файл проекта превышает 128 МБ.");
        WorkspaceProjectFile model;
        try { model = JsonSerializer.Deserialize<WorkspaceProjectFile>(File.ReadAllText(path)) ?? throw new InvalidDataException("Пустой проект."); }
        catch (JsonException e) { throw new InvalidDataException("Некорректный JSON проекта.", e); }
        if (model.Format != "SimBridge.Project" || model.Version != 1 || model.Workspaces is null || model.OpenWorkspaceIds is null)
            throw new InvalidDataException("Неподдерживаемый формат проекта.");
        var ids = new HashSet<Guid>();
        var documents = new List<SchematicWorkspace>();
        foreach (var item in model.Workspaces)
        {
            if (item is null || !ids.Add(item.Id)) throw new InvalidDataException("Повторяющаяся схема в проекте.");
            var document = storage.Restore(item, factory: factory);
            document.MarkProjectSaved(path); documents.Add(document);
        }
        if (model.OpenWorkspaceIds.Distinct().Count() != model.OpenWorkspaceIds.Count || model.OpenWorkspaceIds.Any(id => !ids.Contains(id))
            || (model.ActiveWorkspaceId is { } active && !model.OpenWorkspaceIds.Contains(active)))
            throw new InvalidDataException("Некорректный список вкладок проекта.");
        return new(documents, model.OpenWorkspaceIds, model.ActiveWorkspaceId);
    }
}
