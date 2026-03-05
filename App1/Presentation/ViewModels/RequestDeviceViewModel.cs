using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using App1.Domain.Entities;
using App1.Domain.UseCases;
using App1.Domain.ValueObjects;
using App1.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace App1.Presentation.ViewModels;

public partial class RequestDeviceViewModel : ObservableObject
{
    private readonly GetDeviceModelsUseCase _getModels;
    private readonly BorrowDeviceUseCase _borrow;
    private readonly GetCategoriesUseCase _getCategories;
    private readonly SyncService _sync;

    public RequestDeviceViewModel(
        GetDeviceModelsUseCase getModels,
        BorrowDeviceUseCase borrow,
        GetCategoriesUseCase getCategories,
        SyncService sync)
    {
        _getModels = getModels;
        _borrow = borrow;
        _getCategories = getCategories;
        _sync = sync;
        _sync.DataChanged += OnSyncDataChanged;
    }

    [ObservableProperty] private ObservableCollection<DeviceModel> _items = new();
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private int _totalRecords;
    [ObservableProperty] private int _pageSize = 50;
    [ObservableProperty] private ObservableCollection<PageItem> _pageNumbers = new();

    [ObservableProperty] private string _filterName = string.Empty;
    [ObservableProperty] private string _filterManufacturer = string.Empty;
    [ObservableProperty] private string _filterCategory = string.Empty;
    [ObservableProperty] private string _filterSubCategory = string.Empty;

    [ObservableProperty] private List<string> _categories = new();
    [ObservableProperty] private List<string> _subCategories = new();

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

    private Microsoft.UI.Dispatching.DispatcherQueue? _dispatcher;

    public void SetDispatcher(Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
        => _dispatcher = dispatcher;

    private void OnSyncDataChanged()
    {
        _dispatcher?.TryEnqueue(async () => await LoadDataAsync());
    }

    public async Task LoadCategoriesAsync()
    {
        Categories = await _getCategories.GetCategoriesAsync();
        SubCategories = await _getCategories.GetSubCategoriesAsync();
    }

    public async Task UpdateSubCategoriesAsync(string? category)
    {
        SubCategories = string.IsNullOrEmpty(category)
            ? await _getCategories.GetSubCategoriesAsync()
            : await _getCategories.GetSubCategoriesAsync(category);
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
            if (!string.IsNullOrWhiteSpace(FilterManufacturer))
                query.Filters["Manufacturer"] = FilterManufacturer;
            if (!string.IsNullOrWhiteSpace(FilterCategory))
                query.Filters["Category"] = FilterCategory;
            if (!string.IsNullOrWhiteSpace(FilterSubCategory))
                query.Filters["SubCategory"] = FilterSubCategory;

            var result = await Task.Run(() => _getModels.ExecuteAsync(query));

            if (cts.Token.IsCancellationRequested) return;

            TotalRecords = result.TotalCount;
            TotalPages = result.TotalPages;
            NotifyPaginationProperties();
            GeneratePageNumbers();

            Items = new ObservableCollection<DeviceModel>(result.Items);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_loadCts == cts)
                IsLoading = false;
        }
    }

    public async Task<bool> BorrowAsync(string modelId, int quantity)
    {
        var success = await Task.Run(() => _borrow.ExecuteAsync(modelId, quantity, App.InstanceId));
        if (success)
        {
            _sync.Broadcast();
            await LoadDataAsync();
        }
        return success;
    }

    public void SetSort(string? column, bool ascending)
    {
        SortColumn = column;
        SortAscending = ascending;
        CurrentPage = 1;
    }

    public void ApplyFilter()
    {
        CurrentPage = 1;
    }

    public void ClearFilters()
    {
        FilterName = string.Empty;
        FilterManufacturer = string.Empty;
        FilterCategory = string.Empty;
        FilterSubCategory = string.Empty;
        CurrentPage = 1;
    }

    [RelayCommand] private void GoToFirst() { CurrentPage = 1; }
    [RelayCommand] private void GoToPrevious() { if (CurrentPage > 1) CurrentPage--; }
    [RelayCommand] private void GoToNext() { if (CurrentPage < TotalPages) CurrentPage++; }
    [RelayCommand] private void GoToLast() { CurrentPage = TotalPages; }
    [RelayCommand] private void GoToPage(int page) { if (page >= 1 && page <= TotalPages) CurrentPage = page; }

    public void SetPageSize(int size)
    {
        PageSize = size;
        CurrentPage = 1;
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

public class PageItem
{
    public int? PageNumber { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsEllipsis => PageNumber == null;
    public string DisplayText => PageNumber?.ToString() ?? "...";
    public bool IsClickable => PageNumber != null && !IsCurrent;
}
