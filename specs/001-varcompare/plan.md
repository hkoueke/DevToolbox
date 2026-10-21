# Implementation Plan: varcompare — Azure DevOps Variable Group Comparison (read-only)

**Branch**: `001-varcompare` | **Date**: 2026-08-20 | **Revision**: 2 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-varcompare/spec.md`

## Summary

Compare the variables of two or more Azure DevOps variable groups in a colour-coded console view, showing per
group whether each variable is absent, set, empty, secret, or key-vault-sourced. **Read-only** — where a
difference is found the tool points the developer at the Library page rather than offering to change
anything.

Technical approach: a .NET 10 Spectre.Console app on a Clean Architecture spine (Domain → Application →
Infrastructure → Presentation, inward-only), with tools discovered through DI so `GovernanceAuditor` can join
later as tool #2 without touching this one. The on-premises Azure DevOps Server is read over its REST API
through a typed `HttpClient` using **integrated Windows credentials** and the standard Microsoft resilience
pipeline. Runs execute as named, checkpointed steps so a failure offers retry / continue / restart / abort.

Since the repository contains only Spec Kit artifacts today, this feature also delivers the solution
scaffolding: build props, central package management, analyzers, host, and shell.

**What changed in revision 2** — two corrections, both simplifying:

1. **The tool is read-only.** The entire write path is gone, and with it the riskiest thing in revision 1: a
   full-replacement `PUT` whose interaction with unreadable secret values was unverified and could have
   blanked every secret in a production group. Also gone: client-side concurrency fingerprinting, write
   confirmation UI, and the disposable-project requirement for validation.
2. **The target is an on-premises Azure DevOps Server with integrated Windows authentication**, not Azure
   DevOps Services with Entra tokens. Established by reading the now-accessible reference implementation
   [`hkoueke/AzureRepoGovernance`](https://github.com/hkoueke/AzureRepoGovernance). The `Azure.Identity`
   credential chain, token caching, and browser sign-in fallback are all removed. This supersedes the first
   clarification recorded in the spec.

## Technical Context

**Language/Version**: C# (`LangVersion latest`) on .NET 10 (`net10.0`)

**Primary Dependencies**: Spectre.Console (sole UI library) · `Microsoft.Extensions.Hosting` /
`DependencyInjection` / `Http` / `Options.DataAnnotations` / `Logging` ·
`Microsoft.Extensions.Http.Resilience` (10.9.0) · `System.Text.Json`. **No `Azure.Identity`** — there is no
token to acquire. A rolling-file log sink is the one dependency still open (research R9).

**Storage**: local files only — settings `%APPDATA%\DevToolbox\settings.json`; run recovery
`%LOCALAPPDATA%\DevToolbox\runs\{runId}.json` (selections and step progress only, 7-day expiry); logs
`%LOCALAPPDATA%\DevToolbox\logs\` (rolling, 7-day retention). No database. No variable values on disk, ever.

**Testing**: xUnit · NSubstitute · FluentAssertions **7.2.2** (8.x is commercially licensed) ·
**NetArchTest.Rules** · coverlet · `Spectre.Console.Testing` (`TestConsole`) · `FakeHttpMessageHandler` and
`RecordingLogger` in the style of the reference repo. All aligned to that repo so the two codebases can merge
without carrying two assertion libraries.

**Target Platform**: domain-joined Windows 11 console on the corporate network or VPN. Integrated Windows
authentication is the mechanism, so this is a genuine platform constraint, not a preference.

**Project Type**: Console application (interactive, menu-driven), single solution, multi-project

**Performance Goals**: comparison of 5 groups × 500 distinct variables rendered within 5 s of retrieval
(SC-005); groups retrieved concurrently with bounded parallelism (default 5, matching the reference repo's
`ExecutionOptions`); launch to rendered comparison of 2 groups under 60 s and ≤5 interactions (SC-001)

**Constraints**: warnings-as-errors, zero unjustified suppressions · Spectre.Console is the only renderer,
`System.Console` confined to the non-interactive bootstrap guard and the `Ctrl+C` hook · **every request is a
`GET`** · no secret, credential, or variable value in screen output, logs, or checkpoints · redirects
disabled · certificate validation never disabled · every async API takes and propagates a `CancellationToken`

**Scale/Scope**: one tool, 5 named steps, 3 read endpoints, ~40 functional requirements; designed so tool #2
is additive

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | Gate | Initial | Post-design |
|---|-----------|------|:-------:|:-----------:|
| I | Clean Architecture Boundaries | Domain has zero package refs; Application references neither Infrastructure nor Presentation; composition root is the only omniscient place; NetArchTest enforces it | PASS | PASS |
| II | SOLID, explicit dependencies, testability | Constructor injection only; no static mutable state; `ITool` + per-tool `AddXxx()` registration makes a new tool additive; sealed by default; Core testable with no console, network, or filesystem | PASS | PASS |
| III | Security by default (STRIDE + OWASP) | STRIDE table below; HTTPS-only base URL; redirects off; certificate validation not configurable; validated and escaped route segments; no credential material anywhere; `NuGetAudit` mode `all`, level `low` | PASS | PASS |
| IV | Zero-warning builds, enforced analysis | `TreatWarningsAsErrors`, `EnableNETAnalyzers`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild`, `Nullable enable`; central package versions; Meziantou + AsyncFixer + Sonar | PASS | PASS |
| V | Spectre.Console-only terminal experience | Only Spectre renders; tabbed top level; Back on every sub-menu, Exit at root; cooperative `Ctrl+C`; capability-aware degradation; `Progress` for long work | PASS | PASS |
| VI | Structured, auditable logging | `ILogger<T>` with source-generated `Log` classes; `RunId` scope; mandated events incl. tool id and version at run start; rolling file sink, never raw stdout under Spectre; **type-based redaction enforced at the sink boundary**, not by caller discipline alone | PASS | PASS |
| VII | Resilience on every outbound call | Typed client from `IHttpClientFactory`; `AddStandardResilienceHandler` — retry with exponential backoff **and jitter**, circuit breaker, attempt + total timeout; `Retry-After` honoured | PASS | PASS |
| VIII | Resumable and restartable execution | 5 named steps, checkpoint after each success, four recovery choices, cross-launch resume with re-read, 7-day expiry | PASS | PASS |

