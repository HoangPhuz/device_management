using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    public BorrowedDevice Device { get; }
    [ObservableProperty] private bool _isSelected;

    public SelectableDevice(BorrowedDevice device) => Device = device;

    public long Id => Device.Id;
    public string ModelName => Device.ModelName;
    public string IMEI => Device.IMEI;
    public string Label => Device.Label;
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
    private readonly GetBorrowedDevicesUseCase _getBorrowed;
    private readonly ReturnDeviceUseCase _return;
    private readonly SyncService _sync;

    public MyDeviceViewModel(
        GetBorrowedDevicesUseCase getBorrowed,
        ReturnDeviceUseCase returnDevice,
        SyncService sync)
    {
        _getBorrowed = getBorrowed;
        _return = returnDevice;
        _sync = sync;
        _sync.DataChanged += OnSyncDataChanged;
    }

    [ObservableProperty] private ObservableCollection<SelectableDevice> _items = new();
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private int _totalRecords;
    [ObservableProperty] private int _pageSize = 50;
    [ObservableProperty] private ObservableCollection<PageItem> _pageNumbers = new();

    [ObservableProperty] private string _filterModelName = string.Empty;
    [ObservableProperty] private string _filterStatus = string.Empty;

    [ObservableProperty] private string? _sortColumn;
    [ObservableProperty] private bool _sortAscending = true;

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
        _dispatcher?.TryEnqueue(async () => await LoadDataAsync());
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        var query = new QueryParameters
        {
            Page = CurrentPage,
            PageSize = PageSize,
            SortColumn = SortColumn,
            SortAscending = SortAscending,
            Filters = new Dictionary<string, string>()
        };

        if (!string.IsNullOrWhiteSpace(FilterModelName))
            query.Filters["ModelName"] = FilterModelName;
        if (!string.IsNullOrWhiteSpace(FilterStatus))
            query.Filters["Status"] = FilterStatus;

        var result = await _getBorrowed.ExecuteAsync(query, App.InstanceId);

        Items = new ObservableCollection<SelectableDevice>(
            result.Items.Select(d => new SelectableDevice(d)));
        TotalRecords = result.TotalCount;
        TotalPages = result.TotalPages;

        NotifyPaginationProperties();
        GeneratePageNumbers();
        OnPropertyChanged(nameof(HasSelection));
    }

    public async Task<bool> ReturnSelectedAsync()
    {
        var selectedIds = Items.Where(x => x.IsSelected).Select(x => x.Id).ToList();
        if (selectedIds.Count == 0) return false;

        var success = await _return.ExecuteAsync(selectedIds);
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
        FilterModelName = string.Empty;
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
        var pages = new ObservableCollection<PageItem>();
        const int maxVisible = 5;

        if (TotalPages <= maxVisible)
        {
            for (int i = 1; i <= TotalPages; i++)
                pages.Add(new PageItem { PageNumber = i, IsCurrent = i == CurrentPage });
        }
        else
        {
            int windowEnd = Math.Min(CurrentPage + 2, TotalPages);
            int windowStart = Math.Max(1, windowEnd - 4);
            windowEnd = Math.Min(windowStart + 4, TotalPages);

            for (int i = windowStart; i <= windowEnd; i++)
                pages.Add(new PageItem { PageNumber = i, IsCurrent = i == CurrentPage });
        }

        PageNumbers = pages;
    }
}
