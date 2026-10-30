# Runtime Configuration Management Wire Contract

The optional `CasCap.Common.Configuration.Management` server registers one closed generic
`RuntimeConfigurationManagementController<T>` via `AddRuntimeConfigurationManagement<T>()`.
Its `T` is exactly the registered runtime DTO. A browser references only
`CasCap.Common.Configuration.Management.Contracts`; all records below are in its `.Models`
namespace, and route/policy constants are in `.Constants`.

## Fixed Destination and Permissions

The base route is `/api/runtime-configuration/{application}/{environment}/{contract}`.
URI-escape each segment of the host-provided destination. All three segments must match
`RuntimeConfigurationOptions.Scope` and `ContractId` using ordinal comparison; this is not an
environment selector or a database selector. Another destination returns 404, never an alternate
store. There is intentionally one DTO/scope per reader host.

The host owns authentication and supplies the `RuntimeConfiguration.View`,
`RuntimeConfiguration.Edit` and `RuntimeConfiguration.Publish` policies
(`RuntimeConfigurationManagementPolicies.View/Edit/Publish`). All operations require View;
preview additionally requires Edit; publish additionally requires Edit and Publish.
Authorization handlers receive a `RuntimeConfigurationDestination` resource containing the exact
configured application, environment and contract. Missing policies fail closed. The module never
changes authentication defaults or introduces anonymous/Development authorization.
Named authentication schemes on host policies are evaluated explicitly, allowing browser cookies
alongside an unchanged machine-authentication default.

Browser-safe DTOs and authorized fields are the host's responsibility, including rejecting server-only
or unauthorized field changes through host validators/policies. A UI hiding an input is not a
security boundary. Do not bind a private application root, connection options or EF entity.
Publish requires a nonempty authenticated `ClaimTypes.NameIdentifier` (preferred) or `sub`, bounded
to 200 characters. An unusable identity yields 403; the body never supplies audit actor identity.

## Operations and Typed Responses

All successful operations return 200 JSON. These are command/preview operations, not a resource
creation API. Every module response, including errors and token responses, disables caching.

| Method and suffix | Request | Success response |
| --- | --- | --- |
| `GET` base route | None | `RuntimeConfigurationDocument<T>` |
| `GET /antiforgery` | None | `RuntimeConfigurationAntiforgeryResponse` |
| `POST /preview` | `PreviewRuntimeConfigurationRequest<T>` | `PreviewRuntimeConfigurationResponse<T>` |
| `POST /publish` | `PublishRuntimeConfigurationRequest<T>` | `PublishRuntimeConfigurationResponse<T>` |

`RuntimeConfigurationDocument<T>` has `Revision: long`, `Value: T`,
`RecordedAtUtc: DateTimeOffset`, and nullable `ExpiresAtUtc`. This is the current durable head,
including an expired head if one exists, not the monitor's effective baseline or activation state.
GET reads the authoritative publisher, never the optional cache. No actor, reason, private root,
connection, history metadata or accepted-reader revision is included.

Preview accepts `{ "value": { ... } }` and returns `{ "value": { ... } }`, a validated independent
copy. It does not publish, append audit, change the reader or increment revision.

Publication example using the default camel-case MVC JSON contract:

```json
{
  "value": {
    "verbosity": 4,
    "featureEnabled": true
  },
  "expectedRevision": 1,
  "reason": "Adjust diagnostic verbosity",
  "expiresAtUtc": null
}
```

`value`, `expectedRevision` and `reason` are required. Revision is an exact JSON integer from zero
through `long.MaxValue`, with no string, fractional or floating-point coercion. Zero denotes no
existing durable head; the reader's separate initial-publication startup requirement is unchanged.
Do not pass revisions through a JavaScript `Number` conversion; typed .NET `long` serialization
preserves values above the JavaScript exact-integer boundary. Reason is non-whitespace and at most
500 characters. Expiry is optional; when supplied it must be a future ISO 8601 timestamp.

A successful publication body is:

```json
{
  "document": {
    "revision": 2,
    "value": {
      "verbosity": 4,
      "featureEnabled": true
    },
    "recordedAtUtc": "2026-01-01T00:00:00+00:00",
    "expiresAtUtc": null
  },
  "committed": true,
  "activationPending": true
}
```

