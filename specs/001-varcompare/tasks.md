---
description: "Task list for varcompare — Azure DevOps Variable Group Comparison (read-only)"
---

# Tasks: varcompare — Azure DevOps Variable Group Comparison (read-only)

**Input**: Design documents from `/specs/001-varcompare/`

**Prerequisites**: [plan.md](./plan.md) (revision 2) · [spec.md](./spec.md) · [research.md](./research.md) ·
[data-model.md](./data-model.md) · [contracts/](./contracts/) · [quickstart.md](./quickstart.md) ·
[.specify/memory/constitution.md](../../.specify/memory/constitution.md)

**Tests**: **Included.** Not an optional extra here — the spec states success criteria that are only
verifiable by test (SC-003 colourless states, SC-004 zero secrets in logs, SC-006 GET-only, SC-007 zero
credential prompts, SC-008 no values in recovery state, SC-011 stacked-layout completeness, SC-012 no values
in the comparison), the contracts say "asserted by test" against named guarantees, and constitution
Principle I requires a mechanically enforced layering rule. Those tests are the enforcement mechanism, not
documentation of it.

**Organization**: grouped by user story so each is independently implementable and testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: `[US1]` / `[US2]` / `[US3]`, mapping to the user stories in spec.md
- Every task names the exact file path it touches

## Path Conventions

This is a **multi-project .NET solution** (plan.md → Project Structure). Paths are repository-relative:

- Shared spine: `src/DevToolbox.{Domain,Application,Infrastructure,Presentation,Console}/`
- Per-tool trio: `tools/VarCompare/DevToolbox.Tools.VarCompare.{Core,Infrastructure,Presentation}/`
- Tests: one project, `tests/DevToolbox.Tests/`, folder-per-area

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: the repository contains only Spec Kit artifacts today, so this phase creates the solution itself.

- [X] T001 Create the solution file `DevToolbox.sln` at the repository root
- [X] T002 [P] Create `Directory.Build.props` with `net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`, `EnableNETAnalyzers true`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild true`, `GenerateDocumentationFile true`, `NuGetAudit true`, `NuGetAuditMode all`, `NuGetAuditLevel low`, `NoWarn NU1507`, and `InvariantGlobalization true` — the constitution requires this to be explicitly decided per project, and `true` is correct here because variable-name matching is `OrdinalIgnoreCase` and nothing in the tool is culture-sensitive (research R14, constitution Technology Stack)
- [X] T003 [P] Create `Directory.Packages.props` with `ManagePackageVersionsCentrally true` and pinned versions: Spectre.Console, `Microsoft.Extensions.Hosting`/`DependencyInjection`/`Http`/`Options.DataAnnotations`/`Logging`, `Microsoft.Extensions.Http.Resilience` 10.9.0
- [X] T004 [P] Create `.editorconfig` with the C# style rules enforced by `EnforceCodeStyleInBuild`, including the project-wide `ConfigureAwait` policy the constitution requires to be declared there — `ConfigureAwait(false)` in every library assembly, unconstrained in `DevToolbox.Console` (constitution Technology Stack)
- [X] T005 [P] Create `.gitignore` covering `bin/`, `obj/`, `.vs/`, `TestResults/`, and local `appsettings.*.json` overrides
- [X] T006 [P] Create `src/DevToolbox.Domain/DevToolbox.Domain.csproj` with **zero package references** (Principle I)
- [X] T007 [P] Create `src/DevToolbox.Application/DevToolbox.Application.csproj`
- [X] T008 [P] Create `src/DevToolbox.Infrastructure/DevToolbox.Infrastructure.csproj`
- [X] T009 [P] Create `src/DevToolbox.Presentation/DevToolbox.Presentation.csproj`
- [X] T010 [P] Create `src/DevToolbox.Console/DevToolbox.Console.csproj` as the composition root (`OutputType Exe`)
- [X] T011 [P] Create `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/DevToolbox.Tools.VarCompare.Core.csproj` (no HTTP, no Spectre)
- [X] T012 [P] Create `tools/VarCompare/DevToolbox.Tools.VarCompare.Infrastructure/DevToolbox.Tools.VarCompare.Infrastructure.csproj`
- [X] T013 [P] Create `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/DevToolbox.Tools.VarCompare.Presentation.csproj`
- [X] T014 [P] Create `tests/DevToolbox.Tests/DevToolbox.Tests.csproj` with folders `Architecture/`, `Application/`, `Infrastructure/`, `Presentation/`, `Tools/VarCompare/`
- [X] T015 Wire the inward-only `ProjectReference` graph across all nine `.csproj` files and add every project to `DevToolbox.sln` — Application→Domain; Infrastructure→Application; Presentation→Application; Console→everything; VarCompare.Core→Application+Domain; VarCompare.Infrastructure→Core+Infrastructure; VarCompare.Presentation→Core+Presentation
- [X] T016 [P] Add analyzer packages (`Meziantou.Analyzer`, `AsyncFixer`, `SonarAnalyzer.CSharp`, `Microsoft.CodeAnalysis.BannedApiAnalyzers`, all `PrivateAssets=all`) to `Directory.Packages.props` and reference them from `Directory.Build.props` (research R14, constitution Principles V and VII)
- [X] T016a [P] Create `BannedSymbols.txt` at the repository root banning `M:System.Net.Http.HttpClient.#ctor` and every overload (Principle VII — typed clients from `IHttpClientFactory` only, `new HttpClient()` is a violation) and `T:System.Console` (Principle V — direct console reads and writes are forbidden outside the guarded bootstrap fallback), and wire it as `AdditionalFiles` in `Directory.Build.props`
- [X] T017 [P] Add the test stack to `Directory.Packages.props` and `tests/DevToolbox.Tests/DevToolbox.Tests.csproj`: xUnit, NSubstitute, **FluentAssertions 7.2.2** (8.x is commercially licensed), NetArchTest.Rules, coverlet, Spectre.Console.Testing
- [X] T018 Resolve research **R9 open item** — choose the rolling-file log sink (Serilog confined to the composition root, or a small custom `ILoggerProvider` adding no dependency), record the decision and rationale in `specs/001-varcompare/research.md` R9, and pin the package in `Directory.Packages.props` if one is chosen
- [X] T019 [P] Create `src/DevToolbox.Console/appsettings.json` with the `AzureDevOpsServer` section (`BaseUrl`, `Collection`, `ApiVersion` 7.1, `AllowInsecureHttp` false, `HttpTimeoutSeconds` 30, `MaxDegreeOfParallelism` 5) and the resilience settings (research R3, R8)

