# CasCap.Common.Configuration.Management

Optional .NET 10 ASP.NET Core controllers for authenticated management of one registered typed
runtime configuration destination. This source project references Runtime and browser-neutral
Contracts, not a PostgreSQL/SQLite provider or Redis.

## Configuration Examples

After registering the existing runtime authority and host authentication, opt in with
[`AddRuntimeConfigurationManagement<T>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Configuration.Management/Extensions/ServiceCollectionExtensions.cs):

```csharp
using CasCap.Common.Configuration.Management.Extensions;

builder.Services.AddRuntimeConfigurationManagement<BrowserControls>();
```

`BrowserControls` represents the host's browser-safe registered runtime DTO, not its private
application configuration root. Define all three policies from
`RuntimeConfigurationManagementPolicies` in Contracts. Every operation requires `View`;
preview also requires `Edit`, and publication requires both `Edit` and `Publish`.
Policies receive the configured `RuntimeConfigurationDestination` as their authorization resource.
Missing policies deny access. The module does not configure authentication schemes, alter host
authentication defaults, create permissive policies or provide a Development bypass.
Policy-specific authentication schemes are honored without changing existing machine-auth defaults.

With the host's authentication configured, map the registered controller:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

One closed generic controller is registered directly; there is no assembly/plugin discovery
mechanism for selecting other contracts. A second management registration fails. The application,
environment and contract in every route must match the runtime registration exactly.

## Behavior

[`IRuntimeConfigurationManagement<T>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Configuration.Management/Abstractions/IRuntimeConfigurationManagement%7BT%7D.cs)
uses the uncached publisher for authoritative reads and single-attempt optimistic publication.
The existing runtime codec validates and copies candidates before publication. Audit actor comes
only from an authenticated `NameIdentifier` or `sub` claim; bodies cannot set it. Ordinary responses
omit actor, reason, storage details and accepted-reader internals.

All POST operations, including JSON preview, require a principal-bound antiforgery token and its
paired cookie. Read/token endpoints require view permission. Responses are `no-store`;
authentication/authorization failures are problem responses, not sign-in redirects.
Request JSON is bounded and strict only for this controller, preserving host DTO converters and
unrelated serializers. The host must provide matching converters through DTO attributes or MVC
JSON configuration; numeric enum defaults are not globally changed.

Publication returns durable commit with activation pending. Cancellation, authority failure or a
lost response must not trigger blind retry. Reload and make an explicit decision after a conflict.
The existing authorities' EF concurrency exception is recognized by its exact type identity, keeping
EF out of this module's references; other failures return sanitized 503 and log only exception type.

See the [wire contract](../../docs/runtime-configuration-management.md) and
[acceptance tests](../CasCap.Common.Configuration.Runtime.Tests/README.md).
History, restore, promotion, idempotent retry, schema provisioning and a Blazor editor are not
implemented by this module.
