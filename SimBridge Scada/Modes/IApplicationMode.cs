namespace SimBridge_Scada.Modes;

/// <summary>A mode configures the host window and owns the lifetime of its services and UI.</summary>
public interface IApplicationMode : IDisposable
{
    string Name { get; }
    void Initialize(MainWindow window);
    bool CanClose();
}
