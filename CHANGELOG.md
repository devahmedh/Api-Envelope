# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) — see
"Versioning" in `README.md` for exactly what counts as major, minor and patch for this
particular library, since the wire shape is the real public API.

All six artifacts (`TheSpectre.ApiEnvelope`, `TheSpectre.ApiEnvelope.AspNetCore`,
`TheSpectre.ApiEnvelope.FluentValidation`, `TheSpectre.ApiEnvelope.DataAnnotations`,
`TheSpectre.ApiEnvelope.EntityFrameworkCore`, and the npm package
`thespectre-apienvelope-types`) version in lockstep — one version number covers all of them,
whether or not a given release touched a particular package.

## [1.1.0]

Every change in this release comes from one source: a full migration of an existing production
backend from AutoWrapper onto this library — 107 files, 307 rewritten throw sites, 2 709 passing
tests. Nothing here changes the envelope's wire shape. Two of the three findings produced a green
build and a silently wrong response, which is the class of defect a 1.0.0 only meets the first
time it lands in a large existing codebase.

### Fixed

**Controllers now serialise the envelope with MVC's JSON options.** `AddControllers().AddJsonOptions(…)`
configures `Microsoft.AspNetCore.Mvc.JsonOptions`; the envelope previously read
`Microsoft.AspNetCore.Http.Json.JsonOptions` on every path, so a converter registered the
documented MVC way was skipped for every enveloped controller response. The failure was silent:
a `DateTime` converter pinning values to UTC never ran, and a client at UTC+3 read every
calendar date one day early, with nothing thrown and a green build. `ApiEnvelopeResultFilter`
and the DataAnnotations field-path mapper now read MVC's options; minimal APIs, error envelopes
and status-code envelopes continue to read `Http.Json.JsonOptions`, which is what their own
pipelines bind with.

> **Behaviour change.** An MVC application that compensated for the old behaviour by registering
> its converters or naming policy on `ConfigureHttpJsonOptions(…)` only will find those settings
> no longer applied to controller responses. Register them on both objects — the new startup
> warning below names exactly what is missing where.

### Added

**A startup warning when another `IExceptionHandler` is registered first.** Handlers run in
registration order and the first one returning `true` wins, so a catch-all registered before
`AddApiEnvelope()` claims every exception the envelope would have handled — `AppException`
included, which loses its status code with it. A 401 login failure reaches the client as a 500
with the other handler's body, and the package looks correctly installed throughout.
`UseApiEnvelope()` now names any handler registered ahead of its own and states both remedies:
register `AddApiEnvelope()` first, or make the other handler return `false` so it logs without
writing the response.

**`AttributeErrorCodeAudit` for DataAnnotations.** The counterpart to
`ValidatorErrorCodeAudit`: it lists every `ValidationAttribute` whose `ErrorMessage` is prose
rather than a `SCREAMING_SNAKE_CASE` key, and so will be discarded in favour of a key inferred
from the attribute. `PascalCase` is the case worth catching — it looks enough like a key to
survive review, and the response still carries a plausible one, so the mismatch surfaces only as
a missed translation lookup in the running client.

**A startup warning when the two JSON option objects diverge.** `UseApiEnvelope()` compares
`Mvc.JsonOptions` against `Http.Json.JsonOptions` in applications that host controllers, and
logs one warning listing every difference in converters, `PropertyNamingPolicy` or
`DefaultIgnoreCondition` — the three settings that change what a client receives. An application
serving controllers and minimal APIs from one host can otherwise serialise the same DTO two
different ways with no signal anywhere. Minimal-API-only applications are never warned: they have
no MVC pipeline for the options to disagree with.

**`TheSpectre.ApiEnvelope.EntityFrameworkCore`** — a new package containing one method,
`IQueryable<T>.GetPagedAsync(page, pageSize, cancellationToken)`. It returns the same
`PagedResult<T>` the synchronous helper returns, so the wire shape is unchanged, but executes
both round-trips — the count and the page — asynchronously.

The synchronous `GetPaged` on `IQueryable<T>` blocks the calling thread across two database
round-trips. On a request-serving thread pool that is thread starvation under load, not a
style preference. The core package declares no dependencies and therefore cannot call
`CountAsync`/`ToListAsync`; a provider-specific package can.

The new package references the core package only, not `TheSpectre.ApiEnvelope.AspNetCore` —
paging is not an HTTP concern, so it stays usable from a background job or a console
application. Its `Microsoft.EntityFrameworkCore` reference is bounded to one major version per
target framework (`[8.0.0,9.0.0)` on `net8.0`, `[10.0.0,11.0.0)` on `net10.0`).

