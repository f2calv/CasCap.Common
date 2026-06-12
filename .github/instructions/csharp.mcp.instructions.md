---
description: 'Model Context Protocol (MCP) server tool conventions — attributes, descriptions and naming.'
applyTo: '**/*.cs'
---

# MCP (Model Context Protocol)

Feature libraries with `*QueryService` types decorated with `[McpServerToolType]` follow these conventions. Individual methods exposed to the Agent are decorated with `[McpServerTool]` and every such method — including currently commented-out candidates — must also carry a `[Description]` attribute on the method and on each of its parameters. Return-type objects and their nested types must have `[Description]` on every public property.

## Pattern

```csharp
[McpServerTool]
[Description("...")]
public async Task<Foo> DoSomething(
    [Description("...")] string bar,
    CancellationToken cancellationToken = default)
```

> Note: `McpServerToolAttribute` (v1.1.0) has **no** `Description` property. The correct pattern is always two separate attributes: `[McpServerTool]` then `[Description(...)]`.

## MCP `[Description]` text vs XML doc comments

XML doc comments (`<summary>`, `<param>`, `<returns>`) are read by developers and tooling (IntelliSense, generated docs). They may contain `<see cref="..."/>` deep-links, `<remarks>` blocks, multi-sentence explanations, and coding-specific detail.

`[Description]` text on MCP tools is **not** UI copy and is **not** a contract with humans. It is:

- Contextual guidance for an LLM deciding **which tool to call** and **how to map arguments**
- Never shown to end users
- Not subject to grammar or localisation requirements

Therefore MCP descriptions should be:

- **Concise** — one sentence per method/parameter is usually enough
- **Semantically rich** — include the key noun, verb, and any units or constraints the LLM needs (e.g. `"0=fully open, 100=fully closed"`, `"range 14–25"`)
- **Disambiguation-first** — if two tools or parameters could be confused, the description must distinguish them
- **Enum-aware** — when a parameter is typed as `string` but represents an enum, list **all valid values with a brief label** in the description text (the LLM cannot infer them from the type):

```csharp
[Description("Status filter. Values: Active, Suspended, Cancelled, Expired.")]
string? status = null
```

- **No XML markup** — plain text only; `<see cref="..."/>` links are meaningless to an LLM
- **No localization** — English only; multiple languages add noise without benefit

## Checklist when adding or editing a `[McpServerTool]` method

1. Add `[McpServerTool]` then `[Description("...")]` on the method — one sentence naming what it does.
2. Add `[Description("...")]` on every non-`CancellationToken` parameter.
3. For `string` parameters representing an enum: list all enum member names with a brief description each.
4. For complex request-object parameters: summarise the key fields and their constraints in the description.
5. Ensure every public property on the return type (and any nested types) has `[Description("...")]` with a concise label and unit/range where applicable.
6. Keep XML `<summary>` comments intact — they serve a different audience and must not be replaced by or merged with `[Description]` text.

## Method naming

.NET MCP servers convert PascalCase method names to `snake_case` for the tool registry. Both forms must read naturally and be unambiguous.

- **Domain-prefix every tool name** so it is globally unique across all `[McpServerToolType]` classes (e.g. `GetOrder`, not `GetItem`; `GetCustomerAddress`, not `GetAddress`).
- **Verb-first for actions**: `CancelSubscription`, `ExecutePayment` — not `SubscriptionCancel`.
- **Get/List pairing**: Use `Get<Noun>` for a single-item lookup and `Get<Noun>s` (plural) for the list variant.
- **Human/LLM-friendly vocabulary**: Prefer everyday words over protocol or industry jargon (e.g. *Payment* over *Settlement*, *retry* over *exponential backoff*).
- **Read in snake_case**: Before committing, mentally convert the name — `GetCustomerSubscriptionPaymentHistory` → `get_customer_subscription_payment_history` is too long; `GetCustomerPaymentStatus` → `get_customer_payment_status` is fine.

## snake_case references

When an `[McpServerTool]` method is renamed, search for its old `snake_case` form in:

- `appsettings*.json` — `IncludeTools` / `ExcludeTools` arrays reference tools by snake_case name.
- System prompt / instructions markdown files that may list tool names.

## Description anti-patterns

Avoid these in `[Description]` text:

- **Return-type narration** — *"Returns the names of affected records"* duplicates what the LLM already infers from the method signature.
- **Restating parameter constraints on the method** — keep constraints on the parameter `[Description]`; the method description should say *what* the tool does, not *how* to fill in every field.
- **Identical wording across tools** — if two tools could be confused, their descriptions must explicitly cross-reference each other (e.g. *"For a summary overview use GetOrderSummary"* on the full-detail tool).
