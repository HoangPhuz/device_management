using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App1.Data.Interfaces;
using App1.Domain.Entities;
using App1.Domain.Interfaces;
using App1.Domain.ValueObjects;

namespace App1.Data.Repositories;

public class DeviceModelRepository : IDeviceModelRepository
{
    private readonly ISqliteDataSource _ds;
    private List<DeviceModel>? _cache;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public DeviceModelRepository(ISqliteDataSource ds) => _ds = ds;

    private async Task<List<DeviceModel>> EnsureCacheAsync()
    {
        if (_cache != null) return _cache;
        await _cacheLock.WaitAsync();
        try
        {
            if (_cache != null) return _cache;
            _cache = await Task.Run(LoadAllFromDb);
            return _cache;
        }
        finally { _cacheLock.Release(); }
    }

    private List<DeviceModel> LoadAllFromDb()
    {
        using var conn = _ds.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Manufacturer, Category, SubCategory, Available, Reserved FROM Models";
        var list = new List<DeviceModel>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new DeviceModel
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Manufacturer = reader.GetString(2),
                Category = reader.GetString(3),
                SubCategory = reader.GetString(4),
                Available = reader.GetInt32(5),
                Reserved = reader.GetInt32(6)
            });
        }
        return list;
    }

    public async Task<PagedResult<DeviceModel>> GetPagedAsync(QueryParameters q)
    {
        var all = await EnsureCacheAsync();
        IEnumerable<DeviceModel> filtered = all;

        foreach (var (key, value) in q.Filters)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            filtered = key switch
            {
                "Category" => filtered.Where(m =>
                    m.Category.Equals(value, StringComparison.OrdinalIgnoreCase)),
                "SubCategory" => filtered.Where(m =>
                    m.SubCategory.Equals(value, StringComparison.OrdinalIgnoreCase)),
                "Name" => filtered.Where(m =>
                    m.Name.Contains(value, StringComparison.OrdinalIgnoreCase)),
                "Manufacturer" => filtered.Where(m =>
                    m.Manufacturer.Contains(value, StringComparison.OrdinalIgnoreCase)),
                _ => filtered
            };
        }

        var filteredList = filtered.ToList();
        var sorted = ApplySort(filteredList, q.SortColumn, q.SortAscending);
        var paged = sorted.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToList();

        return new PagedResult<DeviceModel>
        {
            Items = paged,
            TotalCount = filteredList.Count,
            Page = q.Page,
            PageSize = q.PageSize
        };
    }

    private static IEnumerable<DeviceModel> ApplySort(
        List<DeviceModel> source, string? column, bool ascending)
    {
        var col = column ?? "Name";
        if (ascending)
        {
            return col switch
            {
                "Name" => source.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase),
                "Manufacturer" => source.OrderBy(m => m.Manufacturer, StringComparer.OrdinalIgnoreCase),
                "Category" => source.OrderBy(m => m.Category, StringComparer.OrdinalIgnoreCase),
                "SubCategory" => source.OrderBy(m => m.SubCategory, StringComparer.OrdinalIgnoreCase),
                "Available" => source.OrderBy(m => m.Available),
                "Reserved" => source.OrderBy(m => m.Reserved),
                _ => source.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            };
        }
        return col switch
        {
            "Name" => source.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase),
            "Manufacturer" => source.OrderByDescending(m => m.Manufacturer, StringComparer.OrdinalIgnoreCase),
            "Category" => source.OrderByDescending(m => m.Category, StringComparer.OrdinalIgnoreCase),
            "SubCategory" => source.OrderByDescending(m => m.SubCategory, StringComparer.OrdinalIgnoreCase),
            "Available" => source.OrderByDescending(m => m.Available),
            "Reserved" => source.OrderByDescending(m => m.Reserved),
            _ => source.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase)
        };
    }

    public async Task<List<string>> GetDistinctCategoriesAsync()
    {
        var all = await EnsureCacheAsync();
        return all.Select(m => m.Category).Distinct().OrderBy(c => c).ToList();
    }

    public async Task<List<string>> GetDistinctSubCategoriesAsync(string? category = null)
    {
        var all = await EnsureCacheAsync();
        var query = string.IsNullOrEmpty(category)
            ? all.AsEnumerable()
            : all.Where(m => m.Category == category);
        return query.Select(m => m.SubCategory).Distinct().OrderBy(s => s).ToList();
    }

    public async Task<bool> BorrowAsync(string modelId, int quantity, string instanceId)
    {
        using var conn = _ds.GetConnection();
        using var tx = conn.BeginTransaction();

        try
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.Transaction = tx;
            checkCmd.CommandText = "SELECT Available FROM Models WHERE Id = @id";
            checkCmd.Parameters.AddWithValue("@id", modelId);

            int available;
            using (var reader = await checkCmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) return false;
                available = reader.GetInt32(0);
            }

            if (available < quantity) return false;

            using var findCmd = conn.CreateCommand();
            findCmd.Transaction = tx;
            findCmd.CommandText = @"
                SELECT Id FROM Devices
                WHERE ModelId = @modelId AND Status = 'Available'
                LIMIT @qty";
            findCmd.Parameters.AddWithValue("@modelId", modelId);
            findCmd.Parameters.AddWithValue("@qty", quantity);

            var deviceIds = new List<string>();
            using (var reader = await findCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    deviceIds.Add(reader.GetString(0));
            }

            if (deviceIds.Count < quantity) return false;

            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            foreach (var deviceId in deviceIds)
            {
                using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = @"
                    UPDATE Devices
                    SET Status = 'Occupied', BorrowedDate = @date, InstanceId = @inst
                    WHERE Id = @id";
                updateCmd.Parameters.AddWithValue("@date", now);
                updateCmd.Parameters.AddWithValue("@inst", instanceId);
                updateCmd.Parameters.AddWithValue("@id", deviceId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            using var decrCmd = conn.CreateCommand();
            decrCmd.Transaction = tx;
            decrCmd.CommandText = "UPDATE Models SET Available = Available - @qty, Reserved = Reserved + @qty WHERE Id = @id";
            decrCmd.Parameters.AddWithValue("@qty", deviceIds.Count);
            decrCmd.Parameters.AddWithValue("@id", modelId);
            await decrCmd.ExecuteNonQueryAsync();

            tx.Commit();
            _cache = null;
            return true;
        }
        catch
        {
            tx.Rollback();
            return false;
        }
    }

    public async Task RefreshCacheAsync()
    {
        _cache = null;
        await EnsureCacheAsync();
    }
}
