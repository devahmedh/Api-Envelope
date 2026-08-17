# TheSpectre.ApiEnvelope

**One JSON shape for every response your API returns — success and error alike.**

[![ci](https://github.com/devahmedh/Api-Envelope/actions/workflows/ci.yml/badge.svg)](https://github.com/devahmedh/Api-Envelope/actions/workflows/ci.yml)
[![license: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4)](https://dotnet.microsoft.com/)

```jsonc
// success
{ "isSuccess": true,  "statusCode": 200, "result": { "id": 42 }, "errorCode": null,        "correlationId": "4bf92f35…" }

// error
{ "isSuccess": false, "statusCode": 409, "result": null,         "errorCode": "PROJECT_CODE_TAKEN", "correlationId": "4bf92f35…" }
```

---

## Why

Three problems show up in almost every HTTP API, and they compound.

**1. The client has to guess the shape before it can read anything.** A bare DTO on success and some error object on failure means every call site asks "which of these did I get?" before it can touch a field. Add a validation failure, a 401 from the auth middleware and a routing 404 and you have four shapes for one endpoint.

**2. The server leaks English.** `"The Title field must be 400 characters or fewer."` cannot be shown to an Arabic-speaking user. Once that sentence is composed on the server, no amount of frontend work fixes it.

**3. A bug report can't be traced.** "It failed around 3pm" is not a log query.

`TheSpectre.ApiEnvelope` fixes all three, in one place, for every endpoint:

- **One shape, always.** Controllers, minimal APIs, thrown exceptions, a 401 challenge, a routing 404 — every response is the same seven-property envelope.
- **Stable keys, never prose.** The server returns `errorCode: "TOO_LONG"` plus `params: { "max": 400 }`. The client owns every translation and renders `الحد الأقصى 400 حرف` or `Maximum 400 characters` from its own strings.
- **A correlation id on every response**, taken from the active `Activity` — so the value in the body is the same trace id your APM already indexes.

---

## Install

```bash
dotnet add package TheSpectre.ApiEnvelope.AspNetCore
```

That's all most apps need — it brings the core package with it.

| Package | When you need it |
|---|---|
| `TheSpectre.ApiEnvelope` | The envelope types alone: shared libraries, Azure Functions, anything without an HTTP pipeline. **Zero dependencies.** |
| `TheSpectre.ApiEnvelope.AspNetCore` | The middleware and filters. **Zero package dependencies** — framework reference only. |
| `TheSpectre.ApiEnvelope.FluentValidation` | Turn FluentValidation failures into error keys. |
| `TheSpectre.ApiEnvelope.DataAnnotations` | Same, for `[Required]`, `[StringLength]`, `[Range]`. **Zero dependencies.** |
| `thespectre-apienvelope-types` (npm) | TypeScript types for the client. |

Targets **.NET 8** and **.NET 10**.

---

## Quick start

### Minimal APIs

```csharp
using TheSpectre.ApiEnvelope;
using TheSpectre.ApiEnvelope.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApiEnvelope();

var app = builder.Build();

app.UseApiEnvelope();      // must come BEFORE UseAuthentication
app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("").WithApiEnvelope();

api.MapGet("/projects/{id:int}", (int id) => new Project(id, "Falcon"));

app.Run();
```

### Controllers

```csharp
builder.Services.AddControllers();
builder.Services.AddApiEnvelope();   // covers every controller, no per-action opt-in

// …

app.UseApiEnvelope();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

```csharp
[ApiController]
[Route("projects")]
public sealed class ProjectsController : ControllerBase
{
    [HttpGet("{id:int}")]
    public IActionResult Get(int id) => Ok(new Project(id, "Falcon"));
}
```

Both produce byte-identical responses:

```json
{"isSuccess":true,"statusCode":200,"result":{"id":42,"name":"Falcon"},"errorCode":null,"correlationId":"4bf92f3577b34da6a3ce929d0e0e4736"}
```

> **`UseApiEnvelope()` must sit before `UseAuthentication()`.** A 401 challenge short-circuits the pipeline; if the envelope middleware is inside that, 401 responses ship with an empty body. The failure is invisible in development, because a developer holding a valid token never sees it.

Minimal APIs need `.WithApiEnvelope()` once at the root — MVC filters register application-wide, minimal APIs have no equivalent global hook.

---

## The envelope

Seven properties, always in this order.

| Property | Success | Error |
|---|---|---|
| `isSuccess` | `true` | `false` |
| `statusCode` | the HTTP status | the HTTP status |
| `result` | your payload, or `null` | `null` |
| `errorCode` | `null` | a stable `SCREAMING_SNAKE_CASE` key |
| `message` | *omitted* | diagnostics only — **never render this** |
| `correlationId` | always present | always present |
| `details` | *omitted* | field-level failures, when there are any |

`isSuccess`, `statusCode`, `result`, `errorCode` and `correlationId` are **always present**, so the client reads them unconditionally. `message` and `details` are omitted when absent.

### `message` is not for users

It is English, it is for your logs, and by default it is **not sent outside Development** at all. An unrecognised exception never leaks its text or a stack frame in production.

```csharp
builder.Services.AddApiEnvelope(o => o.MessageVisibility = MessageVisibility.Never);
```

Add a frontend lint rule forbidding `message` in templates. Translate `errorCode` instead.

---

## Errors

Throw `AppException` from anywhere — a service, a repository, a background job. One handler renders it.

```csharp
throw new AppException("PROJECT_CODE_TAKEN", statusCode: 409);
```

```json
{"isSuccess":false,"statusCode":409,"result":null,"errorCode":"PROJECT_CODE_TAKEN","correlationId":"…"}
```

The inner exception is always preserved, so a caller recovering from an EF Core concurrency conflict can still reach `ex.Entries`:

```csharp
catch (DbUpdateConcurrencyException ex)
{
    throw new AppException(ErrorCodes.Conflict, "row version mismatch", 409, ex);
}
```

Give the domain named types when it helps — `AppException` is not sealed:

```csharp
public sealed class ProjectCodeTakenException : AppException
{
    public ProjectCodeTakenException(string code)
        : base("PROJECT_CODE_TAKEN", $"code '{code}' already exists", 409) { }
}
```

### Responses no endpoint produced

A 401 challenge, a 403, a routing 404, a 405 or a 415 never reaches your code — so no filter can see them. They are enveloped anyway:

```json
{"isSuccess":false,"statusCode":404,"result":null,"errorCode":"NOT_FOUND","correlationId":"…"}
```

### Default exception mapping

| Exception | Status | `errorCode` |
|---|---|---|
| `AppException` | its own | its own |
| `KeyNotFoundException` | 404 | `NOT_FOUND` |
| `ArgumentException` and subtypes | 400 | `BAD_REQUEST` |
| `TimeoutException` | 504 | `TIMEOUT` |
| `NotImplementedException` | 501 | `NOT_IMPLEMENTED` |
| `BadHttpRequestException` | its own | matching that status |
| `OperationCanceledException` | *nothing written* | — |
| anything else | 500 | `INTERNAL_ERROR` |

Lookup walks the type hierarchy, so the most derived mapping wins. Adjust it freely:

```csharp
builder.Services.AddApiEnvelope(o =>
{
    o.ExceptionStatusCodes[typeof(DbUpdateConcurrencyException)] = 409;
    o.ExceptionStatusCodes.Remove(typeof(KeyNotFoundException));   // if 404 is misleading for you
});
```

A cancelled request writes nothing: the client has disconnected, and `499` is not a real status code.

---

## Validation — keys and params, never sentences

This is the part that makes the library worth adopting. A validation failure returns **what** failed and **the bound**, so the client renders its own message in the active language.

```json
{
  "isSuccess": false,
  "statusCode": 400,
  "result": null,
  "errorCode": "VALIDATION_FAILED",
  "correlationId": "…",
  "details": [
    { "field": "code",  "errorCode": "REQUIRED" },
    { "field": "title", "errorCode": "TOO_LONG", "params": { "max": 400 } }
  ]
}
```

`params` is why this works. Without it you would need a key per bound — `TOO_LONG_400`, `TOO_LONG_200` — or the server would compose the sentence.

`field` is the **camelCase JSON path** the client already knows: `title`, `address.city`, `items[0].quantity`.

### FluentValidation

```csharp
public sealed class CreateProjectValidator : AbstractValidator<CreateProject>
{
    public CreateProjectValidator()
    {
        RuleFor(p => p.Code).NotEmpty().WithErrorCode(ValidationErrorCodes.Required);
        RuleFor(p => p.Title).MaximumLength(400).WithErrorCode(ValidationErrorCodes.TooLong);
    }
}
```

```csharp
api.MapPost("/projects", (CreateProject model) => Create(model))
   .WithFluentValidation<RouteHandlerBuilder, CreateProject>();
```

Guard against a forgotten `.WithErrorCode(...)` with one test. A rule without it ships FluentValidation's internal default — `NotEmptyValidator` — which no client has a translation for, so the user sees nothing at all:

```csharp
[Test]
public void AllValidators_DeclareExplicitErrorCodes()
{
    var findings = ValidatorErrorCodeAudit.FindRulesWithoutExplicitErrorCodes(
        typeof(CreateProjectValidator).Assembly);

    Assert.That(findings, Is.Empty);
}
```

### DataAnnotations

No new attribute — put the key in `ErrorMessage`:

```csharp
public sealed class CreateProject
{
    [Required(ErrorMessage = ValidationErrorCodes.Required)]
    public string? Code { get; set; }

    [StringLength(400, ErrorMessage = ValidationErrorCodes.TooLong)]
    public string? Title { get; set; }
}
```

```csharp
builder.Services.AddApiEnvelope();
builder.Services.AddApiEnvelopeDataAnnotations();
```

This also replaces `[ApiController]`'s automatic `ValidationProblemDetails` response, which is server-composed English prose.

> **Both integrations produce identical `details[]`** — same field names, same keys, same `params` keys — enforced by a shared test suite run against both. Your client has one contract regardless of which library an API happens to use.

### Built-in keys

`ErrorCodes` — `INTERNAL_ERROR` `VALIDATION_FAILED` `BAD_REQUEST` `UNAUTHORIZED` `FORBIDDEN` `NOT_FOUND` `METHOD_NOT_ALLOWED` `CONFLICT` `UNSUPPORTED_MEDIA_TYPE` `PAYLOAD_TOO_LARGE` `TOO_MANY_REQUESTS` `TIMEOUT` `NOT_IMPLEMENTED`

`ValidationErrorCodes` — `REQUIRED` `TOO_LONG` `TOO_SHORT` `OUT_OF_RANGE` `INVALID_FORMAT` `INVALID_EMAIL` `INVALID_URL` `NOT_EQUAL` `DUPLICATE`

Your own keys are just strings. Add them to your client's translation map.

---

## Paged results

```csharp
api.MapGet("/projects", (AppDbContext db, int page = 1, int pageSize = 20) =>
    db.Projects.OrderBy(p => p.Id).GetPaged(page, pageSize));
```

```json
{
  "isSuccess": true,
  "statusCode": 200,
  "result": {
    "data": [ { "id": 3, "name": "Falcon" } ],
    "pagination": {
      "currentPage": 2, "pageSize": 20, "rowCount": 57,
      "pageCount": 3, "firstRowOnPage": 21, "lastRowOnPage": 40
    }
  },
  "errorCode": null,
  "correlationId": "…"
}
```

`pageCount`, `firstRowOnPage` and `lastRowOnPage` are computed and sent, so no frontend re-derives `Math.ceil(rowCount / pageSize)` and gets the off-by-one wrong.

> **`GetPaged` on `IQueryable<T>` runs synchronously** and makes two database round-trips. For a hot path, query asynchronously yourself and construct `PagedResult<T>` directly:
>
> ```csharp
> var count = await query.CountAsync(ct);
> var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
> return new PagedResult<Project>(items, new PaginationData(page, pageSize, count));
> ```

---

## Correlation ids

Every response carries one, success included, and echoes it in `X-Correlation-Id`.

It comes from `Activity.Current.TraceId` when there is one — so the value in the body is the **same trace id** Application Insights or any OpenTelemetry backend indexes by. A user's bug report becomes one lookup in the system your traces already live in, rather than a second identifier to join on. With no active `Activity`, a 32-hex value is generated so the shape never changes.

An inbound `X-Correlation-Id` is honoured only if it matches `^[A-Za-z0-9._-]{1,128}$`. Anything else is discarded and replaced — an unvalidated header reaches both a log line and a response header, where CR/LF means log injection and response splitting.

The id is pushed into the `ILogger` scope, so every log line written during the request already carries it. If you need it directly:

```csharp
app.MapGet("/whoami", (HttpContext ctx) => ctx.GetCorrelationId());
```

---

## Configuration

Four options. That is deliberate — every option is a permanent support obligation.

```csharp
builder.Services.AddApiEnvelope(o =>
{
    o.MessageVisibility      = MessageVisibility.DevelopmentOnly;  // default
    o.CorrelationIdHeaderName = "X-Correlation-Id";                // default
    o.ExcludedPathPrefixes.Add("/internal");                       // + /swagger /health /metrics /hubs
    o.ExceptionStatusCodes[typeof(MyException)] = 422;
});
```

`ExcludedPathPrefixes` matches on **path segments**, so `/health` does not swallow `/healthcheck`.

---

## What is *not* enveloped

Deliberately, and by type check rather than by path pattern:

| Kind | Why |
|---|---|
| `File(...)`, `Results.File(...)` | a download is bytes, not JSON |
| Redirects | the `Location` header is the payload |
| Challenge / Forbid / SignIn / SignOut | the framework owns these |
| `Content(...)`, `Results.Text(...)` | you chose `text/plain` on purpose |
| Anything already an `ApiResponse<T>` | never double-wrapped |
| `/swagger` `/health` `/metrics` `/hubs` | SignalR negotiation in particular breaks if altered |

Opt a single endpoint out:

```csharp
api.MapGet("/raw", () => new Payload()).WithoutApiEnvelope();
```

```csharp
[HttpGet("callback")]
[NoEnvelope]
public IActionResult Callback() => Ok(thirdPartyShape);
```

### Two behaviours worth knowing

**`204 No Content` becomes `200` with `"result": null`.** HTTP forbids a body on a 204, and a body-less response is the one case where a client would have to check the status before daring to read the body — the exact branching this library removes. `NoContent()` therefore returns a full envelope at 200.

**A 4xx is never `isSuccess: true`.** `BadRequest(dto)` and `NotFound(dto)` produce an error envelope with the key matching their status. To attach field-level information, throw `AppException` with `ErrorDetail`s — the envelope has no slot for an arbitrary error payload, by design.

---

## OpenAPI

The generated document describes the **envelope**, not the bare DTO. `WithApiEnvelope()` and `AddApiEnvelope()` rewrite response metadata, so Swashbuckle, NSwag and `Microsoft.AspNetCore.OpenApi` all report `ApiResponse<Project>` for a 200 — and report **200** for a `NoContent()` endpoint, which no hand-written document would know.

Under Native AOT the success schema degrades to `ApiResponse<object>`; the runtime envelope is unaffected.

---

## TypeScript client

```bash
npm install thespectre-apienvelope-types
```

`ApiResponse<T>` is a **discriminated union**, so one check narrows both fields:

```ts
import type { ApiResponse } from 'thespectre-apienvelope-types';
import { translateError } from './messages';

const res: ApiResponse<Project[]> = await http.get('/api/projects');

if (res.isSuccess) {
  this.projects = res.result;                        // Project[] — no `!` needed
} else {
  this.error = translateError(res.errorCode, lang);  // string — never render res.message
}
```

A single flat interface would type `result` as `T | null` and `errorCode` as `string | null` in **both** branches, forcing a non-null assertion at every call site — and `!` is where a contract stops being enforced.

Keep the **code** in state, not the translated string, so the message re-renders live when the user toggles language:

```ts
// lib/messages.ts
const MESSAGES: Record<string, { ar: string; en: string }> = {
  REQUIRED:  { ar: 'هذا الحقل مطلوب',       en: 'This field is required' },
  TOO_LONG:  { ar: 'الحد الأقصى {max} حرف', en: 'Maximum {max} characters' },
  NOT_FOUND: { ar: 'غير موجود',              en: 'Not found' },
};

export function translateError(code: string, lang: 'ar' | 'en', params?: Record<string, unknown>) {
  const template = MESSAGES[code]?.[lang] ?? code;
  return Object.entries(params ?? {})
    .reduce((s, [k, v]) => s.replaceAll(`{${k}}`, String(v)), template);
}
```

The types are verified in CI against the same fixture files the .NET tests assert on byte-for-byte, so they cannot drift from what the server actually sends.

---

## Native AOT

| | AOT |
|---|---|
| `TheSpectre.ApiEnvelope` | ✅ |
| `.AspNetCore` — minimal APIs | ✅ |
| `.AspNetCore` — MVC | ❌ MVC itself is not AOT-compatible |
| `.FluentValidation` | ❌ rules are built from expression trees |
| `.DataAnnotations` | ❌ attribute reflection is the mechanism |

**You never declare the wrapper type.** The envelope's own fields are written directly with `Utf8JsonWriter`; only `result` is delegated to your serializer, resolved from the payload's own contract. So this is all you need:

```csharp
[JsonSerializable(typeof(Project))]      // ApiResponse<Project> is NOT required
internal sealed partial class AppJsonContext : JsonSerializerContext;
```

CI publishes a real native binary and curls it on every commit, so this is verified rather than asserted.

---

## Non-goals

- **No RFC 7807 `ProblemDetails`.** It centres on human-readable `title` and `detail` — exactly what this library exists to keep off the wire.
- **No response body logging.** Responses carry business content; logging them by default is a data-exposure default.
- **No content negotiation.** The envelope is always `application/json`.
- **No C# client unwrapper.** The envelope is trivial to deserialize, and the intended clients are TypeScript.

---

## Versioning

The envelope shape is the public API. Breaking it breaks every consumer silently — the code compiles and the frontend stops working. So:

- **Major** — renaming or removing a wire property, changing casing, moving a property between always-present and omitted, changing an existing exception mapping, changing a `const` value.
- **Minor** — a new property omitted when null, a new type, member, option or package.
- **Patch** — anything touching neither the wire shape nor the public API.

This is enforced, not just documented. Byte-exact fixture files pin the JSON, approval files pin every public member, and CI fails any pull request that edits either without an explicit `BREAKING CHANGE:` footer or a `semver:` label. All packages version in lockstep.

---

## Building

```bash
dotnet test                                   # full suite, both target frameworks
cd npm/thespectre-apienvelope-types && npm test   # TypeScript contract
```

Requires the .NET 8 and .NET 10 SDKs. Native AOT publishing additionally needs a C++ toolchain — clang and zlib on Linux, the MSVC build tools on Windows.

---

## Licence

[MIT](LICENSE)
