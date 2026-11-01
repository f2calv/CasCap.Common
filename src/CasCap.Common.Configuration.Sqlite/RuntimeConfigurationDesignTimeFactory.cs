using Microsoft.EntityFrameworkCore.Design;

namespace CasCap.Common.Configuration.Sqlite;

/// <summary>Builds an offline model without starting a host or opening a database.</summary>
public sealed class RuntimeConfigurationDesignTimeFactory : IDesignTimeDbContextFactory<RuntimeConfigurationDbContext>
{
    /// <inheritdoc/>
    public RuntimeConfigurationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<RuntimeConfigurationDbContext>()
            .UseSqlite("Data Source=configuration-design.db",
                provider => provider.MigrationsHistoryTable(RuntimeConfigurationStorage.HistoryTable))
            .Options);
}
