using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using App1.Data.Interfaces;
using App1.Domain.Entities;
using App1.Domain.Interfaces;
using App1.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace App1.Data.Repositories;

public class BorrowedDeviceRepository : IBorrowedDeviceRepository
{
    private readonly ISqliteDataSource _ds;

    public BorrowedDeviceRepository(ISqliteDataSource ds) => _ds = ds;

    public async Task<PagedResult<BorrowedDevice>> GetPagedAsync(QueryParameters q, string instanceId)
    {
        using var conn = _ds.GetConnection();

        var where = new StringBuilder(" WHERE InstanceId = @inst");
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
            "ModelName", "IMEI", "Label", "SerialNumber", "CircuitSerialNumber",
            "HWVersion", "BorrowedDate", "ReturnDate", "Invoice", "Status", "Inventory"
        };
        var sortCol = allowedSort.Contains(q.SortColumn ?? "") ? q.SortColumn! : "Id";
        var sortDir = q.SortAscending ? "ASC" : "DESC";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM BorrowedDevices{whereClause}";
        foreach (var p in parameters) countCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $@"
            SELECT Id, DeviceModelId, ModelName, IMEI, Label, SerialNumber,
                   CircuitSerialNumber, HWVersion, BorrowedDate, ReturnDate,
                   Invoice, Status, Inventory, InstanceId
            FROM BorrowedDevices{whereClause}
            ORDER BY {sortCol} {sortDir}
            LIMIT @pageSize OFFSET @offset";
        foreach (var p in parameters) dataCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
        dataCmd.Parameters.AddWithValue("@pageSize", q.PageSize);
        dataCmd.Parameters.AddWithValue("@offset", (q.Page - 1) * q.PageSize);

        var items = new List<BorrowedDevice>();
        using var reader = await dataCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new BorrowedDevice
            {
                Id = reader.GetInt64(0),
                DeviceModelId = reader.GetInt64(1),
                ModelName = reader.GetString(2),
                IMEI = reader.GetString(3),
                Label = reader.GetString(4),
                SerialNumber = reader.GetString(5),
                CircuitSerialNumber = reader.GetString(6),
                HWVersion = reader.GetString(7),
                BorrowedDate = reader.GetString(8),
                ReturnDate = reader.GetString(9),
                Invoice = reader.GetString(10),
                Status = reader.GetString(11),
                Inventory = reader.GetString(12),
                InstanceId = reader.GetString(13)
            });
        }

        return new PagedResult<BorrowedDevice>
        {
            Items = items,
            TotalCount = totalCount,
            Page = q.Page,
            PageSize = q.PageSize
        };
    }

    public async Task<bool> ReturnAsync(List<long> deviceIds)
    {
        if (deviceIds.Count == 0) return false;

        using var conn = _ds.GetConnection();
        using var tx = conn.BeginTransaction();

        try
        {
            var idList = string.Join(",", deviceIds);

            using var modelCountCmd = conn.CreateCommand();
            modelCountCmd.Transaction = tx;
            modelCountCmd.CommandText = $@"
                SELECT DeviceModelId, COUNT(*) as cnt
                FROM BorrowedDevices
                WHERE Id IN ({idList})
                GROUP BY DeviceModelId";

            var modelCounts = new List<(long modelId, int count)>();
            using (var reader = await modelCountCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    modelCounts.Add((reader.GetInt64(0), reader.GetInt32(1)));
            }

            foreach (var (modelId, count) in modelCounts)
            {
                using var updateCmd = conn.CreateCommand();
                updateCmd.Transaction = tx;
                updateCmd.CommandText = "UPDATE DeviceModels SET Available = Available + @cnt, Reserved = Reserved - @cnt WHERE Id = @id";
                updateCmd.Parameters.AddWithValue("@cnt", count);
                updateCmd.Parameters.AddWithValue("@id", modelId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            using var deleteCmd = conn.CreateCommand();
            deleteCmd.Transaction = tx;
            deleteCmd.CommandText = $"DELETE FROM BorrowedDevices WHERE Id IN ({idList})";
            await deleteCmd.ExecuteNonQueryAsync();

            tx.Commit();
            return true;
        }
        catch
        {
            tx.Rollback();
            return false;
        }
    }
}
