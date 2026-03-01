using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using App1.Presentation.Views;

namespace App1;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentFrame.Navigate(typeof(RequestDevicePage));
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem item)
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
}