**Checkpoint**: `dotnet build` succeeds on an empty solution with warnings-as-errors active.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the shared spine every tool sits on — layers, ports, the Azure DevOps client, storage, logging,
the Spectre shell, and the composition root.

**⚠️ CRITICAL**: no user story work can begin until this phase is complete.

### Domain (shared kernel, zero package references)

- [X] T020 [P] Implement `Result` and `Result<T>` with typed failure reasons (`AuthenticationFailed`, `PermissionDenied`, `GroupNotFound`, `ServiceUnavailable`, `ServerUnreachable`, `ServerUntrusted`, `RedirectRefused`, `Cancelled`) in `src/DevToolbox.Domain/Results/Result.cs`
- [X] T021 [P] Implement `ToolRun` (`RunId`, `ToolId`, `Steps`, `StartedAt`, `EndedAt?`) and `RunOutcome` (`Success`/`Failed`/`Cancelled`/`Abandoned`) in `src/DevToolbox.Domain/Runs/ToolRun.cs`
- [X] T022 [P] Implement `StepState` and `StepStatus` with the `Pending → Running → (Completed | Failed)` transition guard in `src/DevToolbox.Domain/Runs/StepState.cs` (data-model §4)
- [X] T023 [P] Implement `ToolRunContext` carrying run identity and per-step scratch state in `src/DevToolbox.Domain/Runs/ToolRunContext.cs`

### Application ports (owned inward, implemented outward)

- [X] T024 [P] Define `ITool` (`Id`, `DisplayName`, `Tab`, `Description`, `RunAsync`) in `src/DevToolbox.Application/Abstractions/ITool.cs` (contracts → shell contract)
- [X] T025 [P] Define `IToolStep` (including `bool IsIdempotent { get; }`) and `IStepRunner` in `src/DevToolbox.Application/Abstractions/IStepRunner.cs` — every varcompare step returns `true` since all are reads, but Principle VIII requires a step that cannot be safely repeated to declare itself non-resumable, and tool #2 will have such steps
- [X] T026 [P] Define `IRunCheckpointStore` (`SaveAsync`, `ListResumableAsync`, `DiscardAsync`, `SweepExpiredAsync`) and the `PersistedRun` record in `src/DevToolbox.Application/Abstractions/IRunCheckpointStore.cs` — `PersistedRun` must be structurally unable to reach a variable value
- [X] T027 [P] Define `IClock` in `src/DevToolbox.Application/Abstractions/IClock.cs`
- [X] T028 [P] Define `IRecoveryPrompt` and `RecoveryChoice` (`RetryStep`, `ContinueFromStep`, `RestartAll`, `Abort`) in `src/DevToolbox.Application/Abstractions/IRecoveryPrompt.cs`
- [X] T029 [P] Define `ITargetAnnouncer` and the `TargetAnnouncement` record (`ServerHost`, `Collection`, constant `AuthenticationMode`) in `src/DevToolbox.Application/Abstractions/ITargetAnnouncer.cs` — **no credential, token, or sign-in port exists** (contracts, SC-007)

### Shared Azure DevOps infrastructure (tool #2 inherits this)

- [X] T030 Implement `AzureDevOpsServerOptions` with DataAnnotations validation and a custom validator rejecting non-HTTPS `BaseUrl` unless `AllowInsecureHttp` is explicitly true, in `src/DevToolbox.Infrastructure/AzureDevOps/AzureDevOpsServerOptions.cs` — the validation failure message MUST name the missing or invalid setting and the full path of the settings file, so a misconfigured install fails readably rather than as a raw options exception (FR-031a, research R3, spec edge case "Server not configured")
- [X] T031 Implement the primary handler factory setting `UseDefaultCredentials = true`, `PreAuthenticate = true`, `AllowAutoRedirect = false` in `src/DevToolbox.Infrastructure/AzureDevOps/IntegratedAuthHandlerFactory.cs` (FR-028a/d, research R1)
- [X] T032 Implement the typed `AzureDevOpsClient` with base address `{BaseUrl}/` (trailing slash enforced) in `src/DevToolbox.Infrastructure/AzureDevOps/AzureDevOpsClient.cs` — certificate validation is **never** configurable (FR-031b)
- [X] T033 Configure `AddStandardResilienceHandler()` bound from `appsettings.json` (total timeout 30 s, attempt timeout 10 s, 3 retries, breaker sampling 30 s / ratio 0.2 / min throughput 5, `Retry-After` honoured) in `src/DevToolbox.Infrastructure/AzureDevOps/ResilienceConfiguration.cs` (Principle VII, FR-036–FR-038, research R8)
- [X] T034 Implement `AzureDevOpsApiReader` — `GET` only, `HttpCompletionOption.ResponseHeadersRead`, `System.Text.Json` with explicit options and no polymorphism, following `x-ms-continuationtoken` to exhaustion so a partial list is a failure not a truncated success — in `src/DevToolbox.Infrastructure/AzureDevOps/AzureDevOpsApiReader.cs` (FR-016, research R4)
- [X] T035 Implement the error mapper translating status codes and transport failures into typed `Result` failures with no status code, URL, or response body escaping, in `src/DevToolbox.Infrastructure/AzureDevOps/AzureDevOpsErrorMapper.cs` (contracts/azure-devops-rest.md §5)
- [X] T036 Implement `ServiceCollectionExtensions.AddAzureDevOps(...)` registering options with `ValidateOnStart`, the typed client, the handler, and the resilience pipeline in `src/DevToolbox.Infrastructure/AzureDevOps/ServiceCollectionExtensions.cs`

### Shared storage and system adapters

- [X] T037 [P] Implement `SystemClock : IClock` in `src/DevToolbox.Infrastructure/System/SystemClock.cs`
- [X] T038 Implement `FileRunCheckpointStore : IRunCheckpointStore` over `%LOCALAPPDATA%\DevToolbox\runs\{runId}.json` with a runtime guard rejecting any `PersistedRun` carrying a variable value, plus `SweepExpiredAsync`, in `src/DevToolbox.Infrastructure/Storage/FileRunCheckpointStore.cs` (FR-039a/c, research R10)
- [X] T039 [P] Implement `SettingsStore` over `%APPDATA%\DevToolbox\settings.json` (last collection/project, layout preference) in `src/DevToolbox.Infrastructure/Storage/SettingsStore.cs`
- [X] T040 [P] Implement the path-confinement helper resolving through `Environment.GetFolderPath` and rejecting paths escaping the DevToolbox root in `src/DevToolbox.Infrastructure/System/AppPaths.cs` (Principle III)
- [X] T041 Implement `ServiceCollectionExtensions.AddInfrastructureServices(...)` for clock, storage, and paths in `src/DevToolbox.Infrastructure/ServiceCollectionExtensions.cs`

