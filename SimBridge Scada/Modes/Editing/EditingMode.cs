using SimBridge_Scada.Project.Services;
using SimBridge_Scada.Services;
using SimBridge_Scada.Modbus.Services;
using SimBridge_Scada.Modbus.ViewModel;
using SimBridge_Scada.Modbus.View;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SimBridge_Scada.Infrastructure;
using SimBridge_Scada.Shared.Menus;
using SimBridge_Scada.Workspaces.Persistence;
using SimBridge_Scada.ViewModels;
using SimBridge_Scada.ViewModels.Components;
using SimBridge_Scada.Views;
using SimBridge_Scada.Views.Components.ObjectProperties;
using SimBridge_Scada.Views.Components.Transform;
namespace SimBridge_Scada.Modes.Editing;

/// <summary>Owns editor services, view registrations, panels, commands and document actions.</summary>
public sealed class EditingMode : IApplicationMode
{
    private WorkspaceManagerViewModel _workspaces = null!;
    private WorkspaceStorage _storage = null!;
    private WorkspaceProjectStorage _projectStorage = null!;
    private IProjectService _projectService = null!;
    private ModbusService _modbus = null!;
    private ModbusWindow? _modbusWindow;
    public IProjectService ProjectService => _projectService;
    private MainWindow _window = null!;
    private readonly List<object> _resourceKeys = [];
    private readonly List<InputBinding> _bindings = [];
    private bool _initialized, _disposed;
    public string Name => "Редактирование";
    public void Initialize(MainWindow window)
    {
        if(_initialized)throw new InvalidOperationException("Режим уже инициализирован.");
        _initialized=true;_window=window;
        _modbus=new ModbusService();
        ServiceLocator.Current.Register<IModbusService>(_modbus,replace:true);
        RegisterView<WorkspaceViewModel, WorkspaceView>();
        RegisterView<WorkspaceManagerViewModel, WorkspaceTabsView>();
        RegisterView<WorkspaceExplorerViewModel, WorkspaceExplorerView>();
        RegisterView<ElementLibraryViewModel, ElementLibraryView>();
        RegisterView<SchemeElementsViewModel, SchemeElementsView>();
        RegisterView<PropertiesViewModel, PropertiesView>();
        RegisterView<LeftSidebarViewModel, LeftSidebarView>();
        RegisterView<RightSidebarViewModel, RightSidebarView>();
        RegisterView<ObjectPropertiesComponentViewModel, ObjectPropertiesComponentView>();
        RegisterView<TransformComponentViewModel, TransformComponentView>();
        RegisterView<ImageComponentViewModel, ImageComponentView>();
        RegisterView<TextComponentViewModel, TextComponentView>();
        RegisterView<SymbolComponentViewModel, SymbolComponentView>();

        var types = new WorkspaceTypeRegistry();
        types.RegisterElement("empty", () => new EmptyElementViewModel());
        types.RegisterElement("pump", () => new PumpElementViewModel());
        types.RegisterElement("valve", () => new ValveElementViewModel());
        types.RegisterElement("tank", () => new TankElementViewModel());
        types.RegisterComponent("object", () => new ObjectPropertiesComponentViewModel());
        types.RegisterComponent("transform", () => new TransformComponentViewModel());
        types.RegisterComponent("image", () => new ImageComponentViewModel());
        types.RegisterComponent("text", () => new TextComponentViewModel());
        types.RegisterComponent("symbol", () => new SymbolComponentViewModel());
        _storage = new WorkspaceStorage(types);
        _projectStorage = new WorkspaceProjectStorage(_storage);
        _workspaces = new WorkspaceManagerViewModel(CreateDocument);
        _workspaces.FileImporter = paths => { foreach (var path in paths.Where(File.Exists)) LoadWorkspace(path,mergeProject:true); };
        var first = _workspaces.CreateWorkspace();
        _workspaces.CreateWorkspace(); _workspaces.CreateWorkspace(open: false); _workspaces.Open(first);

        _projectService = new ProjectService(_workspaces, _storage, CreateDocument,participants:[_modbus]);
        ServiceLocator.Current.Register<IProjectService>(_projectService,replace:true);

        var library = new ElementLibraryViewModel([
            new("Пустой объект", () => new EmptyElementViewModel(), _workspaces),
            new("Насос", () => new PumpElementViewModel(), _workspaces),
            new("Задвижка", () => new ValveElementViewModel(), _workspaces),
            new("Резервуар", () => new TankElementViewModel(), _workspaces)
        ]);
        var elements = new SchemeElementsViewModel(_workspaces);
        var explorer = new WorkspaceExplorerViewModel(_workspaces, () => LoadWorkspace(), () => SaveAllWorkspaces());
        var inspector = new PropertiesViewModel(_workspaces, [
            new("Изображение", () => new ImageComponentViewModel()),
            new("Текст", () => new TextComponentViewModel()),
            new("Символ оборудования", () => new SymbolComponentViewModel())
        ]);
        var libraryButton = new SidebarButtonViewModel("Библиотека элементов", "Assets/Icons/library.png", library);
        var elementsButton = new SidebarButtonViewModel("Элементы схемы", "Assets/Icons/elements.png", elements);
        var explorerButton = new SidebarButtonViewModel("Рабочие области", "Assets/Icons/workspaces.png", explorer);
        var inspectorButton = new SidebarButtonViewModel("Inspector", "Assets/Icons/properties.png", inspector);
        var modbusButton=new SidebarButtonViewModel("Modbus TCP","Assets/Icons/modbus.png",ShowModbusWindow);
        var left = new LeftSidebarViewModel([explorerButton, libraryButton, elementsButton,modbusButton]) { Width = 260 };
        var right = new RightSidebarViewModel([inspectorButton]) { Width = 340 };
        explorerButton.ClickCommand.Execute(null); inspectorButton.ClickCommand.Execute(null);
        _window.DataContext = new EditorViewModel(_workspaces, left, right, new MenuButtonViewModel(CreateFileMenu));

        // Window scope also saves while keyboard focus is inside Inspector text fields.
        AddBinding(new KeyBinding(new RelayCommand(() => SaveProject()), new KeyGesture(Key.S, ModifierKeys.Control)));
        AddBinding(new KeyBinding(new RelayCommand(() => LoadWorkspace()), new KeyGesture(Key.O, ModifierKeys.Control)));
        AddBinding(new KeyBinding(_workspaces.CreateWorkspaceCommand, new KeyGesture(Key.N, ModifierKeys.Control)));
    }
    public void ShowModbusWindow()
    {
        if(_modbusWindow is not null){if(_modbusWindow.WindowState==WindowState.Minimized)_modbusWindow.WindowState=WindowState.Normal;_modbusWindow.Activate();return;}
        _modbusWindow=new ModbusWindow(new ModbusWindowViewModel(_modbus)){Owner=_window};
        _modbusWindow.Closed+=(_,_)=>_modbusWindow=null;
        _modbusWindow.Show();
    }
    private SchematicWorkspace CreateDocument(string title) => new(title, document =>
    {
        document.ConfigureActions(d => SaveWorkspace(d), d => DeleteWorkspace(d), CreateWorkspaceMenu);
        // document.RegisterShortcut(...) configures additional document shortcuts here.
    });
    private ActionMenu CreateFileMenu() => new([
        new("Создать", [new ActionMenuItem("Мнемосхему", () => _workspaces.CreateWorkspace()) { GestureText = "Ctrl+N" }]),
        new("Открыть…", () => LoadWorkspace()) { GestureText = "Ctrl+O" },
        ActionMenuItem.Separator(),
        new("Сохранить проект", () => SaveProject()) { GestureText = "Ctrl+S" },
        new("Сохранить проект как…", () => SaveProject(saveAs: true)),
        new("Сохранить все области в проект…", () => SaveAllWorkspaces()),
        ActionMenuItem.Separator(),
        new("Удалить схему из списка…", () => DeleteWorkspace(), () => _workspaces.ActiveWorkspace is not null)
    ]);
    private ActionMenu CreateWorkspaceMenu(SchematicWorkspace document) => new([
        new("Открыть", () => _workspaces.Open(document)),
        new("Переименовать…", () => RenameWorkspace(document)),
        new("Сохранение", [
            new ActionMenuItem("Сохранить", () => SaveWorkspace(document)) { GestureText = "Ctrl+S" },
            new ActionMenuItem("Сохранить как…", () => SaveWorkspace(document, saveAs: true))
        ]),
        new("Закрыть вкладку", () => _workspaces.Close(document), () => document.IsOpen),
        ActionMenuItem.Separator(),
        new("Удалить из списка…", () => DeleteWorkspace(document))
    ]);

