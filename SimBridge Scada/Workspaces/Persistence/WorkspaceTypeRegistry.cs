using System.IO;
using System.Reflection;
using System.Text.Json;
using SimBridge_Scada.ViewModels;
using SimBridge_Scada.ViewModels.Components;
namespace SimBridge_Scada.Workspaces.Persistence;

/// <summary>Explicit file type registry. File data never resolves arbitrary CLR types.</summary>
public sealed class WorkspaceTypeRegistry
{
    private readonly Dictionary<string, Registration> _elements = [], _components = [];
    public void RegisterElement<T>(string key, Func<T> factory) where T : SchematicElementViewModel => Add(_elements, key, typeof(T), () => factory());
    public void RegisterComponent<T>(string key, Func<T> factory) where T : ComponentViewModel => Add(_components, key, typeof(T), () => factory());
    private static void Add(Dictionary<string, Registration> entries, string key, Type type, Func<object> factory)
    {
        if (string.IsNullOrWhiteSpace(key) || entries.ContainsKey(key) || entries.Values.Any(r => r.Type == type)) throw new ArgumentException("Duplicate or empty persistence registration.");
        entries.Add(key, new(type, factory));
    }
    public string ElementKey(SchematicElementViewModel element) => Key(_elements, element.GetType());
    public string ComponentKey(ComponentViewModel component) => Key(_components, component.GetType());
    public SchematicElementViewModel CreateElement(string key) => (SchematicElementViewModel)Find(_elements, key).Factory();
    public ComponentViewModel CreateComponent(string key) => (ComponentViewModel)Find(_components, key).Factory();
    public Type ComponentType(string key) => Find(_components, key).Type;
    private static string Key(Dictionary<string, Registration> entries, Type type) => entries.FirstOrDefault(p => p.Value.Type == type).Key
        ?? throw new InvalidDataException($"Тип {type.Name} не зарегистрирован для сохранения.");
    private static Registration Find(Dictionary<string, Registration> entries, string key) => entries.TryGetValue(key, out var value)
        ? value : throw new InvalidDataException($"Неизвестный тип в файле: {key}.");
    private sealed record Registration(Type Type, Func<object> Factory);

    // Persist only public editable properties. Owner links, commands, events and runtime selection are excluded.
    private static IEnumerable<PropertyInfo> Properties(object value, bool element) => value.GetType().GetProperties()
        .Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod?.IsPublic == true && p.SetMethod?.IsPublic == true
            && (!element || p.DeclaringType != typeof(SchematicElementViewModel)));
    public static Dictionary<string, JsonElement> Capture(object value, bool element = false) => Properties(value, element)
        .ToDictionary(p => p.Name, p => JsonSerializer.SerializeToElement(p.GetValue(value), p.PropertyType));
    public static void Apply(object target, Dictionary<string, JsonElement> state, bool element = false)
    {
        var properties = Properties(target, element).ToDictionary(p => p.Name);
        foreach (var pair in state)
        {
            if (!properties.TryGetValue(pair.Key, out var property)) throw new InvalidDataException($"Неизвестное свойство {pair.Key} у {target.GetType().Name}.");
            try { property.SetValue(target, pair.Value.Deserialize(property.PropertyType)); }
            catch (Exception e) when (e is JsonException or TargetInvocationException or ArgumentException)
            { throw new InvalidDataException($"Некорректное свойство {pair.Key}.", e); }
        }
    }
}

