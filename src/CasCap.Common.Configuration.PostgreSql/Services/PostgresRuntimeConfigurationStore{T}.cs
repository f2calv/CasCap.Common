using CasCap.Common.Configuration.PostgreSql.Entities;
using CasCap.Common.Configuration.Runtime.Abstractions;
using CasCap.Common.Configuration.Runtime.Services;
using Npgsql;

namespace CasCap.Common.Configuration.PostgreSql.Services;

/// <summary>Reads a scoped head and journal atomically and publishes complete typed revisions.</summary>
public sealed class PostgresRuntimeConfigurationStore<T>(
    TimeProvider timeProvider,
    RuntimeConfigurationOptions settings,
    IDbContextFactory<RuntimeConfigurationDbContext> dbContextFactory,
    RuntimeConfigurationCodec<T> codec
) : IRuntimeConfigurationPublisher<T> where T : class
{
    /// <inheritdoc/>
    public async Task<RuntimeConfigurationRevision<T>?> ReadAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await (from head in db.Set<RuntimeHeadEntity>().AsNoTracking()
                         join revision in db.Set<RuntimeRevisionEntity>().AsNoTracking()
                         on new { head.Application, head.Environment, head.ContractId, head.Revision }
                         equals new { revision.Application, revision.Environment, revision.ContractId, revision.Revision }
                         where head.Application == settings.Scope.Application
                             && head.Environment == settings.Scope.Environment
                             && head.ContractId == settings.ContractId
                         select revision).SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : Convert(row);
    }

    /// <summary>Appends an audited revision using an expected head; a conflict never overwrites another writer.</summary>
    public async Task<RuntimeConfigurationRevision<T>> PublishAsync(
        T value,
        long expectedRevision,
        string actor,
        string reason,
        DateTimeOffset? expiresAtUtc = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (actor.Length > 200 || reason.Length > 500 || expectedRevision < 0
            || expiresAtUtc <= timeProvider.GetUtcNow())
            throw new ArgumentException("Publication metadata is outside supported bounds.");
        // Npgsql rejects non-zero offsets for timestamp with time zone columns.
        expiresAtUtc = expiresAtUtc?.ToUniversalTime();
        var json = codec.Serialize(value);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var head = await db.Set<RuntimeHeadEntity>().SingleOrDefaultAsync(row =>
            row.Application == settings.Scope.Application && row.Environment == settings.Scope.Environment
            && row.ContractId == settings.ContractId, cancellationToken);
        if ((head?.Revision ?? 0) != expectedRevision)
            throw new DbUpdateConcurrencyException("The current configuration revision changed.");
        var row = new RuntimeRevisionEntity
        {
            Application = settings.Scope.Application,
            Environment = settings.Scope.Environment,
            ContractId = settings.ContractId,
            Revision = checked(expectedRevision + 1),
            ContractVersion = settings.ContractVersion,
            Payload = json,
            RecordedAtUtc = timeProvider.GetUtcNow(),
            Actor = actor,
            Reason = reason,
            ExpiresAtUtc = expiresAtUtc,
        };
        db.Add(row);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (head is null)
                db.Add(new RuntimeHeadEntity
                {
                    Application = row.Application,
                    Environment = row.Environment,
                    ContractId = row.ContractId,
                    Revision = row.Revision,
                });
            else
                head.Revision = row.Revision;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "pk_revisions" or "pk_heads" })
        {
            throw new DbUpdateConcurrencyException("Another publisher committed this configuration revision.");
        }
        await transaction.CommitAsync(cancellationToken);
        return Convert(row);
    }

    /// <summary>Reads an immutable historical revision without changing active state.</summary>
    public async Task<RuntimeConfigurationRevision<T>?> ReadRevisionAsync(long revision, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.Set<RuntimeRevisionEntity>().AsNoTracking().SingleOrDefaultAsync(row =>
            row.Application == settings.Scope.Application && row.Environment == settings.Scope.Environment
            && row.ContractId == settings.ContractId && row.Revision == revision, cancellationToken);
        return row is null ? null : Convert(row);
    }

    private RuntimeConfigurationRevision<T> Convert(RuntimeRevisionEntity row)
    {
        if (row.ContractVersion != settings.ContractVersion)
            throw new InvalidOperationException("The published DTO contract version is not supported.");
        return new RuntimeConfigurationRevision<T>
        {
            Revision = row.Revision,
            Value = codec.Deserialize(row.Payload),
            RecordedAtUtc = row.RecordedAtUtc,
            Actor = row.Actor,
            Reason = row.Reason,
            ExpiresAtUtc = row.ExpiresAtUtc,
            RestoredFromRevision = row.RestoredFromRevision,
        };
    }
}
