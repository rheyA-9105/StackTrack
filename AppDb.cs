using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StockTrack.Api.Models;

namespace StockTrack.Api.Data;

// SQLite drops DateTime "Kind" — this makes every date read back as UTC
// so JSON ends with "Z" and the browser converts to local time correctly.
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
}

// Replaces localStorage ("stocktrack_db_v1") with a real SQLite database.
public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<StockTransaction> Transactions => Set<StockTransaction>();
    public DbSet<ActivityLog> Activity => Set<ActivityLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder cb) =>
        cb.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();
        b.Entity<Category>().HasIndex(c => c.Name).IsUnique();
    }
}
