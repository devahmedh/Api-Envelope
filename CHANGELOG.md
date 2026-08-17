# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) — see
"Versioning" in `README.md` for exactly what counts as major, minor and patch for this
particular library, since the wire shape is the real public API.

All five artifacts (`TheSpectre.ApiEnvelope`, `TheSpectre.ApiEnvelope.AspNetCore`,
`TheSpectre.ApiEnvelope.FluentValidation`, `TheSpectre.ApiEnvelope.DataAnnotations`, and the npm
package `thespectre-apienvelope-types`) version in lockstep — one version number covers all of
them, whether or not a given release touched a particular package.

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
