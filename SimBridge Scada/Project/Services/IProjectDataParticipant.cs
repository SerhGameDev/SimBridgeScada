namespace SimBridge_Scada.Project.Services;
/// <summary>A service owns a file in project Data, and validates its state before project activation.</summary>
public interface IProjectDataParticipant
{
    string DataPath {get;}
    event EventHandler? ConfigurationChanged;
    byte[] CaptureProjectData();
    PreparedProjectData PrepareProjectData(byte[]? data,bool merge);
}
public sealed record PreparedProjectData(byte[] Data,Action Apply);
