using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace SimBridge_Scada.Controls;

/// <summary>View interaction: middle-button pan and pointer-anchored wheel zoom. No object mutations.</summary>
public sealed class WorkspaceViewport : ScrollViewer
{
    private Point _panStart, _panOrigin;
    private bool _panning, _restoring;
    public WorkspaceViewport()
    {
        DataContextChanged += (_, _) => RestoreViewport();
        Loaded += (_, _) => RestoreViewport();
        Unloaded += (_, _) => EndPan();
    }
    private void RestoreViewport()
    {
        if (DataContext is not SimBridge_Scada.ViewModels.WorkspaceViewModel workspace) return;
        _restoring = true;
        UpdateLayout(); ScrollToHorizontalOffset(workspace.OffsetX); ScrollToVerticalOffset(workspace.OffsetY);
        UpdateLayout(); _restoring = false;
    }
    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        base.OnScrollChanged(e);
        if (!_restoring && IsLoaded && DataContext is SimBridge_Scada.ViewModels.WorkspaceViewModel workspace)
        { workspace.OffsetX = HorizontalOffset; workspace.OffsetY = VerticalOffset; }
    }
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        if (e.ChangedButton != MouseButton.Middle) return;
        _panStart = e.GetPosition(this); _panOrigin = new(HorizontalOffset, VerticalOffset);
        Focus(); _panning = CaptureMouse(); Cursor = _panning ? Cursors.Hand : null; e.Handled = true;
    }
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (!_panning) return;
        if (e.MiddleButton != MouseButtonState.Pressed) { EndPan(); return; }
        var delta = e.GetPosition(this) - _panStart;
        PanTo(_panOrigin.X - delta.X, _panOrigin.Y - delta.Y); e.Handled = true;
    }
    public void PanTo(double x, double y)
    { ScrollToHorizontalOffset(x); ScrollToVerticalOffset(y); }
    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);
        if (e.ChangedButton == MouseButton.Middle && (_panning || IsMouseCaptured)) { EndPan(); e.Handled = true; }
    }
    private void EndPan() { _panning = false; Cursor = null; if (IsMouseCaptured) ReleaseMouseCapture(); }
    protected override void OnLostMouseCapture(MouseEventArgs e) { if (!IsMouseCaptured) { _panning = false; Cursor = null; } base.OnLostMouseCapture(e); }
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        base.OnPreviewMouseWheel(e);
        ZoomAt(e.GetPosition(this), e.Delta); e.Handled = true;
    }
    public void ZoomAt(Point anchor, int wheelDelta)
    {
        if (DataContext is not SimBridge_Scada.ViewModels.WorkspaceViewModel workspace || wheelDelta == 0) return;
        var oldZoom = workspace.Zoom;
        var worldX = (HorizontalOffset + anchor.X) / oldZoom;
        var worldY = (VerticalOffset + anchor.Y) / oldZoom;
        workspace.Zoom *= Math.Pow(1.1, wheelDelta / 120.0);
        UpdateLayout();
        ScrollToHorizontalOffset(worldX * workspace.Zoom - anchor.X);
        ScrollToVerticalOffset(worldY * workspace.Zoom - anchor.Y);
    }
}

