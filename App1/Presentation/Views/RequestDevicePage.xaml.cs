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

        var quantityCombo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        for (int i = 1; i <= model.Available; i++)
        {
            quantityCombo.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = i });
        }
        quantityCombo.SelectedIndex = 0;

        // === Helper: create a styled label/value row ===
        StackPanel MakeRow(string label, string value)
        {
            var row = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 6) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 51, 127, 148)), // #337F94
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            row.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = 14,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 51, 51, 51))
            });
            return row;
        }

        // === Content form ===
        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(MakeRow("Model", model.Model));
        content.Children.Add(MakeRow("Manufacturer", model.Manufacturer));
        content.Children.Add(MakeRow("Category", $"{model.Category} / {model.SubCategory}"));
        content.Children.Add(MakeRow("Available", model.Available.ToString()));
        // Separator
        content.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 222, 222, 222)),
            Margin = new Thickness(0, 6, 0, 6)
        });
        var qtyLabel = new TextBlock
        {
            Text = "Quantity",
            FontSize = 12,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 51, 127, 148)),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };
        content.Children.Add(qtyLabel);
        content.Children.Add(quantityCombo);

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
            Text = "Borrow Device",
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
        var contentBorder = new Border
        {
            Background = new SolidColorBrush(Colors.White),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24, 20, 24, 20),
            Margin = new Thickness(24, 24, 24, 16),
            Child = content
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
                ["ContentDialogMinWidth"] = 480.0,
                ["ContentDialogMaxWidth"] = 520.0,
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
            var qty = quantityCombo.SelectedItem is ComboBoxItem ci && ci.Tag is int tagVal ? tagVal : 1;
            var success = await _vm.BorrowAsync(model.Id, qty);
            if (!success)
            {
                // === Error Dialog ===
                var errTitleBar = new Grid
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 86, 107, 127)),
                    Height = 44,
                    CornerRadius = new CornerRadius(8, 8, 0, 0),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                        new ColumnDefinition { Width = GridLength.Auto }
                    }
                };
                var errTitleText = new TextBlock
                {
                    Text = "Error",
                    Foreground = new SolidColorBrush(Colors.White),
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    FontSize = 15,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 0, 0)
                };
                Grid.SetColumn(errTitleText, 0);
                errTitleBar.Children.Add(errTitleText);

                var errCloseBtn = new Button
                {
                    Content = new FontIcon { Glyph = "\uE8BB", FontSize = 12 },
                    Style = (Style)Application.Current.Resources["DialogCloseButtonStyle"],
                    Margin = new Thickness(0, 0, 6, 0)
                };
                Grid.SetColumn(errCloseBtn, 1);
                errTitleBar.Children.Add(errCloseBtn);

                // Error icon + message
                var errIcon = new FontIcon
                {
                    Glyph = "\uEA39",
                    FontSize = 28,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 53, 69)),
                    Margin = new Thickness(0, 0, 12, 0)
                };
                var errMsg = new TextBlock
                {
                    Text = "Không có đủ số lượng thiết bị có sẵn.",
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var errContentPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                errContentPanel.Children.Add(errIcon);
                errContentPanel.Children.Add(errMsg);

                var errContentBorder = new Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(24, 24, 24, 24),
                    Margin = new Thickness(24, 24, 24, 16),
                    Child = errContentPanel
                };

                var errOkBtn = new Button
                {
                    Content = "OK",
                    MinWidth = 90,
                    Height = 34,
                    CornerRadius = new CornerRadius(6),
                    FontSize = 13,
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 86, 107, 127)), // #566B7F
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 24, 20)
                };

                var errBodyStack = new StackPanel
                {
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 240, 240))
                };
                errBodyStack.Children.Add(errContentBorder);
                errBodyStack.Children.Add(errOkBtn);

                var errOuterStack = new StackPanel();
                errOuterStack.Children.Add(errTitleBar);
                errOuterStack.Children.Add(errBodyStack);

                var errorDialog = new ContentDialog
                {
                    Content = errOuterStack,
                    XamlRoot = this.XamlRoot,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                    Background = new SolidColorBrush(Colors.Transparent),
                    Resources =
                    {
                        ["ContentDialogPadding"] = new Thickness(0),
                        ["ContentDialogMinWidth"] = 420.0,
                        ["ContentDialogMaxWidth"] = 460.0,
                    }
                };
                errOkBtn.Click += (_, _) => errorDialog.Hide();
                errCloseBtn.Click += (_, _) => errorDialog.Hide();
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
