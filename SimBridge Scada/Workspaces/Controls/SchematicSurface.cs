using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimBridge_Scada.ViewModels;

namespace SimBridge_Scada.Controls;

/// <summary>View-only pointer handling; element state and movement rules live in the view model.</summary>
public sealed class SchematicSurface : ItemsControl
{
    private SchematicElementViewModel? _dragged;
    private Point _start;
    private Point _origin;
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (DataContext is not WorkspaceViewModel workspace) return;
        var visual = e.OriginalSource as DependencyObject;
        SchematicElementViewModel? element = null;
        while (visual is not null && visual != this)
        {
            if (visual is FrameworkElement { DataContext: SchematicElementViewModel item }) { element = item; break; }
            visual = VisualTreeHelper.GetParent(visual);
        }
        workspace.SelectedElement = element;
        Focus();
        if (element is not null && !element.IsLocked)
        {
            _dragged = element; _start = e.GetPosition(this); _origin = new(element.WorldX, element.WorldY);
            CaptureMouse();
        }
        e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragged is null || e.LeftButton != MouseButtonState.Pressed || DataContext is not WorkspaceViewModel workspace) return;
        var delta = e.GetPosition(this) - _start;
        workspace.MoveElement(_dragged, _origin.X + delta.X, _origin.Y + delta.Y);
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _dragged = null;
        if (IsMouseCaptured) ReleaseMouseCapture();
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { _dragged = null; base.OnLostMouseCapture(e); }
}


