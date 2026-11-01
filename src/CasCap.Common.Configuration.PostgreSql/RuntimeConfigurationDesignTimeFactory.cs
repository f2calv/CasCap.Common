using Microsoft.EntityFrameworkCore.Design;

namespace CasCap.Common.Configuration.PostgreSql;

/// <summary>Creates an offline migration model without initializing an application or reading secrets.</summary>
public sealed class RuntimeConfigurationDesignTimeFactory : IDesignTimeDbContextFactory<RuntimeConfigurationDbContext>
{
    /// <inheritdoc/>
    public RuntimeConfigurationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<RuntimeConfigurationDbContext>()
            .UseNpgsql("Host=example.invalid;Database=configuration;Username=placeholder",
                provider => provider.MigrationsHistoryTable("__ef_migrations_history", RuntimeConfigurationStorage.Schema))
            .Options);
}
