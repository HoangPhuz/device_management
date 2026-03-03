using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using App1.Presentation.Views;

namespace App1;

public sealed partial class MainWindow : Window
{
    private bool _sidebarExpanded = true;

    public MainWindow()
    {
        InitializeComponent();
        ContentFrame.Navigate(typeof(RequestDevicePage));
        SidebarMenu.SelectedIndex = 0;
    }

    private void SidebarMenu_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SidebarMenu.SelectedItem is ListViewItem item)
        {
            var tag = item.Tag as string;
            switch (tag)
            {
                case "RequestDevice":
                    ContentFrame.Navigate(typeof(RequestDevicePage));
                    break;
                case "MyDevice":
                    ContentFrame.Navigate(typeof(MyDevicePage));
                    break;
            }
        }
    }

    private void HamburgerButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _sidebarExpanded = !_sidebarExpanded;
        SidebarColumn.Width = _sidebarExpanded
            ? new GridLength(220)
            : new GridLength(0);
    }
}
