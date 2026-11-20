using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using PgContext = CasCap.Common.Configuration.PostgreSql.RuntimeConfigurationDbContext;
using PgRevision = CasCap.Common.Configuration.PostgreSql.Entities.RuntimeRevisionEntity;
using SqliteContext = CasCap.Common.Configuration.Sqlite.RuntimeConfigurationDbContext;
using SqliteRevision = CasCap.Common.Configuration.Sqlite.Entities.RuntimeRevisionEntity;

namespace CasCap.Common.Configuration.Aot.TestHost.Services;

/// <summary>Demonstrates runtime query shapes which cannot use the adapters' build-time interceptors.</summary>
internal static class DynamicQueryProbe
{
    public static async Task RunAsync(
        IServiceProvider services,
        string backend,
        string mode,
        string scenario,
        CancellationToken token
    )
    {
        if (backend == "postgres")
        {
            await using var db = await services.GetRequiredService<IDbContextFactory<PgContext>>().CreateDbContextAsync(token);
            var revisions = db.Set<PgRevision>()
                .Where(row => row.Application == "published-test" && row.Environment == "jit" && row.ContractId == "controls")
                .Select(row => row.Revision);
            await AssertQueryAsync(revisions, mode, scenario, token);
        }
        else
        {
            await using var db = await services.GetRequiredService<IDbContextFactory<SqliteContext>>().CreateDbContextAsync(token);
            var revisions = db.Set<SqliteRevision>()
                .Where(row => row.Application == "published-test" && row.Environment == "jit" && row.ContractId == "controls")
                .Select(row => row.Revision);
            await AssertQueryAsync(revisions, mode, scenario, token);
        }
    }

    private static async Task AssertQueryAsync(IQueryable<long> revisions, string mode, string scenario, CancellationToken token)
    {
        // The predicate is assembled at runtime, as in a user-defined search/filter builder.
        var row = Expression.Parameter(typeof(long), "revision");
        long minimum = 3;
        Expression<Func<long>> threshold = () => minimum;
        var predicate = Expression.Lambda<Func<long, bool>>(
            Expression.GreaterThanOrEqual(row, threshold.Body), row);
        var query = revisions.Where(predicate);
        if (scenario == "dynamic-aggregate")
            query = query.Join(revisions, left => left, right => right, (left, right) => left)
                .GroupBy(value => 1).Select(group => group.Sum()).Where(total => total > 10);
        else
            query = query.OrderBy(value => value).Take(3);

        try
        {
            var actual = await query.ToListAsync(token);
            long[] expected = scenario == "dynamic-aggregate" ? [12] : [3, 4, 5];
            if (mode != "jit" || !actual.SequenceEqual(expected))
                throw new InvalidOperationException("Dynamic EF probe unexpectedly succeeded or returned incorrect rows.");
            Console.WriteLine("JIT_QUERY_RESULT_VERIFIED");
        }
        catch (InvalidOperationException exception) when (mode == "aot" && exception.Message == CoreStrings.QueryNotPrecompiled)
        {
            Console.WriteLine("AOT_QUERY_NOT_PRECOMPILED_VERIFIED");
        }
    }
}