**Post-design note**: no principle required a waiver. Revision 1's one compromise — best-effort optimistic
concurrency, a platform limitation — disappeared with the write path. Principle VII got simpler too: with
every call a `GET`, the "retries only on idempotent operations" rule holds by construction rather than by
configuration.

### STRIDE pass (required by Principle III)

| Threat | Vector in this feature | Mitigation |
|--------|------------------------|------------|
| **S**poofing | Windows credentials presented to an unintended host; the developer reads the wrong environment | HTTPS-only base URL (FR-031a); **`AllowAutoRedirect = false`** so credentials never follow a redirect to another host (FR-028d); certificate validation never disabled (FR-031b); the server and collection are announced before any read (FR-028c) |
| **T**ampering | *Largely eliminated.* The tool cannot modify anything — no write endpoint is reachable and the gateway interface exposes no way to express a change (FR-018) | Enforced structurally by the port shape, and by an architecture test that fails the build on any HTTP verb other than `GET` (SC-006) |
| **R**epudiation | No record of what was read | Per-run `RunId` scope; every step, retry, breaker transition, and group retrieval logged with collection, project, group and outcome — never a value (FR-041, FR-042) |
| **I**nformation disclosure | Config values on screen during a shared call; secrets in logs or checkpoints | Comparison renders **state only**, values only in an explicitly opened detail view (FR-015a); secrets and key-vault values never retrievable and never displayed; only selections and step names persisted (FR-039a); no stack traces, raw bodies, or full URLs in output |
| **D**enial of service | Retry storms against a throttling server; unbounded fan-out across many groups | Standard resilience pipeline with circuit breaker and total timeout; `Retry-After` honoured (FR-037); bounded parallelism (default 5); retry exhaustion reported as unavailable (FR-038) |
| **E**levation of privilege | Acting beyond the developer's rights | The developer's own Windows session only; no service account, no impersonation, no stored credential; the tool requires only read access (FR-018d) and `403` surfaces as a plain message (FR-029) |

