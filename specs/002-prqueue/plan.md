# Implementation Plan: prqueue — the pull requests waiting on you, across the whole collection (read-only)

**Branch**: `002-prqueue` | **Date**: 2026-08-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-prqueue/spec.md`

## Summary

One list of every pull request awaiting the developer's review across every project in the collection,
oldest first, with the identity it belongs to announced before anything is read. **Read-only** — the tool
names what is waiting and where to go and review it, and offers no way to vote.

Technical approach: tool #2 on the existing spine. It adds a `Core` / `Infrastructure` / `Presentation`
trio under `tools/PrQueue/`, reuses the shared Azure DevOps typed client, resilience pipeline, integrated
Windows authentication, checkpoint store, and Spectre shell unchanged, and registers itself with two lines
in `Program.ConfigureServices`. No existing tool is modified.

The one architectural fact that shapes everything: **the platform has no collection-wide pull request
query** (research R1). Microsoft's own guidance is to enumerate projects and query each. The sweep is
therefore ours to perform, bounded and cancellable, and that sweep *is* the feature — it is the thing the
web interface cannot do on-premises.

Two spec-level findings came out of planning and were written back into the spec before this plan was
finished, as the repository's rules require: the wait cannot be read from the server and is redefined as
age with the column labelled accordingly (FR-008a), and a reviewer who has declined is no longer awaited
(FR-007a).

## Technical Context

**Language/Version**: C# (`LangVersion latest`) on .NET 10 (`net10.0`)

**Primary Dependencies**: none new. Spectre.Console, `Microsoft.Extensions.*`,
`Microsoft.Extensions.Http.Resilience`, `System.Text.Json` — all already referenced and centrally versioned.
A feature that adds no package is a feature that adds no audit surface.

**Storage**: local files only. Remembered project subset in `%APPDATA%\DevToolbox\settings.json`; run
recovery in `%LOCALAPPDATA%\DevToolbox\runs\` (selected projects and step progress only, 7-day expiry);
logs in `%LOCALAPPDATA%\DevToolbox\logs\`. No pull request content on disk, ever.

**Testing**: xUnit · NSubstitute · FluentAssertions 7.2.2 · NetArchTest.Rules · coverlet ·
`Spectre.Console.Testing` · the existing `FakeHttpMessageHandler` and `RecordingLogger`. New tests land in
`tests/DevToolbox.Tests/Tools/PrQueue/` plus additions to the existing Architecture, Infrastructure, and
Presentation folders.

**Target Platform**: domain-joined Windows 11 console on the corporate network or VPN, against an
on-premises Azure DevOps Server. Integrated Windows authentication is the mechanism.

**Project Type**: Console application (interactive, menu-driven), single solution, multi-project

**Performance Goals**: complete queue for 50 projects within 30 s (SC-004) with progress throughout; first
list on screen within 15 s for a typical working subset (SC-001). Reads fan out over projects with bounded
parallelism, default 5 (R10).

**Constraints**: warnings-as-errors · Spectre.Console the only renderer · **every request a `GET`** · no
pull request content in logs or checkpoints · identity announced before any read · every async API takes and
propagates a `CancellationToken` · no unbounded concurrency and no unbounded accumulation

**Scale/Scope**: one tool, 5 named steps, 4 read endpoints, 27 functional requirements. Two shared-layer
additions (`IProjectCatalog`, a `$skip` paging helper), both additive.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | Gate | Initial | Post-design |
|---|-----------|------|:-------:|:-----------:|
| I | Clean Architecture Boundaries | Tool trio keeps the inward rule inside the tool; `PrQueue.Core` references no Spectre and no `HttpClient`; `Presentation` never references `Infrastructure`; NetArchTest enforces it | PASS | PASS |
| II | SOLID, explicit dependencies, testability | Constructor injection only; `ITool` + `AddPrQueue()` makes the addition purely additive — **no existing tool file is edited**; sealed by default; Core testable with no console, network, or filesystem | PASS | PASS |
| III | Security by default (STRIDE + OWASP) | STRIDE table below; every route segment validated and escaped; identity resolved from the server, never typed; no credential material; `NuGetAudit` unchanged and no new package | PASS | PASS |
| IV | Zero-warning builds, enforced analysis | Inherits `Directory.Build.props` unchanged; no new suppression planned; the banned-verb list applies to the new gateway automatically | PASS | PASS |
| V | Spectre.Console-only terminal experience | Only Spectre renders; appears under the existing **Azure DevOps** tab; Back on every list per FR-021 and the shared `NavigationLabels`; cooperative `Ctrl+C`; `Progress` during the sweep; colourless and Unicode-less degradation | PASS | PASS |
| VI | Structured, auditable logging | `ILogger<T>` with source-generated `Log` class; `RunId` scope; per-project read outcomes logged with project and count, **never a title, description, or identity name**; the tool's own value-bearing types registered with `RedactionRegistry` | PASS | PASS |
| VII | Resilience on every outbound call | Reuses the shared typed client and its standard resilience handler; every call a `GET`, so "retries only on idempotent operations" holds by construction | PASS | PASS |
| VIII | Resumable and restartable execution | 5 named steps (R9), checkpoint after each success, four recovery choices, cross-launch resume; every step idempotent because every step reads | PASS | PASS |

**Post-design note**: no principle required a waiver. Principle II deserves a word: this feature is the
first real test of the claim that a second tool is additive. It adds two members to the *shared* layers
(`IProjectCatalog` and a paging helper) — permitted and intended, since shared infrastructure exists to be
shared — while editing **no file under `tools/VarCompare/`**. An architecture test should assert that, so
the claim stays true for tool #3 rather than being re-argued each time.

### STRIDE pass (required by Principle III)

| Threat | Vector in this feature | Mitigation |
|--------|------------------------|------------|
| **S**poofing | The queue is filtered by an identity; a wrong identity silently shows someone else's work, or an empty list read as "nothing to do" | Identity resolved from the authenticated connection, never typed or inferred from the OS (R3); announced before any read (FR-002); unresolvable identity stops the run rather than listing (FR-003). HTTPS-only base URL and redirects disabled, both inherited |
| **T**ampering | *Not reachable.* No write endpoint is expressible: the gateway exposes reads only, and the banned-symbol list fails the build on any verb but `GET` | Structural, inherited from the existing guard; extended by an architecture test over the new gateway |
| **R**epudiation | No record of what was swept | `RunId` scope; each project read logged with its outcome and entry count; retry, breaker, and cancellation transitions logged; never a title or a person's name |
| **I**nformation disclosure | Pull request titles, descriptions, and colleagues' names on screen during a shared call, or worse, in logs and checkpoints | Titles and descriptions never logged and never persisted; checkpoints hold only selected project ids and step progress (R9); the tool's DTOs registered with `RedactionRegistry` so the sink refuses them by type; no stack traces or raw bodies in output |
| **D**enial of service | A sweep over every project in a large collection, multiplied by retries, is the most load this toolbox has ever placed on the server | Bounded parallelism, default 5 (R10); inherited circuit breaker, total timeout, and `Retry-After`; FR-011's remembered subset lets a developer narrow the sweep permanently; cancellation is cooperative and leaves a described state |
| **E**levation of privilege | Seeing pull requests the developer may not read | The developer's own session only — the server returns what their rights allow, and the tool never re-implements permissions. A project that returns `403` is reported as uninspected (FR-013), never silently skipped |

**OWASP touchpoints**: A01 (permissions are the platform's; a `403` becomes a named uninspected project,
not an omission) · A02 (no credential material; TLS defaults untouched) · A03 (no shell-out; every route
segment validated and escaped, project ids are GUIDs) · A05 (DataAnnotations-validated `PrQueueOptions` with
`ValidateOnStart`) · A06 (no new package, `NuGetAudit` unchanged) · A08 (explicit `System.Text.Json`
contracts, no polymorphic deserialization) · A09 (structured run logging) · A10 (SSRF: host fixed by
configuration; only collection, project, and pull request ids vary, all validated).

## Project Structure

### Documentation (this feature)

```text
specs/002-prqueue/
├── plan.md              # This file
├── spec.md              # Feature specification (two clarification sessions)
├── research.md          # Phase 0 — R1–R11 and the open verification items
├── data-model.md        # Phase 1 — entities, states, wire mapping
├── quickstart.md        # Phase 1 — build, run, V1–V8 validation
├── contracts/           # Phase 1
│   ├── azure-devops-rest.md     # external REST surface consumed (GET only)
│   ├── application-ports.md     # interfaces the Core layer owns
│   └── console-experience.md    # shell + rendering + recovery UI contract
├── checklists/
│   └── requirements.md  # spec quality checklist
└── tasks.md             # Phase 2 — created by /speckit-tasks, NOT here
```

### Source Code (repository root)

Only the marked paths are new. Everything else is listed to show what is reused untouched.

```text
src/
├── DevToolbox.Domain/                 # unchanged
├── DevToolbox.Application/
│   └── Abstractions/
│       └── IProjectCatalog.cs         # NEW (R8) — shared project enumeration
├── DevToolbox.Infrastructure/
│   └── AzureDevOps/
│       ├── AzureDevOpsApiReader.cs    # CHANGED — adds a $top/$skip paging helper (R2)
│       ├── ProjectCatalog.cs          # NEW — IProjectCatalog over the shared reader
│       └── ConnectionDataReader.cs    # NEW (R3) — resolves the authenticated identity
├── DevToolbox.Presentation/           # unchanged — NavigationLabels, shell, capabilities reused
└── DevToolbox.Console/
    └── Program.cs                     # CHANGED — two registration lines, nothing else

