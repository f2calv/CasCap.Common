# ADR 0002: AOT-Safe Runtime-Control Discovery and Reload-Aware Consumption

**Status:** Proposed draft; deferred pending a concrete second runtime control.
**Date:** 2026-11-03.
**Scope:** Explicit reloadability contracts, an administration catalogue, build-time consumption
checks and shared background-service change handling.

## Context and Problem

[ADR 0001](adr-0001-runtime-configuration.md) completed the bounded runtime-configuration PoC,
including a typed logging editor and user-confirmed activation and expiry. This proposal extends
discoverability and consumption discipline; it does not supersede that decision or reopen its
completed delivery scope.

[IAppConfig](../src/CasCap.Common.Abstractions/Abstractions/_IAppConfig.cs) identifies configuration
DTOs and, on modern targets, supplies a static section name.
[Configuration registration](../src/CasCap.Common.Configuration/Extensions/ConfigurationServiceCollectionExtensions.cs)
currently binds validated options and also exposes eager instance-binding helpers. Neither DTO
identification nor successful binding proves that a running consumer observes later changes.

The deployment direction supplied for this proposal is AOT compilation across applications.
Treat server-side Native AOT/trimming compatibility as a design constraint; verify browser
WebAssembly AOT separately where applicable. Existing provider, host and UI dependencies have
not been certified AOT-compatible by the PoC. In particular, successful JIT tests and an AOT-safe
catalogue would not establish compatibility of the complete persistence/API stack.

The background-service abstraction suggested for shared change handling was described as
`IBgService`. In this checkout the corresponding launch contract is
[IBgFeature](../src/CasCap.Common.Abstractions/Abstractions/IBgFeature.cs), supervised by
[FeatureFlagBgService](../src/CasCap.Common.Services/Services/FeatureFlagBgService.cs).
It currently selects features at startup using `IOptions<FeatureFlagConfig>`.
This draft proposes integration with that lifecycle, not a new interface rename or automatic
live feature start/stop.

## Goals and Constraints

- Discover only explicitly registered, strongly typed operational controls.
- Keep immutable deployment configuration as the default. Ordinary `IAppConfig` DTOs need not
  become reloadable or change their options-consumption model.
- Avoid runtime assembly scanning, arbitrary type activation and an unbounded reflection-based
  form engine. Prefer explicit concrete registrations and, if justified, compile-time generation.
- Separate permission to reload from permission to disclose or edit through a browser.
- Enforce permitted mutations on the server, not just through generated UI visibility.
- Reuse ADR 0001's SQL authority, fixed destination, validation, optimistic concurrency, audit,
  expiry and last-known-good behavior. No direct SQL key/value editor or new store is proposed.
- Preserve host-owned authentication, background fault supervision and shutdown behavior.

## Options Considered

| Option | Benefits | Costs and risks | Assessment |
| --- | --- | --- | --- |
| Keep explicit typed forms and consumption code | Proven PoC surface; no new tooling | Repeated registration and lifecycle work as controls grow | Default until a second real control exists |
| Proposed `IAppConfigReloadable : IAppConfig` | Clear whole-contract opt-in; straightforward diagnostics | Requires separating startup-only and live DTOs | Preferred starting contract |
| Proposed `[Reloadable]` property attribute | Precise allowlist for mixed DTOs | Nested paths, partial edits and mixed consumption semantics complicate enforcement | Retain as an alternative, not a second mandatory mechanism |
| Runtime reflection over all configuration types | Little initial registration work | Trimming metadata, disclosure and discovery risks; does not prove consumer behavior | Not the default |
| Explicit or generated typed catalogue | Bounded types and metadata; suited to AOT | Registration effort or generator ownership | Preferred discovery direction |

## Proposed Decision

All names and APIs in this section are illustrative and unimplemented. This is a draft
recommendation, not acceptance of an analyzer, source generator or generic UI implementation.

### 1. Opt In at a Coherent Typed Boundary