### Logging

- [X] T042 [P] Implement the source-generated `Log` class (`LoggerMessage` partial methods) for infrastructure events in `src/DevToolbox.Infrastructure/Internal/Log.cs` (Principle VI, research R9)
- [X] T043 Implement the rolling file sink chosen in T018 under `%LOCALAPPDATA%\DevToolbox\logs\` with 7-day retention, console logging off by default and routed through Spectre when enabled, in `src/DevToolbox.Infrastructure/Logging/FileLoggingExtensions.cs` (FR-043)
- [X] T043a Implement a redacting decorator over the file logger provider enforcing redaction **at the sink boundary**, independently of caller discipline — any log-state argument whose type is `VariableEntry`, `VariableGroupSnapshot`, `VariableDetail`, or a collection of them is replaced with `[redacted:{TypeName}]` before formatting, and the substitution is itself logged as a defect — in `src/DevToolbox.Infrastructure/Logging/RedactingLoggerProvider.cs` (Principle VI, Principle III, FR-030)
- [X] T044 Implement the logging scope helper attaching `RunId`, `ToolId`, `Collection`, `Project` to every line of a run in `src/DevToolbox.Application/Runs/RunLoggingScope.cs` (FR-041)

### Spectre shell (shared, Principle V)

- [X] T045 Implement the shell `Layout` with a `Panel` tab strip built by enumerating registered `ITool.Tab` values, and a footer showing `server ▸ collection`, in `src/DevToolbox.Presentation/Shell/ShellLayout.cs` (contracts/console-experience.md §1, FR-028c)
- [X] T046 Implement menu navigation with `SelectionPrompt<T>` — **Exit** at root, **Back** in every sub-menu — in `src/DevToolbox.Presentation/Shell/ShellNavigator.cs` (FR-001)
- [X] T047 Implement cooperative `Ctrl+C` handling (intercept once, `e.Cancel = true`, trip a `CancellationTokenSource`, flush checkpoints, dispose sinks, documented non-zero exit code) in `src/DevToolbox.Presentation/Shell/CancellationHook.cs`
- [X] T048 Implement capability detection and the non-interactive guard (`AnsiConsole.Profile.Capabilities`; one explanatory line then a clean exit when input is unavailable, never a hang) in `src/DevToolbox.Presentation/Shell/ConsoleCapabilities.cs`
- [X] T048a Add narrowly scoped, inline-justified `#pragma warning disable RS0030` around the only two sanctioned `System.Console` uses — the non-interactive bootstrap fallback and the `Ctrl+C` hook — in `src/DevToolbox.Presentation/Shell/ConsoleCapabilities.cs` and `src/DevToolbox.Presentation/Shell/CancellationHook.cs`; every other use stays banned by T016a (Principle V, Principle IV)
- [X] T049 Implement `SpectreTargetAnnouncer : ITargetAnnouncer` rendering the server, collection, and "integrated Windows authentication" before any read in `src/DevToolbox.Presentation/Shell/SpectreTargetAnnouncer.cs` (FR-028c)

### Runner and composition root

- [X] T050 Implement `SequentialStepRunner : IStepRunner` — happy path only: run named steps in order, checkpoint after each success, report progress (failure handling arrives in US3) — in `src/DevToolbox.Application/Runs/SequentialStepRunner.cs` (FR-033)
- [X] T050a [P] Step runner success-path test asserting steps execute in declared order, each checkpoints exactly once on success, and the run ends `Success` with a recorded duration, in `tests/DevToolbox.Tests/Application/SequentialStepRunnerTests.cs` (constitution Quality Gate 2 — "step success")
- [X] T051 Implement `Program.cs` with the generic host, DI wiring, options `ValidateOnStart`, log sink wiring, `ITool` discovery, and documented exit codes in `src/DevToolbox.Console/Program.cs`
- [X] T051a Implement the first-run server configuration prompt — read the saved base URL and collection from the per-user settings file; when absent, prompt with the `appsettings.json` values shown as defaults, validate the answer, save it, and feed it into configuration before the host is built — in `src/DevToolbox.Presentation/Shell/FirstRunConfigurationPrompt.cs` (spec clarification 2026-08-20, spec edge case "Server not configured", FR-031a)
- [X] T052 [P] Write NetArchTest architecture tests — Domain has zero package references, Application references neither Infrastructure nor Presentation, Spectre types appear only in Presentation assemblies, `HttpClient` appears only in Infrastructure — in `tests/DevToolbox.Tests/Architecture/LayeringTests.cs` (Principle I)

**Checkpoint**: the shell launches, announces its target, shows an empty tool list, and exits cleanly. Layering is mechanically enforced.

---

## Phase 3: User Story 1 - Compare variable groups side by side (Priority: P1) 🎯 MVP

**Goal**: from launch to a colour-coded side-by-side comparison table of two or more selected groups — one row
per distinct variable name, one column per group, every cell carrying exactly one state.

**Independent Test**: point the tool at a project with two groups containing deliberate drift (one variable in
both, one missing from the second, one present but empty). Every distinct name appears exactly once and each
cell's state matches the real group contents (quickstart V1).

### Tests for User Story 1

> Write these first and confirm they fail before implementing.

- [X] T053 [P] [US1] Comparison-building tests — union of names, `OrdinalIgnoreCase` matching, exactly one state per cell, fewer than two groups rejected, and a group with zero variables rendering as an entirely `Absent` column rather than an error — in `tests/DevToolbox.Tests/Tools/VarCompare/ComparisonBuilderTests.cs` (FR-004, FR-006, FR-007, FR-008, spec edge case "Empty group")
- [X] T054 [P] [US1] Gateway tests over `FakeHttpMessageHandler` covering the three routes, `{ count, value }` shapes, and continuation-token paging followed to exhaustion, in `tests/DevToolbox.Tests/Infrastructure/VariableGroupGatewayTests.cs`
- [X] T055 [P] [US1] Working-folder inspector tests with `.git/config` fixtures for HTTPS, user-info, port + virtual-directory, and SSH remote forms, plus multiple-remote and foreign-host rejection, in `tests/DevToolbox.Tests/Infrastructure/WorkingFolderInspectorTests.cs` (FR-002a/d, research R11)
- [X] T056 [P] [US1] `TestConsole` test asserting the side-by-side table renders every row and column and **no variable value**, in `tests/DevToolbox.Tests/Presentation/SideBySideRendererTests.cs` (FR-015a, SC-012)
- [X] T057 [P] [US1] Architecture test failing the build if any HTTP method other than `GET` appears in the gateway implementation, in `tests/DevToolbox.Tests/Architecture/ReadOnlyVerbTests.cs` (FR-018b, SC-006)

