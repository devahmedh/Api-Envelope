using Microsoft.EntityFrameworkCore;

namespace TheSpectre.ApiEnvelope.EntityFrameworkCore.Tests;

public sealed class Row
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class PagingTestContext : DbContext
{
    public PagingTestContext(DbContextOptions<PagingTestContext> options)
        : base(options)
    {
    }

    public DbSet<Row> Rows => Set<Row>();
}