Start with small whole-contract reloadable DTOs implementing the proposed marker interface.
Separate credentials, endpoints, service topology and other startup-only settings from live
operational controls wherever practical. The marker declares intended consumption semantics;
it does not itself enable persistence, change notification or browser access.

If property-level attributes prove necessary, first define nesting, collections, inheritance,
unknown fields and cross-property invariants. Reject non-permitted changes server-side,
including values submitted outside the generated UI. Validate the complete resulting snapshot
and publish it atomically; do not reduce the authority to independent string assignments.

### 2. Use an AOT-Safe Registered Catalogue

Initially use explicit concrete typed registrations and descriptors. Consider a source generator
only when repeated registrations or forms justify its maintenance cost. Attributes, if adopted,
can be inputs to compile-time analysis rather than runtime property searches.

Each registered control needs a stable contract identity/version, section and fixed scope,
typed binding/serialization/validation, permitted fields, safe presentation metadata and host
authorization policies. A renamed CLR property must not silently rename a durable contract.
Named options, if supported, need explicit identity rather than accidental default-name behavior.

Return only authorized browser-safe descriptors and values. Reloadability alone must never expose
secrets or unrestricted appsettings/environment contents. The same catalogue must constrain API
publication, not merely determine which inputs the UI renders.

Binding, JSON serialization, validation and typed field access must all survive trimming and AOT.
Evaluate generated binding, generated validation and `System.Text.Json` source-generated metadata
at the actual concrete call sites. Do not assume framework generators cover existing generic
library wrappers or every DTO shape. Rooted reflection may be evaluated for a bounded unavoidable
case, with explicit metadata and published-binary evidence; AOT does not ban all reflection.

This catalogue must fit or explicitly revise ADR 0001's one-contract-per-reader-host constraint.
A multi-section display is not permission to add arbitrary scopes or independent publication
streams. Prefer grouping related controls under one registered contract initially.

### 3. Check Reload-Aware Consumption

A proposed Roslyn analyzer should target opted-in reloadable contracts, not all `IAppConfig` types.
Start with symbol-based diagnostics for `IOptions<T>` consumption, eager-binding helper use and
obvious long-lived captures of monitor values. Define diagnostics and narrowly justified exceptions
before enabling them across consumers.

Prefer `IOptionsMonitor<T>` for live consumers. Reading `CurrentValue` once in a constructor still
captures a stale object. Consumers must read a consistent snapshot per operation or apply changes
through a lifecycle-aware adapter. `IOptionsSnapshot<T>` provides scoped refresh, not continuously
updated singleton state; its eligibility needs an explicit decision.

Static analysis cannot generally prove that every resource or downstream object is reconfigured.
Raw `IConfiguration`, custom wrappers, indirect captures and third-party consumers need explicit
review and behavioral tests. Do not claim a clean analyzer result proves successful activation.

### 4. Share Background Change-Handling Infrastructure

Explore a typed helper/adapter composed into the existing background-feature lifecycle, rather than
placing stateful orchestration in a marker interface or forcing all features to subscribe.
Direct monitor reads remain sufficient where no resource reconfiguration is needed.

For consumers that need callbacks, the shared mechanism should own:

- Subscription lifetime, initial application and reconciliation of changes during startup.
- Short synchronous `OnChange` callbacks feeding owned serialized asynchronous work; no
  `async void` handlers, overlapping resource swaps or unobserved fire-and-forget tasks.
- Explicit revision/generation ordering, with safe coalescing only for replaceable state controls.
  An options callback alone does not carry the durable publication revision.
- Cancellation, subscription disposal and bounded draining during feature/host shutdown.
- Visible application failures and host-appropriate supervision; no success-shaped fallback.

The feature supplies the application-specific apply operation and resource safety policy.
Keep old resources active until a replacement is ready where feasible; define what happens when
an apply operation partially changes external state. Shared code cannot promise rollback for
arbitrary callbacks.

Expiry and removal of an override must use the same path to restore the effective baseline.
Feature selection remains startup-only unless a separate requirement defines safe live start,
stop, leadership handoff and draining semantics.

