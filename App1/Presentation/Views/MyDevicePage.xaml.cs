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
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadDataAsync();
        _isLoaded = true;
        UpdatePaginationUI();
        DeviceDataGrid.ItemsSource = _vm.Items;
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

        if (tag == "ModelName") _vm.FilterModelName = tb.Text;

        _vm.ApplyFilter();
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        DeviceDataGrid.ItemsSource = _vm.Items;
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
            UpdatePaginationUI();
            DeviceDataGrid.ItemsSource = _vm.Items;
        }
    }

    private async void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        _vm.ClearFilters();
        foreach (var col in DeviceDataGrid.Columns) col.SortDirection = null;
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        DeviceDataGrid.ItemsSource = _vm.Items;
    }

    private async void DataGrid_Sorting(object sender, DataGridColumnEventArgs e)
    {
        var col = e.Column;
        var asc = col.SortDirection != DataGridSortDirection.Ascending;
        col.SortDirection = asc ? DataGridSortDirection.Ascending : DataGridSortDirection.Descending;

        foreach (var c in DeviceDataGrid.Columns)
            if (c != col) c.SortDirection = null;

        string[] colNames = { "", "ModelName", "IMEI", "Label", "SerialNumber", "CircuitSerialNumber",
                              "HWVersion", "BorrowedDate", "ReturnDate", "Invoice", "Status", "Inventory" };
        var idx = DeviceDataGrid.Columns.IndexOf(col);
        if (idx >= 1 && idx < colNames.Length)
        {
            _vm.SetSort(colNames[idx], asc);
            await _vm.LoadDataAsync();
            UpdatePaginationUI();
            DeviceDataGrid.ItemsSource = _vm.Items;
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
            sb.AppendLine($"  - {d.ModelName} (IMEI: {d.IMEI})");
        if (selected.Count > 20)
            sb.AppendLine($"  ... and {selected.Count - 20} more");

        var dialog = new ContentDialog
        {
            Title = "Return Devices",
            Content = new TextBlock { Text = sb.ToString(), TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Confirm",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await _vm.ReturnSelectedAsync();
            ReturnSelectedBtn.IsEnabled = false;
            UpdatePaginationUI();
            DeviceDataGrid.ItemsSource = _vm.Items;
        }
    }

    // Pagination handlers
    private async void FirstPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToFirstCommand.Execute(null);
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        DeviceDataGrid.ItemsSource = _vm.Items;
    }

    private async void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToPreviousCommand.Execute(null);
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        DeviceDataGrid.ItemsSource = _vm.Items;
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToNextCommand.Execute(null);
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        DeviceDataGrid.ItemsSource = _vm.Items;
    }

    private async void LastPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToLastCommand.Execute(null);
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        DeviceDataGrid.ItemsSource = _vm.Items;
    }

    private async void PageNumber_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PageItem pi && pi.PageNumber.HasValue)
        {
            _vm.GoToPageCommand.Execute(pi.PageNumber.Value);
            await _vm.LoadDataAsync();
            UpdatePaginationUI();
            DeviceDataGrid.ItemsSource = _vm.Items;
        }
    }

    private async void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) return;
        if (PageSizeComboBox?.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag as string, out int size))
        {
            _vm.SetPageSize(size);
            await _vm.LoadDataAsync();
            UpdatePaginationUI();
            DeviceDataGrid.ItemsSource = _vm.Items;
        }
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