Nothing was removed or altered: `PagedResult<T>`, `PaginationData` and both `GetPaged`
overloads remain in `TheSpectre.ApiEnvelope` exactly as they shipped in 1.0.0 — they are the
wire contract, pinned by the golden files and mirrored in the TypeScript package, not
utilities to be relocated.

### Documentation

**Which JSON options each pipeline reads** is now a table in the Controllers quick-start section
of `README.md`, with the divergence warning's text and the UTC-converter failure that motivated
it.

**The `ErrorMessage` key pattern is documented**, in the DataAnnotations validation section, with
one preserved and one discarded example side by side. The rule — `^[A-Z][A-Z0-9_]*$` or the value
is dropped — was load-bearing and discoverable only by reading the source.

**Request data on a failure** now has a section under "Correlation ids", closing the one gap the
adoption report identified with no replacement. AutoWrapper's `LogRequestDataOnException` has no
equivalent here and will not get one — but the exception handler already logs the method, path,
key and correlation id, and Serilog's `EnrichDiagnosticContext` covers the rest. The section
spells out why the correlation id must be set explicitly there (the logger scope closes before
the request-completion line is written) and why the request body is a deliberate second step
rather than part of the snippet.

**The `IExceptionHandler` ordering constraint is documented** next to the existing
`UseApiEnvelope()` / `UseAuthentication()` note, with the log-only handler pattern spelled out.

**FluentValidation does not share the key pattern**, and the README now says so where the
confusion arises. FluentValidation has a real `ErrorCode` slot, so a key set with
`.WithErrorCode(...)` survives in any casing and the message is ignored entirely rather than
pattern-matched. A FluentValidation key that never arrives is almost always one written into
`.WithMessage(...)` instead, which leaves the rule carrying FluentValidation's own
`NotEmptyValidator`-style default — the case `ValidatorErrorCodeAudit` already catches.

## [1.0.0]

Initial release.

### Added

**One envelope, always.** Every HTTP response — a controller action, a minimal API handler, a
thrown exception, a validation failure, an authentication challenge, a routing 404 — is wrapped
in the same seven-property JSON shape. A client reads `isSuccess` and never has to guess which
of several possible response shapes it received.

**Stable error keys instead of server-composed prose.** Failures carry a `SCREAMING_SNAKE_CASE`
`errorCode` (for example `VALIDATION_FAILED`, `NOT_FOUND`, `PROJECT_CODE_TAKEN` for an
application-thrown error) plus, for validation failures, a `details[]` array of
`{ field, errorCode, params }` entries. `params` carries the structured bound a message would
otherwise need to interpolate — `{ "max": 400 }` for a length rule — so the client composes its
own sentence in its own language instead of receiving one pre-written in English. `params`
values are restricted to `string`, `int`, `double`, `bool` and `null`: the types JSON and
JavaScript's `number` both represent exactly. A `long` or `decimal` bound is omitted rather than
silently narrowed, since a silent conversion could corrupt one specific large or high-precision
value in production.

**Two validation integrations, one contract.** `TheSpectre.ApiEnvelope.FluentValidation` and
`TheSpectre.ApiEnvelope.DataAnnotations` turn validator/attribute failures into identical
`details[]` shapes — same field-naming convention (camelCase JSON path, e.g. `title`,
`address.city`, `items[0].quantity`), same key vocabulary, same `params` keys — enforced by a
shared test suite exercised against both. A consumer's error-rendering code does not change
depending on which validation library a given API happens to use. A rule that forgets an
explicit error key is caught at test time (`ValidatorErrorCodeAudit`) rather than shipping
FluentValidation's untranslatable internal default name to a user.

**A correlation id on every response, success included.** Sourced from `Activity.Current.TraceId`
when one is active, so the id in the JSON body is the same trace id already indexed by
Application Insights or any OpenTelemetry-backed APM — a bug report resolves to one log query
instead of a second identifier to reconcile. Also echoed on the `X-Correlation-Id` response
header and pushed into the `ILogger` scope for the duration of the request. An inbound
`X-Correlation-Id` is honoured only when it matches `^[A-Za-z0-9._-]{1,128}$`; anything else is
replaced rather than trusted, since an unvalidated header reaching both a log line and a
response header is a log-injection and response-splitting vector.

**Paged results as a first-class shape.** `IQueryable<T>.GetPaged(page, pageSize)` (and the
`PagedResult<T>` / `PaginationData` types for building the same shape from an already-executed
query) returns `{ data, pagination: { currentPage, pageSize, rowCount, pageCount,
firstRowOnPage, lastRowOnPage } }` as the `result` payload — with `pageCount`,
`firstRowOnPage` and `lastRowOnPage` computed server-side, so no client re-derives
`Math.ceil(rowCount / pageSize)` and gets the off-by-one wrong.

