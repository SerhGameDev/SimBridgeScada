using System.IO;
using System.Text.Json;
using SimBridge_Scada.ViewModels;
using SimBridge_Scada.ViewModels.Components;
namespace SimBridge_Scada.Workspaces.Persistence;

public sealed class WorkspaceStorage(WorkspaceTypeRegistry registry)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = false };
    public const string FileExtension = ".simbridge";
    public void Save(SchematicWorkspace document, string path)
    {
        var model = Capture(document);
        var json = JsonSerializer.Serialize(model, Options); // Validate every registered type before touching disk.
        path = Path.GetFullPath(path);
        var temp = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            { writer.Write(json); writer.Flush(); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            document.MarkSaved(path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public SchematicWorkspace Load(string path, Func<string, SchematicWorkspace>? factory = null)
    {
        path = Path.GetFullPath(path);
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Файл схемы превышает 32 МБ.");
        WorkspaceFile model;
        try { model = JsonSerializer.Deserialize<WorkspaceFile>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Пустой файл схемы."); }
        catch (JsonException e) { throw new InvalidDataException("Файл не содержит корректную схему SimBridge.", e); }
        return Restore(model, path, factory);
    }
    public WorkspaceFile Capture(SchematicWorkspace document)
    {
        return new WorkspaceFile
        {
            Id = document.Id, Title = document.Title, Zoom = document.ViewModel.Zoom,
            OffsetX = document.ViewModel.OffsetX, OffsetY = document.ViewModel.OffsetY, SnapToGrid = document.ViewModel.SnapToGrid,
            Elements = document.Elements.Select(element => new ElementFile
            {
                Id = element.Id, ParentId = element.Parent?.Id, SiblingIndex = document.ViewModel.GetSiblingIndex(element), Kind = registry.ElementKey(element), Data = WorkspaceTypeRegistry.Capture(element, true),
                Components = element.Components.Select(component => new ComponentFile
                { Kind = registry.ComponentKey(component), Data = WorkspaceTypeRegistry.Capture(component) }).ToList()
            }).ToList()
        };
    }
    public SchematicWorkspace Restore(WorkspaceFile model, string? path = null, Func<string, SchematicWorkspace>? factory = null)
    {
        if (model.Version is not 1 and not 2 || model.Id == Guid.Empty || string.IsNullOrWhiteSpace(model.Title) || model.Elements is null
            || !double.IsFinite(model.Zoom) || model.Zoom < .25 || model.Zoom > 2
            || !double.IsFinite(model.OffsetX) || model.OffsetX < 0 || !double.IsFinite(model.OffsetY) || model.OffsetY < 0)
            throw new InvalidDataException("Некорректная или неподдерживаемая версия схемы.");
        var document = factory?.Invoke(model.Title) ?? new SchematicWorkspace(model.Title);
        document.Id = model.Id;
        var ids = new HashSet<Guid>();
        foreach (var item in model.Elements)
        {
            if (item is null || item.Id == Guid.Empty || !ids.Add(item.Id) || item.Components is null || item.Data is null || string.IsNullOrWhiteSpace(item.Kind))
                throw new InvalidDataException("Некорректный объект схемы.");
            var element = registry.CreateElement(item.Kind); element.Id = item.Id;
            WorkspaceTypeRegistry.Apply(element, item.Data, true);
            if (model.Version >= 2)
                foreach (var optional in element.Components.Where(c => c != element.Transform && c != element.ObjectProperties).ToArray()) element.RemoveComponent(optional);
            var types = new HashSet<Type>();
            var restoredComponents = new List<ComponentViewModel>();
            foreach (var saved in item.Components)
            {
                if (saved is null || saved.Data is null || string.IsNullOrWhiteSpace(saved.Kind)) throw new InvalidDataException("Некорректный компонент.");
                var type = registry.ComponentType(saved.Kind);
                if (!types.Add(type) && (type == typeof(ObjectPropertiesComponentViewModel) || type == typeof(TransformComponentViewModel)))
                    throw new InvalidDataException("Повторяющийся обязательный компонент.");
                var component = element.Components.FirstOrDefault(c => c.GetType() == type && !restoredComponents.Contains(c)) ?? element.AddComponent(registry.CreateComponent(saved.Kind));
                WorkspaceTypeRegistry.Apply(component, saved.Data); restoredComponents.Add(component);
            }
            if (!types.Contains(typeof(ObjectPropertiesComponentViewModel)) || !types.Contains(typeof(TransformComponentViewModel)))
                throw new InvalidDataException("Отсутствуют обязательные компоненты объекта.");
            for (var i = 0; i < restoredComponents.Count; i++) element.MoveComponent(restoredComponents[i], i);
            document.ViewModel.AddElement(element, element.X, element.Y);
        }
        var byId = document.Elements.ToDictionary(e => e.Id);
        foreach (var item in model.Elements.OrderBy(e => e.SiblingIndex))
        {
            SchematicElementViewModel? parent = null;
            if (item.ParentId is { } parentId && !byId.TryGetValue(parentId, out parent)) throw new InvalidDataException("Неизвестный родитель объекта.");
            if (item.SiblingIndex < 0 || !document.ViewModel.CanSetParent(byId[item.Id], parent)) throw new InvalidDataException("Некорректная иерархия объектов.");
            document.ViewModel.SetParent(byId[item.Id], parent, false, item.SiblingIndex);
        }
        document.ViewModel.SelectedElement = null;
        document.ViewModel.Zoom = model.Zoom; document.ViewModel.OffsetX = model.OffsetX; document.ViewModel.OffsetY = model.OffsetY;
        document.ViewModel.SnapToGrid = model.SnapToGrid;
        if (path is not null) document.MarkSaved(path);
        return document;
    }
}