### Core model (pure, no I/O)

- [X] T058 [P] [US1] Implement `ServerTarget` with `ToCollectionSegment()` as the only sanctioned way to place the collection in a URL, in `src/DevToolbox.Domain/AzureDevOps/ServerTarget.cs` — **shared kernel, not tool-local**, so `GovernanceAuditor` inherits it as tool #2 (data-model §1, plan.md Structure Decision)
- [X] T059 [P] [US1] Implement `ProjectIdentifier` with `ToRouteSegment()` returning `Id` when known, else `Uri.EscapeDataString(Name)`, in `src/DevToolbox.Domain/AzureDevOps/ProjectIdentifier.cs` — **shared kernel**, same reason as T058 (FR-031c)
- [X] T060 [P] [US1] Implement `RepositoryOrigin` (`Collection`, `Project`, `RepositoryName`, `RemoteName`, `SourceUrl`) in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Targets/RepositoryOrigin.cs`
- [X] T061 [P] [US1] Implement `VariableGroupSummary` and `VariableGroupOrigin` (`Ordinary` / `KeyVaultBacked`) in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Groups/VariableGroupSummary.cs`
- [X] T062 [P] [US1] Implement `VariableEntry` (`Name`, `Value?`, `IsSecret`, `IsReadOnly`, derived `IsEmpty`) in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Groups/VariableEntry.cs`
- [X] T063 [P] [US1] Implement `VariableGroupSnapshot` with an `OrdinalIgnoreCase` dictionary and `RetrievedAt` from the injected clock, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Groups/VariableGroupSnapshot.cs` — in-memory only, never serialized
- [X] T064 [P] [US1] Implement the `CellState` enum (`Absent`, `PresentWithValue`, `PresentEmpty`, `PresentSecret`, `Undetermined`) in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/CellState.cs` (FR-008)
- [X] T065 [P] [US1] Implement `ComparisonCell` (`State`, `IsKeyVaultSourced`, `IsReadOnly`, `NameAsStored`) — **carrying no value** — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonCell.cs` (FR-015a)
- [X] T066 [P] [US1] Implement `ComparisonRow` (`CanonicalName`, `DisplayName`, `Cells`, `HasCasingDivergence`, `ReadableValuesDiffer`, `IsPresentEverywhere`) in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonRow.cs`
- [X] T067 [P] [US1] Implement `ComparisonSummary` with `TotalVariables`, `PresentEverywhere`, `MissingSomewhere` in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonSummary.cs` (the remaining counts arrive in US2)
- [X] T068 [US1] Implement the `VariableComparison` aggregate root (`Groups` in selection order, `Rows` sorted canonically, `Summary`, `RetrievedAt` = earliest snapshot) in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/VariableComparison.cs`
- [X] T069 [US1] Define `IVariableGroupGateway` with `ListProjectsAsync`, `ListGroupsAsync`, `GetGroupAsync` — **retrieval only, no member able to express a change** — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Abstractions/IVariableGroupGateway.cs` (FR-018a, FR-018b, contracts/application-ports.md)
- [X] T069a [P] [US1] Define `IWorkingFolderInspector` returning `Result<RepositoryOrigin?>` from `DetectOrigin(string startDirectory)` in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Abstractions/IWorkingFolderInspector.cs` — the port is owned inward and implemented by T075 (FR-002a/d, contracts/application-ports.md)
- [X] T070 [US1] Implement `ComparisonBuilder` producing the union of names, the canonical/display name split, and the state derivation (missing key ⇒ `Absent`; `isSecret` ⇒ `PresentSecret`; `""` ⇒ `PresentEmpty`; non-empty ⇒ `PresentWithValue`), in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonBuilder.cs` (research R4, R7)

### Tool infrastructure

- [X] T071 [P] [US1] Implement the wire DTOs (`VariableGroupDto`, `VariableDto`, `ProjectDto`, `ListResponse<T>`) with explicit `System.Text.Json` contracts and unknown fields ignored, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Infrastructure/Wire/VariableGroupDto.cs` (data-model §6)
- [X] T072 [US1] Implement the DTO→domain mapper including `type` ⇒ `VariableGroupOrigin` and the deliberate non-mapping of `variableGroupProjectReferences`, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Infrastructure/Wire/VariableGroupMapper.cs`
- [X] T073 [US1] Implement `VariableGroupGateway : IVariableGroupGateway` over the shared `AzureDevOpsApiReader` calling the three `GET` routes, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Infrastructure/VariableGroupGateway.cs` (contracts/azure-devops-rest.md §1–3)
- [X] T074 [US1] Implement the route builder validating and escaping every segment (collection 1–64 chars, no separators, no `..`; project as `Guid` or escaped name; collection taken relative to the configured base URL so a `/tfs` virtual directory is not mistaken for it), in `tools/VarCompare/DevToolbox.Tools.VarCompare.Infrastructure/VariableGroupRoutes.cs` (FR-031c, research R11)
- [X] T075 [US1] Implement `WorkingFolderInspector : IWorkingFolderInspector` (port from T069a) — walk up for `.git`, parse `.git/config` as INI (no process spawn), accept a remote only when its host matches the configured server, return `None` when absent, ambiguous, or foreign — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Infrastructure/WorkingFolderInspector.cs` (FR-002a/d)

### Steps

