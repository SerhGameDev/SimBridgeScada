using System.Windows;
namespace SimBridge_Scada.Shared.FileDrop;

public interface IFileDropTarget { void OpenFiles(IReadOnlyList<string> paths); }

public static class FileDrop
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(FileDrop), new PropertyMetadata(false, Changed));
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement element) return;
        element.AllowDrop = (bool)e.NewValue;
        if ((bool)e.OldValue) { element.PreviewDragOver -= DragOver; element.PreviewDrop -= Drop; }
        if ((bool)e.NewValue) { element.PreviewDragOver += DragOver; element.PreviewDrop += Drop; }
    }
    private static void DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return; // Leave internal tab dragging alone.
        e.Effects = DragDropEffects.Copy; e.Handled = true;
    }
    private static void Drop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement element || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        var handler = element as IFileDropTarget ?? element.DataContext as IFileDropTarget;
        if (handler is null) return;
        handler.OpenFiles(paths); e.Handled = true;
    }
}