tools/
├── VarCompare/                        # UNTOUCHED — asserted by an architecture test
└── PrQueue/                           # NEW
    ├── DevToolbox.Tools.PrQueue.Core/
    │   ├── Abstractions/              # IPullRequestGateway, IIdentityResolver, IProjectScopeChooser
    │   ├── Queue/                     # QueueEntry, AwaitedRoute, ReviewVote, BlockingReason,
    │   │                              #   InspectionScope, QueueBuilder
    │   ├── Steps/                     # ResolveIdentity, ListProjects, SelectProjects,
    │   │                              #   ReadPullRequests, BuildQueue
    │   └── PrQueueOptions.cs
    ├── DevToolbox.Tools.PrQueue.Infrastructure/
    │   ├── PullRequestGateway.cs      # over the shared reader; GET only
    │   ├── PullRequestRoutes.cs       # validated, escaped route building
    │   └── Wire/                      # DTOs + mapper, explicit contracts
    └── DevToolbox.Tools.PrQueue.Presentation/
        ├── PrQueueTool.cs             # ITool: Tab = "Azure DevOps"
        ├── Rendering/                 # QueueRenderer, EntryDetailRenderer, StaleRenderer, SymbolSet
        ├── Prompts/                   # ProjectScopePrompt
        └── ServiceCollectionExtensions.cs   # AddPrQueue()

