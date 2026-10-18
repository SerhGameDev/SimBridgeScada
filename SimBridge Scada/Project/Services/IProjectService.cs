using SimBridge_Scada.Project.Models;
using SimBridge_Scada.ViewModels;
using SimBridge_Scada.Workspaces.Persistence;
namespace SimBridge_Scada.Project.Services;
public interface IProjectService : IDisposable
{
    string WorkingDirectory { get; }
    string SchemesDirectory { get; }
    string DataDirectory { get; }
    string? CurrentFilePath { get; }
    string? LastWorkingCopyError { get; }
    bool HasChanges { get; }
    IReadOnlyList<SchemeInfo> GetSchemes();
    SchematicWorkspace? GetScheme(Guid id);
    WorkspaceFile GetSchemeData(Guid id);
    SchematicWorkspace AddScheme(WorkspaceFile data, bool open = true);
    void SaveScheme(Guid id);
    bool DeleteScheme(Guid id);
    void FlushWorkingCopy();
    void SaveProject(string path);
    IReadOnlyList<SchematicWorkspace> LoadProject(string path, bool merge = false);
    IReadOnlyList<SchematicWorkspace> LoadLegacyProject(string path);
    void WriteData(string relativePath, byte[] data);
    byte[] ReadData(string relativePath);
    IReadOnlyList<string> GetDataFiles();
}
