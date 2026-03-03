using System;
using System.Linq;
using App1.Domain.Entities;
using App1.Presentation.ViewModels;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace App1.Presentation.Views;

public sealed partial class RequestDevicePage : Page
{
    private readonly RequestDeviceViewModel _vm;
    private ComboBox? _categoryComboBox;
    private ComboBox? _subCategoryComboBox;
    private bool _suppressFilterEvents;
    private bool _isLoaded;

    private static readonly SolidColorBrush RowEven = new(Colors.White);
    private static readonly SolidColorBrush RowOdd = new(Windows.UI.Color.FromArgb(255, 245, 245, 245));

    public RequestDevicePage()
    {
        _vm = App.Services.GetRequiredService<RequestDeviceViewModel>();
        _vm.SetDispatcher(DispatcherQueue);
        InitializeComponent();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        await _vm.LoadCategoriesAsync();
        PopulateCategoryComboBox();
        PopulateSubCategoryComboBox();
        await _vm.LoadDataAsync();
        _isLoaded = true;
        UpdatePaginationUI();
        ModelDataGrid.ItemsSource = _vm.Items;
    }

    private void CategoryComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            _categoryComboBox = cb;
            PopulateCategoryComboBox();
        }
    }

    private void SubCategoryComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            _subCategoryComboBox = cb;
            PopulateSubCategoryComboBox();
        }
    }

    private void PopulateCategoryComboBox()
    {
        if (_categoryComboBox == null) return;
        _suppressFilterEvents = true;
        _categoryComboBox.Items.Clear();
        _categoryComboBox.Items.Add(new ComboBoxItem { Content = "-- All --", Tag = "" });
        foreach (var c in _vm.Categories)
            _categoryComboBox.Items.Add(new ComboBoxItem { Content = c, Tag = c });
        _categoryComboBox.SelectedIndex = 0;
        _suppressFilterEvents = false;
    }

    private void PopulateSubCategoryComboBox()
    {
        if (_subCategoryComboBox == null) return;
        _suppressFilterEvents = true;
        _subCategoryComboBox.Items.Clear();
        _subCategoryComboBox.Items.Add(new ComboBoxItem { Content = "-- All --", Tag = "" });
        foreach (var c in _vm.SubCategories)
            _subCategoryComboBox.Items.Add(new ComboBoxItem { Content = c, Tag = c });
        _subCategoryComboBox.SelectedIndex = 0;
        _suppressFilterEvents = false;
    }

    private async void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            _categoryComboBox ??= cb;
            if (_suppressFilterEvents) return;

            var selected = cb.SelectedItem as ComboBoxItem;
            _vm.FilterCategory = selected?.Tag as string ?? string.Empty;
            await _vm.UpdateSubCategoriesAsync(string.IsNullOrEmpty(_vm.FilterCategory) ? null : _vm.FilterCategory);
            PopulateSubCategoryComboBox();
            _vm.ApplyFilter();
            await _vm.LoadDataAsync();
            UpdatePaginationUI();
            ModelDataGrid.ItemsSource = _vm.Items;
        }
    }

    private async void SubCategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            _subCategoryComboBox ??= cb;
            if (_suppressFilterEvents) return;

            var selected = cb.SelectedItem as ComboBoxItem;
            _vm.FilterSubCategory = selected?.Tag as string ?? string.Empty;
            _vm.ApplyFilter();
            await _vm.LoadDataAsync();
            UpdatePaginationUI();
            ModelDataGrid.ItemsSource = _vm.Items;
        }
    }

    private System.Threading.CancellationTokenSource? _filterDebounce;

    private async void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        var tag = tb.Tag as string;

        _filterDebounce?.Cancel();
        _filterDebounce = new System.Threading.CancellationTokenSource();
        var token = _filterDebounce.Token;

        try
        {
            await System.Threading.Tasks.Task.Delay(300, token);
        }
        catch (System.Threading.Tasks.TaskCanceledException) { return; }

        switch (tag)
        {
            case "Model": _vm.FilterModel = tb.Text; break;
            case "Manufacturer": _vm.FilterManufacturer = tb.Text; break;
        }

        _vm.ApplyFilter();
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        ModelDataGrid.ItemsSource = _vm.Items;
    }

    private async void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        _vm.ClearFilters();
        await _vm.LoadCategoriesAsync();
        PopulateCategoryComboBox();
        PopulateSubCategoryComboBox();

        ClearFilterTextBoxes();

        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        ModelDataGrid.ItemsSource = _vm.Items;
    }

    private void ClearFilterTextBoxes()
    {
        foreach (var col in ModelDataGrid.Columns)
        {
            col.SortDirection = null;
        }
    }

    private void FilterArea_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private async void DataGrid_Sorting(object sender, DataGridColumnEventArgs e)
    {
        var col = e.Column;
        var asc = col.SortDirection != DataGridSortDirection.Ascending;
        col.SortDirection = asc ? DataGridSortDirection.Ascending : DataGridSortDirection.Descending;

        foreach (var c in ModelDataGrid.Columns)
            if (c != col) c.SortDirection = null;

        string[] colNames = { "Model", "Manufacturer", "Category", "SubCategory", "Available", "Reserved" };
        var idx = ModelDataGrid.Columns.IndexOf(col);
        if (idx >= 0 && idx < colNames.Length)
        {
            _vm.SetSort(colNames[idx], asc);
            await _vm.LoadDataAsync();
            UpdatePaginationUI();
            ModelDataGrid.ItemsSource = _vm.Items;
        }
    }

    private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        var idx = e.Row.GetIndex();
        e.Row.Background = idx % 2 == 0 ? RowEven : RowOdd;
    }

    private async void BorrowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not DeviceModel model) return;
        if (model.Available <= 0) return;

        var quantityBox = new NumberBox
        {
            Minimum = 1,
            Maximum = model.Available,
            Value = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            SmallChange = 1
        };

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock { Text = $"Model: {model.Model}" });
        content.Children.Add(new TextBlock { Text = $"Manufacturer: {model.Manufacturer}" });
        content.Children.Add(new TextBlock { Text = $"Category: {model.Category} / {model.SubCategory}" });
        content.Children.Add(new TextBlock { Text = $"Available: {model.Available}" });
        content.Children.Add(new TextBlock { Text = "Quantity:", Margin = new Thickness(0, 8, 0, 0) });
        content.Children.Add(quantityBox);

        var dialog = new ContentDialog
        {
            Title = "Borrow Device",
            Content = content,
            PrimaryButtonText = "Confirm",
            CloseButtonText = "Cancel",
            XamlRoot = this.XamlRoot,
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var qty = (int)quantityBox.Value;
            if (qty < 1) qty = 1;
            var success = await _vm.BorrowAsync(model.Id, qty);
            if (!success)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "Not enough available devices.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }

    // Pagination handlers
    private async void FirstPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToFirstCommand.Execute(null);
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        ModelDataGrid.ItemsSource = _vm.Items;
    }

    private async void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToPreviousCommand.Execute(null);
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        ModelDataGrid.ItemsSource = _vm.Items;
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToNextCommand.Execute(null);
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        ModelDataGrid.ItemsSource = _vm.Items;
    }

    private async void LastPage_Click(object sender, RoutedEventArgs e)
    {
        _vm.GoToLastCommand.Execute(null);
        await _vm.LoadDataAsync();
        UpdatePaginationUI();
        ModelDataGrid.ItemsSource = _vm.Items;
    }

    private async void PageNumber_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PageItem pi && pi.PageNumber.HasValue)
        {
            _vm.GoToPageCommand.Execute(pi.PageNumber.Value);
            await _vm.LoadDataAsync();
            UpdatePaginationUI();
            ModelDataGrid.ItemsSource = _vm.Items;
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
            ModelDataGrid.ItemsSource = _vm.Items;
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
        UpdatePaginationUI();
        ModelDataGrid.ItemsSource = _vm.Items;
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
