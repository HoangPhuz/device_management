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

public class DeviceModelRepository : IDeviceModelRepository
{
    private readonly ISqliteDataSource _ds;

    public DeviceModelRepository(ISqliteDataSource ds) => _ds = ds;

    public async Task<PagedResult<DeviceModel>> GetPagedAsync(QueryParameters q)
    {
        using var conn = _ds.GetConnection();

        var where = new StringBuilder();
        var parameters = new List<SqliteParameter>();

        foreach (var (key, value) in q.Filters)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;

            if (key is "Category" or "SubCategory")
            {
                where.Append(where.Length == 0 ? " WHERE " : " AND ");
                where.Append($"{key} = @{key}");
                parameters.Add(new SqliteParameter($"@{key}", value));
            }
            else
            {
                where.Append(where.Length == 0 ? " WHERE " : " AND ");
                where.Append($"{key} LIKE @{key}");
                parameters.Add(new SqliteParameter($"@{key}", $"%{value}%"));
            }
        }

        var whereClause = where.ToString();

        var allowedSort = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Name", "Manufacturer", "Category", "SubCategory", "Available", "Reserved" };
        var sortCol = allowedSort.Contains(q.SortColumn ?? "") ? q.SortColumn! : "Name";
        var sortDir = q.SortAscending ? "ASC" : "DESC";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM Models{whereClause}";
        foreach (var p in parameters) countCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $@"
            SELECT Id, Name, Manufacturer, Category, SubCategory, Available, Reserved
            FROM Models{whereClause}
            ORDER BY {sortCol} {sortDir}
            LIMIT @pageSize OFFSET @offset";
        foreach (var p in parameters) dataCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
        dataCmd.Parameters.AddWithValue("@pageSize", q.PageSize);
        dataCmd.Parameters.AddWithValue("@offset", (q.Page - 1) * q.PageSize);

        var items = new List<DeviceModel>();
        using var reader = await dataCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new DeviceModel
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

        return new PagedResult<DeviceModel>
        {
            Items = items,
            TotalCount = totalCount,
            Page = q.Page,
            PageSize = q.PageSize
        };
    }

    public async Task<List<string>> GetDistinctCategoriesAsync()
    {
        using var conn = _ds.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT Category FROM Models ORDER BY Category";
        var result = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<List<string>> GetDistinctSubCategoriesAsync(string? category = null)
    {
        using var conn = _ds.GetConnection();
        using var cmd = conn.CreateCommand();
        if (string.IsNullOrEmpty(category))
        {
            cmd.CommandText = "SELECT DISTINCT SubCategory FROM Models ORDER BY SubCategory";
        }
        else
        {
            cmd.CommandText = "SELECT DISTINCT SubCategory FROM Models WHERE Category = @cat ORDER BY SubCategory";
            cmd.Parameters.AddWithValue("@cat", category);
        }
        var result = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
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
            return true;
        }
        catch
        {
            tx.Rollback();
            return false;
        }
    }
}