### 5. Separate Publication from Application

SQL remains authoritative for accepted snapshots. The management API validates permissions,
allowed fields and the whole candidate before expected-revision publication.
One process failing to apply an accepted revision does not undo its durable acceptance.

Preserve the distinction between committed, pending application, applied and failed application.
Do not imply fleet-wide convergence from one callback or status response. Any new status protocol
must specify per-instance identity, restart behavior and failure visibility before implementation.
Keep audit and diagnostics free of values and secrets.

## Consequences

The proposal makes reloadable intent discoverable without abandoning immutable deployment
defaults. Explicit/generated descriptors can drive a reusable UI while preserving typed server
contracts and AOT boundaries.

The cost is additional contract/versioning discipline, optional build tooling and lifecycle
infrastructure. A marker or attribute cannot make a database pool, scheduler or HTTP client safe
to rebuild. Mixed DTOs, arbitrary callback rollback and automatic fleet acknowledgement are
deliberately not solved by metadata.

## Validation and Delivery

No implementation, analyzer execution, AOT publication or new tests are claimed by this draft.
Reopen delivery only for a named second operational control:

1. Select the real DTO, consumer, application behavior and failure/expiry policy; settle the
   marker-versus-property boundary and its relationship to the existing single-contract model.
2. Prototype explicit concrete registration and a typed lifecycle adapter without building a
   general form engine or changing unrelated background features.
3. Test publication through the real provider to observable consumer behavior: activation,
   expiry, invalid candidates, stale revisions, rapid updates, startup races, apply failures
   and shutdown. Assert relevant resource identity/cache preservation rather than DTO values alone.
4. Test server rejection of unregistered/non-editable fields and unauthorized discovery/mutations.
   Verify neither browser responses nor audit/diagnostics disclose excluded values.
5. If an analyzer is justified, cover positive/negative diagnostics, immutable DTO exemptions,
   aliases/generic wrappers, named options and stale-capture limits with compile-time fixtures.
6. Publish and execute a representative trimmed Native AOT host for each supported deployment RID,
   exercising registration, binding, validation, serialization, persistence and change application.
   Review trimming/AOT warnings without blanket suppressions. Verify browser AOT separately if used.
7. Add approved automated checks to default CI when implemented. An ordinary build, generator output
   or JIT `WebApplicationFactory` test does not substitute for running the published AOT binary.

Package release, broad AOT migration and existing-consumer conversion remain separately approved.

## Open Questions and Reconsideration

- What second concrete control warrants reopening this work?
- Is whole-contract opt-in sufficient, or is a demonstrated mixed DTO worth property-level rules?
- Should descriptors remain handwritten, or do real repetitions justify generation?
- Should asynchronous apply helpers be feature-owned or hosted centrally, and who owns failure
  readiness versus process termination for each control?
- Which supported provider/API dependencies need changes before an end-to-end AOT host can run?
- What contract-version, named-options and per-instance status behavior is actually required?

## Evidence and Related Decisions

- [ADR 0001](adr-0001-runtime-configuration.md): completed PoC and preserved authority/security model.
- [IAppConfig](../src/CasCap.Common.Abstractions/Abstractions/_IAppConfig.cs) and
  [configuration helpers](../src/CasCap.Common.Configuration/Extensions/ConfigurationServiceCollectionExtensions.cs):
  current identity, binding and eager-read surfaces.
- [IBgFeature](../src/CasCap.Common.Abstractions/Abstractions/IBgFeature.cs),
  [IBgFeatureHost](../src/CasCap.Common.Abstractions/Abstractions/IBgFeatureHost.cs) and
  [feature supervisor](../src/CasCap.Common.Services/Services/FeatureFlagBgService.cs):
  current lifecycle integration points.
- [Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/),
  [configuration binding generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-generator)
  and [options semantics](https://learn.microsoft.com/en-us/dotnet/core/extensions/options):
  platform constraints to recheck against the selected implementation SDK and dependencies.
