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
            { "Model", "Manufacturer", "Category", "SubCategory", "Available", "Reserved" };
        var sortCol = allowedSort.Contains(q.SortColumn ?? "") ? q.SortColumn! : "Id";
        var sortDir = q.SortAscending ? "ASC" : "DESC";

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"SELECT COUNT(*) FROM DeviceModels{whereClause}";
        foreach (var p in parameters) countCmd.Parameters.Add(new SqliteParameter(p.ParameterName, p.Value));
        var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

        using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $@"
            SELECT Id, Model, Manufacturer, Category, SubCategory, Available, Reserved
            FROM DeviceModels{whereClause}
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
                Id = reader.GetInt64(0),
                Model = reader.GetString(1),
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
        cmd.CommandText = "SELECT DISTINCT Category FROM DeviceModels ORDER BY Category";
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
            cmd.CommandText = "SELECT DISTINCT SubCategory FROM DeviceModels ORDER BY SubCategory";
        }
        else
        {
            cmd.CommandText = "SELECT DISTINCT SubCategory FROM DeviceModels WHERE Category = @cat ORDER BY SubCategory";
            cmd.Parameters.AddWithValue("@cat", category);
        }
        var result = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<bool> BorrowAsync(long modelId, int quantity, string instanceId)
    {
        using var conn = _ds.GetConnection();
        using var tx = conn.BeginTransaction();

        try
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.Transaction = tx;
            checkCmd.CommandText = "SELECT Available, Model, Manufacturer, Category, SubCategory FROM DeviceModels WHERE Id = @id";
            checkCmd.Parameters.AddWithValue("@id", modelId);

            string modelName, manufacturer, category, subCategory;
            int available;

            using (var reader = await checkCmd.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) return false;
                available = reader.GetInt32(0);
                modelName = reader.GetString(1);
                manufacturer = reader.GetString(2);
                category = reader.GetString(3);
                subCategory = reader.GetString(4);
            }

            if (available < quantity) return false;

            using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = tx;
            updateCmd.CommandText = "UPDATE DeviceModels SET Available = Available - @qty, Reserved = Reserved + @qty WHERE Id = @id";
            updateCmd.Parameters.AddWithValue("@qty", quantity);
            updateCmd.Parameters.AddWithValue("@id", modelId);
            await updateCmd.ExecuteNonQueryAsync();

            var rng = new Random();
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            for (int i = 0; i < quantity; i++)
            {
                using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx;
                insertCmd.CommandText = @"
                    INSERT INTO BorrowedDevices
                    (DeviceModelId, ModelName, IMEI, Label, SerialNumber, CircuitSerialNumber, HWVersion, BorrowedDate, InstanceId)
                    VALUES (@modelId, @modelName, @imei, @label, @serial, @circuit, @hw, @date, @inst)";
                insertCmd.Parameters.AddWithValue("@modelId", modelId);
                insertCmd.Parameters.AddWithValue("@modelName", modelName);
                insertCmd.Parameters.AddWithValue("@imei", $"{rng.NextInt64(100000000000000, 999999999999999)}");
                insertCmd.Parameters.AddWithValue("@label", $"{modelName}-{rng.Next(1000, 9999)}");
                insertCmd.Parameters.AddWithValue("@serial", $"SN-{Guid.NewGuid().ToString()[..8].ToUpper()}");
                insertCmd.Parameters.AddWithValue("@circuit", $"CS-{Guid.NewGuid().ToString()[..8].ToUpper()}");
                insertCmd.Parameters.AddWithValue("@hw", $"v{rng.Next(1, 5)}.{rng.Next(0, 9)}");
                insertCmd.Parameters.AddWithValue("@date", now);
                insertCmd.Parameters.AddWithValue("@inst", instanceId);
                await insertCmd.ExecuteNonQueryAsync();
            }

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
