using System;
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

        var modelsJson = await File.ReadAllTextAsync(Path.Combine(sampleDataDir, "dbModels.json"));
        var devicesJson = await File.ReadAllTextAsync(Path.Combine(sampleDataDir, "dbDevices.json"));

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var models = JsonSerializer.Deserialize<JsonModel[]>(modelsJson, options) ?? [];
        var devices = JsonSerializer.Deserialize<JsonDevice[]>(devicesJson, options) ?? [];

        using var tx = conn.BeginTransaction();

        foreach (var m in models)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO Models (Id, Name, Manufacturer, Category, SubCategory, Available, Reserved)
                VALUES (@id, @name, @mfr, @cat, @sub, @avail, 0)";
            cmd.Parameters.AddWithValue("@id", m.Id);
            cmd.Parameters.AddWithValue("@name", m.Name);
            cmd.Parameters.AddWithValue("@mfr", m.Manufacturer);
            cmd.Parameters.AddWithValue("@cat", m.Category);
            cmd.Parameters.AddWithValue("@sub", m.SubCategory);
            cmd.Parameters.AddWithValue("@avail", m.Available);
            await cmd.ExecuteNonQueryAsync();
        }

        foreach (var d in devices)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
                INSERT INTO Devices (Id, ModelId, Name, IMEI, SerialLab, SerialNumber, CircuitSerialNumber, HWVersion)
                VALUES (@id, @modelId, @name, @imei, @serialLab, @serial, @circuit, @hw)";
            cmd.Parameters.AddWithValue("@id", d.Id);
            cmd.Parameters.AddWithValue("@modelId", d.ModelId);
            cmd.Parameters.AddWithValue("@name", d.Name);
            cmd.Parameters.AddWithValue("@imei", d.IMEI);
            cmd.Parameters.AddWithValue("@serialLab", d.SerialLab);
            cmd.Parameters.AddWithValue("@serial", d.SerialNumber);
            cmd.Parameters.AddWithValue("@circuit", d.CircuitSerialNumber);
            cmd.Parameters.AddWithValue("@hw", d.HWVersion);
            await cmd.ExecuteNonQueryAsync();
        }

        tx.Commit();
    }

    private record JsonModel(string Id, string Name, string Manufacturer, string Category, string SubCategory, int Available);
    private record JsonDevice(string Id, string ModelId, string Name, string IMEI, string SerialLab, string SerialNumber, string CircuitSerialNumber, string HWVersion);
}