This confirms durable commit only. It does not acknowledge reader, local monitor or replica
activation. Clients must not turn this into an “applied everywhere” message.

The module clones host MVC JSON options for request parsing, then rejects unknown/duplicate
properties, wrong casing, string-number coercion, excessive depth and bodies above 98,304 UTF-8
bytes. The codec additionally bounds the persisted typed snapshot to 65,536 UTF-8 bytes and
validates DTO annotations, nested/undefined enums and registered options validators before a write.
Validation errors contain fixed safe field messages, never raw provider/validator exceptions.
Host MVC property naming and DTO converters are preserved for both requests and responses;
unrelated controller and HTTP serializers are not globally changed. Attribute-based string enum
converters also work at the runtime codec boundary. A converter configured only in MVC must still
produce a DTO the runtime codec can round-trip; no global string-enum convention is imposed on
existing runtime contracts.

## Browser Cookies and Antiforgery

1. Authenticate using the host's existing mechanism and retain its cookie.
2. GET the authorized `/antiforgery` endpoint on the same origin with browser credentials.
3. Retain the HttpOnly antiforgery cookie set by that response. Its JSON body contains `headerName`
   and `requestToken` (`RuntimeConfigurationAntiforgeryResponse`).
4. Send that header/token and both cookies on **every POST**, including JSON preview.
   The standard header name is `RequestVerificationToken`; always use the issued name instead of
   hardcoding it. The host may configure its antiforgery header.
5. After sign-in identity changes, discard the token and fetch a new one. Tokens cannot cross
   authenticated principals.

The production module has no test login endpoint or identity-header authentication. Tests install
real cookie authentication and a sign-in middleware only inside the test assembly. Bearer requests
do not bypass this module's POST antiforgery requirement. Use HTTPS and the host's secure cookie,
Data Protection and same-origin/CORS policies; do not enable credentialed cross-origin access as
a workaround. Multi-replica/replacement hosts must share appropriately protected Data Protection
keys and an application discriminator to accept the same browser cookie/token pair.

## Error Contract and Retry Boundary

Errors use standard `application/problem+json`. Validation problems include an `errors` object;
known fields are `request`, `value`, `expectedRevision`, `reason`, `expiresAtUtc` and `antiforgery`.
Clients must tolerate additional problem fields and display only safe server titles/field errors.

| Status | Meaning | Client action |
| --- | --- | --- |
| 400 | Malformed shape/metadata, invalid typed value or missing/bad/principal-mismatched antiforgery | Correct the form or refresh the token; no publication occurred |
| 401 | No authenticated principal | Use host sign-in; API does not redirect to an identity provider |
| 403 | Missing policy/permission or unusable publication actor | Do not offer the operation as authorized |
| 404 | Wrong fixed destination or no durable current document | Do not switch scope/database automatically |
| 409 | Expected durable revision changed or provider reports a writer conflict | Reload the authoritative document and let the operator decide |
| 503 | Authority/validation infrastructure unavailable; mutation outcome can be uncertain | Do not blindly retry; investigate/re-read after recovery |

Authority failures log only exception type, without exception objects, payloads or actor identifiers.
This module's sanitization does not replace host HTTP/provider logging controls: do not enable
request-body, sensitive-data or audit-identifier logging in the surrounding application.
The server never retries publication. Request cancellation is propagated to persistence; a
disconnect or uncertain result is not proof of rollback. There is no idempotency key protocol,
activation acknowledgement, history, restore, as-of, pagination or promotion endpoint.

## Typed Editor Integration Boundary

An eventual RCL should keep `T` in its loaded document, candidate, preview and publication responses.
Use explicit host-supplied `RenderFragment<T>` fields rather than a reflection form engine or
string dictionary. Keep an independent editable candidate, carry the authoritative `long` revision
unchanged, and show committed/activation-pending separately. Render permission hints are advisory;
server policies and validators remain authoritative. This phase implements the API/contracts only,
not the RCL, a typed HTTP client or a browser workflow.
