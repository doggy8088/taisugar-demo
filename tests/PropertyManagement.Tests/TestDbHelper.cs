using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Data;

namespace PropertyManagement.Tests;

public sealed class TestDbScope : IDisposable
{
    public SqliteConnection Connection { get; }
    public AppDbContext Context { get; }

    public TestDbScope()
    {
        Connection = new SqliteConnection("Data Source=:memory:");
        Connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(Connection)
            .Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        Connection.Dispose();
    }
}
