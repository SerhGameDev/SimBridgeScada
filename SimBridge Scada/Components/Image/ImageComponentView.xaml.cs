using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SimBridge_Scada.ViewModels.Components;
namespace SimBridge_Scada.Views;
public partial class ImageComponentView : UserControl
{
    public ImageComponentView() => InitializeComponent();
    private void Browse(object sender,RoutedEventArgs e)
    {
        if(DataContext is not ImageComponentViewModel component) return;
        var dialog=new OpenFileDialog { Title="Выбрать изображение", Filter="Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Все файлы|*.*" };
        if(dialog.ShowDialog(Window.GetWindow(this))==true) component.ImagePath=dialog.FileName;
    }
}