tests/DevToolbox.Tests/
├── Architecture/                      # + "PrQueue adds no edit to VarCompare"; + GET-only over the new gateway
├── Infrastructure/                    # + $skip paging, 403 → uninspected, identity resolution failure
├── Presentation/                      # + no list without identity; colourless states; Back reachable
└── Tools/PrQueue/                     # NEW — queue building, awaited/declined/voted, group route, ordering
```

**Structure Decision**: the per-tool trio, exactly as varcompare established it, because that is the shape
the constitution's Open/Closed requirement produces. The two shared-layer additions are deliberate: project
enumeration and `$skip` paging are now demonstrably needed by more than one tool, which is the evidence that
they belong in the shared layer rather than in a tool. `IProjectCatalog` is added *beside* varcompare's
existing `ListProjectsAsync` rather than replacing it, so this feature edits no existing tool.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Two ways to list projects coexist (`IProjectCatalog` and varcompare's `IVariableGroupGateway.ListProjectsAsync`) | The constitution forbids this feature from editing varcompare, and prqueue must enumerate projects | Migrating varcompare to the shared port would edit an existing tool — the exact thing Principle II exists to prevent. Duplicating the route inside prqueue would put one read, one failure translation, and one paging behaviour in two places. The duplication is therefore deliberate and bounded, and migrating varcompare later is a separate, safe change |
| Client-side reviewer matching instead of the server's `reviewerId` filter | FR-006 requires each entry to say *why* it is in the queue, which a filtered result set cannot express; and group expansion by the server is unverified | The server filter is cheaper and remains the optimisation to revisit if quickstart V4 proves equivalence. Building on it now would mean either dropping a requirement or trusting undocumented behaviour |
| 3 new production projects for one tool | Same justification as varcompare: the inward-only rule must hold *inside* a tool, and only separate assemblies make that mechanically checkable | A single `DevToolbox.Tools.PrQueue` project would have to reference Spectre and `HttpClient` alongside the queue rules, and the architecture test would have nothing to assert |

## Working agreements

- **Read-only means read-only.** The banned-verb list already fails the build on any verb but `GET`; the
  architecture test is extended to cover the new gateway. A queue of reviews sitting one keystroke from a
  vote is the obvious next feature and the one that would end the guarantee — FR-017 and FR-024 make
  refusing it a decision rather than a drift.
- **No existing tool is edited.** Asserted by a test, not by review discipline, so the claim survives tool #3.
- **The empty list is the dangerous output.** Nothing ships until "nothing is waiting" is distinguishable
  from "I could not look" on screen, in every path that can produce either.
- **No Claude attribution in version control, ever.** No `Co-Authored-By`, no session trailer, no
  "Generated with" footer. Work is committed by the user, as the user.

## Open items

All four are environment questions, carried from research with a defined behaviour if the answer is no. None
blocks design; each is settled by a quickstart scenario against the developer's own server.

| # | Question | Settled by | If no |
|---|----------|-----------|-------|
| V-A | Does `_apis/connectionData` expose `authenticatedUser.id` here? | V1 | The tool stops per FR-003; the feature cannot ship without it |
| V-B | Can a non-administrator resolve their own group memberships? | V4 | FR-006a — groups named as uninspected, queue never called complete |
| V-C | Does `searchCriteria.reviewerId` expand groups server-side? | V4 | Nothing changes; a "yes" only unlocks a future optimisation |
| V-D | How many projects and active pull requests does this collection hold? | V2 | FR-011's remembered subset is the mitigation, not a redesign |
