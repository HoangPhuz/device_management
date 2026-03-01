using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace App1.Data.Interfaces;

public interface ISqliteDataSource
{
    SqliteConnection GetConnection();
    Task InitializeAsync();
}
