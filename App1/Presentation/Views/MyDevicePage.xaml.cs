using System;
using System.Linq;
using System.Text;
using App1.Presentation.ViewModels;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace App1.Presentation.Views;

public sealed partial class MyDevicePage : Page
{
    private readonly MyDeviceViewModel _vm;
    private bool _isLoaded;

    private static readonly SolidColorBrush RowEven = new(Colors.White);
    private static readonly SolidColorBrush RowOdd = new(Windows.UI.Color.FromArgb(255, 245, 245, 245));

    public MyDevicePage()
    {
        _vm = App.Services.GetRequiredService<MyDeviceViewModel>();
        _vm.SetDispatcher(DispatcherQueue);
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        InitializeComponent();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MyDeviceViewModel.Items):
                DeviceDataGrid.ItemsSource = _vm.Items;
                UpdatePaginationUI();
                break;
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadDataAsync();
        _isLoaded = true;
    }

    private void FilterArea_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private System.Threading.CancellationTokenSource? _filterDebounce;

    private async void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var tag = tb.Tag as string;

        _filterDebounce?.Cancel();
        _filterDebounce = new System.Threading.CancellationTokenSource();
        var token = _filterDebounce.Token;

        try { await System.Threading.Tasks.Task.Delay(300, token); }
        catch (System.Threading.Tasks.TaskCanceledException) { return; }

        switch (tag)
        {
            case "Name": _vm.FilterName = tb.Text; break;
            case "IMEI": _vm.FilterIMEI = tb.Text; break;
            case "SerialLab": _vm.FilterSerialLab = tb.Text; break;
            case "SerialNumber": _vm.FilterSerialNumber = tb.Text; break;
            case "CircuitSerialNumber": _vm.FilterCircuitSerialNumber = tb.Text; break;
            case "HWVersion": _vm.FilterHWVersion = tb.Text; break;
            case "Inventory": _vm.FilterInventory = tb.Text; break;
        }

        _vm.ApplyFilter();
        await _vm.LoadDataAsync();
    }

    private void StatusComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        // Reference captured; items are defined inline in XAML
    }

    private async void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
        {
            _vm.FilterStatus = item.Tag as string ?? string.Empty;
            _vm.ApplyFilter();
            await _vm.LoadDataAsync();
        }
    }

    private async void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        _vm.ClearFilters();

        // Clear sort directions
        foreach (var col in DeviceDataGrid.Columns) col.SortDirection = null;

        // Clear all TextBoxes & ComboBoxes inside DataGrid column headers
        ClearFilterControlsInVisualTree(DeviceDataGrid);

        await _vm.LoadDataAsync();
    }

    private static void ClearFilterControlsInVisualTree(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBox tb && tb.Tag is string)
            {
                tb.Text = string.Empty;
            }
            else if (child is ComboBox cb && cb.Items.Count > 0)
            {
                cb.SelectedIndex = 0;
            }
            else
            {
                ClearFilterControlsInVisualTree(child);
            }
        }
    }

    private async void DataGrid_Sorting(object sender, DataGridColumnEventArgs e)
    {
        var col = e.Column;
        var asc = col.SortDirection != DataGridSortDirection.Ascending;
        col.SortDirection = asc ? DataGridSortDirection.Ascending : DataGridSortDirection.Descending;

        foreach (var c in DeviceDataGrid.Columns)
            if (c != col) c.SortDirection = null;

        string[] colNames = { "", "Name", "IMEI", "SerialLab", "SerialNumber", "CircuitSerialNumber",
                              "HWVersion", "BorrowedDate", "ReturnDate", "Invoice", "Status", "Inventory" };
        var idx = DeviceDataGrid.Columns.IndexOf(col);
        if (idx >= 1 && idx < colNames.Length)
        {
            _vm.SetSort(colNames[idx], asc);
            await _vm.LoadDataAsync();
        }
    }

    private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        var idx = e.Row.GetIndex();
        e.Row.Background = idx % 2 == 0 ? RowEven : RowOdd;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb)
        {
            _vm.ToggleSelectAll(cb.IsChecked == true);
            ReturnSelectedBtn.IsEnabled = _vm.HasSelection;
            DeviceDataGrid.ItemsSource = null;
            DeviceDataGrid.ItemsSource = _vm.Items;
        }
    }

    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        _vm.NotifySelectionChanged();
        ReturnSelectedBtn.IsEnabled = _vm.HasSelection;
    }

    private async void ReturnSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = _vm.GetSelectedDevices();
        if (selected.Count == 0) return;

        var sb = new StringBuilder();
        sb.AppendLine($"You are about to return {selected.Count} device(s):");
        sb.AppendLine();
        foreach (var d in selected.Take(20))
            sb.AppendLine($"  - {d.Name} (IMEI: {d.IMEI})");
        if (selected.Count > 20)
            sb.AppendLine($"  ... and {selected.Count - 20} more");

        // === Title bar ===
        var titleBar = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 86, 107, 127)), // #566B7F
            Height = 44,
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var titleText = new TextBlock
        {
            Text = "Return Devices",
            Foreground = new SolidColorBrush(Colors.White),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0)
        };
        Grid.SetColumn(titleText, 0);
        titleBar.Children.Add(titleText);

        var closeBtn = new Button
        {
            Content = new FontIcon { Glyph = "\uE8BB", FontSize = 12 },
            Style = (Style)Application.Current.Resources["DialogCloseButtonStyle"],
            Margin = new Thickness(0, 0, 6, 0)
        };
        Grid.SetColumn(closeBtn, 1);
        titleBar.Children.Add(closeBtn);

        // === Content card ===
        var contentText = new TextBlock
        {
            Text = sb.ToString(),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14
        };

        var contentBorder = new Border
        {
            Background = new SolidColorBrush(Colors.White),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24, 20, 24, 20),
            Margin = new Thickness(24, 24, 24, 16),
            Child = contentText
        };

        // === Buttons ===
        var cancelBtn = new Button
        {
            Content = "Cancel",
            MinWidth = 90,
            Height = 34,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 10, 0),
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var okBtn = new Button
        {
            Content = "Confirm",
            MinWidth = 90,
            Height = 34,
            CornerRadius = new CornerRadius(6),
            FontSize = 13,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 84, 176, 125)), // #54B07D
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 24, 20)
        };
        buttonPanel.Children.Add(cancelBtn);
        buttonPanel.Children.Add(okBtn);

        // === Body ===
        var bodyStack = new StackPanel
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240)) // #F0F0F0
        };
        bodyStack.Children.Add(contentBorder);
        bodyStack.Children.Add(buttonPanel);

        var outerStack = new StackPanel();
        outerStack.Children.Add(titleBar);
        outerStack.Children.Add(bodyStack);

        var dialog = new ContentDialog
        {
            Content = outerStack,
            XamlRoot = this.XamlRoot,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            Resources =
            {
                ["ContentDialogPadding"] = new Thickness(0),
                ["ContentDialogMinWidth"] = 520.0,
                ["ContentDialogMaxWidth"] = 560.0,
            }
        };

        // Wire up buttons
        cancelBtn.Click += (_, _) => dialog.Hide();
        closeBtn.Click += (_, _) => dialog.Hide();
        ContentDialogResult dialogResult = ContentDialogResult.None;
        okBtn.Click += (_, _) =>
        {
            dialogResult = ContentDialogResult.Primary;
            dialog.Hide();
        };

        await dialog.ShowAsync();
        if (dialogResult == ContentDialogResult.Primary)
        {
            await _vm.ReturnSelectedAsync();
            ReturnSelectedBtn.IsEnabled = false;
        }
    }

    // Pagination handlers
    private async void FirstPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToFirstCommand.Execute(null);
        await _vm.LoadDataAsync();
    }

    private async void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToPreviousCommand.Execute(null);
        await _vm.LoadDataAsync();
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToNextCommand.Execute(null);
        await _vm.LoadDataAsync();
    }

    private async void LastPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToLastCommand.Execute(null);
        await _vm.LoadDataAsync();
    }

    private async void PageNumber_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border border && border.DataContext is PageItem pi && pi.PageNumber.HasValue)
        {
            _vm.GoToPageCommand.Execute(pi.PageNumber.Value);
            await _vm.LoadDataAsync();
        }
    }

    private async void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        if (PageSizeComboBox?.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag as string, out int size))
        {
            _vm.SetPageSize(size);
            await _vm.LoadDataAsync();
        }
    }

    public async void SetPageSize(int size)
    {
        _vm.SetPageSize(size);

        if (PageSizeComboBox != null)
        {
            for (int i = 0; i < PageSizeComboBox.Items.Count; i++)
            {
                if (PageSizeComboBox.Items[i] is ComboBoxItem ci && ci.Tag as string == size.ToString())
                {
                    PageSizeComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        if (!_isLoaded) return;
        await _vm.LoadDataAsync();
    }

    private void UpdatePaginationUI()
    {
        if (ShowingText == null || FirstBtn == null) return;

        ShowingText.Text = _vm.TotalRecords == 0
            ? "No entries"
            : $"Showing {_vm.ShowingFrom} to {_vm.ShowingTo} of {_vm.TotalRecords:N0} entries";

        FirstBtn.IsEnabled = _vm.CanGoFirst;
        PrevBtn.IsEnabled = _vm.CanGoPrevious;
        NextBtn.IsEnabled = _vm.CanGoNext;
        LastBtn.IsEnabled = _vm.CanGoLast;
    }
}
