using System.Diagnostics.CodeAnalysis;
namespace SimBridge_Scada.Services;
public sealed class ServiceLocator
{
    public static ServiceLocator Current { get; } = new();
    private readonly Dictionary<Type,object> _services=[];
    private readonly object _gate=new();
    public void Register<T>(T service,bool replace=false) where T:class
    { ArgumentNullException.ThrowIfNull(service);lock(_gate){if(!replace&&_services.ContainsKey(typeof(T)))throw new InvalidOperationException($"Service {typeof(T).Name} already registered.");_services[typeof(T)]=service;} }
    public bool TryGet<T>([NotNullWhen(true)] out T? service) where T:class
    { lock(_gate){service=_services.TryGetValue(typeof(T),out var value)?(T)value:null;return service is not null;} }
    public T Get<T>() where T:class => TryGet<T>(out var service)?service:throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    public void Remove<T>(T service) where T:class
    {lock(_gate){if(_services.TryGetValue(typeof(T),out var current)&&ReferenceEquals(current,service))_services.Remove(typeof(T));}}
}
