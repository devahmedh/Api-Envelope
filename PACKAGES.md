# Dependencies

Every entry below was checked against its package's actual `.nuspec` (fetched from
`api.nuget.org/v3-flatcontainer`) or, for the npm package, its `package.json` on the npm
registry — not copied from a prior list on trust. The exact commands used are in the
"How this was verified" section at the bottom.

## Shipped

These are the only dependencies that reach a consumer's build. Three of the five shipped
packages ship with **zero** package dependencies — a framework reference is not a package
dependency, it resolves against the shared runtime already on the machine.

| Package | Version | Used by | Purpose | Licence |
|---|---|---|---|---|
| `FluentValidation` | `[12.0.0,13.0.0)` (resolves to `12.0.0`) | `TheSpectre.ApiEnvelope.FluentValidation` | Validator discovery and rule descriptors, so a validator's declared error keys can be read without running validation | Apache-2.0 |
| `Microsoft.EntityFrameworkCore` | `[8.0.30,9.0.0)` on `net8.0`, `[10.0.11,11.0.0)` on `net10.0` | `TheSpectre.ApiEnvelope.EntityFrameworkCore` | `CountAsync` and `ToListAsync`, so a paged database query executes without blocking the calling thread | MIT |
| `Microsoft.AspNetCore.App` | framework reference (not a package) | `TheSpectre.ApiEnvelope.AspNetCore`, `TheSpectre.ApiEnvelope.DataAnnotations`, `TheSpectre.ApiEnvelope.FluentValidation` | The HTTP pipeline: middleware, minimal API routing, MVC filters | MIT |

The version range on `FluentValidation` is deliberate, not a habit: the error-code audit
(`ValidatorErrorCodeAudit`) reads FluentValidation's internal default validator naming
(`NotEmptyValidator` and similar) to detect a rule with no explicit `.WithErrorCode(...)`. A
FluentValidation major version could rename those internals, so the reference is pinned to the
major version that was actually tested against rather than left open-ended with `12.0.0` and up.

The `Microsoft.EntityFrameworkCore` range is bounded at **both** ends for different reasons. The
upper bound stops an unrelated restore pulling a major version this package was never compiled
against. The lower bound is the patch actually built and tested against — not `8.0.0` — because
NuGet resolves a range to its *lowest* satisfying version: an `8.0.0` floor resolves to EF Core
`8.0.0`, whose nuspec pins `Microsoft.Extensions.Caching.Memory 8.0.0`, carrying advisory
GHSA-qj66-m88j-hmgj. Every consumer would inherit it. EF Core `8.0.30` declares the patched
`8.0.1`. A floor of `x.0.0` looks like the permissive, consumer-friendly choice and is in fact
how a known vulnerability reaches a consumer's build.

`TheSpectre.ApiEnvelope` and `TheSpectre.ApiEnvelope.DataAnnotations` declare no
`PackageReference` at all — confirmed by their `.csproj` files and by the packed `.nuspec`
(see Task 4 verification). `TheSpectre.ApiEnvelope.AspNetCore` declares no `PackageReference`
either; its only dependency is the `Microsoft.AspNetCore.App` framework reference, which NuGet
does not record as a package dependency.

**Dependency budget: 0 / 0 / 0 / 1 / 1** — `TheSpectre.ApiEnvelope`,
`TheSpectre.ApiEnvelope.AspNetCore` and `TheSpectre.ApiEnvelope.DataAnnotations` ship zero
package dependencies each; `TheSpectre.ApiEnvelope.FluentValidation` and
`TheSpectre.ApiEnvelope.EntityFrameworkCore` ship exactly one each.

`TheSpectre.ApiEnvelope.EntityFrameworkCore` is the first package in the family with a
heavyweight dependency. That cost is inherent to its purpose — it exists to call EF Core's
async query operators — and is isolated behind an opt-in package boundary: a consumer who does
not page a database never resolves it.

## Test-only (never shipped)

These appear only in `tests/*.csproj` and `samples/*.csproj`. None are referenced by, or
packed into, any shipped `.nupkg` — confirmed by `.nuspec` inspection in Task 4.

