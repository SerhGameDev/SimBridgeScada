using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;
using SimBridge_Scada.Project.Models;
using SimBridge_Scada.ViewModels;
using SimBridge_Scada.ViewModels.Components;
using SimBridge_Scada.Workspaces.Persistence;
namespace SimBridge_Scada.Project.Services;

/// <summary>UI-thread project service. Each instance owns a recovery folder beside the application.</summary>
public sealed class ProjectService : IProjectService
{
    public const string FileExtension=".simbridgescada";
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    private readonly WorkspaceManagerViewModel _manager;
    private readonly WorkspaceStorage _storage;
    private readonly Func<string,SchematicWorkspace> _factory;
    private readonly IProjectDataParticipant[] _participants;
    private readonly string _session;
    private readonly List<Action> _subscriptions=[];
    private readonly DispatcherTimer _timer;
    private bool _pending=true, _busy, _disposed, _catalogDirty=true;
    public string WorkingDirectory {get;}
    public string SchemesDirectory=>Path.Combine(WorkingDirectory,"Schemes");
    public string DataDirectory=>Path.Combine(WorkingDirectory,"Data");
    public string? CurrentFilePath {get;private set;}
    public string? LastWorkingCopyError {get;private set;}
    public bool HasChanges=>_catalogDirty || _manager.AllWorkspaces.Any(w=>w.IsDirty);
    public ProjectService(WorkspaceManagerViewModel manager,WorkspaceStorage storage,Func<string,SchematicWorkspace> factory,string? applicationDirectory=null,IEnumerable<IProjectDataParticipant>? participants=null)
    {
        _manager=manager;_storage=storage;_factory=factory;_participants=participants?.ToArray()??[];
        _session=Path.Combine(Path.GetFullPath(applicationDirectory??AppContext.BaseDirectory),"ProjectTemp",Guid.NewGuid().ToString("N"));
        WorkingDirectory=Path.Combine(_session,"Current");
        Directory.CreateDirectory(SchemesDirectory);Directory.CreateDirectory(DataDirectory);Directory.CreateDirectory(Path.Combine(WorkingDirectory,"Assets"));
        Rewire();
        _timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(2)};
        _timer.Tick+=AutoFlush;_timer.Start();
    }
    public IReadOnlyList<SchemeInfo> GetSchemes()=>_manager.AllWorkspaces.Select(w=>new SchemeInfo(w.Id,w.Title,w.Elements.Count,w.IsOpen,w.IsDirty,SchemePath(SchemesDirectory,w.Id))).ToArray();
    public SchematicWorkspace? GetScheme(Guid id)=>_manager.AllWorkspaces.FirstOrDefault(w=>w.Id==id);
    public WorkspaceFile GetSchemeData(Guid id)=>_storage.Capture(GetScheme(id)??throw new KeyNotFoundException("Схема не найдена."));
    public SchematicWorkspace AddScheme(WorkspaceFile data,bool open=true)=>_manager.Add(_storage.Restore(data,factory:_factory),open);
    public void SaveScheme(Guid id) { _=GetScheme(id)??throw new KeyNotFoundException("Схема не найдена.");FlushWorkingCopy(); }
    public bool DeleteScheme(Guid id)
    {
        var document=GetScheme(id);if(document is null)return false;
        _manager.Remove(document);_pending=true;
        try {FlushWorkingCopy();}
        catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or NotSupportedException or ArgumentException){LastWorkingCopyError=error.Message;}
        return true;
    }
    public void WriteData(string relativePath,byte[] data)
    {
        Check();var target=ProjectFiles.Inside(DataDirectory,relativePath);
        ProjectFiles.WriteAtomic(target,stream=>stream.Write(data));_catalogDirty=true;_pending=true;
    }
    public byte[] ReadData(string relativePath)=>File.ReadAllBytes(ProjectFiles.Inside(DataDirectory,relativePath));
    public IReadOnlyList<string> GetDataFiles()=>Directory.EnumerateFiles(DataDirectory,"*",SearchOption.AllDirectories).Select(p=>Path.GetRelativePath(DataDirectory,p)).ToArray();
    private static string SchemePath(string folder,Guid id)=>Path.Combine(folder,id.ToString("N")+".json");
    public void FlushWorkingCopy()
    {
        Check();if(_busy)return;_busy=true;var candidate=NewStage();
        try
        {
            BuildSnapshot(candidate);CommitDirectory(candidate);SetDocumentDirectories();_pending=false;LastWorkingCopyError=null;
        }
        finally { DeleteStage(candidate);_busy=false; }
    }
    public void SaveProject(string path)
    {
        Check();path=Path.GetFullPath(path);
        if(!path.EndsWith(FileExtension,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Проект должен иметь расширение .simbridgescada.");
        if(path.StartsWith(_session+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Архив проекта должен находиться вне временной рабочей папки.");
        _busy=true;var candidate=NewStage();
        try
        {
            BuildSnapshot(candidate);
            var tempArchive=Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using(var output=new FileStream(tempArchive,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {ProjectFiles.Zip(candidate,output);output.Flush(true);}
                CommitDirectory(candidate);
                if(File.Exists(path))File.Replace(tempArchive,path,null);else File.Move(tempArchive,path);
            }
            finally {if(File.Exists(tempArchive))File.Delete(tempArchive);}
            CurrentFilePath=path;SetDocumentDirectories();            foreach(var document in _manager.AllWorkspaces)document.MarkProjectSaved(path);
            _catalogDirty=false;_pending=false;LastWorkingCopyError=null;
        }
        finally {DeleteStage(candidate);_busy=false;}
    }
    private void BuildSnapshot(string destination)
    {
        ProjectFiles.CopyTree(DataDirectory,Path.Combine(destination,"Data"));
        foreach(var participant in _participants)ProjectFiles.WriteAtomic(ProjectFiles.Inside(Path.Combine(destination,"Data"),participant.DataPath),s=>s.Write(participant.CaptureProjectData()));
        ProjectFiles.CopyTree(Path.Combine(WorkingDirectory,"Assets"),Path.Combine(destination,"Assets"));
        WriteSnapshot(destination,_manager.AllWorkspaces.ToArray(),new ProjectManifest
        {
            Schemes=_manager.AllWorkspaces.Select(w=>w.Id).ToList(),OpenSchemes=_manager.OpenWorkspaces.Select(w=>w.Id).ToList(),ActiveScheme=_manager.ActiveWorkspace?.Id
        });
    }
    private void WriteSnapshot(string destination,IReadOnlyList<SchematicWorkspace> documents,ProjectManifest manifest)
    {
        if(documents.Count>5000)throw new InvalidDataException("Проект содержит более 5000 схем.");
        var schemes=Path.Combine(destination,"Schemes");Directory.CreateDirectory(schemes);
        foreach(var document in documents)
        {
            var model=_storage.Capture(document);
            foreach(var element in document.Elements)
            {
                var saved=model.Elements.Single(e=>e.Id==element.Id);
                for(var i=0;i<element.Components.Count;i++)
                {
                    if(element.Components[i] is not ImageComponentViewModel image || string.IsNullOrWhiteSpace(image.ImagePath))continue;
                    var source=image.ResolvePath();
                    if(!File.Exists(source))throw new FileNotFoundException($"Не найдено изображение объекта «{element.Name}».",source);
                    using var file=File.OpenRead(source);
                    var hash=Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();
                    var extension=Path.GetExtension(source).ToLowerInvariant();
                    if(extension.Length>12 || extension.Skip(1).Any(c=>!char.IsLetterOrDigit(c)))extension=".bin";
                    var relative=$"Assets/Images/{hash}{extension}";
                    var target=ProjectFiles.Inside(destination,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    if(!File.Exists(target))File.Copy(source,target);
                    saved.Components[i].Data[nameof(ImageComponentViewModel.ImagePath)]=JsonSerializer.SerializeToElement(relative);
                }
            }
            var json=JsonSerializer.Serialize(model,Json);
            if(System.Text.Encoding.UTF8.GetByteCount(json)>32*1024*1024)throw new InvalidDataException("Схема превышает 32 МБ.");
            File.WriteAllText(SchemePath(schemes,document.Id),json);
        }
        File.WriteAllText(Path.Combine(destination,"project.json"),JsonSerializer.Serialize(manifest,Json));
    }
    public IReadOnlyList<SchematicWorkspace> LoadProject(string path,bool merge=false)
    {
        Check();path=Path.GetFullPath(path);var wasCatalogDirty=_catalogDirty;_busy=true;var extracted=NewStage();var candidate=NewStage();
        try
        {
            ProjectFiles.Extract(path,extracted);
            var manifest=ReadManifest(extracted);
            var prepared=_participants.Select(p=>{var file=ProjectFiles.Inside(Path.Combine(extracted,"Data"),p.DataPath);return (Participant:p,Prepared:p.PrepareProjectData(File.Exists(file)?File.ReadAllBytes(file):null,merge));}).ToArray();
            var serviceDataChanged=merge&&prepared.Any(p=>!p.Prepared.Data.SequenceEqual(p.Participant.CaptureProjectData()));
            var documents=new List<SchematicWorkspace>();
            foreach(var id in manifest.Schemes)
            {
                var schemeFile=SchemePath(Path.Combine(extracted,"Schemes"),id);
                if(new FileInfo(schemeFile).Length>32*1024*1024)throw new InvalidDataException("Схема превышает 32 МБ.");
                var model=JsonSerializer.Deserialize<WorkspaceFile>(File.ReadAllText(schemeFile))??throw new InvalidDataException("Пустая схема.");
                if(model.Id!=id)throw new InvalidDataException("Идентификатор схемы не совпадает с манифестом.");
                var document=_storage.Restore(model,factory:_factory);document.WorkingDirectory=extracted;
                foreach(var image in document.Elements.SelectMany(e=>e.Components).OfType<ImageComponentViewModel>())
                    if(!string.IsNullOrWhiteSpace(image.ImagePath) && !File.Exists(ProjectFiles.Inside(extracted,image.ImagePath)))throw new InvalidDataException("В архиве отсутствует изображение.");
                documents.Add(document);
            }
            if(merge)
            {
                ProjectFiles.CopyTree(DataDirectory,Path.Combine(candidate,"Data"));
                ProjectFiles.CopyTree(Path.Combine(WorkingDirectory,"Assets"),Path.Combine(candidate,"Assets"));
            }
            // Service data is validated and merged by its owning participant.
            foreach(var item in prepared){var file=ProjectFiles.Inside(Path.Combine(extracted,"Data"),item.Participant.DataPath);if(File.Exists(file))File.Delete(file);}
            ProjectFiles.CopyTree(Path.Combine(extracted,"Data"),Path.Combine(candidate,"Data"),merge);
            foreach(var item in prepared)ProjectFiles.WriteAtomic(ProjectFiles.Inside(Path.Combine(candidate,"Data"),item.Participant.DataPath),s=>s.Write(item.Prepared.Data));
            ProjectFiles.CopyTree(Path.Combine(extracted,"Assets"),Path.Combine(candidate,"Assets"),merge);
            var incoming=documents.ToDictionary(w=>w.Id);
            var existing=merge?_manager.AllWorkspaces.ToArray():[];
            var map=existing.ToDictionary(w=>w.Id);
            foreach(var document in documents)map.TryAdd(document.Id,document);
            var openIds=merge?_manager.OpenWorkspaces.Select(w=>w.Id).ToList():[];
            foreach(var id in manifest.OpenSchemes)if(!openIds.Contains(id))openIds.Add(id);
            var activeId=manifest.ActiveScheme??(merge?_manager.ActiveWorkspace?.Id:null);
            WriteSnapshot(candidate,map.Values.ToArray(),new ProjectManifest { Schemes=map.Keys.ToList(),OpenSchemes=openIds,ActiveScheme=activeId });
            // Nothing is applied until all JSON, types, hierarchy, assets and data conflicts pass validation.
            CommitDirectory(candidate);
            foreach(var item in prepared)item.Prepared.Apply();
            if(!merge)_manager.Reset();
            foreach(var document in documents)
                if(!_manager.AllWorkspaces.Any(w=>w.Id==document.Id)){document.WorkingDirectory=WorkingDirectory;document.MarkProjectSaved(path);_manager.Add(document,false);}
            foreach(var id in manifest.OpenSchemes)_manager.Open(map[id]);
            if(manifest.ActiveScheme is { } active)_manager.Open(map[active]);
            else if(!merge)_manager.ActiveWorkspace=null;
            CurrentFilePath=path;_catalogDirty=merge && (wasCatalogDirty||serviceDataChanged||existing.Any(w=>!incoming.ContainsKey(w.Id)||w.IsDirty));
            SetDocumentDirectories();Rewire();_pending=false;LastWorkingCopyError=null;return documents.Select(d=>map[d.Id]).ToArray();
        }
        finally {DeleteStage(extracted);DeleteStage(candidate);_busy=false;}
    }
    /// <summary>Replace an old JSON project and reset services that were not part of that format.</summary>
    public IReadOnlyList<SchematicWorkspace> LoadLegacyProject(string path)
    {
        Check();path=Path.GetFullPath(path);
        var loaded=new WorkspaceProjectStorage(_storage).Load(path,_factory);
        var prepared=_participants.Select(p=>(Participant:p,Prepared:p.PrepareProjectData(null,false))).ToArray();
        var candidate=NewStage();_busy=true;
        try
        {
            foreach(var item in prepared)ProjectFiles.WriteAtomic(ProjectFiles.Inside(Path.Combine(candidate,"Data"),item.Participant.DataPath),s=>s.Write(item.Prepared.Data));
            WriteSnapshot(candidate,loaded.Workspaces,new ProjectManifest{Schemes=loaded.Workspaces.Select(w=>w.Id).ToList(),OpenSchemes=loaded.OpenIds.ToList(),ActiveScheme=loaded.ActiveId});
            CommitDirectory(candidate);
            foreach(var item in prepared)item.Prepared.Apply();
            _manager.Reset();
            foreach(var document in loaded.Workspaces)_manager.Add(document,false);
            foreach(var id in loaded.OpenIds)_manager.Open(_manager.AllWorkspaces.Single(w=>w.Id==id));
            _manager.ActiveWorkspace=loaded.ActiveId is {} active?_manager.AllWorkspaces.Single(w=>w.Id==active):null;
            CurrentFilePath=null;_catalogDirty=true;_pending=false;LastWorkingCopyError=null;
            SetDocumentDirectories();Rewire();return loaded.Workspaces;
        }
        finally{DeleteStage(candidate);_busy=false;}
    }
    private static ProjectManifest ReadManifest(string directory)
    {
        var path=Path.Combine(directory,"project.json");
        if(new FileInfo(path).Length>1024*1024)throw new InvalidDataException("Слишком большой манифест проекта.");
        var model=JsonSerializer.Deserialize<ProjectManifest>(File.ReadAllText(path))??throw new InvalidDataException("Пустой манифест проекта.");
        if(model.Format!="SimBridge.ScadaProject"||model.Version!=1||model.Schemes is null||model.OpenSchemes is null||model.Schemes.Count>5000
            ||model.Schemes.Contains(Guid.Empty)||model.Schemes.Distinct().Count()!=model.Schemes.Count
            ||model.OpenSchemes.Distinct().Count()!=model.OpenSchemes.Count||model.OpenSchemes.Any(id=>!model.Schemes.Contains(id))
            ||model.ActiveScheme is {} active&&!model.OpenSchemes.Contains(active))throw new InvalidDataException("Некорректный манифест проекта.");
        return model;
    }
    private string NewStage(){var path=Path.Combine(_session,"Stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);return path;}
    private void CommitDirectory(string stage)
    {
        var backup=Path.Combine(_session,"Backup-"+Guid.NewGuid().ToString("N"));
        if(Directory.Exists(WorkingDirectory))Directory.Move(WorkingDirectory,backup);
        try {Directory.Move(stage,WorkingDirectory);Directory.CreateDirectory(DataDirectory);Directory.CreateDirectory(SchemesDirectory);}
        catch {if(Directory.Exists(backup)&&!Directory.Exists(WorkingDirectory))Directory.Move(backup,WorkingDirectory);throw;}
        DeleteStage(backup);
    }
    private void DeleteStage(string directory)
    {
        var full=Path.GetFullPath(directory);
        if(!full.StartsWith(_session+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||full==WorkingDirectory)throw new InvalidOperationException("Недопустимая временная папка.");
        if(Directory.Exists(full))Directory.Delete(full,true);
    }
    private void SetDocumentDirectories(){foreach(var document in _manager.AllWorkspaces)document.WorkingDirectory=WorkingDirectory;}
    private void Check(){ObjectDisposedException.ThrowIf(_disposed,this);}
    private void AutoFlush(object? sender,EventArgs e)
    {
        if(!_pending||_busy||_disposed)return;
        try {FlushWorkingCopy();}catch(Exception error) when(error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or InvalidOperationException or NotSupportedException or ArgumentException){LastWorkingCopyError=error.Message;}
    }
    private void Schedule(object? sender,PropertyChangedEventArgs e){if(sender?.GetType().GetProperty(e.PropertyName??"")?.IsDefined(typeof(SimBridge_Scada.Infrastructure.RuntimeStateAttribute),true)==true)return;if(!_busy)_pending=true;}
    private void ConfigurationChanged(object? sender,EventArgs e){if(!_busy){_catalogDirty=true;_pending=true;}}
    private void StructureChanged(object? sender,NotifyCollectionChangedEventArgs e){if(_busy)return;_catalogDirty=true;Rewire();_pending=true;}
    private void Rewire()
    {
        foreach(var unsubscribe in _subscriptions)unsubscribe();_subscriptions.Clear();
        foreach(var participant in _participants){participant.ConfigurationChanged+=ConfigurationChanged;_subscriptions.Add(()=>participant.ConfigurationChanged-=ConfigurationChanged);}
        void Watch(INotifyPropertyChanged value){value.PropertyChanged+=Schedule;_subscriptions.Add(()=>value.PropertyChanged-=Schedule);}
        void WatchList(INotifyCollectionChanged value){value.CollectionChanged+=StructureChanged;_subscriptions.Add(()=>value.CollectionChanged-=StructureChanged);}
        Watch(_manager);WatchList(_manager.AllWorkspaces);WatchList(_manager.OpenWorkspaces);
        foreach(var document in _manager.AllWorkspaces)
        {
            Watch(document);Watch(document.ViewModel);WatchList(document.Elements);
            foreach(var element in document.Elements){Watch(element);WatchList(element.Components);foreach(var component in element.Components)Watch(component);}
        }
    }
    public void Dispose(){if(_disposed)return;_timer.Stop();foreach(var unsubscribe in _subscriptions)unsubscribe();_subscriptions.Clear();_disposed=true;}
}



