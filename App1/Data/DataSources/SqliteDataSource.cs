using System;
using System.IO;
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
            CREATE TABLE IF NOT EXISTS DeviceModels (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Model TEXT NOT NULL,
                Manufacturer TEXT NOT NULL,
                Category TEXT NOT NULL,
                SubCategory TEXT NOT NULL,
                Available INTEGER NOT NULL DEFAULT 0,
                Reserved INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS BorrowedDevices (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DeviceModelId INTEGER NOT NULL,
                ModelName TEXT NOT NULL,
                IMEI TEXT NOT NULL,
                Label TEXT NOT NULL,
                SerialNumber TEXT NOT NULL,
                CircuitSerialNumber TEXT NOT NULL,
                HWVersion TEXT NOT NULL,
                BorrowedDate TEXT NOT NULL,
                ReturnDate TEXT NOT NULL DEFAULT '',
                Invoice TEXT NOT NULL DEFAULT '',
                Status TEXT NOT NULL DEFAULT 'Occupied',
                Inventory TEXT NOT NULL DEFAULT '',
                InstanceId TEXT NOT NULL,
                FOREIGN KEY (DeviceModelId) REFERENCES DeviceModels(Id)
            );

            CREATE INDEX IF NOT EXISTS idx_dm_model ON DeviceModels(Model);
            CREATE INDEX IF NOT EXISTS idx_dm_manufacturer ON DeviceModels(Manufacturer);
            CREATE INDEX IF NOT EXISTS idx_dm_category ON DeviceModels(Category);
            CREATE INDEX IF NOT EXISTS idx_dm_subcategory ON DeviceModels(SubCategory);
            CREATE INDEX IF NOT EXISTS idx_bd_instanceid ON BorrowedDevices(InstanceId);
            CREATE INDEX IF NOT EXISTS idx_bd_modelid ON BorrowedDevices(DeviceModelId);
        ";
        await createCmd.ExecuteNonQueryAsync();

        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM DeviceModels";
        var count = (long)(await countCmd.ExecuteScalarAsync())!;

        if (count == 0)
        {
            await SeedAsync(conn);
        }
    }

    private static async Task SeedAsync(SqliteConnection conn)
    {
        string[] manufacturers = { "Samsung", "Apple", "Xiaomi", "OPPO", "Vivo", "Huawei", "Sony", "LG", "Nokia", "Google" };
        string[] categories = { "Smartphone", "Tablet", "Wearable", "Laptop", "Accessory" };
        string[][] subCategories =
        {
            new[] { "Flagship", "Mid-range", "Budget", "Foldable" },
            new[] { "Standard", "Pro", "Mini" },
            new[] { "Smartwatch", "Band", "Earbuds" },
            new[] { "Ultrabook", "Gaming", "Workstation" },
            new[] { "Case", "Charger", "Cable", "Screen Protector" }
        };

        const int totalRecords = 1_000_000;
        const int batchSize = 50_000;
        var rng = new Random(42);

        using var tx = conn.BeginTransaction();

        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;

        for (int batch = 0; batch < totalRecords; batch += batchSize)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("INSERT INTO DeviceModels (Model, Manufacturer, Category, SubCategory, Available, Reserved) VALUES ");

            int end = Math.Min(batch + batchSize, totalRecords);
            for (int i = batch; i < end; i++)
            {
                if (i > batch) sb.Append(',');
                var mfr = manufacturers[rng.Next(manufacturers.Length)];
                var catIdx = rng.Next(categories.Length);
                var cat = categories[catIdx];
                var subCat = subCategories[catIdx][rng.Next(subCategories[catIdx].Length)];
                var modelName = $"{mfr}-{cat[..3]}-{i + 1:D7}";
                var avail = rng.Next(1, 20);
                sb.Append($"('{modelName}','{mfr}','{cat}','{subCat}',{avail},0)");
            }

            cmd.CommandText = sb.ToString();
            await cmd.ExecuteNonQueryAsync();
        }

        tx.Commit();
    }
}
