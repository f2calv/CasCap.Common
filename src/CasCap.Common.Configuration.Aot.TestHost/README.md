# CasCap.Common.Configuration.Aot.TestHost

Non-packable .NET 11 console fixture for published JIT/Native AOT parity against the direct
PostgreSQL and SQLite configuration adapters. It is not a production host or a performance benchmark.

## Running

From the repository root:

```powershell
docker compose --profile configuration-aot-tests run --rm configuration-aot-tests
```

The [existing test suite](../CasCap.Common.Configuration.Runtime.Tests/README.md) launches each
published binary and requires successful migration, exercise and fresh-process verification.
Each process checks its actual dynamic-code capability. The AOT executable has no migration branch;
the managed executable applies migrations before either deployment mode is exercised.
JSON reflection fallback is disabled in both builds.

## Configuration Examples

The parent fixture passes `ConfigurationAot__Connection` only through the child process environment.
Use only a fixture-owned database/file; do not invoke this host against application data.
Arguments are `<postgres|sqlite> <jit|aot> <migrate|exercise|verify|dynamic-filter|dynamic-aggregate>`.
`exercise` writes synthetic scoped publications; `verify` reopens their durable state.
Any unmet assertion throws and produces a nonzero process exit.

`dynamic-filter` and `dynamic-aggregate` query the JIT-seeded scope using the same runtime-composed
EF expression in both modes. JIT must return the expected rows/aggregate; native must produce
EF's exact `QueryNotPrecompiled` error. They emit only fixed verification markers, never row data
or connection details. See [the query code](Services/DynamicQueryProbe.cs) and the
[limitations explained](../../docs/runtime-configuration.md#examples-of-queries-that-require-runtime-compilation).

The [Compose profile](../../docker-compose.yml) owns the build environment and output locations.
Its per-run build/publish workspace is removed on shell exit; the NuGet cache is retained.
See the [experimental AOT guide](../../docs/runtime-configuration.md#experimental-native-aot)
for generated DTO metadata/validation, EF opt-in, visible upstream warnings and distribution limits.
Validation covers Linux amd64 only and uses the SDK container, not a hardened production image.