**204 becomes 200.** HTTP forbids a body on `204 No Content`, which is exactly the one response
a client would have to branch on before reading — the branching this library exists to remove.
`NoContent()` and an empty MVC action both return the full envelope at status `200` with
`"result": null` instead.

**A 4xx is never `isSuccess: true`.** `BadRequest(dto)`, `NotFound(dto)` and any other non-2xx
result produce an error envelope keyed to their status code. Field-level detail is attached only
through `AppException` with `ErrorDetail`s — the envelope has no slot for an arbitrary error
payload on a result-returned response, by design, so there is exactly one path for structured
error detail rather than two that can drift.

**One exception handler for the whole app.** Throw `AppException` (not sealed, so domain
exception subtypes are welcome) from a service, a repository, or a background job, and it is
turned into the matching error envelope by one middleware — no per-controller try/catch. A
built-in mapping table covers common framework exceptions
(`KeyNotFoundException` → 404, `ArgumentException` → 400, `TimeoutException` → 504,
`NotImplementedException` → 501, `BadHttpRequestException` → its own status) and is
extensible per-application. An `OperationCanceledException` from a disconnected client writes
nothing, since the caller is gone and `499` is not a real HTTP status.

**Native AOT compatible where the underlying stack allows it.** `TheSpectre.ApiEnvelope` and the
minimal-API path of `TheSpectre.ApiEnvelope.AspNetCore` are AOT-compatible — the envelope's own
fields are written directly with `Utf8JsonWriter`, and only the `result` payload is delegated to
the caller's own `JsonSerializerContext`, so a consumer never has to declare `ApiResponse<T>`
itself in their AOT type registrations. MVC, `TheSpectre.ApiEnvelope.FluentValidation` and
`TheSpectre.ApiEnvelope.DataAnnotations` are not AOT-compatible, because the frameworks they sit
on (MVC's runtime compilation, FluentValidation's expression-tree rule building, DataAnnotations'
attribute reflection) are not. CI publishes and runs a real Native AOT binary on every commit
rather than relying on the analyzer alone.

**OpenAPI metadata describes the envelope, not the bare DTO.** `WithApiEnvelope()` (minimal
APIs) and `AddApiEnvelope()` (MVC) rewrite each endpoint's response metadata so any OpenAPI
generator built on `IProducesResponseTypeMetadata` / `ProducesResponseTypeAttribute` — including
Swashbuckle, NSwag and `Microsoft.AspNetCore.OpenApi` — reports `ApiResponse<T>` for a 200 and
reports the normalised status (200, not 204) for a no-content endpoint. Under Native AOT the
reported success schema degrades to `ApiResponse<object>`, since the concrete generic type
cannot always be constructed reflectively; the runtime envelope itself is unaffected.

**Configuration kept small on purpose.** Four options total on `AddApiEnvelope(...)`:
`MessageVisibility` (default: `message` is included only in Development, never in a higher
environment, since it is diagnostic English text and not meant for a user), the correlation
header name, excluded path prefixes (`/swagger`, `/health`, `/metrics`, `/hubs` excluded by
default, matched on path segments so `/health` does not swallow `/healthcheck`), and the
exception-to-status-code mapping table. Every option is a permanent support obligation, so the
surface is deliberately narrow rather than configurable-by-default.

### Contract

The wire shape below is what every consumer of every version of this library actually depends
on — it is pinned by byte-exact fixture files on the .NET side and the same fixtures on the
TypeScript side, so the two cannot drift from each other. Seven properties, always in this
order:

1. `isSuccess` — `boolean`. `true` on success, `false` on error. Always present.
2. `statusCode` — `number`. The HTTP status code, already normalised (a `204` response is
   reported as `200`). Always present.
3. `result` — the payload on success, or `null`. Always present as a key, even when its value
   is `null`.
4. `errorCode` — `null` on success, a stable `SCREAMING_SNAKE_CASE` string key on error. Always
   present as a key.
5. `message` — diagnostic English text, present only on some error responses and only outside
   Development-restricted visibility settings. **Never render this to a user; omitted, not
   `null`, when absent.**
6. `correlationId` — `string`. Always present, success and error alike.
7. `details` — an array of `{ field, errorCode, params }` entries for a validation failure.
   Omitted, not `null` or `[]`, when there is nothing to report.

`isSuccess`, `statusCode`, `result`, `errorCode` and `correlationId` are unconditionally present
on every response, so a client reads them without an existence check. `message` and `details`
are omitted (not sent as `null`) when there is nothing to say — a client checks for the key's
presence, not its value, before reading either.

The TypeScript package models this as a discriminated union on `isSuccess` (`ApiSuccess<T>` /
`ApiError`), not a single interface with optional fields — a single interface would type
`result` as `T | null` and `errorCode` as `string | null` in both branches, forcing a non-null
assertion (`!`) at every call site regardless of which branch was actually reached.
