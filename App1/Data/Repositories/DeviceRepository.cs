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

public class DeviceRepository : IDeviceRepository
{
    private readonly ISqliteDataSource _ds;
    private readonly IDeviceModelRepository _modelRepo;
    private List<Device>? _cache;
    private string? _cacheInstanceId;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public DeviceRepository(ISqliteDataSource ds, IDeviceModelRepository modelRepo)
    {
        _ds = ds;
        _modelRepo = modelRepo;
    }

    private async Task<List<Device>> EnsureCacheAsync(string instanceId)
    {
        if (_cache != null && _cacheInstanceId == instanceId) return _cache;
        await _cacheLock.WaitAsync();
        try
        {
            if (_cache != null && _cacheInstanceId == instanceId) return _cache;
            _cache = await Task.Run(() => LoadAllFromDb(instanceId));
            _cacheInstanceId = instanceId;
            return _cache;
        }
        finally { _cacheLock.Release(); }
    }

    private List<Device> LoadAllFromDb(string instanceId)
    {
        using var conn = _ds.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, ModelId, Name, IMEI, SerialLab, SerialNumber,
                   CircuitSerialNumber, HWVersion, Status, BorrowedDate,
                   ReturnDate, Invoice, Inventory, InstanceId
            FROM Devices
            WHERE InstanceId = @inst AND Status = 'Occupied'";
        cmd.Parameters.AddWithValue("@inst", instanceId);

        var list = new List<Device>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new Device
            {
                Id = reader.GetString(0),
                ModelId = reader.GetString(1),
                Name = reader.GetString(2),
                IMEI = reader.GetString(3),
                SerialLab = reader.GetString(4),
                SerialNumber = reader.GetString(5),
                CircuitSerialNumber = reader.GetString(6),
                HWVersion = reader.GetString(7),
                Status = reader.GetString(8),
                BorrowedDate = reader.GetString(9),
                ReturnDate = reader.GetString(10),
                Invoice = reader.GetString(11),
                Inventory = reader.GetString(12),
                InstanceId = reader.GetString(13)
            });
        }
        return list;
    }

    public async Task<PagedResult<Device>> GetPagedAsync(QueryParameters q, string instanceId)
    {
        var all = await EnsureCacheAsync(instanceId);
        IEnumerable<Device> filtered = all;

        foreach (var (key, value) in q.Filters)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            filtered = key switch
            {
                "Name" => filtered.Where(d =>
                    d.Name.Contains(value, StringComparison.OrdinalIgnoreCase)),
                "Status" => filtered.Where(d =>
                    d.Status.Equals(value, StringComparison.OrdinalIgnoreCase)),
                "IMEI" => filtered.Where(d =>
                    d.IMEI.Contains(value, StringComparison.OrdinalIgnoreCase)),
                "SerialLab" => filtered.Where(d =>
                    d.SerialLab.Contains(value, StringComparison.OrdinalIgnoreCase)),
                "SerialNumber" => filtered.Where(d =>
                    d.SerialNumber.Contains(value, StringComparison.OrdinalIgnoreCase)),
                "CircuitSerialNumber" => filtered.Where(d =>
                    d.CircuitSerialNumber.Contains(value, StringComparison.OrdinalIgnoreCase)),
                "HWVersion" => filtered.Where(d =>
                    d.HWVersion.Contains(value, StringComparison.OrdinalIgnoreCase)),
                "Inventory" => filtered.Where(d =>
                    d.Inventory.Contains(value, StringComparison.OrdinalIgnoreCase)),
                _ => filtered
            };
        }

        var filteredList = filtered.ToList();
        var sorted = ApplySort(filteredList, q.SortColumn, q.SortAscending);
        var paged = sorted.Skip((q.Page - 1) * q.PageSize).Take(q.PageSize).ToList();

        return new PagedResult<Device>
        {
            Items = paged,
            TotalCount = filteredList.Count,
            Page = q.Page,
            PageSize = q.PageSize
        };
    }

    private static IEnumerable<Device> ApplySort(List<Device> source, string? column, bool ascending)
    {
        var col = column ?? "Name";
        if (ascending)
        {
            return col switch
            {
                "Name" => source.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase),
                "IMEI" => source.OrderBy(d => d.IMEI, StringComparer.OrdinalIgnoreCase),
                "SerialLab" => source.OrderBy(d => d.SerialLab, StringComparer.OrdinalIgnoreCase),
                "SerialNumber" => source.OrderBy(d => d.SerialNumber, StringComparer.OrdinalIgnoreCase),
                "CircuitSerialNumber" => source.OrderBy(d => d.CircuitSerialNumber, StringComparer.OrdinalIgnoreCase),
                "HWVersion" => source.OrderBy(d => d.HWVersion, StringComparer.OrdinalIgnoreCase),
                "BorrowedDate" => source.OrderBy(d => d.BorrowedDate, StringComparer.OrdinalIgnoreCase),
                "ReturnDate" => source.OrderBy(d => d.ReturnDate, StringComparer.OrdinalIgnoreCase),
                "Invoice" => source.OrderBy(d => d.Invoice, StringComparer.OrdinalIgnoreCase),
                "Status" => source.OrderBy(d => d.Status, StringComparer.OrdinalIgnoreCase),
                "Inventory" => source.OrderBy(d => d.Inventory, StringComparer.OrdinalIgnoreCase),
                _ => source.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            };
        }
        return col switch
        {
            "Name" => source.OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase),
            "IMEI" => source.OrderByDescending(d => d.IMEI, StringComparer.OrdinalIgnoreCase),
            "SerialLab" => source.OrderByDescending(d => d.SerialLab, StringComparer.OrdinalIgnoreCase),
            "SerialNumber" => source.OrderByDescending(d => d.SerialNumber, StringComparer.OrdinalIgnoreCase),
            "CircuitSerialNumber" => source.OrderByDescending(d => d.CircuitSerialNumber, StringComparer.OrdinalIgnoreCase),
            "HWVersion" => source.OrderByDescending(d => d.HWVersion, StringComparer.OrdinalIgnoreCase),
            "BorrowedDate" => source.OrderByDescending(d => d.BorrowedDate, StringComparer.OrdinalIgnoreCase),
            "ReturnDate" => source.OrderByDescending(d => d.ReturnDate, StringComparer.OrdinalIgnoreCase),
            "Invoice" => source.OrderByDescending(d => d.Invoice, StringComparer.OrdinalIgnoreCase),
            "Status" => source.OrderByDescending(d => d.Status, StringComparer.OrdinalIgnoreCase),
            "Inventory" => source.OrderByDescending(d => d.Inventory, StringComparer.OrdinalIgnoreCase),
            _ => source.OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase)
        };
    }

    public async Task RefreshCacheAsync(string instanceId)
    {
        _cache = null;
        _cacheInstanceId = null;
        await EnsureCacheAsync(instanceId);
    }

    public async Task<bool> ReturnAsync(List<string> deviceIds)
    {
        if (deviceIds.Count == 0) return false;

        using var conn = _ds.GetConnection();
        using var tx = conn.BeginTransaction();

        try
        {
            var placeholders = string.Join(",", deviceIds.Select((_, i) => $"@id{i}"));

            using var modelCountCmd = conn.CreateCommand();
            modelCountCmd.Transaction = tx;
            modelCountCmd.CommandText = $@"
                SELECT ModelId, COUNT(*) as cnt
                FROM Devices
                WHERE Id IN ({placeholders})
                GROUP BY ModelId";
            for (int i = 0; i < deviceIds.Count; i++)
                modelCountCmd.Parameters.AddWithValue($"@id{i}", deviceIds[i]);

            var modelCounts = new List<(string modelId, int count)>();
            using (var reader = await modelCountCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    modelCounts.Add((reader.GetString(0), reader.GetInt32(1)));
            }

            foreach (var (modelId, count) in modelCounts)
            {
                using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = "UPDATE Models SET Available = Available + @cnt, Reserved = Reserved - @cnt WHERE Id = @id";
                updateCmd.Parameters.AddWithValue("@cnt", count);
                updateCmd.Parameters.AddWithValue("@id", modelId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            var returnDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            using var resetCmd = conn.CreateCommand();
            resetCmd.Transaction = tx;
            resetCmd.CommandText = $@"
                UPDATE Devices
                SET Status = 'Available', ReturnDate = @returnDate, InstanceId = ''
                WHERE Id IN ({placeholders})";
            resetCmd.Parameters.AddWithValue("@returnDate", returnDate);
            for (int i = 0; i < deviceIds.Count; i++)
                resetCmd.Parameters.AddWithValue($"@id{i}", deviceIds[i]);
            await resetCmd.ExecuteNonQueryAsync();

            tx.Commit();

            foreach (var (modelId, count) in modelCounts)
                _modelRepo.UpdateCachedModel(modelId, +count, -count);

            RemoveFromCache(deviceIds);

            return true;
        }
        catch
        {
            tx.Rollback();
            return false;
        }
    }

    private void RemoveFromCache(List<string> deviceIds)
    {
        if (_cache == null) return;
        var idsSet = new HashSet<string>(deviceIds);
        _cache.RemoveAll(d => idsSet.Contains(d.Id));
    }

    public void AddToCache(Device device)
    {
        if (_cache == null || _cacheInstanceId != device.InstanceId) return;
        _cache.Add(device);
    }
}
