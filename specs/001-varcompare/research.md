# Phase 0 Research: varcompare (read-only)

**Feature**: `specs/001-varcompare/` | **Date**: 2026-08-20 | **Revision**: 2

**Revision 2 changed the foundations.** The reference repository
[`hkoueke/AzureRepoGovernance`](https://github.com/hkoueke/AzureRepoGovernance) turned out to be public and
readable, and the tool became read-only. Together these removed the two riskiest items in revision 1 (the
full-replacement `PUT` and its secret-blanking hazard, and the client-side concurrency fingerprint) and
replaced the entire authentication design. What follows is aligned with that repository's proven conventions,
because it will itself become the second tool in this app.

---

## R0 — What the reference implementation actually does

`hkoueke/AzureRepoGovernance` (solution `GovernanceAuditor`, C#, `net10.0`) is a read-only Azure DevOps
auditor. The decisive facts:

| Aspect | What it does |
|--------|--------------|
| **Target** | **On-premises Azure DevOps Server** — `AzureDevOpsServerOptions` with a base URL and a **collection** segment; sample config `https://devops.entreprise.local` + `DefaultCollection` |
| **Authentication** | **Integrated Windows credentials (NTLM/Kerberos)** with pre-authentication enabled. No token, no PAT, no browser. This is literally "the user's session" |
| **Redirects** | `AllowAutoRedirect = false` |
| **API version** | `7.1`, configurable |
| **Routes** | `{collection}/_apis/{service}/{resource}?api-version={v}` |
| **Resilience** | `AddStandardResilienceHandler()` on the typed client |
| **Paging** | Follows the `x-ms-continuationtoken` response header until empty |
| **Layout** | `Core` (domain + abstractions + analyzers) · `Infrastructure.AzureDevOps` · `Reporting` · `Console`, each with a `ServiceCollectionExtensions` |
| **Options** | Bound from configuration, DataAnnotations-validated, `ValidateOnStart` |
| **Build** | `net10.0`, `LangVersion latest`, nullable, `TreatWarningsAsErrors`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`, `GenerateDocumentationFile`, central package management, `NuGetAudit` mode `all` level `low` |
| **Analyzers** | **Meziantou.Analyzer · AsyncFixer · SonarAnalyzer.CSharp**, `PrivateAssets=all` |
| **Tests** | xUnit · NSubstitute · FluentAssertions 7.2.2 · **NetArchTest.Rules** for dependency rules · `FakeHttpMessageHandler` · `RecordingLogger` · a dedicated privacy test (`ExitCodeAndPrivacyTests`) |

**Decision**: adopt these conventions wholesale rather than the ones proposed in revision 1. Consistency with
the code that is about to move into this solution is worth more than any marginal preference of mine.

**Superseded from revision 1**: Entra/`Azure.Identity` credential chain · `Azure.Identity.Broker` ·
`DefaultAzureCredential` · token caching and refresh · `dev.azure.com` URL shapes · xUnit v3 · Shouldly ·
ArchUnitNET · `Microsoft.VisualStudio.Threading.Analyzers`.

---

## R1 — How to talk to the server

**Decision**: A typed `HttpClient` registered through `IHttpClientFactory`, base address =
`{ServerBaseUrl}/` (trailing slash enforced), with a primary handler configured as:

```
UseDefaultCredentials = true      // the developer's Windows session
PreAuthenticate       = true      // avoid a 401 round-trip per request
AllowAutoRedirect     = false     // never present credentials to another host
```

Requests are `GET` only. JSON is read with `System.Text.Json` using explicit options and
`ResponseHeadersRead` streaming, mirroring `AzureDevOpsApiReader`.

**Rationale**: This is exactly the mechanism the user described as "the app works by using the user's
session", proven in their own code. It satisfies FR-028 with no credential-handling code of our own — there
is nothing to leak, cache, or accidentally log, because the app never holds a credential at all.

**Alternatives considered**: the `Microsoft.TeamFoundation.*` SDK (owns its own `HttpClient`, defeating
Principles VI and VII); Entra tokens (wrong product — that is Azure DevOps Services, not Server).

---

## R2 — Authentication ⭐ replaces revision 1 entirely

**Decision**: Integrated Windows authentication only. No credential type, no token cache, no sign-in chain,
no `Azure.Identity` dependency.

**Consequences for the spec**:
- FR-028a–d are now about integrated auth and non-redirection, not about a source chain.
- There is no "which identity did we pick" ambiguity to announce — it is the Windows session, always. What
  the tool announces instead is **which server and collection it is about to read** (FR-028c), which is the
  spoofing risk that actually exists here.
- A `401` means the server rejected the Windows session — a flat failure with a clear message (FR-032), not a
  fallback to some other credential.

**Non-Windows hosts**: `UseDefaultCredentials` degrades to whatever Negotiate support the platform has.
DevToolbox targets Windows; other platforms are best-effort and out of scope.

**Open, tenant-specific**: whether the server requires the client to be domain-joined and on the corporate
network/VPN. Operationally true today for the reference tool; worth confirming.

---

## R3 — Configuration

**Decision**: An options type bound from `appsettings.json`, DataAnnotations-validated with
`ValidateOnStart`, mirroring `AzureDevOpsServerOptions`:

| Setting | Notes |
|---------|-------|
| `BaseUrl` | required, **HTTPS** (FR-031a); a bare host name such as `azure` is read as `https://azure/`, an explicit scheme is honoured as given |
| `Collection` | required, e.g. `DefaultCollection` |
| `ApiVersion` | default `7.1` |
| `AllowInsecureHttp` | default `false`; the only way to permit plain HTTP, and it is named so it cannot be set by accident |
| `HttpTimeoutSeconds` | default 30 |
| `MaxDegreeOfParallelism` | default 5 (their `ExecutionOptions` value) |

**Rationale**: fail at startup with a readable message rather than at the first request. Certificate
validation is never configurable (FR-031b) — there is deliberately no setting for it.

---

## R4 — Reading variable groups

**Decision**: three `GET` routes, api-version `7.1`, all prefixed by the collection:

| Purpose | Route |
|---------|-------|
| List projects (FR-002b) | `{collection}/_apis/projects?api-version=7.1` |
| List variable groups (FR-003) | `{collection}/{project}/_apis/distributedtask/variablegroups?api-version=7.1` |
| Get one group (FR-006) | `{collection}/{project}/_apis/distributedtask/variablegroups/{groupId}?api-version=7.1` |

Paging follows `x-ms-continuationtoken` to exhaustion — a partial list is a failure, never a truncated
success (FR-016).

**Shapes**: `VariableGroup { id, name, description, type, isShared, modifiedOn, variables,
variableGroupProjectReferences }` where `type` is `"Vsts"` or `"AzureKeyVault"`, and `variables` is a map of
`name → { value, isSecret, isReadOnly }`.

**State derivation** (FR-008): key missing ⇒ `Absent` · `isSecret: true` ⇒ `PresentSecret` (the API returns
`value: null` for secrets) · `value == ""` ⇒ `PresentEmpty` · non-empty ⇒ `PresentWithValue` ·
`type == "AzureKeyVault"` ⇒ every entry additionally key-vault-sourced (FR-009).

---

## R5 — ~~Writing a variable~~ REMOVED

Revision 1 flagged a critical hazard: the update endpoint is a **full replacement** `PUT`, secrets read back
as `null`, and it was unproven whether Azure DevOps preserves them — a wrong assumption would have silently
blanked every secret in a production variable group. It also required round-tripping
`variableGroupProjectReferences` and used an organization-scoped URL unlike the reads.

**None of this applies any more.** The tool is read-only (FR-018), so the hazard, its verification task, its
pre-flight guard, and its post-write read-back are all gone. This is the single largest risk reduction in the
feature, and it came free with the scope decision.

---

## R6 — ~~Concurrency detection~~ REMOVED

Revision 1 needed a client-side fingerprint (`modifiedOn` + content hash) because the API exposes no ETag and
no revision, making true compare-and-swap impossible. With no writes there is nothing to conflict with.

What remains is a display concern only: a comparison shows the groups **as at the moment they were read**.
The UI states the read time and offers a refresh (spec edge case "group changed while being compared").
`modifiedOn` is still surfaced as information; it no longer guards anything.

---

## R7 — Variable name matching

**Decision**: match across groups with `StringComparer.OrdinalIgnoreCase`; keep and display each group's
stored casing; flag rows where casings diverge.

**Rationale**: Azure DevOps treats pipeline variable names case-insensitively, so `API_KEY` and `Api_Key` are
one variable to the platform. Matching case-sensitively would invent a difference that does not exist.

---

## R8 — Resilience

**Decision**: `AddStandardResilienceHandler()` from `Microsoft.Extensions.Http.Resilience` (10.9.0, the
version already pinned in the reference repo), configured from `appsettings.json`.

The standard handler chains, outermost to innermost: *rate limiter → total request timeout → retry →
circuit breaker → attempt timeout*. Its retry strategy already applies **exponential backoff with jitter**,
which is what the constitution requires — no custom strategy needed. `Retry-After` on `429`/`503` is honoured
(FR-037); breaker opening satisfies FR-038.

**Simplification from revision 1**: `DisableForUnsafeHttpMethods()` is no longer needed. Every request this
tool makes is a `GET`, so full retry semantics are safe by construction — there is no non-idempotent call to
protect.

Defaults: total request timeout 30 s, attempt timeout 10 s, 3 retries, breaker sampling 30 s / failure ratio
0.2 / minimum throughput 5.

---

## R9 — Logging

**Decision**: `Microsoft.Extensions.Logging` abstractions throughout, with `LoggerMessage`-style source-
generated log methods in a per-assembly `Log` class — the pattern the reference repo already uses
(`GovernanceAuditor.Console/Log.cs`, `Infrastructure.AzureDevOps/Internal/Log.cs`). Each run opens a scope
carrying `RunId`, `ToolId`, `Collection`, `Project`.

**Sink**: a rolling file under `%LOCALAPPDATA%\DevToolbox\logs\` with 7-day retention. Console logging is off
by default and, when enabled, routes through Spectre so it cannot corrupt the display (FR-043).

**Privacy**: values are never passed to a logger at all. This is asserted by a test in the spirit of their
`ExitCodeAndPrivacyTests` plus a `RecordingLogger` that captures everything and fails if a known value
appears (SC-004).

**RESOLVED 2026-08-20 (T018)**: a **small custom `ILoggerProvider`** in
`DevToolbox.Infrastructure/Logging/`, adding **no new package**. Serilog was the alternative considered.

**Rationale**: two reasons decided it. First, `NuGetAudit` runs at mode `all` / level `low`, so every
dependency is a standing cost; a rolling file writer with day-stamped files and a retention sweep is roughly
120 lines we fully control. Second, and more decisive: Principle VI requires redaction enforced **at the sink
boundary** (T043a). Owning the provider makes that decorator plain `ILoggerProvider` code that wraps our own
writer — auditable in one file. With Serilog the same control becomes a sink wrapper or enricher shaped by
Serilog's pipeline, which is more machinery around the one security control we most need to be able to read.

The reference repo uses only the console provider, so no existing convention is broken either way.

---

## R10 — Local state on disk

| What | Where | Contents |
|------|-------|----------|
| Settings | `%APPDATA%\DevToolbox\settings.json` | last collection/project, layout preference |
| Run recovery | `%LOCALAPPDATA%\DevToolbox\runs\{runId}.json` | tool id, collection, project, selected group ids and names, completed step names, timestamps |
| Logs | `%LOCALAPPDATA%\DevToolbox\logs\` | rolling, 7-day retention |

**No variable values, ever** (FR-039a). Files older than 7 days are swept at startup (FR-039c). Paths resolve
through `Environment.GetFolderPath` and are confined to that root before use (Principle III).

---

## R11 — Detecting collection and project from the working folder (FR-002a)

**Decision**: walk up from the current directory for `.git`, then **parse `.git/config`** for
`[remote "..."] url`. No process spawn.

On-premises remote forms, given a configured server host:

| Form | Example |
|------|---------|
| HTTPS | `https://devops.entreprise.local/DefaultCollection/MyProject/_git/MyRepo` |
| With user info | `https://user@devops.entreprise.local/DefaultCollection/MyProject/_git/MyRepo` |
| With port | `https://devops.entreprise.local:8080/tfs/DefaultCollection/MyProject/_git/MyRepo` |
| SSH | `ssh://devops.entreprise.local:22/DefaultCollection/MyProject/_git/MyRepo` |

A remote is only accepted when its **host matches the configured server host**; anything else falls back to
asking (FR-002d). Note the optional virtual-directory segment (`/tfs`) common on Server installations — the
parser derives the collection relative to the configured base URL rather than assuming the first path
segment.

**Rationale**: no process spawn means no argument-injection surface and no dependency on `git` being on
`PATH`; parsing a small INI file is trivially unit-testable with fixtures.

---

## R12 — Spectre.Console presentation

Unchanged from revision 1 except that the write confirmation and secret entry prompts are gone.

- **Shell**: `Layout` with a `Panel` tab strip (one tab per tool category), body for the active tool, footer
  showing the target server and collection (FR-028c). `SelectionPrompt<T>` for navigation; **Exit** at root,
  **Back** in every sub-menu.
- **Side-by-side**: `Table`, one column per group, cells as *symbol + short text*, colour as reinforcement
  only (FR-012). Legend as a `Grid` beneath (FR-013).
- **Stacked**: `Rows` of `Panel`s, one per variable, past the legibility threshold or on manual toggle
  (FR-016b/d).
- **Threshold**: computed from `AnsiConsole.Profile.Width` against measured column widths — not a hardcoded
  group count.
- **Capabilities**: `AnsiConsole.Profile.Capabilities` for colour/unicode/interactivity; ASCII fallback
  symbols; non-interactive profile exits cleanly.
- **Progress**: `Progress`/`Status` per group so retries are visible rather than looking frozen (FR-036).
- **Cancellation**: `Console.CancelKeyPress` intercepted once, sets `e.Cancel = true`, trips a
  `CancellationTokenSource` so checkpoints flush and sinks dispose.

---

## R13 — Testing stack ⭐ aligned to the reference repo

**Decision**: **xUnit · NSubstitute · FluentAssertions 7.2.2 · NetArchTest.Rules · coverlet**, plus
`Spectre.Console.Testing` (`TestConsole`) for rendering assertions and a `FakeHttpMessageHandler` copied in
spirit from theirs.

**Rationale**: revision 1 proposed xUnit v3 / Shouldly / ArchUnitNET on general merit. That was the wrong
call for a solution that is about to absorb `GovernanceAuditor` — two assertion libraries and two
architecture-test frameworks in one repo is a real cost for no benefit. FluentAssertions is pinned at
**7.2.2** deliberately: version 8 moved to a commercial licence, and 7.x is what the user already runs.

`TestConsole` is what makes SC-003 (identifiable without colour) and SC-012 (no values in the comparison)
directly assertable against rendered output.

---

## R14 — Build and analyzers ⭐ aligned to the reference repo

`Directory.Build.props`: `net10.0` · `LangVersion latest` · `Nullable enable` · `ImplicitUsings enable` ·
`TreatWarningsAsErrors true` · `EnableNETAnalyzers true` · `AnalysisLevel latest-recommended` ·
`EnforceCodeStyleInBuild true` · `GenerateDocumentationFile true` · `NuGetAudit true` / `NuGetAuditMode all`
/ `NuGetAuditLevel low` · `NoWarn NU1507` (multiple feeds without a source mapping).

`Directory.Packages.props`: `ManagePackageVersionsCentrally true`.

Analyzers, `PrivateAssets=all`: **Meziantou.Analyzer · AsyncFixer · SonarAnalyzer.CSharp**.

**Note**: revision 1 proposed `AnalysisLevel=latest-all` and `Microsoft.VisualStudio.Threading.Analyzers`.
Downgrading to `latest-recommended` and using AsyncFixer matches the user's existing, already-passing
configuration; `latest-all` on a fresh codebase with warnings-as-errors mostly produces churn. The
constitution's requirement ("async analyzers enforced") is met by AsyncFixer.

---

## R15 — Making room for the second tool

**Decision**: tools are discovered, not hardcoded. `ITool` (id, display name, tab, description) plus a
`ServiceCollectionExtensions`-style registration per tool — the same idiom the reference repo already uses
per project. The shell builds tabs and menus by enumerating registered `ITool`s.

**Rationale**: `GovernanceAuditor` is moving in as tool #2. Principle II requires that addition to touch no
existing tool's code. Its existing shape (`Core` / `Infrastructure.AzureDevOps` / `Reporting` / `Console`)
maps cleanly onto a tool trio plus a shared shell, so the migration should be mostly project moves and a
registration call rather than a rewrite.

**Worth noting for later**: both tools will want the same Azure DevOps server options, the same typed client,
and the same integrated-auth handler. Those belong in a **shared** `DevToolbox.Infrastructure.AzureDevOps`
from the start, so tool #2 inherits them instead of duplicating them.

---

## R16 — ~~Reference repository unreadable~~ RESOLVED

Revision 1 recorded that `hkoueke/AzureRepoGovernance` returned HTTP 404. It is public; the earlier failure
was on the `tree/main/src` URL form. Read via the GitHub API on 2026-08-20 and reflected throughout this
revision — most consequentially in R0, R2, R13, and R14.

**Remaining question for the user**: is the varcompare target the **same on-premises server** as
`GovernanceAuditor` (`https://devops.entreprise.local/DefaultCollection`), or is there also a cloud
organization in play? Everything above assumes the former. If both exist, authentication becomes pluggable
and a token path returns — a contained change, but one worth knowing before implementation starts.
