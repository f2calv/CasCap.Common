#if CONFIGURATION_POSTGRESQL
using CasCap.Common.Configuration.PostgreSql.Entities;
#elif CONFIGURATION_SQLITE
using CasCap.Common.Configuration.Sqlite.Entities;
#else
#error Select the configuration database adapter when compiling shared EF source.
#endif
using CasCap.Common.Configuration.Runtime.Models;

#if CONFIGURATION_POSTGRESQL
namespace CasCap.Common.Configuration.PostgreSql.Services;
#elif CONFIGURATION_SQLITE
namespace CasCap.Common.Configuration.Sqlite.Services;
#endif

/// <summary>Owns complete, non-generic query sites for EF's experimental query precompiler.</summary>
internal static class RuntimeConfigurationQueries
{
    /// <summary>Reads the scoped head and its immutable revision in one statement.</summary>
    internal static Task<RuntimeRevisionEntity?> ReadAsync(
        RuntimeConfigurationDbContext db,
        RuntimeConfigurationOptions settings,
        CancellationToken token
    )
    {
        // EF 10's precompiler supports local context/parameter symbols, not method parameter symbols.
        var context = db;
        var cancellation = token;
        var application = settings.Scope.Application;
        var environment = settings.Scope.Environment;
        var contractId = settings.ContractId;
        return context.Set<RuntimeHeadEntity>().AsNoTracking()
        .Where(head => head.Application == application
            && head.Environment == environment && head.ContractId == contractId)
        .Join(context.Set<RuntimeRevisionEntity>().AsNoTracking(),
            head => new { head.Application, head.Environment, head.ContractId, head.Revision },
            revision => new { revision.Application, revision.Environment, revision.ContractId, revision.Revision },
            (head, revision) => new RuntimeRevisionEntity(
                revision.Application, revision.Environment, revision.ContractId, revision.Payload, revision.Actor, revision.Reason)
            {
                Revision = revision.Revision,
                ContractVersion = revision.ContractVersion,
                RecordedAtUtc = revision.RecordedAtUtc,
                ExpiresAtUtc = revision.ExpiresAtUtc,
                RestoredFromRevision = revision.RestoredFromRevision,
            })
        .SingleOrDefaultAsync(cancellation);
    }

    /// <summary>Reads a tracked head for expected-revision publication.</summary>
    internal static Task<RuntimeHeadEntity?> ReadHeadAsync(
        RuntimeConfigurationDbContext db,
        RuntimeConfigurationOptions settings,
        CancellationToken token
    )
    {
        var context = db;
        var cancellation = token;
        var application = settings.Scope.Application;
        var environment = settings.Scope.Environment;
        var contractId = settings.ContractId;
        return context.Set<RuntimeHeadEntity>().SingleOrDefaultAsync(row =>
        row.Application == application && row.Environment == environment
        && row.ContractId == contractId, cancellation);
    }

    /// <summary>Reads one immutable revision within its complete scope.</summary>
    internal static Task<RuntimeRevisionEntity?> ReadRevisionAsync(
        RuntimeConfigurationDbContext db,
        RuntimeConfigurationOptions settings,
        long revision,
        CancellationToken token
    )
    {
        var context = db;
        var cancellation = token;
        var application = settings.Scope.Application;
        var environment = settings.Scope.Environment;
        var contractId = settings.ContractId;
        var revisionId = revision;
        return context.Set<RuntimeRevisionEntity>().AsNoTracking().Where(row =>
        row.Application == application && row.Environment == environment
        && row.ContractId == contractId && row.Revision == revisionId)
        .Select(row => new RuntimeRevisionEntity(
            row.Application, row.Environment, row.ContractId, row.Payload, row.Actor, row.Reason)
        {
            Revision = row.Revision,
            ContractVersion = row.ContractVersion,
            RecordedAtUtc = row.RecordedAtUtc,
            ExpiresAtUtc = row.ExpiresAtUtc,
            RestoredFromRevision = row.RestoredFromRevision,
        }).SingleOrDefaultAsync(cancellation);
    }
}