- [X] T076 [US1] Implement `IdentifyProjectStep` — detected origin shown for confirmation, otherwise the accessible project list, with override available at any point — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Steps/IdentifyProjectStep.cs` (FR-002a/b/c)
- [X] T077 [US1] Implement `ListGroupsStep` retrieving readable groups with their key-vault marker in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Steps/ListGroupsStep.cs` (FR-003)
- [X] T078 [US1] Implement `SelectGroupsStep` enforcing ≥2 with an explanation and preserving earlier choices on rejection, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Steps/SelectGroupsStep.cs` (FR-004)
- [X] T079 [US1] Implement `RetrieveGroupStep` with bounded parallelism (`MaxDegreeOfParallelism`, default 5) and per-group progress reporting, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Steps/RetrieveGroupStep.cs` (SC-005)
- [X] T080 [US1] Implement `BuildComparisonStep` invoking `ComparisonBuilder` over the retrieved snapshots in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Steps/BuildComparisonStep.cs`

### Presentation and registration

- [X] T081 [US1] Implement the project confirmation and selection prompt (detected collection/project with remote name and source URL shown; searchable project list otherwise) in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Prompts/ProjectPrompt.cs` (FR-002)
- [X] T082 [US1] Implement the group list with a name filter and ordinary/key-vault markers in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Prompts/GroupListPrompt.cs` (FR-003, FR-005)
- [X] T083 [US1] Implement the multi-select prompt with unambiguous column identity for duplicate group names in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Prompts/GroupSelectionPrompt.cs` (FR-004)
- [X] T084 [US1] Implement `SideBySideRenderer` — Spectre `Table`, one column per group, cells as symbol + short text with colour as reinforcement only — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/SideBySideRenderer.cs` (FR-012, contracts/console-experience.md §3)
- [X] T085 [US1] Implement the legend as a Spectre `Grid` rendered beneath every comparison, plus the stated total row count and the "as at" read time, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/LegendRenderer.cs` (FR-013, FR-016)
- [X] T086 [US1] Implement the empty-result messages — no readable groups in the project (a permission or empty distinction, not a crash), and only one group available — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/EmptyStateRenderer.cs` (US1 scenarios 4–5, FR-029)
- [X] T087 [US1] Implement `VarCompareTool : ITool` (`Id` `"varcompare"`, `Tab` `"Azure DevOps"`) orchestrating the five steps through `IStepRunner`, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/VarCompareTool.cs`
- [X] T088 [US1] Implement `ServiceCollectionExtensions.AddVarCompare(...)` registering the gateway, steps, renderers, and the tool, and call it from `src/DevToolbox.Console/Program.cs`, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/ServiceCollectionExtensions.cs` (research R15)
- [X] T089 [US1] Implement the source-generated `Log` class emitting run start with the tool id and assembly version, run end with outcome and duration, step transitions, and per-group retrieval (collection, project, group name and id, outcome — **never a value**) in `tools/VarCompare/DevToolbox.Tools.VarCompare.Infrastructure/Internal/Log.cs` (FR-041, FR-042, Principle VI)

**Checkpoint**: User Story 1 is fully functional — quickstart V1 passes end to end. **This is the MVP.**

---

## Phase 4: User Story 2 - Understand the state and origin of each variable (Priority: P2)

**Goal**: make the table interpretable — key-vault provenance, secret and read-only markers, a differences-only
filter, an on-demand detail view, and the layouts that keep all of it legible at any group count.

**Independent Test**: compare one ordinary group containing a secret and a read-only variable against a
key-vault-backed group. Origin, secret, and read-only indicators are correct, and no secret value is ever
displayed (quickstart V2, V3).

**Scope note**: FR-016 (layout switching and scale) sits here rather than in US1. US1 delivers the grid; this
story delivers everything needed to keep the grid honest as it grows, which is what SC-011 measures.

### Tests for User Story 2

- [X] T090 [P] [US2] Marker tests — key-vault column label and `[kv]` on every entry, `PresentSecret` rendering with no value, `[ro]` on read-only entries — in `tests/DevToolbox.Tests/Presentation/QualifierRenderingTests.cs` (FR-009, FR-010, US2 scenarios 1, 2, 4)
- [X] T091 [P] [US2] Differences-only filter tests asserting identical rows are hidden and the hidden count is stated, in `tests/DevToolbox.Tests/Tools/VarCompare/DifferencesOnlyTests.cs` (FR-014)
- [X] T092 [P] [US2] Detail view tests — values appear only after an explicit open, secrets and key-vault entries state "value exists — not retrievable", and no value reaches a logger or the checkpoint store — in `tests/DevToolbox.Tests/Presentation/VariableDetailTests.cs` (FR-015b/c/d, SC-012)
- [X] T093 [P] [US2] Stacked-layout parity tests asserting identical states, symbols, legend, filtering, and paging behaviour, with every variable and every group still present, in `tests/DevToolbox.Tests/Presentation/StackedRendererTests.cs` (FR-016b/c/e, SC-011)
- [X] T094 [P] [US2] Colourless-profile test classifying 100% of cells with colour disabled, in `tests/DevToolbox.Tests/Presentation/ColourlessRenderingTests.cs` (FR-012, SC-003)

### Implementation for User Story 2

- [X] T095 [US2] Extend state derivation with `Undetermined` for degraded retrieval — unreachable key vault, partially readable group — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonBuilder.cs` (FR-008)
- [X] T096 [US2] Implement `ReadableValuesDiffer` and `HasCasingDivergence` computation in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonBuilder.cs` (FR-011, R7)
- [X] T097 [US2] Extend `ComparisonSummary` with `DifferingValues`, `SecretCount`, `KeyVaultSourcedCount`, `ReadOnlyCount` in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonSummary.cs`
- [X] T098 [US2] Implement `VariableDetail` and its per-group projection built from in-memory snapshots only, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/VariableDetail.cs` (FR-015b, data-model §3)
- [X] T099 [US2] Implement `ComparisonViewOptions` (`Layout`: `SideBySide`/`Stacked`/`Auto`; `DifferencesOnly`) and the row filter reporting the hidden count, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonViewOptions.cs` (FR-014)
- [X] T099a [US2] Define `IComparisonPresenter` (`Render`, `RenderLegend`, `RenderDetail`) in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Abstractions/IComparisonPresenter.cs` — the port is owned inward and implemented by the Spectre layer in T104 (contracts/application-ports.md, FR-015b)
- [X] T100 [US2] Render the key-vault column label and the `[kv]` qualifier on every cell of a key-vault-backed group in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/SideBySideRenderer.cs` (FR-009)
- [X] T101 [US2] Render the `[ro]` qualifier, the `≠` row-header marker for differing readable values, and the `~` casing-divergence marker with its footnote, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/CellRenderer.cs` (FR-010, FR-011)
- [X] T102 [US2] Implement `StackedRenderer` as Spectre `Rows` of `Panel`s, one per variable with its per-group states beneath, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/StackedRenderer.cs` (FR-016b)
- [X] T103 [US2] Implement `LayoutSelector` computing the legibility threshold from `AnsiConsole.Profile.Width` against measured column widths — not a hardcoded group count — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/LayoutSelector.cs` (FR-016a, research R12)
- [X] T104 [US2] Implement the warn-before-switch notice and the manual layout toggle available at any group count, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/ComparisonPresenter.cs` (FR-016b/d)
- [X] T104a [US2] Implement comparison paging for long results — page height derived from `AnsiConsole.Profile.Height`, a "rows X–Y of N" indicator, forward/back navigation, and a row-name filter — with the legend and column headers repeated on every page so a 500-row comparison stays readable without relying on terminal scrollback, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/ComparisonPager.cs` (SC-005 "remains navigable", FR-013, spec edge case "Large groups")
- [X] T105 [US2] Implement the detail view renderer with safe handling of long and multi-line values and an explicit shortening indicator, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/VariableDetailRenderer.cs` (FR-015c, FR-017)
- [X] T106 [US2] Handle partial access — readable groups still compared, unreadable columns marked `Undetermined` rather than shown as empty — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Steps/RetrieveGroupStep.cs` (spec edge case "Partial access", FR-029)
- [X] T107 [US2] Render the case-insensitive-collision warning naming the divergent spellings in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/ComparisonPresenter.cs` (spec edge case "Case-insensitive names")
- [X] T108 [US2] Implement the refresh action re-reading the selected groups, with the "as at" time updated, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/VarCompareTool.cs` (spec edge case "Group changed while being compared", research R6)
- [X] T109 [US2] Implement ASCII symbol fallback for profiles lacking Unicode in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/SymbolSet.cs` (contracts/console-experience.md §3)
- [X] T110 [US2] Implement the FR-016e guard — when neither layout can show everything, say so rather than render a partial result — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/ComparisonPresenter.cs`

**Checkpoint**: User Stories 1 and 2 both work independently — quickstart V2 and V3 pass.

---

## Phase 5: User Story 3 - Recover an interrupted run (Priority: P3)

**Goal**: a failed step offers retry / continue / restart / abort; continuing reuses what was already
retrieved; a run interrupted across a restart is offered for resumption and re-read rather than restored.

**Independent Test**: induce a failure on one group retrieval mid-run. All four options are offered in order,
*continue* re-fetches nothing already retrieved, a resume after closing the toolbox re-reads the groups, and
the persisted run file contains zero variable values (quickstart V5).

### Tests for User Story 3

- [X] T111 [P] [US3] Step runner failure tests asserting the four recovery choices are offered in the contract order, in `tests/DevToolbox.Tests/Tools/VarCompare/RecoveryChoiceTests.cs` (FR-034)
- [X] T112 [P] [US3] Continue-semantics test asserting no already-retrieved group is fetched again within a session, in `tests/DevToolbox.Tests/Tools/VarCompare/ContinueFromStepTests.cs` (FR-035, SC-008)
- [X] T112a [P] [US3] Full-restart test asserting *restart* returns every step to `Pending`, discards all held snapshots, and re-retrieves every group on the subsequent pass, in `tests/DevToolbox.Tests/Tools/VarCompare/RestartAllTests.cs` (constitution Quality Gate 2 — "full restart", FR-034)
- [X] T113 [P] [US3] Test inspecting the **serialized** `PersistedRun` JSON and failing if any field holds a variable value, in `tests/DevToolbox.Tests/Infrastructure/PersistedRunPrivacyTests.cs` (FR-039a, SC-008)
- [X] T114 [P] [US3] Expiry test backdating a run file beyond 7 days and asserting it is swept at startup, in `tests/DevToolbox.Tests/Infrastructure/CheckpointExpiryTests.cs` (FR-039c)
- [X] T115 [P] [US3] Resilience tests over `FakeHttpMessageHandler` for retry exhaustion, circuit-breaker-open, `Retry-After` honouring, and `401`/`403`/`3xx` mapping, in `tests/DevToolbox.Tests/Infrastructure/ResiliencePipelineTests.cs` (FR-036–FR-038, contracts §5)
- [X] T115a [P] [US3] Failure-rendering test asserting each of the eight mapped outcomes produces a distinct actionable message, that `ServerUnreachable` and `PermissionDenied` are not confusable, and that no message contains a status code, URL, response body, or stack trace, in `tests/DevToolbox.Tests/Presentation/FailureRendererTests.cs` (FR-029, FR-032, Principle III, Principle VII)

### Implementation for User Story 3

- [X] T116 [US3] Extend `SequentialStepRunner` with failure handling that consults `IRecoveryPrompt` and acts on the choice, in `src/DevToolbox.Application/Runs/SequentialStepRunner.cs` (FR-034)
- [X] T117 [US3] Implement *continue* semantics reusing in-session snapshots without re-fetching, in `src/DevToolbox.Application/Runs/SequentialStepRunner.cs` (FR-035)
- [X] T118 [US3] Implement *restart* resetting all steps to `Pending` and discarding held snapshots, in `src/DevToolbox.Application/Runs/SequentialStepRunner.cs`
- [X] T119 [US3] Implement *abort* setting `Outcome = Abandoned` and stating which groups had been read and which had not, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/AbortSummaryRenderer.cs` (FR-040)
- [X] T120 [US3] Checkpoint after each successful step through `IRunCheckpointStore`, persisting only collection, project, selected group ids and names, and completed step names, in `src/DevToolbox.Application/Runs/SequentialStepRunner.cs` (FR-039a)
- [X] T121 [US3] Implement the resume-or-discard prompt offered at launch for an interrupted run, in `src/DevToolbox.Presentation/Shell/ResumePrompt.cs` (FR-039)
- [X] T122 [US3] Implement resume behaviour that **re-reads** the chosen groups rather than presenting remembered contents, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/VarCompareTool.cs` (FR-039b)
- [X] T123 [US3] Handle a remembered group that no longer exists or is no longer readable — continue with the remainder or change the selection — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/VarCompareTool.cs` (FR-039d)
- [X] T124 [US3] Render retry progress ("retrying 2/3…") through Spectre `Progress`/`Status` so a retry never looks frozen, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/RetrievalProgress.cs` (FR-036)
- [X] T125 [US3] Render the throttling notice while `Retry-After` is being honoured, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/RetrievalProgress.cs` (FR-037)
- [X] T126 [US3] Implement `FailureRenderer` covering every outcome in the error contract — `AuthenticationFailed` (terminal for the run; "the server rejected your Windows credentials"; names server and collection; **no credential prompt, ever**), `PermissionDenied`, `GroupNotFound`, `ServiceUnavailable` (breaker open or retry budget exhausted, with an actionable next step), `ServerUnreachable` (worded distinctly from a permission failure), `ServerUntrusted`, `RedirectRefused`, and `Cancelled` — with no status code, URL, response body, or stack trace in any message — in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/FailureRenderer.cs` (FR-029, FR-032, FR-038, Principle VII, contracts/azure-devops-rest.md §5, contracts/console-experience.md §7)
- [X] T127 [US3] Call `SweepExpiredAsync(TimeSpan.FromDays(7))` during host startup in `src/DevToolbox.Console/Program.cs` (FR-039c)
- [X] T128 [US3] Emit recovery log events — step failure, chosen recovery, retry attempts, breaker transitions — through the source-generated `Log` class in `tools/VarCompare/DevToolbox.Tools.VarCompare.Infrastructure/Internal/Log.cs` (FR-041)

**Checkpoint**: all three user stories are independently functional — quickstart V5 passes.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: the guarantees that span every story, and the validation that closes the feature.

- [X] T129 [P] Implement the `RecordingLogger` privacy test capturing every log line from a run containing secrets and failing if a known value appears, in `tests/DevToolbox.Tests/Infrastructure/LoggingPrivacyTests.cs` (FR-030, SC-004)
- [X] T129a [P] Sink-boundary redaction test that **deliberately violates the upstream discipline** — passing a `VariableEntry` carrying a known value straight to `ILogger` — and asserts the value never reaches the file and the substitution is recorded as a defect, in `tests/DevToolbox.Tests/Infrastructure/SinkRedactionTests.cs` (Principle VI, SC-004)
- [X] T129b [P] Log-completeness test asserting a captured run trace lets an observer reconstruct which groups were read, in what order, and the terminal outcome, in `tests/DevToolbox.Tests/Infrastructure/LogCompletenessTests.cs` (SC-010, FR-041)
- [X] T130 [P] Implement the prompt-enumeration test walking every reachable prompt and failing if one collects a username, password, or token, in `tests/DevToolbox.Tests/Presentation/NoCredentialPromptTests.cs` (FR-028b, SC-007)
- [X] T131 [P] Implement the performance and navigability check rendering 5 groups × 500 distinct variables within 5 seconds of retrieval **and** asserting every row is reachable page by page with none unreachable, in `tests/DevToolbox.Tests/Presentation/LargeComparisonPerformanceTests.cs` (SC-005, both halves)
- [X] T132 [P] Implement the completeness test asserting the row count equals the distinct-name union of the source groups with zero silent omissions, in `tests/DevToolbox.Tests/Tools/VarCompare/ComparisonCompletenessTests.cs` (SC-002)
- [X] T133 [P] Implement the read-only affordance test asserting no edit, add, set-value, or apply action exists anywhere in the UI, in `tests/DevToolbox.Tests/Presentation/ReadOnlyAffordanceTests.cs` (FR-018c)
- [X] T134 Implement the "where to change it" pointer (`Pipelines ▸ Library ▸ {group}`) shown where a difference is found, in `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/Rendering/ComparisonPresenter.cs` (FR-018c, contracts/console-experience.md §6)
- [X] T135 [P] Write `README.md` at the repository root covering prerequisites, configuration, build, run, and the read-only guarantee
- [X] T135a [P] Write `CLAUDE.md` at the repository root with the build, test, and run commands, the inward-only layering rule, and the read-only and no-attribution working agreements — the constitution names this file as where runtime guidance lives; it elaborates on the constitution and never overrides it (constitution Governance)
- [X] T136 Verify the full build is clean with warnings-as-errors, `NuGetAudit` at level `low` mode `all`, and Meziantou / AsyncFixer / Sonar active, with zero unjustified suppressions (Principle IV)
- [X] T137 Configure coverlet collection and confirm the architecture, privacy, and rendering suites all run in `tests/DevToolbox.Tests/DevToolbox.Tests.csproj`
- [ ] T138 Execute quickstart scenarios V1–V7 against the real Azure DevOps Server and record the outcomes in `specs/001-varcompare/quickstart.md`, including the elapsed time and interaction count for V1 measured against SC-001's 60-second and five-interaction budget, and confirming in V4 that a read-only account completes every scenario without ever needing permission to manage variable groups (FR-018d)
- [ ] T139 Confirm the first-run prompt captures a real server base URL, collection, and any virtual directory (`/tfs`) correctly, and that the saved value is reused on the next start. `appsettings.json` keeps its shipped hint and is not edited per install (spec clarification 2026-08-20, plan open items 1 and 3)
- [X] T140 Update the "Open items carried into implementation" section of `specs/001-varcompare/plan.md` once T018, T138, and T139 have resolved them
- [X] T141 [P] Add XML documentation comments to every public type and member so `GenerateDocumentationFile` produces no warnings (Principle IV)
- [X] T142 Re-review the STRIDE table in `specs/001-varcompare/plan.md` against the built code and confirm each threat is mitigated or explicitly accepted

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies — start immediately
- **Foundational (Phase 2)**: depends on Setup — **blocks all user stories**
- **User Story 1 (Phase 3)**: depends on Foundational only
- **User Story 2 (Phase 4)**: depends on Foundational; **extends US1's model and renderers**, so it is not
  fully parallel with US1 (see below)
- **User Story 3 (Phase 5)**: depends on Foundational; extends `SequentialStepRunner` from T050
- **Polish (Phase 6)**: depends on the stories it validates

### User Story Dependencies

- **US1 (P1)**: independent. Delivers the whole MVP on its own.
- **US2 (P2)**: builds on US1 by design — the spec says so plainly ("It builds directly on P1 and is worthless
  without it"). T095–T097 and T100–T101 modify files US1 creates, so US2 should follow US1 rather than run
  beside it. Its *tests* (T090–T094) can be written in parallel with US1 implementation.
- **US3 (P3)**: genuinely independent of US2. Its only shared file is `SequentialStepRunner.cs` (T050,
  Foundational), so a second developer can take US3 while US2 is in progress.

### Within Each User Story

- Tests first, failing, before the implementation they cover
- Core model → gateway/infrastructure → steps → presentation → registration
- T068 depends on T064–T067; T070 depends on T058–T069; T073 depends on T071, T072, T074
- T087 depends on T076–T086; T088 depends on T087
- T016a depends on T016; T048a depends on T016a and T047–T048; T043a depends on T043 and wraps it
- T075 depends on T069a; T104 depends on T099a; T104a depends on T103–T104

### Parallel Opportunities

- **Setup**: T002–T014, T016–T017, T016a, T019 are all `[P]` — the whole project scaffold can be created at once
- **Foundational**: T020–T023 (Domain), T024–T029 (ports), T037/T039/T040 (adapters), T042, T050a, T052 are `[P]`
- **US1**: all six test tasks T053–T057 plus T069a in parallel; then the ten `[P]` core model tasks T058–T067
- **US2**: all five test tasks T090–T094 in parallel
- **US3**: all six test tasks T111–T115 and T115a in parallel; T112a joins them
- **Polish**: T129, T129a, T129b, T130–T133, T135, T135a, T141 in parallel
- **Across stories**: once Foundational is done, one developer on US1→US2 and a second on US3

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together (they must fail first):
Task: "Comparison-building tests in tests/DevToolbox.Tests/Tools/VarCompare/ComparisonBuilderTests.cs"
Task: "Gateway tests in tests/DevToolbox.Tests/Infrastructure/VariableGroupGatewayTests.cs"
Task: "Working-folder inspector tests in tests/DevToolbox.Tests/Infrastructure/WorkingFolderInspectorTests.cs"
Task: "Side-by-side rendering test in tests/DevToolbox.Tests/Presentation/SideBySideRendererTests.cs"
Task: "GET-only architecture test in tests/DevToolbox.Tests/Architecture/ReadOnlyVerbTests.cs"

# Then launch the pure core model together (ten separate files, no shared state):
Task: "ServerTarget in tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Targets/ServerTarget.cs"
Task: "ProjectIdentifier in tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Targets/ProjectIdentifier.cs"
Task: "RepositoryOrigin in tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Targets/RepositoryOrigin.cs"
Task: "VariableGroupSummary in tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Groups/VariableGroupSummary.cs"
Task: "VariableEntry in tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Groups/VariableEntry.cs"
Task: "VariableGroupSnapshot in tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Groups/VariableGroupSnapshot.cs"
Task: "CellState in tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/CellState.cs"
Task: "ComparisonCell in tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonCell.cs"
Task: "ComparisonRow in tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonRow.cs"
Task: "ComparisonSummary in tools/VarCompare/DevToolbox.Tools.VarCompare.Core/Comparison/ComparisonSummary.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 Setup — the solution exists and builds clean
2. Phase 2 Foundational — **blocks everything; do not shortcut it**
3. Phase 3 User Story 1
4. **STOP and VALIDATE**: run quickstart V1 against the real server
5. This is a genuinely useful tool at this point: two groups, colour-coded, read-only

### Incremental Delivery

1. Setup + Foundational → the shell launches and announces its target
2. + US1 → **MVP**: the comparison table (quickstart V1)
3. + US2 → interpretable states, detail view, layouts that scale (quickstart V2, V3)
4. + US3 → survives flaky corporate networks (quickstart V5)
5. + Polish → V4, V6, V7 and the cross-cutting guarantee tests

### Parallel Team Strategy

1. Everyone on Setup + Foundational
2. Then: developer A takes US1 → US2 (they share files); developer B takes US3 in parallel
3. Polish is shared, mostly `[P]`

---

## Notes

- `[P]` means a different file with no dependency on incomplete work
- Commit after each task or logical group
- **No Claude attribution in version control** — no `Co-Authored-By`, no generated-with footer (plan.md
  working agreements)
- T018 (log sink) and T139 (real base URL) are the two decisions the plan left open; neither blocks the MVP,
  but both should be settled before Polish
- **Two placement questions were raised by `/speckit-analyze` and are now settled in the source artifacts**,
  so no ambiguity remains for the implementer:
  1. `PersistedRun` lives in `DevToolbox.Application` beside `IRunCheckpointStore` (T026). data-model.md §4
     previously labelled it Infrastructure and has been corrected. The invariant that matters — no field can
     reach a variable value — is unaffected and is asserted by T113.
  2. `ServerTarget` and `ProjectIdentifier` live in `DevToolbox.Domain` (T058, T059), not in
     `VarCompare.Core`. They are shared kernel: `GovernanceAuditor` needs both when it arrives as tool #2, and
     duplicating them per tool would undercut the plan's stated reason for keeping the Azure DevOps spine
     shared. data-model.md §1 has been updated to match. `RepositoryOrigin` stays tool-local in
     `VarCompare.Core`, which may reference Domain.

### Traceability of the analysis remediation

Twelve tasks were added after the `/speckit-analyze` pass, using suffixed IDs so that no existing task number
moved and every reference elsewhere stays valid — the same convention the spec used when FR-019–FR-027 were
removed.

| Finding | Severity | Closed by |
|---------|----------|-----------|
| C1 — no sink-boundary redaction (Principle VI MUST) | CRITICAL | T043a, T129a, plan.md Principle VI gate |
| C2 — no test for full restart or step success (Quality Gate 2) | CRITICAL | T050a, T112a, T014 |
| C3 — `System.Console` and `new HttpClient()` unenforced | HIGH | T016, T016a, T048a |
| G1 — SC-005 "remains navigable" uncovered | HIGH | T104a, T131, T093 |
| G5 — only 1 of 8 failure outcomes had a renderer | HIGH | T126, T115a |
| C4 — tool version missing from run-start log | MEDIUM | spec.md FR-041, T089 |
| C5 — `InvariantGlobalization` never decided | MEDIUM | T002 |
| C6 — `ConfigureAwait` policy not declared | MEDIUM | T004 |
| C7 — no `CLAUDE.md` | MEDIUM | T135a |
| C8 — `IsIdempotent` flag contradiction | MEDIUM | T025, contracts/application-ports.md |
| G2 — two contract ports had no defining task | MEDIUM | T069a, T099a |
| G3 — SC-010 log completeness untested | MEDIUM | T129b |
| G4 — SC-001 interaction budget unowned | MEDIUM | T138 |
| I1 — `PersistedRun` layer contradiction | MEDIUM | data-model.md §4, Notes above |
| I2 — `ServerTarget` placement vs. shared-spine goal | MEDIUM | T058, T059, data-model.md §1 |
| U1 — "Server not configured" message quality | MEDIUM | T030 |

Not actioned, by decision: **D1**, **D2** (benign defensive redundancy in the spec), **A1** (resolved
downstream by research R12 and T103), **I3** (the plan's `Infrastructure/System/` file-system abstraction —
left as written; the constitution's determinism rule binds Domain and Application only, and storage sits in
Infrastructure), **I4** (spec-to-design entity naming drift, normal refinement), **U2** (empty-group
behaviour, folded into T053's assertions rather than given its own task).
