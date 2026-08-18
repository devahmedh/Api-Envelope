using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TheSpectre.ApiEnvelope.EntityFrameworkCore.Tests;

/// A SQLite in-memory database seeded with sequential rows.
public sealed class SqliteFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteFixture(int rowCount)
    {
        // The connection is held open for the fixture's whole lifetime on purpose: a SQLite
        // in-memory database exists only while a connection to it is open. Letting EF open
        // and close per operation would drop the schema and the data between calls.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PagingTestContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new PagingTestContext(options);
        Context.Database.EnsureCreated();

        Context.Rows.AddRange(
            Enumerable.Range(1, rowCount).Select(i => new Row { Id = i, Name = $"Row {i}" }));
        Context.SaveChanges();
    }

    public PagingTestContext Context { get; }

    /// Rows ordered by id. Paging an unordered query has no defined result.
    public IQueryable<Row> OrderedRows => Context.Rows.OrderBy(r => r.Id);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