| Package | Version | Purpose | Licence |
|---|---|---|---|
| `NUnit` | 4.6.1 | Test framework | MIT |
| `NUnit3TestAdapter` | 5.1.0 | Test discovery/runner adapter for `dotnet test` | MIT |
| `NUnit.Analyzers` | 4.9.2 | Roslyn analyzers catching common NUnit misuse at compile time | MIT |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | The MSBuild/VSTest test SDK | MIT |
| `NSubstitute` | 6.2.0 | Mocking, used instead of the banned Moq | BSD-3-Clause |
| `PublicApiGenerator` | 11.5.4 | Generates the approved-public-API text the approval tests diff against | MIT |
| `Mono.Cecil` | 0.11.6 (transitive, via `PublicApiGenerator`) | Assembly reflection used to enumerate public API surface | MIT |
| `System.CodeDom` | 6.0.0 (transitive, via `PublicApiGenerator`) | Code-model rendering used by `PublicApiGenerator` | MIT |
| `Microsoft.AspNetCore.TestHost` | 8.0.30 (net8.0) / 10.0.11 (net10.0) | In-memory `TestServer` for integration tests, no real socket | MIT |
| `Microsoft.EntityFrameworkCore.Sqlite` | 8.0.30 (net8.0) / 10.0.11 (net10.0) | Real SQL translation and async execution in the paging tests; the InMemory provider translates no SQL and would pass without exercising the async provider path | MIT |
| `Microsoft.Data.Sqlite` | transitive, via `Microsoft.EntityFrameworkCore.Sqlite` | The SQLite ADO.NET connection the in-memory test database is held open on | MIT |
| `typescript` | `^5.9.0` (devDependency, resolves to `5.9.3` at time of writing) | Compiles and type-checks the npm package's `.d.ts` contract; never a runtime dependency of a consumer | Apache-2.0 |

**Note on `NUnit`'s licence:** NUnit is MIT **only from v4 onward**. NUnit 3.x and earlier
shipped under a different (though also permissive) licence. "NUnit is MIT" is true of the
version actually taken here (4.6.1) and should not be assumed to generalise to any NUnit
release.

**Note on `typescript`'s licence:** Apache-2.0, not MIT — recorded as observed rather than
assumed, since this repository's other MIT-heavy dependency list could invite the wrong guess.
Apache-2.0 is permissive and carries no obligation that affects a devDependency never shipped
to a consumer.

## Rejected, with reasons

None of these appear anywhere in the repository — confirmed by
`grep -rn "FluentAssertions\|Moq\b\|AutoMapper\|Newtonsoft" --include="*.csproj" .` returning no
matches.

| Package | Reason rejected |
|---|---|
| `FluentAssertions` | Proprietary licence from v8 onward — no longer permissive. `NUnit`'s own `Assert.That(...)` constraint model covers every assertion this project needs. |
| `Moq` | Project standard is NSubstitute; Moq is explicitly banned regardless of its own licence. |
| `AutoMapper` | Project standard is manual mapping via extension methods; mapping libraries add an indirection this project's DTOs don't need. |
| `Newtonsoft.Json` | Would be a second JSON serializer alongside `System.Text.Json` in the response-writing path — `ApiEnvelopeWriter` writes the envelope directly with `Utf8JsonWriter`, and a second serializer in that path is a correctness and AOT risk, not just a size cost. |
| Any OpenAPI generator (Swashbuckle, NSwag, etc.) | The response metadata (`ApiResponse<T>` typing, normalised status codes) is produced by rewriting `IProducesResponseTypeMetadata` / `ProducesResponseTypeAttribute` directly — no OpenAPI document is generated by this library itself, so no generator dependency is needed. |

## Licence compliance summary

**Every licence recorded above is OSI-approved and permissive** (MIT, Apache-2.0, BSD-3-Clause).
None impose copyleft, none require source disclosure on distribution, and none restrict
commercial use. **Nothing flagged as non-permissive.**

## How this was verified

Each NuGet entry was checked against the package's actual `.nuspec` file, fetched directly —
not the nuget.org marketing page, which can lag or summarise:

```bash
curl -s "https://api.nuget.org/v3-flatcontainer/<id-lowercase>/<version>/<id-lowercase>.nuspec" \
  | grep -oE '<license[^/]*/?>[^<]*(</license>)?'
```

This was run for every row above, including `Mono.Cecil` and `System.CodeDom` (found by reading
the resolved dependency graph in `tests/TheSpectre.ApiEnvelope.Tests/obj/project.assets.json`,
not assumed from `PublicApiGenerator`'s own listed dependencies, since a lockfile-free restore
can resolve a different transitive version than the package's nominal minimum). The exact
resolved `FluentValidation` version was confirmed the same way, from
`tests/TheSpectre.ApiEnvelope.FluentValidation.Tests/obj/project.assets.json`.

`typescript`'s licence was checked against the npm registry:

```bash
curl -s https://registry.npmjs.org/typescript | python3 -c \
  "import json,sys; d=json.load(sys.stdin); v=d['dist-tags']['latest']; print(d['versions'][v]['license'])"
```
