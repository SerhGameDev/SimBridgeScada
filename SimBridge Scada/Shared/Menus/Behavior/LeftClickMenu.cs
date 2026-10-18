using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SimBridge_Scada.Infrastructure;
namespace SimBridge_Scada.Shared.Menus;

/// <summary>Configurable mouse-button menu behavior (right click by default). The element or its DataContext supplies the menu through an interface.</summary>
public static class LeftClickMenu
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(LeftClickMenu), new PropertyMetadata(false, Changed));
    public static bool GetIsEnabled(DependencyObject value) => (bool)value.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject value, bool enabled) => value.SetValue(IsEnabledProperty, enabled);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement element) return;
        if ((bool)e.OldValue) element.PreviewMouseDown -= Open;
        if ((bool)e.NewValue) element.PreviewMouseDown += Open;
    }
    public static readonly DependencyProperty OpenOnReleaseProperty = DependencyProperty.RegisterAttached("OpenOnRelease", typeof(bool), typeof(LeftClickMenu), new PropertyMetadata(false));
    public static bool GetOpenOnRelease(DependencyObject value) => (bool)value.GetValue(OpenOnReleaseProperty);
    public static void SetOpenOnRelease(DependencyObject value, bool enabled) => value.SetValue(OpenOnReleaseProperty, enabled);
    public static readonly DependencyProperty MouseButtonProperty = DependencyProperty.RegisterAttached("MouseButton", typeof(MouseButton), typeof(LeftClickMenu), new PropertyMetadata(MouseButton.Right));
    public static MouseButton GetMouseButton(DependencyObject element) => (MouseButton)element.GetValue(MouseButtonProperty);
    public static void SetMouseButton(DependencyObject element, MouseButton value) => element.SetValue(MouseButtonProperty, value);
    private static void Open(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || e.ChangedButton != GetMouseButton(element)) return;
        var provider = element as ILeftClickMenuProvider ?? element.DataContext as ILeftClickMenuProvider;
        if (provider is null) return;
        if (GetOpenOnRelease(element))
        {
            WaitForClick(element, provider, e); return;
        }
        Show(element, provider); e.Handled = true;
    }
    // Let selection and dragging receive the press. Only a completed click opens the menu.
    private static void WaitForClick(FrameworkElement element, ILeftClickMenuProvider provider, MouseButtonEventArgs down)
    {
        var window = Window.GetWindow(element);
        if (window is null) return;
        var start = down.GetPosition(window);
        var moved = false;
        MouseEventHandler? move = null;
        MouseButtonEventHandler? up = null;
        EventHandler? deactivate = null;
        void Detach()
        {
            window.RemoveHandler(Mouse.PreviewMouseMoveEvent, move!);
            window.RemoveHandler(Mouse.PreviewMouseUpEvent, up!);
            window.Deactivated -= deactivate;
        }
        move = (_, e) =>
        {
            var delta = e.GetPosition(window) - start;
            moved |= Math.Abs(delta.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(delta.Y) >= SystemParameters.MinimumVerticalDragDistance;
        };
        up = (_, e) =>
        {
            if (e.ChangedButton != down.ChangedButton) return;
            var delta = e.GetPosition(window) - start;
            moved |= Math.Abs(delta.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(delta.Y) >= SystemParameters.MinimumVerticalDragDistance;
            Detach();
            if (!moved) element.Dispatcher.BeginInvoke(new Action(() => { if (element.IsLoaded) Show(element, provider); }));
        };
        deactivate = (_, _) => Detach();
        window.AddHandler(Mouse.PreviewMouseMoveEvent, move, true);
        window.AddHandler(Mouse.PreviewMouseUpEvent, up, true);
        window.Deactivated += deactivate;
    }
    public static void Show(FrameworkElement element, ILeftClickMenuProvider provider)
    {
        var menu = Build(provider.CreateMenu());
        menu.PlacementTarget = element; menu.Placement = PlacementMode.MousePoint;
        menu.Closed += (_, _) => { if (element.ContextMenu == menu) element.ClearValue(FrameworkElement.ContextMenuProperty); };
        element.ContextMenu = menu; menu.IsOpen = true;
    }
    public static ContextMenu Build(ActionMenu model)
    {
        var result = new ContextMenu();
        foreach (var item in model.Items) result.Items.Add(BuildItem(item));
        return result;
    }
    private static Control BuildItem(ActionMenuItem item)
    {
        if (item.IsSeparator) return new Separator();
        var result = new MenuItem { Header = item.Title, Command = item.Command, InputGestureText = item.GestureText };
        if (!string.IsNullOrWhiteSpace(item.IconPath)) result.Icon = new Image
        {
            Width = 16, Height = 16,
            Source = (System.Windows.Media.ImageSource)new IconPathConverter().Convert(item.IconPath, typeof(System.Windows.Media.ImageSource), null!, System.Globalization.CultureInfo.InvariantCulture)
        };
        foreach (var child in item.Children) result.Items.Add(BuildItem(child));
        return result;
    }
}