**OWASP touchpoints**: A01 (permissions are the platform's, never re-implemented; read-only by design) ·
A02 (no credential material at rest or in memory; TLS defaults untouched) · A03 (no shell-out; every route
segment validated and escaped) · A05 (DataAnnotations-validated options with `ValidateOnStart`; insecure HTTP
requires an explicitly named opt-in) · A06 (`NuGetAudit` fails the build at `low`, mode `all`) · A08
(explicit `System.Text.Json` contracts, no polymorphic deserialization) · A09 (structured run logging) ·
A10 (SSRF: the host is fixed by configuration, only collection/project/group id vary and all are validated).

## Project Structure

### Documentation (this feature)

```text
specs/001-varcompare/
├── plan.md              # This file
├── spec.md              # Feature specification (with Clarifications session)
├── research.md          # Phase 0 — R0–R16 (revision 2)
├── data-model.md        # Phase 1 — entities, states, wire mapping
├── quickstart.md        # Phase 1 — build, run, V1–V7 validation
├── contracts/           # Phase 1
│   ├── azure-devops-rest.md     # external REST surface consumed (GET only)
│   ├── application-ports.md     # interfaces the Core layer owns
│   └── console-experience.md    # shell + rendering + recovery UI contract
├── checklists/
│   └── requirements.md  # spec quality checklist
└── tasks.md             # Phase 2 — created by /speckit-tasks, NOT here
```

### Source Code (repository root)

Naming and layout follow `GovernanceAuditor`'s conventions so the two solutions merge cleanly.

```text
Directory.Build.props            # net10.0, nullable, warnings-as-errors, analyzers, NuGetAudit
Directory.Packages.props         # central package versions
.editorconfig
DevToolbox.sln

src/
├── DevToolbox.Domain/                 # shared kernel: ToolRun, StepState, Result<T>. Zero package refs
├── DevToolbox.Application/            # shell use cases + shared ports (ITool, IStepRunner,
│                                      #   IRunCheckpointStore, IClock). No Infrastructure, no Spectre
├── DevToolbox.Infrastructure/         # shared adapters, folder-per-concern:
│                                      #   AzureDevOps/ — server options, typed client, integrated-auth
│                                      #     handler, resilience wiring, paged API reader
│                                      #   Storage/     — checkpoint store, settings store
│                                      #   System/      — clock, file system, working-folder inspector
├── DevToolbox.Presentation/           # shared Spectre shell: tab strip, menus, prompts, recovery prompt
└── DevToolbox.Console/                # composition root: Program.cs, hosting, DI, log sink wiring

tools/
└── VarCompare/
    ├── DevToolbox.Tools.VarCompare.Core/            # comparison model + use cases + IVariableGroupGateway
    ├── DevToolbox.Tools.VarCompare.Infrastructure/  # gateway over the shared client, JSON DTOs, mapping
    └── DevToolbox.Tools.VarCompare.Presentation/    # comparison/detail views + AddVarCompare()

tests/
└── DevToolbox.Tests/              # one test project, folder-per-area (their convention)
    ├── Architecture/              # NetArchTest: layering + "GET only" verb rule
    ├── Infrastructure/            # FakeHttpMessageHandler: paging, retry exhaustion, breaker open,
    │                              #   401/403/redirect mapping; RecordingLogger privacy assertions
    ├── Presentation/              # TestConsole: no values rendered, colourless states, no credential prompt
    └── Tools/VarCompare/          # comparison building, state derivation, resume and restart
```

**Structure Decision**: four constitutional layers plus a composition root for the shared spine, and one
Core / Infrastructure / Presentation trio per tool. The trio exists because `GovernanceAuditor` is moving in
as tool #2 and Principle II requires that addition to touch no existing tool code; splitting it across the
three layers keeps the inward-only rule true *inside* a tool, which a single `DevToolbox.Tools.X` project
could not (it would have to reference both Spectre and `HttpClient` alongside the comparison rules). The
shared Azure DevOps client, options, and integrated-auth handler deliberately live in
`DevToolbox.Infrastructure/AzureDevOps/` rather than in the tool, so tool #2 inherits them. Tests are a
single project with folders, matching `GovernanceAuditor.Tests`.

## Complexity Tracking

> Filled because the project count is high for a console app and deserves justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| 8 production projects for one tool | Principle I requires four distinct layers with a mechanically enforced inward-only rule; Principle II requires tool #2 to be purely additive | A single project cannot be checked by an architecture test — layering becomes a naming convention, and the first stray `HttpClient` reference in comparison code goes unnoticed |
| Per-tool Core/Infrastructure/Presentation trio | Keeps the layering rule true inside a tool, and lets the tool own its ports (`IVariableGroupGateway`) instead of polluting the shared Application layer with one tool's vocabulary | One project per tool forces UI + HTTP + rules into one assembly; shared-Application ports make every future tool's vocabulary everyone's problem |
| Separate `DevToolbox.Domain` alongside `DevToolbox.Application` | Principle I names Domain as a layer with zero package references — the one assembly whose freedom from I/O is mechanically verifiable | `GovernanceAuditor` merges these into `Core`; adopting that here would drop the only assembly the architecture test can assert is dependency-free |

Revision 1 also justified a client-side concurrency fingerprint. **That row is gone** — no writes, nothing to
conflict with.

## Working agreements

Recorded here because they constrain how this plan is executed, not just what is built.

- **The app never asks for a username or password.** With integrated Windows authentication there is no
  credential type in the design at all. A test enumerates every reachable prompt and fails if one collects
  credentials (SC-007).
- **Read-only means read-only.** An architecture test fails the build if any HTTP verb other than `GET`
  appears in the gateway (SC-006).
- **No Claude attribution in version control, ever.** Commits and PRs carry no `Co-Authored-By`, no
  `Claude-Session`, no "Generated with Claude Code" footer. Work is committed by the user, as the user.
- **`GovernanceAuditor` is coming.** Its conventions are adopted now — package versions, analyzer set, test
  stack, `ServiceCollectionExtensions` idiom, `Log` classes, `FakeHttpMessageHandler` — so the merge is
  project moves plus a registration call rather than a reconciliation.

## Open items — status after implementation

**All three are resolved except where the developer's own environment is needed.** The original text is kept
below for traceability.

1. **Log sink** — RESOLVED (T018). A small custom `ILoggerProvider` in
   `DevToolbox.Infrastructure/Logging/`, adding no package. Decisive reason: Principle VI requires redaction
   *at the sink boundary*, and owning the provider makes that decorator plain `ILoggerProvider` code we can
   read in one file. Recorded in research R9.
2. **Server base URL** — RESOLVED in design (T051a), pending confirmation against a live server (T139).
   `appsettings.json` is now only a *hint*: the tool asks on first run, validates the answer, and saves it
   per user. The spec carries this as a clarification dated 2026-08-20.
3. **Server virtual directory** — RESOLVED (T074, T075). The collection is derived relative to the configured
   base URL, so `https://host/tfs/DefaultCollection/...` yields `DefaultCollection`, not `tfs`. Covered by a
   test in `WorkingFolderInspectorTests`.

**Still needing your environment**: T138 (quickstart V1–V7 against the real server) and T139 (confirm the
first-run prompt captures your real base URL and reuses it). Neither can be discharged from here.

### Original open items

1. **Your real base URL.** The target itself is settled — the spec's last clarification and its Assumptions
   fix it as an on-premises Azure DevOps Server reached with integrated Windows authentication, with Azure
   DevOps Services explicitly out of scope. What still has to come from your environment is the concrete base
   URL and collection segment; `https://devops.entreprise.local/DefaultCollection` is `GovernanceAuditor`'s
   value, carried here as the shape to validate against rather than as a verified address. Should a cloud
   organization ever enter scope, authentication becomes pluggable and a token path returns — contained, but
   a spec change first.
2. **Log sink choice** (research R9). A rolling file sink needs one package (Serilog is the usual answer),
   confined to the composition root, or a small custom file provider to add no dependency. The reference repo
   uses only the console provider, so this is a genuine new decision rather than an existing convention.
3. **Server virtual directory.** Some Server installations sit under a path such as `/tfs`. The remote parser
   derives the collection relative to the configured base URL rather than assuming the first path segment —
   worth confirming against your actual remote URLs.

**Resolved since revision 1**: the reference repository is public and has been read (research R0, R16); the
`PUT` secret-preservation hazard and its verification task are gone with the write path (R5); concurrency
fingerprinting is gone (R6).

---

## STRIDE re-review against the built code (T142)

Each threat below was re-checked against what was actually implemented, not against what was planned.

| Threat | Mitigation as built | Verified by |
|--------|---------------------|-------------|
| **S**poofing | HTTPS enforced at both the options validator and the first-run prompt; `AllowAutoRedirect = false`; certificate validation has no configuration switch; server and collection announced before any read | `FailureRendererTests`, `NoCredentialPromptTests`, options validator |
| **T**ampering | Every non-GET verb is a **build error** at the call site via `BannedSymbols.txt`; the gateway interface exposes no way to express a change | `ReadOnlyVerbTests`, `VariableGroupGatewayTests` (real traffic asserted GET-only), verified by deliberate probe |
| **R**epudiation | `RunId` scope; run start with tool id and version; every group retrieval logged with collection, project, group and outcome | `LoggingPrivacyTests` (trace reconstructs which groups were read) |
| **I**nformation disclosure | `ComparisonCell` carries no value, so the comparison cannot render one; sink-level redaction catches a mistaken log statement; checkpoint type shape verified before every write | `ComparisonPresenterTests`, `StackedRendererTests`, `LoggingPrivacyTests`, `RunCheckpointStoreTests` |
| **D**enial of service | Standard resilience pipeline with breaker and total timeout; retry budget exhausts to `ServiceUnavailable`; group retrieval bounded at 5 concurrent | `ResiliencePipelineTests` (4 attempts then stop) |
| **E**levation of privilege | Windows session only; no credential type exists in any assembly; `403` surfaces as a plain message with no retry | `NoCredentialPromptTests`, `ResiliencePipelineTests` |

**One mitigation strengthened during implementation.** Partial access originally failed the whole run. It now
degrades only the affected group — and the state derivation had to be corrected so a degraded column reports
`Undetermined` rather than `Absent`, because claiming a variable is missing from a group we could not read is
an assertion the tool has no basis for.

**No threat was downgraded or accepted.** Nothing in the table is a residual risk note.
