using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using App1.Data.Interfaces;
using Microsoft.Data.Sqlite;

namespace App1.Data.DataSources;

public class SqliteDataSource : ISqliteDataSource
{
    private readonly string _connectionString;

    public SqliteDataSource()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "App1");
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "devices.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        pragma.ExecuteNonQuery();
        return conn;
    }

    public async Task InitializeAsync()
    {
        using var conn = GetConnection();

        using var createCmd = conn.CreateCommand();
        createCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Models (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Manufacturer TEXT NOT NULL,
                Category TEXT NOT NULL,
                SubCategory TEXT NOT NULL,
                Available INTEGER NOT NULL DEFAULT 0,
                Reserved INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Devices (
                Id TEXT PRIMARY KEY,
                ModelId TEXT NOT NULL,
                Name TEXT NOT NULL,
                IMEI TEXT NOT NULL,
                SerialLab TEXT NOT NULL,
                SerialNumber TEXT NOT NULL,
                CircuitSerialNumber TEXT NOT NULL,
                HWVersion TEXT NOT NULL,
                Status TEXT NOT NULL DEFAULT 'Available',
                BorrowedDate TEXT NOT NULL DEFAULT '',
                ReturnDate TEXT NOT NULL DEFAULT '',
                Invoice TEXT NOT NULL DEFAULT '',
                Inventory TEXT NOT NULL DEFAULT '',
                InstanceId TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (ModelId) REFERENCES Models(Id)
            );

            CREATE INDEX IF NOT EXISTS idx_models_name ON Models(Name);
            CREATE INDEX IF NOT EXISTS idx_models_manufacturer ON Models(Manufacturer);
            CREATE INDEX IF NOT EXISTS idx_models_category ON Models(Category);
            CREATE INDEX IF NOT EXISTS idx_models_subcategory ON Models(SubCategory);
            CREATE INDEX IF NOT EXISTS idx_devices_modelid ON Devices(ModelId);
            CREATE INDEX IF NOT EXISTS idx_devices_instanceid ON Devices(InstanceId);
            CREATE INDEX IF NOT EXISTS idx_devices_status ON Devices(Status);
        ";
        await createCmd.ExecuteNonQueryAsync();

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Models";
        var count = (long)(await countCmd.ExecuteScalarAsync())!;

        if (count == 0)
        {
            await SeedAsync(conn);
        }
    }

    private static async Task SeedAsync(SqliteConnection conn)
    {
        var baseDir = AppContext.BaseDirectory;
        var sampleDataDir = Path.Combine(baseDir, "SampleData");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        using var bulkPragma = conn.CreateCommand();
        bulkPragma.CommandText = "PRAGMA temp_store = MEMORY; PRAGMA cache_size = -64000;";
        await bulkPragma.ExecuteNonQueryAsync();

        await SeedModelsAsync(conn, Path.Combine(sampleDataDir, "dbModels.json"), options);
        await SeedDevicesAsync(conn, Path.Combine(sampleDataDir, "dbDevices.json"), options);
    }

    private const int BatchSize = 5000;

    private static async Task SeedModelsAsync(SqliteConnection conn, string filePath, JsonSerializerOptions options)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Models (Id, Name, Manufacturer, Category, SubCategory, Available, Reserved)
            VALUES (@id, @name, @mfr, @cat, @sub, @avail, 0)";

        var pId   = cmd.Parameters.Add("@id", SqliteType.Text);
        var pName = cmd.Parameters.Add("@name", SqliteType.Text);
        var pMfr  = cmd.Parameters.Add("@mfr", SqliteType.Text);
        var pCat  = cmd.Parameters.Add("@cat", SqliteType.Text);
        var pSub  = cmd.Parameters.Add("@sub", SqliteType.Text);
        var pAvl  = cmd.Parameters.Add("@avail", SqliteType.Integer);

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 81920, useAsync: true);

        var tx = conn.BeginTransaction();
        cmd.Transaction = tx;
        cmd.Prepare();

        int count = 0;
        await foreach (var m in JsonSerializer.DeserializeAsyncEnumerable<JsonModel>(stream, options))
        {
            if (m is null) continue;

            pId.Value   = m.Id;
            pName.Value = m.Name;
            pMfr.Value  = m.Manufacturer;
            pCat.Value  = m.Category;
            pSub.Value  = m.SubCategory;
            pAvl.Value  = m.Available;
            await cmd.ExecuteNonQueryAsync();

            if (++count % BatchSize == 0)
            {
                tx.Commit();
                tx = conn.BeginTransaction();
                cmd.Transaction = tx;
            }
        }

        tx.Commit();
    }

    private static async Task SeedDevicesAsync(SqliteConnection conn, string filePath, JsonSerializerOptions options)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Devices (Id, ModelId, Name, IMEI, SerialLab, SerialNumber, CircuitSerialNumber, HWVersion)
            VALUES (@id, @modelId, @name, @imei, @serialLab, @serial, @circuit, @hw)";

        var pId      = cmd.Parameters.Add("@id", SqliteType.Text);
        var pModelId = cmd.Parameters.Add("@modelId", SqliteType.Text);
        var pName    = cmd.Parameters.Add("@name", SqliteType.Text);
        var pImei    = cmd.Parameters.Add("@imei", SqliteType.Text);
        var pSLab    = cmd.Parameters.Add("@serialLab", SqliteType.Text);
        var pSerial  = cmd.Parameters.Add("@serial", SqliteType.Text);
        var pCircuit = cmd.Parameters.Add("@circuit", SqliteType.Text);
        var pHw      = cmd.Parameters.Add("@hw", SqliteType.Text);

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize: 81920, useAsync: true);

        var tx = conn.BeginTransaction();
        cmd.Transaction = tx;
        cmd.Prepare();

        int count = 0;
        await foreach (var d in JsonSerializer.DeserializeAsyncEnumerable<JsonDevice>(stream, options))
        {
            if (d is null) continue;

            pId.Value      = d.Id;
            pModelId.Value = d.ModelId;
            pName.Value    = d.Name;
            pImei.Value    = d.IMEI;
            pSLab.Value    = d.SerialLab;
            pSerial.Value  = d.SerialNumber;
            pCircuit.Value = d.CircuitSerialNumber;
            pHw.Value      = d.HWVersion;
            await cmd.ExecuteNonQueryAsync();

            if (++count % BatchSize == 0)
            {
                tx.Commit();
                tx = conn.BeginTransaction();
                cmd.Transaction = tx;
            }
        }

        tx.Commit();
    }

    private record JsonModel(string Id, string Name, string Manufacturer, string Category, string SubCategory, int Available);
    private record JsonDevice(string Id, string ModelId, string Name, string IMEI, string SerialLab, string SerialNumber, string CircuitSerialNumber, string HWVersion);
}
