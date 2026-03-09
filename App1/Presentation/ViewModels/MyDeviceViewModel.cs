using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App1.Domain.Entities;
using App1.Domain.UseCases;
using App1.Domain.ValueObjects;
using App1.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App1.Presentation.ViewModels;

public partial class SelectableDevice : ObservableObject
{
    public Device Device { get; }
    [ObservableProperty] private bool _isSelected;

    public SelectableDevice(Device device) => Device = device;

    public string Id => Device.Id;
    public string Name => Device.Name;
    public string IMEI => Device.IMEI;
    public string SerialLab => Device.SerialLab;
    public string SerialNumber => Device.SerialNumber;
    public string CircuitSerialNumber => Device.CircuitSerialNumber;
    public string HWVersion => Device.HWVersion;
    public string BorrowedDate => Device.BorrowedDate;
    public string ReturnDate => Device.ReturnDate;
    public string Invoice => Device.Invoice;
    public string Status => Device.Status;
    public string Inventory => Device.Inventory;
}

public partial class MyDeviceViewModel : ObservableObject
{
    private readonly GetDevicesUseCase _getDevices;
    private readonly ReturnDeviceUseCase _return;
    private readonly SyncService _sync;

    public MyDeviceViewModel(
        GetDevicesUseCase getDevices,
        ReturnDeviceUseCase returnDevice,
        SyncService sync)
    {
        _getDevices = getDevices;
        _return = returnDevice;
        _sync = sync;
        _sync.DataChanged += OnSyncDataChanged;
        _sync.LocalDataChanged += OnLocalDataChanged;
    }

    [ObservableProperty] private ObservableCollection<SelectableDevice> _items = new();
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private int _totalRecords;
    [ObservableProperty] private int _pageSize = 50;
    [ObservableProperty] private ObservableCollection<PageItem> _pageNumbers = new();

    [ObservableProperty] private string _filterName = string.Empty;
    [ObservableProperty] private string _filterIMEI = string.Empty;
    [ObservableProperty] private string _filterSerialLab = string.Empty;
    [ObservableProperty] private string _filterSerialNumber = string.Empty;
    [ObservableProperty] private string _filterCircuitSerialNumber = string.Empty;
    [ObservableProperty] private string _filterHWVersion = string.Empty;
    [ObservableProperty] private string _filterInventory = string.Empty;
    [ObservableProperty] private string _filterStatus = string.Empty;

    [ObservableProperty] private string? _sortColumn;
    [ObservableProperty] private bool _sortAscending = true;
    [ObservableProperty] private bool _isLoading;

    private CancellationTokenSource? _loadCts;

    public int ShowingFrom => TotalRecords == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
    public int ShowingTo => Math.Min(CurrentPage * PageSize, TotalRecords);

    public bool CanGoFirst => CurrentPage > 1;
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < TotalPages;
    public bool CanGoLast => CurrentPage < TotalPages;

    public bool HasSelection => Items.Any(x => x.IsSelected);

    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcher;

    public void SetDispatcher(Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
        => _dispatcher = dispatcher;

    private void OnSyncDataChanged()
    {
        _dispatcher?.TryEnqueue(async () =>
        {
            await _getDevices.RefreshAsync(App.InstanceId);
            await LoadDataAsync();
        });
    }

    private void OnLocalDataChanged()
    {
        _dispatcher?.TryEnqueue(async () =>
        {
            await _getDevices.RefreshAsync(App.InstanceId);
        });
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;

        IsLoading = true;
        try
        {
            var query = new QueryParameters
            {
                Page = CurrentPage,
                PageSize = PageSize,
                SortColumn = SortColumn,
                SortAscending = SortAscending,
                Filters = new Dictionary<string, string>()
            };

            if (!string.IsNullOrWhiteSpace(FilterName))
                query.Filters["Name"] = FilterName;
            if (!string.IsNullOrWhiteSpace(FilterIMEI))
                query.Filters["IMEI"] = FilterIMEI;
            if (!string.IsNullOrWhiteSpace(FilterSerialLab))
                query.Filters["SerialLab"] = FilterSerialLab;
            if (!string.IsNullOrWhiteSpace(FilterSerialNumber))
                query.Filters["SerialNumber"] = FilterSerialNumber;
            if (!string.IsNullOrWhiteSpace(FilterCircuitSerialNumber))
                query.Filters["CircuitSerialNumber"] = FilterCircuitSerialNumber;
            if (!string.IsNullOrWhiteSpace(FilterHWVersion))
                query.Filters["HWVersion"] = FilterHWVersion;
            if (!string.IsNullOrWhiteSpace(FilterInventory))
                query.Filters["Inventory"] = FilterInventory;
            if (!string.IsNullOrWhiteSpace(FilterStatus))
                query.Filters["Status"] = FilterStatus;

            var result = await Task.Run(() => _getDevices.ExecuteAsync(query, App.InstanceId));

            if (cts.Token.IsCancellationRequested) return;

            TotalRecords = result.TotalCount;
            TotalPages = result.TotalPages;
            NotifyPaginationProperties();
            GeneratePageNumbers();

            Items = new ObservableCollection<SelectableDevice>(
                result.Items.Select(d => new SelectableDevice(d)));
            OnPropertyChanged(nameof(HasSelection));
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_loadCts == cts)
                IsLoading = false;
        }
    }

