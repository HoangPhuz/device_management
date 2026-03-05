using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using App1.Data.Interfaces;
using App1.Domain.Entities;
using App1.Domain.Interfaces;
using App1.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace App1.Data.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly ISqliteDataSource _ds;
    private readonly IDeviceModelRepository _modelRepo;

    public DeviceRepository(ISqliteDataSource ds, IDeviceModelRepository modelRepo)
    {
        _ds = ds;
        _modelRepo = modelRepo;
    }

    public async Task<PagedResult<Device>> GetPagedAsync(QueryParameters q, string instanceId)
    {
        using var conn = _ds.GetConnection();

        var where = new StringBuilder(" WHERE InstanceId = @inst AND Status = 'Occupied'");
        var parameters = new List<SqliteParameter> { new("@inst", instanceId) };

        foreach (var (key, value) in q.Filters)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;

            if (key == "Status")
            {
                where.Append($" AND Status = @{key}");
                parameters.Add(new SqliteParameter($"@{key}", value));
            }
            else
            {
                where.Append($" AND {key} LIKE @{key}");
                parameters.Add(new SqliteParameter($"@{key}", $"%{value}%"));
            }
        }

        var whereClause = where.ToString();

        var allowedSort = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Name", "IMEI", "SerialLab", "SerialNumber", "CircuitSerialNumber",
            "HWVersion", "BorrowedDate", "ReturnDate", "Invoice", "Status", "Inventory"
        };
        var sortCol = allowedSort.Contains(q.SortColumn ?? "") ? q.SortColumn! : "Name";
        var sortDir = q.SortAscending ? "ASC" : "DESC";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM Devices{whereClause}";
        foreach (var p in parameters) countCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $@"
            SELECT Id, ModelId, Name, IMEI, SerialLab, SerialNumber,
                   CircuitSerialNumber, HWVersion, Status, BorrowedDate,
                   ReturnDate, Invoice, Inventory, InstanceId
            FROM Devices{whereClause}
            ORDER BY {sortCol} {sortDir}
            LIMIT @pageSize OFFSET @offset";
        foreach (var p in parameters) dataCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
        dataCmd.Parameters.AddWithValue("@pageSize", q.PageSize);
        dataCmd.Parameters.AddWithValue("@offset", (q.Page - 1) * q.PageSize);

        var items = new List<Device>();
        using var reader = await dataCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new Device
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

        return new PagedResult<Device>
        {
            Items = items,
            TotalCount = totalCount,
            Page = q.Page,
            PageSize = q.PageSize
        };
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
            await _modelRepo.RefreshCacheAsync();
            return true;
        }
        catch
        {
            tx.Rollback();
            return false;
        }
    }
}
