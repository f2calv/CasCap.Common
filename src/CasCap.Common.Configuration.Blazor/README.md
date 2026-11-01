# CasCap.Common.Configuration.Blazor

Optional .NET 10 Razor class library for embedding typed runtime-control editing in an existing
Blazor frontend. It calls the authorized management API; it references only browser-neutral
Contracts and Blazor packages, never Runtime, EF Core, SQL/Redis clients or server configuration.
The source is unpublished; reference the project until a package release is approved.

## Usage

Register [`RuntimeConfigurationClient<T>`](Services/RuntimeConfigurationClient{T}.cs)
in the existing browser DI container. Reuse its same-origin authenticated `HttpClient`:

```csharp
builder.Services.AddScoped<RuntimeConfigurationClient<BrowserControls>>();
```

The host provides its browser-safe DTO from a shared contracts assembly and its existing
authentication, navigation and render mode. Interactive WebAssembly is the simplest cookie-based
integration: the browser retains the HttpOnly cookies and sends same-origin requests.
The RCL imposes no render mode. An Interactive Server host must explicitly provide a per-user
authenticated HTTP client and paired-cookie handling; server-side `HttpClient` does not
automatically inherit browser authentication. Never use an application-wide privileged client
for every browser user.

Mount [`RuntimeConfigurationEditor<T>`](Components/RuntimeConfigurationEditor.razor)
inside an existing interactive page, with typed fields rather than a key/string-value catalogue:

```razor
@using CasCap.Common.Configuration.Blazor.Components
@using CasCap.Common.Configuration.Management.Contracts.Models

<RuntimeConfigurationEditor T="BrowserControls" Destination="_destination">
    <Fields Context="value">
        <label for="diagnostic-verbosity">Verbosity</label>
        <InputNumber id="diagnostic-verbosity" @bind-Value="value.Verbosity" min="0" max="5" />
        <label for="diagnostic-feature">Feature enabled</label>
        <InputCheckbox id="diagnostic-feature" @bind-Value="value.FeatureEnabled" />
        <label for="diagnostic-window">Diagnostic window</label>
        <InputDuration id="diagnostic-window" @bind-Value="value.Window" />
    </Fields>
</RuntimeConfigurationEditor>

@code {
    private readonly RuntimeConfigurationDestination _destination =
        new("sample-app", "staging", "controls");
}
```

The page imports the host's DTO and `Microsoft.AspNetCore.Components.Forms`.
Use an ordinary forms-compatible mutable DTO or record for the independent editing copy, with
DataAnnotations; no accepted monitor object is mutated. Enum fields use typed `InputSelect<TEnum>`,
numbers use `InputNumber<TNumber>`, and [`InputDuration`](Components/InputDuration.razor.cs)
preserves `TimeSpan` with invariant `hours:minutes:seconds` input. The host chooses bounds and
DTO enum converters shared with the server. Supply unique visible labels/IDs in every field
fragment. The page owns its `h1`, routing, sign-in and navigation chrome.

Include the host's normal generated `.styles.css` bundle so the RCL's isolated styles load.
No JavaScript plugin, assembly scanner, external font or extra component framework is needed.

## Configuration Examples

The destination is immutable host bootstrap, not a user-selected arbitrary database/environment.
It must exactly match the server's registered scope. Server policies remain authoritative, even
when the UI hides a field or operation.

```razor
<RuntimeConfigurationEditor T="BrowserControls"
                            Destination="_destination"
                            Title="Operational controls"
                            OnCommitted="DocumentCommittedAsync">
    <Fields Context="value">
        <label for="operational-verbosity">Verbosity</label>
        <InputNumber id="operational-verbosity" @bind-Value="value.Verbosity" />
    </Fields>
</RuntimeConfigurationEditor>
```

`DocumentCommittedAsync` receives a `RuntimeConfigurationDocument<BrowserControls>` only after
durable commit; it is not an applied/replica acknowledgement. If the host customizes MVC JSON
naming/converters, construct the client with matching `JsonSerializerOptions` explicitly.
Do not globally register browser serializer settings that break other typed clients.

## Editing and Failure Behavior

- Read the authoritative durable document, including its revision and any expiry. It is not the
  runtime reader's effective state; an expired durable head can differ from the startup baseline.
- Preview validates the typed candidate without publishing. Editing a previewed control invalidates
  that preview. Publication requires a reason and explicit destination confirmation.
- Optional temporary lifetime is 1-1440 minutes; absence publishes without expiry.
- Every POST obtains a new principal-bound antiforgery token using the host's issued header name.
  Tokens remain local to the request, never browser storage or logs.
- Publication uses exact .NET `long` revision serialization, not a JavaScript number conversion.
  Committed replies explicitly leave activation pending.
- Validation, sign-in, permission, missing-document, conflict, unavailable and malformed-response
  states are distinct. Failed publication preserves entered values and never retries automatically.
  After an unavailable/uncertain result, investigate or reload before another explicit decision.
- Reloading dirty values and leaving a dirty page require explicit discard confirmation. Requests
  cancel on destination changes/disposal; the editor does not reuse an old scope's form.
- Native labeled inputs, form submission and buttons support keyboard operation; isolated styles
  allow controls/actions to wrap. The host still needs its own browser/accessibility verification.

See the [server setup](../CasCap.Common.Configuration.Management/README.md),
[wire contract](../../docs/runtime-configuration-management.md), [consumer guide](../../docs/runtime-configuration.md)
and [component tests](../CasCap.Common.Configuration.Blazor.Tests/README.md).
History browsing, restore, promotion, arbitrary scope discovery and live activation dashboards
are deliberately not part of this editor.