    public bool SaveWorkspace(SchematicWorkspace? document = null, string? path = null, bool saveAs = false)
    {
        document ??= _workspaces.ActiveWorkspace;
        if (document is null) return false;
        if (path?.EndsWith(".simbridgescada", StringComparison.OrdinalIgnoreCase) == true) return SaveProject(path);
        // Commit edited text, including numeric fields with LostFocus bindings, before serialization.
        if (document == _workspaces.ActiveWorkspace && !CommitPendingEdits()) return false;
        if (path is null && !saveAs) return SaveProject();
        if (path is null && !saveAs) path = document.FilePath;
        if (path is null)
        {
            var name = string.Concat(document.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var dialog = new SaveFileDialog { Title = "Сохранить мнемосхему", Filter = "Схема SimBridge (*.simbridge)|*.simbridge", DefaultExt = ".simbridge", AddExtension = true, FileName = name };
            if (dialog.ShowDialog(_window) != true) return false;
            path = dialog.FileName;
        }
        try
        {
            path = Path.GetFullPath(path);
            if (_workspaces.AllWorkspaces.Any(w => w != document && string.Equals(w.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Этот файл уже используется другой открытой схемой.");
            _storage.Save(document, path); return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        { MessageBox.Show(_window, e.Message, "Не удалось сохранить схему", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }
    public SchematicWorkspace? LoadWorkspace(string? path = null,bool mergeProject = false)
    {
        if (path is null)
        {
            var dialog = new OpenFileDialog { Title = "Открыть мнемосхему", Filter = "Проект SimBridge SCADA (*.simbridgescada)|*.simbridgescada|Все файлы SimBridge|*.simbridgescada;*.simbridge;*.simbridge-project|Схема (*.simbridge)|*.simbridge|Старый проект (*.simbridge-project)|*.simbridge-project|JSON (*.json)|*.json", CheckFileExists = true };
            if (dialog.ShowDialog(_window) != true) return null;
            path = dialog.FileName;
        }
        try
        {
            path = Path.GetFullPath(path);
            if (path.EndsWith(".simbridgescada", StringComparison.OrdinalIgnoreCase)) return LoadProject(path,mergeProject);
            if (new FileInfo(path).Length > 128 * 1024 * 1024) throw new InvalidDataException("Файл превышает 128 МБ.");
            using (var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)))
                if (json.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && json.RootElement.TryGetProperty("Format", out var format)
                    && format.ValueKind == System.Text.Json.JsonValueKind.String && format.GetString() == "SimBridge.Project")
                    return ImportLegacyProject(path,mergeProject);
            var existing = _workspaces.AllWorkspaces.FirstOrDefault(w => string.Equals(w.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) { _workspaces.Open(existing); return existing; }
            var document = _storage.Load(path, CreateDocument);
            existing = _workspaces.AllWorkspaces.FirstOrDefault(w => w.Id == document.Id);
            if (existing is not null) { _workspaces.Open(existing); return existing; }
            return _workspaces.Add(document);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        { MessageBox.Show(_window, e.Message, "Не удалось открыть схему", MessageBoxButton.OK, MessageBoxImage.Error); return null; }
    }
    public bool SaveAllWorkspaces(string? path = null) => SaveProject(path, saveAs: path is null);
    public bool SaveProject(string? path = null, bool saveAs = false)
    {
        if (!CommitPendingEdits()) return false;
        if (path is null && !saveAs) path = _projectService.CurrentFilePath;
        if (path is null)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Сохранить проект SCADA", Filter = "Проект SimBridge SCADA (*.simbridgescada)|*.simbridgescada",
                DefaultExt = ".simbridgescada", AddExtension = true,
                FileName = _projectService.CurrentFilePath is { } previous ? Path.GetFileName(previous) : "Проект"
            };
            if (dialog.ShowDialog(_window) != true) return false;
            path = dialog.FileName;
        }
        try { _projectService.SaveProject(path); return true; }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        { MessageBox.Show(_window, e.Message, "Не удалось сохранить проект", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }
    public SchematicWorkspace? LoadProject(string? path = null,bool merge = false)
    {
        if (path is null)
        {
            var dialog = new OpenFileDialog { Title = "Открыть проект SCADA", Filter = "Проект SimBridge SCADA (*.simbridgescada)|*.simbridgescada", CheckFileExists = true };
            if (dialog.ShowDialog(_window) != true) return null;
            path = dialog.FileName;
        }
        try
        {
            _projectService.LoadProject(path, merge);
            if(!merge)_modbusWindow?.Close();
            return _workspaces.ActiveWorkspace;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException)
        { MessageBox.Show(_window, e.Message, "Не удалось открыть проект", MessageBoxButton.OK, MessageBoxImage.Error); return null; }
    }
    private SchematicWorkspace? ImportLegacyProject(string path,bool merge)
    {
        if(!merge){_projectService.LoadLegacyProject(path);_modbusWindow?.Close();return _workspaces.ActiveWorkspace;}
        // Deserialize every document before mutating the current catalog.
        var project = _projectStorage.Load(path, CreateDocument);
        var map = new Dictionary<Guid, SchematicWorkspace>();
        foreach (var document in project.Workspaces)
        {
            var existing = _workspaces.AllWorkspaces.FirstOrDefault(w => w.Id == document.Id);
            map[document.Id] = existing ?? _workspaces.Add(document, open: false);
        }
        foreach (var id in project.OpenIds) _workspaces.Open(map[id]);
        if (project.ActiveId is { } active) _workspaces.Open(map[active]);
        else if (project.OpenIds.Count == 0 && project.Workspaces.Count > 0) _workspaces.Open(map[project.Workspaces[0].Id]);

        return _workspaces.ActiveWorkspace;
    }
    public bool RenameWorkspace(SchematicWorkspace? document = null)
    {
        document ??= _workspaces.ActiveWorkspace;
        if (document is null) return false;
        return new RenameWorkspaceWindow(new RenameWorkspaceViewModel(document)) { Owner = _window }.ShowDialog() == true;
    }
    public bool DeleteWorkspace(SchematicWorkspace? document = null, bool confirm = true)
    {
        document ??= _workspaces.ActiveWorkspace;
        if (document is null) return false;
        if (confirm && MessageBox.Show(_window,
            $"Удалить «{document.Title}» из списка рабочих областей?\n" + (document.IsDirty ? "Несохранённые изменения будут потеряны.\n" : "") + "Сохранённый файл на диске останется.",
            "Удаление схемы", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return false;
        return _projectService.DeleteScheme(document.Id);
    }
    public bool CanClose()
    {
        if(!_initialized||_disposed||!_projectService.HasChanges)return true;
        var result=MessageBox.Show(_window,"В проекте есть несохранённые изменения. Сохранить перед выходом?","SimBridge SCADA",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
        return result!=MessageBoxResult.Cancel&&(result!=MessageBoxResult.Yes||SaveProject());
    }
    private void AddBinding(InputBinding binding){_bindings.Add(binding);_window.InputBindings.Add(binding);}
    public void Dispose()
    {
        if(_disposed||!_initialized)return;_disposed=true;
        _modbusWindow?.Close();_projectService?.Dispose();
        if(_projectService is {} project)ServiceLocator.Current.Remove<IProjectService>(project);
        if(_modbus is {} modbus){ServiceLocator.Current.Remove<IModbusService>(modbus);modbus.Dispose();}
        foreach(var binding in _bindings)_window.InputBindings.Remove(binding);
        foreach(var key in _resourceKeys)_window.Resources.Remove(key);
        _window.DataContext=null;
    }
    private bool CommitPendingEdits()
    {
        var fields = FindTextBoxes(_window).ToArray();
        foreach (var field in fields) field.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        var invalid = fields.FirstOrDefault(Validation.GetHasError);
        if (invalid is null) return true;
        MessageBox.Show(_window, "Исправьте некорректное значение в Inspector перед сохранением.", "Сохранение схемы", MessageBoxButton.OK, MessageBoxImage.Warning);
        invalid.Focus(); return false;
    }
    private static IEnumerable<TextBox> FindTextBoxes(DependencyObject root)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is TextBox textBox) yield return textBox;
            foreach (var nested in FindTextBoxes(child)) yield return nested;
        }
    }
    private void RegisterView<TViewModel, TView>() where TView : UserControl, new()
    {
        var template = new DataTemplate(typeof(TViewModel)) { VisualTree = new FrameworkElementFactory(typeof(TView)) };
        var key=new DataTemplateKey(typeof(TViewModel));_window.Resources.Add(key,template);_resourceKeys.Add(key);
    }
}









