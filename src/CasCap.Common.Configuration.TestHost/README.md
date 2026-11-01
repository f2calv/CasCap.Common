# CasCap.Common.Configuration.TestHost

Permanent .NET 10 ASP.NET Core test application for the runtime configuration packages.
It uses modern `WebApplication.CreateBuilder` composition in
[AppHost](AppHost.cs), with a thin [entrypoint](Program.cs) and public partial `Program` marker
for `WebApplicationFactory`.

## Usage

Run it through the [acceptance tests](../CasCap.Common.Configuration.Runtime.Tests/README.md),
which inject an isolated PostgreSQL connection or SQLite file and arbitrary environment before host composition.
The test fixture applies migrations and publishes the required initial revision before startup;
the application never creates its own schema or initial publication.

`GET /observe` exposes the current typed controls and a singleton cache sentinel so tests can
verify reload without host replacement. This is an unauthenticated test-only endpoint; do not
deploy this host as an application or expose it as a management surface.

## Configuration Examples

A standalone run requires an already migrated, populated **test** database:

```json
{
  "ConnectionStrings": {
    "RuntimeConfiguration": "Host=localhost;Port=5432;Database=example_test;Username=configuration_tests;Password=local_test_password"
  },
  "RuntimeConfiguration": {
    "Environment": "example-environment"
  }
}
```

The application scope is `test-application`, the contract is `controls`, and the baseline is
`RuntimeControls`. Missing settings or an absent initial durable revision fail startup.

`RuntimeConfiguration:Backend` defaults to `PostgreSql`; set it to `Sqlite` with a migrated file
connection such as `Data Source=configuration.db;Pooling=False`. Unsupported backend names fail
composition. Both paths use the same host and standard monitor. Tests can decorate reads with
the shared Redis cache registration; the authoritative publisher remains uncached.

`RuntimeConfiguration:ManagementEnabled` defaults to false. The acceptance fixture can explicitly
enable the typed management controller, install real test-only cookie authentication/policies and
exercise the [management wire contract](../../docs/runtime-configuration-management.md).
The module itself supplies no login endpoint or anonymous bypass; test sign-in middleware lives
only in the test assembly. Shared test Data Protection keys are fixture-owned files deleted at
cleanup. The unauthenticated observation/feature probes remain unchanged.
