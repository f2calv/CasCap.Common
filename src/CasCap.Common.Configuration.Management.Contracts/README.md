# CasCap.Common.Configuration.Management.Contracts

Browser-neutral .NET 10 request and response records for a host-owned typed runtime configuration
editor. This source project has no Runtime, database, ASP.NET Core or Blazor dependency.

## Usage

Reference this project from a client and its host. Keep the same `T` through the document,
preview and publication requests; a host-supplied typed form can edit that value without a
string dictionary or a reflection-based form engine.

The host is responsible for **browser-safe DTO design and client-authorized fields**. Never reuse
an application configuration root containing connections, secrets or server-only settings.
Hiding a field in a component does not authorize or protect it. The server validates the complete
registered DTO and enforces destination/operation policies independently of the browser.

## Public Surface

- [`RuntimeConfigurationDocument<T>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Configuration.Management.Contracts/Models/RuntimeConfigurationDocument%7BT%7D.cs)
  contains the authoritative revision, typed value, publication time and optional expiry only.
- [`PreviewRuntimeConfigurationRequest<T>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Configuration.Management.Contracts/Models/PreviewRuntimeConfigurationRequest%7BT%7D.cs)
  and `PreviewRuntimeConfigurationResponse<T>` carry a complete typed candidate.
- [`PublishRuntimeConfigurationRequest<T>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Configuration.Management.Contracts/Models/PublishRuntimeConfigurationRequest%7BT%7D.cs)
  requires `Value`, exact integral `ExpectedRevision` and `Reason`; expiry is optional.
  `PublishRuntimeConfigurationResponse<T>` confirms `Committed` and `ActivationPending`, not
  monitor/replica acknowledgement.
- [`RuntimeConfigurationAntiforgeryResponse`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Configuration.Management.Contracts/Models/RuntimeConfigurationAntiforgeryResponse.cs)
  provides the header name and principal-bound request token.
- [`RuntimeConfigurationDestination`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Configuration.Management.Contracts/Models/RuntimeConfigurationDestination.cs)
  identifies the fixed authorization resource. Route and policy constants live in
  `CasCap.Common.Configuration.Management.Contracts.Constants`.

All records live in `CasCap.Common.Configuration.Management.Contracts.Models`. HTTP errors use
standard problem-details JSON, not a dependency on server MVC types in this project.
See the [wire contract](../../docs/runtime-configuration-management.md) for exact routes,
authentication, antiforgery and error handling.