    public async Task<bool> ReturnSelectedAsync()
    {
        var selectedIds = Items.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        if (selectedIds.Count == 0) return false;

        var success = await Task.Run(() => _return.ExecuteAsync(selectedIds));
        if (success)
        {
            _sync.Broadcast();
            await LoadDataAsync();
        }
        return success;
    }

    public List<SelectableDevice> GetSelectedDevices()
        => Items.Where(x => x.IsSelected).ToList();

    public void SetSort(string? column, bool ascending)
    {
        SortColumn = column;
        SortAscending = ascending;
        CurrentPage = 1;
    }

    public void ApplyFilter() { CurrentPage = 1; }

    public void ClearFilters()
    {
        FilterName = string.Empty;
        FilterIMEI = string.Empty;
        FilterSerialLab = string.Empty;
        FilterSerialNumber = string.Empty;
        FilterCircuitSerialNumber = string.Empty;
        FilterHWVersion = string.Empty;
        FilterInventory = string.Empty;
        FilterStatus = string.Empty;
        CurrentPage = 1;
    }

    public void ToggleSelectAll(bool selectAll)
    {
        foreach (var item in Items) item.IsSelected = selectAll;
        OnPropertyChanged(nameof(HasSelection));
    }

    public void NotifySelectionChanged() => OnPropertyChanged(nameof(HasSelection));

    [RelayCommand] private void GoToFirst() { CurrentPage = 1; }
    [RelayCommand] private void GoToPrevious() { if (CurrentPage > 1) CurrentPage--; }
    [RelayCommand] private void GoToNext() { if (CurrentPage < TotalPages) CurrentPage++; }
    [RelayCommand] private void GoToLast() { CurrentPage = TotalPages; }
    [RelayCommand] private void GoToPage(int page) { if (page >= 1 && page <= TotalPages) CurrentPage = page; }

    public void SetPageSize(int size) { PageSize = size; CurrentPage = 1; }

    partial void OnCurrentPageChanged(int value)
    {
        GeneratePageNumbers();
    }

    private void NotifyPaginationProperties()
    {
        OnPropertyChanged(nameof(ShowingFrom));
        OnPropertyChanged(nameof(ShowingTo));
        OnPropertyChanged(nameof(CanGoFirst));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoLast));
    }

    public void GeneratePageNumbers()
    {
        const int maxVisible = 5;
        var count = Math.Min(TotalPages, maxVisible);
        EnsurePageNumbersCount(count);

        if (count == 0)
        {
            return;
        }

        int start;
        int end;
        if (TotalPages <= maxVisible)
        {
            start = 1;
            end = TotalPages;
        }
        else
        {
            end = Math.Min(CurrentPage + 2, TotalPages);
            start = Math.Max(1, end - 4);
            end = Math.Min(start + 4, TotalPages);
        }

        for (int i = 0; i < count; i++)
        {
            var pageNumber = start + i;
            var item = PageNumbers[i];
            item.PageNumber = pageNumber;
            item.IsCurrent = pageNumber == CurrentPage;
        }
    }

    private void EnsurePageNumbersCount(int count)
    {
        while (PageNumbers.Count < count) PageNumbers.Add(new PageItem());
        while (PageNumbers.Count > count) PageNumbers.RemoveAt(PageNumbers.Count - 1);
    }
}
