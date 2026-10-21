---

description: "Task list for apidelta — OpenAPI semantic delta, audit and safe optimization"
---

# Tasks: apidelta — what a candidate OpenAPI description would do to production, and what production has accumulated

**Input**: Design documents from `/specs/003-openapi-delta-audit/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/application-ports.md),
[quickstart.md](./quickstart.md)

**Tests**: included, and not optional here. The specification makes them deliverables — SC-002 is a
mutation suite, SC-004 is a byte-comparison, SC-009 and SC-016 are assertions about what must never happen
— and the constitution's quality gates require tests for step success, step failure, resume-from-step and
full restart. Where a test is the point of the task it is its own task; where it is the other half of a
unit of work (a rule and its tests) it is bundled, because a rule without its test is not done.

**Organization**: by user story, so each is independently implementable and testable. Stories are numbered
as in the spec; **US2 is executed before US1** — both are P1, and the spec says in as many words that
Story 1 is unsafe without Story 2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: which user story the task serves (US1…US8)
- Paths are repository-relative. The tool's three projects live under `tools/ApiDelta/`, abbreviated below
  as `…ApiDelta.Core/`, `…ApiDelta.Infrastructure/` and `…ApiDelta.Presentation/` for
  `tools/ApiDelta/DevToolbox.Tools.ApiDelta.{Core,Infrastructure,Presentation}/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: the projects, the packages and the corpus exist and build clean.

- [X] T001 Add `PackageVersion` entries for `YamlDotNet` 18.1.0 and `Microsoft.OpenApi` 3.10.2 to `Directory.Packages.props`, then run `dotnet restore DevToolbox.slnx` and confirm `NuGetAudit` reports nothing at `low` or above — this is the gate on R1 and R2, and a single advisory reopens the decision rather than being waived
- [X] T002 Create the three projects under `tools/ApiDelta/` following the `tools/VarCompare/` csproj pattern exactly: `…ApiDelta.Core/DevToolbox.Tools.ApiDelta.Core.csproj` referencing only `src/DevToolbox.Application` and `src/DevToolbox.Domain`; `…ApiDelta.Infrastructure/…csproj` referencing Core and `src/DevToolbox.Infrastructure` with `InternalsVisibleTo DevToolbox.Tests`; `…ApiDelta.Presentation/…csproj` referencing Core and `src/DevToolbox.Presentation`
- [X] T003 Register the three projects in `DevToolbox.slnx` under a new `/tools/ApiDelta/` solution folder
- [X] T004 [P] Create the test tree `tests/DevToolbox.Tests/Tools/ApiDelta/{Loading,Comparison,Versioning,Dependencies,Fingerprinting,Audit,Optimization,Configuration,Mutation,Golden,Corpus}/`
- [X] T005 [P] Add the hand-written corpus fixtures as embedded resources in `tests/DevToolbox.Tests/Tools/ApiDelta/Corpus/`: `baseline-30.yaml`, `baseline-30.reformatted.yaml`, `baseline-30.json`, `baseline-31.yaml` per [quickstart.md](./quickstart.md)
- [X] T006 [P] Add the candidate fixtures `candidate-additive.yaml`, `candidate-breaking.yaml`, `candidate-doc-only.yaml`, `candidate-conflict.yaml` in `tests/DevToolbox.Tests/Tools/ApiDelta/Corpus/` — **derived from `baseline-30.yaml` by one documented edit each**, so a difference the test did not intend cannot creep in through a transcription slip. Cross-format comparison is covered by `baseline-30.json` against `baseline-30.yaml`, which is what SC-001 asks for, so the candidates need not be JSON
- [X] T007 [P] Add the adversarial fixtures `broken-duplicate-key.yaml`, `broken-unresolved-ref.yaml`, `broken-version.yaml`, `cyclic.yaml`, `redundant.yaml`, `secrets.yaml` in `tests/DevToolbox.Tests/Tools/ApiDelta/Corpus/`. **`deep.yaml` moved to the generator** (T008): indentation grows with depth, so 400 levels weigh a megabyte for one useful property — the same argument that keeps the 1,000-operation document out of the repository
- [X] T008 [P] Add `CorpusLoader` with `GenerateLarge` (1,000 operations, 10,000 schemas) and `GenerateDeep` in `tests/DevToolbox.Tests/Tools/ApiDelta/Corpus/`, so neither the large nor the deep document is ever committed; the corpus is embedded in the test assembly rather than read from disk, because a test that reads a relative path depends on where the runner was launched from
- [X] T009 Extend `tests/DevToolbox.Tests/Architecture/LayeringTests.cs` with rows for the three new assemblies: Core depends on neither `YamlDotNet` nor `Microsoft.OpenApi` nor `Spectre.Console` nor `System.Net.Http`; Presentation does not reference the tool's Infrastructure

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the three shared prerequisites from the plan, and the tool's spine — enough that the tool is
reachable from the menu and a run can be started, checkpointed, resumed and recovered, before any engine
exists.

**⚠️ CRITICAL**: no user story work begins until this phase is complete. T010 comes first because it is a
security defect that this feature is about to start depending on.

- [X] T010 Fix the confinement check in `src/DevToolbox.Infrastructure/Platform/AppPaths.cs`: compare against the root with a trailing separator, or use `Path.GetRelativePath` and reject a result that is rooted or starts with `..`; add `tests/DevToolbox.Tests/Infrastructure/AppPathsTests.cs` cases proving `C:\out` rejects `C:\out\..\outside\x`, a device path and a path through a symbolic link (R12)
- [X] T011 Generalize `src/DevToolbox.Application/Abstractions/PersistedRun.cs` to `(RunId, ToolId, Selection, CompletedSteps, StartedAt, LastUpdatedAt)` and add `src/DevToolbox.Application/Abstractions/PersistedSelectionEntry.cs`, deleting `PersistedGroupSelection.cs` (R10). **The entry needed three strings, not two** — `(Kind, Key, Label)`: the planned pair had nowhere to put what a tool needs in order to *find the thing again*, and varcompare's `IResumedSelectionReconciler` is typed on group identifiers rather than on display text. `Key` carries the identifier, `Label` the display text, and `Kind` lets one list hold a collection, a project and its groups without the shared layer learning those words. `PersistedGroupSelection` became `RememberedGroup` inside varcompare's own Core, where "42 is a group id" is a sentence that means something
- [X] T012 Narrow the runtime shape allow-list in `src/DevToolbox.Infrastructure/Storage/FileRunCheckpointStore.cs` to `Guid`, `string`, `DateTimeOffset`, `IReadOnlyList<string>`, `IReadOnlyList<PersistedSelectionEntry>`, and update the existing shape test so it still fails when the record is widened
- [X] T013 Update `src/DevToolbox.Presentation/Shell/ResumePrompt.cs` to render a run's labelled selection entries generically, so one prompt serves every tool
- [X] T014 Update `tools/VarCompare/DevToolbox.Tools.VarCompare.Presentation/VarCompareCheckpointFactory.cs` and its tests to emit labelled entries for collection, project and selected groups — mechanical, and varcompare's behaviour must not change
- [X] T015 Add `bool RequiresServer => true;` as a default interface member on `src/DevToolbox.Application/Abstractions/ITool.cs` (R11)
- [X] T016 Make server configuration lazy in `src/DevToolbox.Console/Program.cs`: the resolver now splits into `TryLoadStoredOverridesAsync` (reads, never asks) at startup and `PromptAndStoreAsync` behind the choice of a tool whose `RequiresServer` is true. **The planned mutable `ServerConfigurationState` was dropped**: the shell enumerates `ITool` to draw the tab strip, so every tool — and the `ServerTarget` its graph pulls in — is constructed *before* any choice is made, and a value deposited after that point would reach an already-built object. Confirming the address therefore rebuilds the host (`RunToolboxAsync` loops once, carrying the chosen tool id so the developer lands in the tool they picked rather than back at the menu). One extra host build in a path that happens once, against a whole class of staleness bugs
- [X] T017 Make the header in `src/DevToolbox.Presentation/Shell/ShellLayout.cs` render the server only when one is configured, so it never names a server the chosen tool does not contact
- [X] T018 Add `tests/DevToolbox.Tests/Presentation/ShellWithoutServerTests.cs`: with no stored server configuration, the shell starts, renders the tab strip, and reaches the OpenAPI tab without prompting for an address
- [X] T019 [P] Add the syntax primitives in `…ApiDelta.Core/Syntax/`: `SpecPointer`, `SourcePosition`, `ByteSpan`, `SpecFormat`, `OpenApiVersion`, `DocumentRole`, `NodeKind`, `ScalarStyle` ([data-model.md](./data-model.md) §1)
- [X] T020 [P] Add the node hierarchy in `…ApiDelta.Core/Syntax/`: `SpecNode`, `MappingNode` (ordered entries), `SequenceNode`, `ScalarNode` (text as written, style, tag, resolved value, `AmbiguousScalar`)
- [X] T021 [P] Add `Diagnostic`, `DiagnosticSeverity` and the diagnostic code constants in `…ApiDelta.Core/Loading/`
- [X] T022 [P] Add every port from [contracts/application-ports.md](./contracts/application-ports.md) as an interface in `…ApiDelta.Core/Abstractions/`, one public type per file, each async member taking a `CancellationToken` and each fallible member returning `Result` or `Result<T>`. **Found during implementation: this task cannot be done in one pass as written.** Most ports are typed on models that belong to later slices — `IDocumentComparer` on `Difference`, `IAuditRule` on `Finding` and `RuleOutcome`, `IExtractBuilder` on `ExtractResult` — and an interface referencing a type that does not exist does not compile. Writing those models now would mean implementing the data model of Phases 4 to 11 inside Phase 2, which is the opposite of what phasing is for. **Each port therefore arrives with the slice that implements it**, and this task reduces to the spine's own ports (`ISpecReader`, `IApiDeltaPrompt`, `IArtifactStore`) plus `SpecDocument`. **Clos en phase 12** : les tranches ont toutes atterri, et chaque port du contrat existe désormais dans `Abstractions/`, un type public par fichier
- [X] T023 [P] Add `ApiDeltaOptions`, `LoadLimits`, `ReferencePolicy`, `CanonicalFormatting` and the shipped-default `Ruleset` record in `…ApiDelta.Core/Configuration/`, with the defaults of [contracts/configuration.md](./contracts/configuration.md) §3 and no file reading yet
- [X] T024 Add `RunSelection`, `RunMode`, `OutcomeCode`, `ApiDeltaRun`, `Artifact`, `ArtifactKind` in `…ApiDelta.Core/`, with outcome resolution for `clean`, `loadFailure`, `outputFailure` and `internalError` only — the threshold-driven codes arrive with US8. **Done except `ApiDeltaRun`**, for the reason recorded on T022: the aggregate is typed on `Difference`, `Finding`, `RuleOutcome` and `ExtractResult`, so it is assembled as those slices land. The shared `ToolRun` already carries the step states and the outcome that the spine needs in the meantime. **Clos en phase 12** : `ApiDeltaRun` porte les différences, l'examen, le plan, l'application et l'extrait, et `OutcomeResolver` résout les sept codes
- [X] T025 Add the step layer in `…ApiDelta.Core/Steps/`: `ApiDeltaContextKeys`, `ApiDeltaStepNames` (the ten names, their order, the mode rules and the skip reason) and `SelectStep` — which prompts, validates both inputs through `ISpecFileProbe`, records their hashes into the selection, and marks the steps its mode excludes as skipped. **Skeletons were rejected**: a step that returns success without doing anything is reported as accomplished, on screen and in the step list, which is the one thing the step list exists to prevent. The remaining nine classes arrive with the slices that already own them (T047, T065, T070, T079, T134, T146, T190), and a run declares only the steps that exist. The skip *reason* lives with the tool rather than on the shared `StepState`: only the tool knows what a mode means, and the shared layer has no business learning the word "audit"
- [X] T026 Add `…ApiDelta.Presentation/ApiDeltaCheckpointFactory.cs` projecting `RunSelection` to labelled entries per [contracts/application-ports.md](./contracts/application-ports.md), plus `tests/DevToolbox.Tests/Tools/ApiDelta/ApiDeltaCheckpointShapeTests.cs` asserting the serialized checkpoint JSON contains no node, finding or document text
- [X] T027 Add `…ApiDelta.Presentation/ApiDeltaTool.cs` implementing `ITool` with `Id` `apidelta`, `Tab` `OpenAPI`, `RequiresServer => false`
- [X] T028 Add `…ApiDelta.Presentation/ApiDeltaSession.cs` wiring the ten steps through the shared `IStepRunner`, checkpointing after each success and routing failure to the shared `IRecoveryPrompt`
- [X] T029 Add the selection prompts in `…ApiDelta.Presentation/Prompts/` per [contracts/console-experience.md](./contracts/console-experience.md) §2, implementing `IApiDeltaPrompt`: production path, mode, candidate path, output directory, ruleset paths, optimization intent, and the final summary confirmation
- [X] T030 Add `ServiceCollectionExtensions.cs` to `…ApiDelta.Infrastructure/` (`AddApiDeltaInfrastructure`, `RegisterRedactedTypes`) and to `…ApiDelta.Presentation/` (`AddApiDelta`), registering every rule explicitly rather than by assembly scanning (R16). **Two multi-tool collisions surfaced here, both latent in the single-tool world and both silent:**<br>• `IRunCheckpointFactory` was registered once and consumed once, but is per-tool by design. With two registrations the shared runner got whichever came last, and since a factory returns `null` for a context it does not recognise, the other tool's resume would have stopped working without a word. The interface now declares `ToolId`, `SequentialStepRunner` takes `IEnumerable<IRunCheckpointFactory>` and selects by `run.ToolId`, and a tool with no factory is logged rather than quietly unresumable.<br>• Steps were registered as `IToolStep`, so each tool's `IEnumerable<IToolStep>` would have received every other tool's steps. Both tools now register their steps by concrete type and compose their own ordered list; nothing is registered as `IToolStep` any more, because a toolbox-wide collection of steps means nothing. `CompositionRootTests` asserts that collection stays empty
- [X] T031 Add the two registration lines to `ConfigureServices` in `src/DevToolbox.Console/Program.cs` and extend `tests/DevToolbox.Tests/Application/CompositionRootTests.cs` so the real graph resolves `ApiDeltaTool` and every registered rule
- [X] T032 Add the source-generated `Log` class in `…ApiDelta.Infrastructure/Internal/`, matching varcompare's placement, emitting only facts outside the content — a path, a size, the first twelve characters of a hash, a verdict. Every refusal in `SpecFileProbe` funnels through one method, so none can be returned without being logged. `ApiDeltaRecoveryTests` covers step success, step failure with the recovery prompt, retry-the-step, restart-the-run, resume-with-matching-hash and **refusal to resume when the file has changed since**. **Also wired `RunLoggingScope`**, which existed but was referenced nowhere: the correlation scope FR-710 requires was never opened, so every run's log lines were indistinguishable from every other's. It now opens in the shared runner, which is the one place that sees all of them — both tools gain it

**Checkpoint**: the OpenAPI tab exists, apidelta can be chosen, a run can be started, checkpointed, recovered and resumed — and it does nothing yet.

---

## Phase 3: User Story 2 — Refuse to reason about a description it could not read faithfully (Priority: P1) 🎯 MVP part 1

**Goal**: both documents are read exactly as written, or the run stops with a located diagnostic and writes nothing.

**Independent Test**: feed `broken-duplicate-key.yaml`, `broken-unresolved-ref.yaml` and `broken-version.yaml`; each is refused with a located diagnostic and an empty output directory. `cyclic.yaml` completes. `baseline-30.json` is read despite the content/extension question.

### Tests for User Story 2

> Write these first and watch them fail.

- [X] T033 [P] [US2] Duplicate-key tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Loading/DuplicateKeyTests.cs`: both positions reported, every duplicate in the file reported rather than just the first, and no value silently lost (SC-009)
- [X] T034 [P] [US2] Refusal tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Loading/RefusalTests.cs`: unresolved reference lists all three using locations; unsupported version names the supported range; a parse error carries line and column
- [X] T035 [P] [US2] Discipline tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Loading/LoadOutcomeTests.cs`: a refused load writes no artifact; `cyclic.yaml` neither fails nor loops; a content/extension mismatch is read by content

### Implementation for User Story 2

- [X] T036 [US2] Implement the YAML reader over YamlDotNet's low-level parser in `…ApiDelta.Infrastructure/Yaml/YamlSpecReader.cs`, building `SpecNode`s with position, byte span and scalar style (R2; confirm the member names against 18.1.0 — plan open item 2)
- [X] T037 [US2] Implement duplicate-key detection inside the tree builder in `…ApiDelta.Infrastructure/Yaml/`, collecting every occurrence with both positions before anything binds them (FR-007)
- [X] T038 [US2] Implement the JSON reader in `…ApiDelta.Infrastructure/Json/JsonSpecReader.cs` over `Utf8JsonReader`, computing line and column from token offsets, — **the `AllowDuplicateProperties` guard of R4 turned out to have nowhere to apply**: it belongs to `JsonSerializer` and `JsonDocument`, and this reader binds to neither. The tree is built straight from `Utf8JsonReader`, so our own detection is not a second line of defence but the only one, which is why it reports every duplicate rather than throwing on the first
- [X] T039 [US2] Implement content-based format detection in `…ApiDelta.Infrastructure/Files/FormatDetector.cs`, used in preference to the extension (FR-001)
- [X] T040 [US2] Implement YAML 1.2 core-schema scalar resolution in `…ApiDelta.Infrastructure/Yaml/ScalarResolver.cs`, marking `y`, `no`, `on`, `~`, leading-zero and sexagesimal forms as ambiguous and reporting them rather than coercing them (R3)
- [X] T041 [US2] Implement the alias and anchor policy in `…ApiDelta.Infrastructure/Yaml/`. **Aliases are refused, not expanded** — the plan's position was "expand for semantics, remember the alias", and implementation showed it cannot hold: an expanded alias has no byte span of its own, so the round-trip invariant of T050 (an unmodified tree rewrites itself byte-for-byte) would be unprovable precisely where fidelity matters most. A document using aliases is therefore refused with that reason stated, rather than silently expanded into something the tool could not write back. Expansions are still counted against the limit, so a malicious alias cascade is bounded even as it is rejected
- [X] T042 [US2] Enforce `LoadLimits` — maximum bytes, nesting depth, reference count, alias expansions — in the readers, refusing with the limit named (FR-010)
- [X] T043 [US2] Implement the tree-to-`JsonNode` projection in `…ApiDelta.Infrastructure/Semantic/JsonProjection.cs`, rejecting non-string mapping keys as a structural error (R3)
- [X] T044 [US2] Implement the semantic adapter in `…ApiDelta.Infrastructure/Semantic/OpenApiSemanticAdapter.cs` (`ISpecValidator`): declared-version detection, structural validation for 3.0.x and 3.1.x, and mapping the library's pointer-located diagnostics back onto our nodes so each error carries a line and a column. **Start with the spike** of plan open item 1: load `baseline-31.yaml` carrying unknown extensions and unknown schema keywords, record what the model preserves, and write the finding into `research.md`
- [X] T045 [US2] Implement `IReferenceGraph` in `…ApiDelta.Infrastructure/Semantic/ReferenceGraph.cs`: canonical reference identity, resolution, cycles treated as legal for schemas and reported for others, unresolved references with every using location (FR-005, FR-201)
- [X] T046 [US2] Implement the reference policy in `…ApiDelta.Infrastructure/Semantic/ReferencePolicyGate.cs`: external off by default, local resolution confined to the configured root through the fixed `CombineConfined`, remote behind its own opt-in with a host allow-list (FR-006). **External references are preserved or refused, never followed**, which is what the default policy means; the confinement, timeout, response-size and private-range guards of FR-006a and FR-006b attach to the act of *resolving* one, and land with the slice that first does so
- [X] T047 [US2] Wire `ReadTargetStep`, `ReadSourceStep` and `ValidateStep` in `…ApiDelta.Core/Steps/`, enforcing the refusal discipline: a document that was not read faithfully produces diagnostics and no `SpecDocument`, and no later step runs (FR-009)
- [X] T048 [US2] Render diagnostics in `…ApiDelta.Presentation/Rendering/DiagnosticsView.cs`: file, line, column, message and every related location, grouped by severity
- [X] T049 [US2] Implement the verbatim emitter in `…ApiDelta.Infrastructure/Emitting/SpecEmitter.cs` (preserve mode), which re-emits an unmodified tree unchanged
- [X] T050 [US2] Add the round-trip property test in `tests/DevToolbox.Tests/Tools/ApiDelta/Loading/RoundTripTests.cs`: every corpus document read then emitted verbatim is byte-identical to its input — the invariant the whole feature's fidelity promise rests on

**Checkpoint**: the tool reads or refuses, and can prove it changed nothing in what it read.

---

## Phase 4: User Story 1 — Know what this candidate would do to production (Priority: P1) 🎯 MVP part 2

**Goal**: a located, classified list of what changed, immune to formatting noise, delivered on screen and as artifacts.

**Independent Test**: compare `baseline-30.yaml` against `baseline-30.reformatted.yaml` and `baseline-30.json` for zero differences; then against `candidate-breaking.json` and `candidate-additive.json` for correctly classified, singly-reported changes.

### Tests for User Story 1

- [X] T051 [P] [US1] Normalization tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Comparison/FormattingIsNotDifferenceTests.cs`: reindentation, reordered keys, requoted response codes, changed line endings and JSON-versus-YAML all yield zero differences and a version recommendation of none (SC-001)
- [X] T052 [P] [US1] Classification tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Comparison/ClassificationTests.cs` over the three candidates: breaking, non-breaking and documentation-only, each with location, rationale and recommended action (SC-005)
- [X] T053 [P] [US1] Attribution tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Comparison/AttributionTests.cs`: one underlying change yields exactly one difference, at the deepest node that explains it, with no ancestor-plus-descendant pair anywhere in the corpus (FR-113)

### Implementation for User Story 1

- [X] T054 [US1] Add the metamodel types in `…ApiDelta.Core/Metamodel/`: `MetamodelEntry`, `LocationPattern`, `CollectionKind`, `IdentityRule`, `FieldRole`, `ImpactRule`, `PolicyKey` ([data-model.md](./data-model.md) §2)
- [X] T055 [US1] Add the document-level metamodel rows in `…ApiDelta.Core/Metamodel/Rows/DocumentRows.cs`: the version field, `info` and its children, `servers`, `tags`, `externalDocs`, with `info.version` marked so it cannot mask another change in `info` (FR-100, FR-101)
- [X] T056 [US1] Add path and operation rows in `…ApiDelta.Core/Metamodel/Rows/PathRows.cs`: paths as a keyed map, all eight HTTP methods, and every operation field of FR-103
- [X] T057 [US1] Add parameter, request-body, response, header, link and callback rows in `…ApiDelta.Core/Metamodel/Rows/OperationContentRows.cs`, with parameter identity as the `(name, in)` pair and responses keyed by status code regardless of quoting (FR-104 to FR-106)
- [X] T058 [US1] Add schema rows in `…ApiDelta.Core/Metamodel/Rows/SchemaRows.cs` for both declared versions, covering every keyword of FR-107, and treating 3.0 `nullable` and 3.1 union-with-null as the same intent rather than a contract change
- [X] T059 [US1] Add component, security and extension rows in `…ApiDelta.Core/Metamodel/Rows/ComponentRows.cs`: every component kind, security requirements with order-insensitive scope lists and order-sensitive alternatives, and `x-` extensions as `FieldRole.Vendor` compared as structured values (FR-108 to FR-110)
- [X] T060 [US1] Implement the unmatched-pointer default in `…ApiDelta.Core/Metamodel/OpenApiMetamodel.cs` — unordered comparison, vendor role, `Unknown` impact, never automatically transformable. **Resolution is a single forward pass, not pattern matching**: in OpenAPI the position decides the nature, and a schema nests in itself without limit, so no glob could say where one begins. The first attempt resolved a field's enclosing container by "the last named field seen", which made `required` under `/components/schemas/Document` look like a *member* of the schemas map — inheriting identity-by-key instead of its own set-like row, and reporting seven phantom differences on a reordered `required` array. The pass now carries one piece of state, the collection whose key it still awaits, which is exactly what distinguishes a key from a field
- [X] T061 [US1] Implement the comparer walk in `…ApiDelta.Core/Comparison/DocumentComparer.cs`, pairing members by the metamodel's identity rule and ignoring differences with no semantic effect (FR-111)
- [X] T062 [US1] Implement `ValueSummary` in `…ApiDelta.Core/Comparison/ValueSummary.cs`: bounded length with an explicit elision marker, passed through the same redaction path as logs so a diff record cannot become a leak
- [X] T063 [US1] Implement the classifier in `…ApiDelta.Core/Comparison/ChangeClassifier.cs`: breaking, non-breaking, documentation-only or unknown, each with a confidence and a rationale, and nothing that removes or narrows ever classified non-breaking (FR-112, FR-114)
- [X] T064 [US1] Implement deepest-node attribution and de-duplication in `…ApiDelta.Core/Comparison/DifferenceCollector.cs` (FR-113)
- [X] T065 [US1] Wire `CompareStep` in `…ApiDelta.Core/Steps/CompareStep.cs`
- [X] T066 [US1] Implement the Markdown report writer in `…ApiDelta.Core/Reporting/MarkdownReportWriter.cs` with all nineteen sections of [contracts/artifacts.md](./contracts/artifacts.md) §3 in fixed order, each present even when empty, filling sections 1 to 11, 18 and 19
- [X] T067 [US1] Implement the JSON report writer in `…ApiDelta.Core/Reporting/JsonReportWriter.cs` with `schemaVersion`, `runMetadata`, `inputs`, `diagnostics`, `differences`, `artifacts` and `outcome` per [contracts/artifacts.md](./contracts/artifacts.md) §4
- [X] T068 [US1] Implement `IArtifactStore` in `…ApiDelta.Infrastructure/Files/ArtifactStore.cs`: confinement through the fixed `CombineConfined`, write to a temporary name then move into place, record path, size and hash, and confirm before writing outside the output directory or over an existing artifact (FR-605, FR-709)
- [X] T069 [US1] Implement deterministic ordering and the delimited run-metadata block in `…ApiDelta.Core/Reporting/`: differences by pointer then change identifier, and no collection ever written in hash-table order; add the golden test asserting two runs differ only inside that block (SC-004)
- [X] T070 [US1] Wire `WriteStep` in `…ApiDelta.Core/Steps/WriteStep.cs`, with a test that a cancelled run leaves no artifact that appears complete (FR-704)
- [X] T071 [US1] Implement the result screen and the differences views in `…ApiDelta.Presentation/Rendering/`: the summary panel of [contracts/console-experience.md](./contracts/console-experience.md) §4, then paged views for breaking changes and all differences, each with Back
- [X] T072 [US1] Add the mutation suite in `tests/DevToolbox.Tests/Tools/ApiDelta/Mutation/BreakingMutationTests.cs`: twelve mutations over `baseline-30.yaml` — requiredness on a parameter and on a body, a required-names list, an upper and a lower bound, enum membership, type, format, a removed response, an added security requirement — each asserted never non-breaking and never documentation-only, plus the two symmetrical loosenings asserted never breaking (SC-002, first half). **The suite's first run failed twice, and the classifier was right both times**: "type change" also deleted a `maxLength` (a loosening) and "response disappears" was a rename (a removal *plus* a compatible addition). Both mutations were compound, so each now changes one thing — which is the sloppiness the suite exists to catch, aimed at its own author

**Checkpoint**: 🎯 **MVP.** The tool answers the question it exists to answer, writes a report and a machine-readable report, and has proved it ignores formatting and never calls a breaking change safe.

---

## Phase 5: User Story 3 — Be told which version number to publish, and why (Priority: P2)

**Goal**: a recommended version with every contributing change and the resolved policy.

**Independent Test**: the three candidates yield major, minor and patch respectively, each naming its contributors; an unparseable baseline is reported, not guessed; a lower explicit version requires a deliberate override and is reported prominently.

### Tests for User Story 3

- [X] T073 [P] [US3] Version tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Versioning/VersionRecommendationTests.cs`: patch, minor, major, none; mixed impacts resolving to the highest; unparseable baseline reported; override below the recommendation requiring force and reported

### Implementation for User Story 3

- [X] T074 [US3] Implement `SemanticVersion` parsing, comparison and increment in `…ApiDelta.Core/Versioning/SemanticVersion.cs`, accepting pre-release and build metadata and reporting what it cannot parse (FR-300)
- [X] T075 [US3] Implement the impact-to-increment mapping in `…ApiDelta.Core/Versioning/IncrementPolicy.cs` from the metamodel's `ImpactRule`, covering every example in FR-301 to FR-303
- [X] T076 [US3] Implement configurable policy resolution in `…ApiDelta.Core/Versioning/VersioningPolicy.cs` for the choices of FR-304, and surface every resolved choice for the report (FR-308)
- [X] T077 [US3] Implement highest-impact selection with contributors in `…ApiDelta.Core/Versioning/VersionRecommender.cs` (FR-305, FR-306)
- [X] T078 [US3] Implement override and force handling in `…ApiDelta.Core/Versioning/VersionOverride.cs` (FR-307)
- [X] T079 [US3] Wire `VersionStep`, fill report section 4 and the JSON `version` object
- [X] T080 [US3] Add the version line and its reasons to the summary panel in `…ApiDelta.Presentation/Rendering/`
- [X] T081 [US3] Cross-check the rule set against `oasdiff checks --severity warn,error` and record any gap, with its disposition, in `research.md` under R13

**Checkpoint**: the tool says what to publish and why.

---

## Phase 6: User Story 4 — Get a patch that is reviewable and complete (Priority: P2)

**Goal**: a mergeable OpenAPI fragment with every dependency and no dangling reference.

**Independent Test**: generate a patch for a candidate whose new operation references a chain of schemas; every link is present, nothing is unresolved, removals are absent unless asked for, and a name conflict is reported rather than overwritten.

### Tests for User Story 4

- [X] T082 [P] [US4] Patch tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Dependencies/PatchTests.cs`: full transitive closure, zero unresolved internal references (SC-006), removals only when requested, no audit content unless combined, and `candidate-conflict.json` reported as a conflict

### Implementation for User Story 4

- [X] T083 [US4] Implement `ReferenceTarget` canonical identity and the visited set in `…ApiDelta.Core/Dependencies/ReferenceTarget.cs` (FR-201)
- [X] T084 [US4] Implement the transitive closure in `…ApiDelta.Core/Dependencies/DependencyResolver.cs` over parameters, schemas, request bodies, responses, headers, examples, links, callbacks and security schemes, iteratively so depth cannot exhaust the stack (FR-200, FR-205)
- [X] T085 [US4] Preserve allowed external references exactly and list them in the dependency report in `…ApiDelta.Core/Dependencies/` (FR-203)
- [X] T086 [US4] Implement conflict detection in `…ApiDelta.Core/Dependencies/ComponentConflictDetector.cs`: same name, different semantics, with the differing fields named and nothing overwritten (FR-204)
- [X] T087 [US4] Implement `IPatchWriter` in `…ApiDelta.Core/Reporting/PatchWriter.cs`: recommended version, added paths and operations, complete modified objects where a fragment would be ambiguous, and every dependency (FR-500, FR-501)
- [X] T088 [US4] Add the marked removals section behind the explicit request (FR-502) and keep audit content out unless combined (FR-503)
- [X] T089 [US4] Validate the patch before publishing it in `…ApiDelta.Core/Reporting/PatchWriter.cs`: it reparses as a valid fragment and contains no unresolved internal reference, or the run fails with `outputFailure` (FR-504, FR-202)
- [X] T090 [US4] Publish the patch artifact, add it to report section 18 and the JSON `artifacts`, and add the patch option to the mode prompt

**Notes on this phase, recorded while doing it.**

- **T151 was pulled forward from Phase 9.** The patch cannot be written without a YAML writer, so
  `CanonicalEmitter` was delivered here rather than with the optimizer. Its own concern turned out to be
  quoting, not layout: a string that would re-read as a number, a boolean or a null must be quoted, or
  `'200'` stops being a response code. Proven by a semantic round trip over nine corpus documents — emit,
  re-read, compare canonical forms — rather than by byte comparison, which would have tested the writer
  against itself. Multi-line scalars are written double-quoted with escapes; the block form is more
  readable and its chomping indicator is a trap, so it waits for Phase 9 and a property test.
- **T086 found that a component-only change produced no conflict at all**, because the closure started from
  difference locations and a changed `maxLength` reaches no path. Recorded as FR-200a.
- **T087 and T084 disagreed by one level, and that was the whole bug.** The writer carried complete
  operations; the closure walked the changed field. So the patch referenced components the closure had
  never visited, and it published with dangling references — the one defect SC-006 exists to prevent. The
  rule now lives once, in `CarriedObject`, and both read it. The second half of the same mismatch: a
  carried operation always travels with its path-level `parameters`, which the closure also had to walk.
- **T088 hit a genuine contradiction between FR-501 and FR-502** for an operation whose only change is an
  internal removal. Resolved in favour of carrying it and naming what disappears; recorded as FR-501a and
  FR-502a. A new fixture, `candidate-path-removed.yaml`, covers the case FR-502 really governs.

**Checkpoint**: the delta is mergeable by hand.

---

## Phase 7: User Story 5 — See what production has accumulated (Priority: P2)

**Goal**: the audit — validity, redundancy, consistency and governance — reported separately from contract changes, with a truthful account of which rules ran.

**Independent Test**: audit `redundant.yaml` and confirm one grouped finding per repetition, the unreachable components, the deprecation mismatch, the optional request body and the mixed quoting, each with identifier, severity, safety class, location and recommended action.

### Tests for User Story 5

- [X] T091 [P] [US5] Audit engine tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Audit/AuditEngineTests.cs`: the 14 repeated headers form **one** finding with 14 locations; every rule appears in the outcome list; a rule that could not run appears as not evaluated and prevents the audit being called complete (FR-418)

### Engine for User Story 5

- [X] T092 [US5] Implement `ICanonicalizer` in `…ApiDelta.Core/Fingerprinting/Canonicalizer.cs`: drop position and style, sort mapping keys by UTF-16 code unit, normalize set-like arrays, leave ordered arrays alone, normalize numbers, keep every validation keyword and extension (FR-400, R5)
- [X] T093 [US5] Implement `IFingerprinter` in `…ApiDelta.Core/Fingerprinting/Fingerprinter.cs`: SHA-256 over the canonical bytes, lowercase hex, cached per node and profile (FR-401)
- [X] T094 [US5] Implement the three equivalence profiles in `…ApiDelta.Core/Fingerprinting/EquivalenceProfile.cs` as predicates over metamodel rows, with a test asserting only `strict` can qualify a transformation for automatic application (FR-402, FR-413)
- [X] T095 [US5] Implement bucketed candidate grouping in `…ApiDelta.Core/Fingerprinting/CandidateBuckets.cs`: exact duplicates by fingerprint; near-duplicates bucketed by kind, property-name set, child count and size band, with a configured cap and truncation reported rather than hidden (R8)
- [X] T096 [US5] Add `Finding`, `RuleOutcome`, `FindingEvidence`, `FindingSeverity`, `SafetyClass` in `…ApiDelta.Core/Audit/`, with `AutofixAvailable` true only when safety is SAFE and strict equivalence was established ([data-model.md](./data-model.md) §6)
- [X] T097 [US5] Implement the audit engine in `…ApiDelta.Core/Audit/AuditEngine.cs`: one pass over the tree with rules keyed by the node kinds they visit, per-rule finding caps, and a `RuleOutcome` for every registered rule whether it produced findings or not (R15)
- [X] T098 [US5] Add the default severity and safety table in `…ApiDelta.Core/Audit/RuleDefaults.cs` matching the catalogue in [spec.md](./spec.md) exactly, with a test that compares the table against the specification row by row

### Validity rules (AUD-001 to AUD-010)

- [X] T099 [P] [US5] AUD-001 invalid structure, over the semantic adapter's verdicts, in `…ApiDelta.Core/Audit/Validity/InvalidStructureRule.cs` with tests
- [X] T100 [P] [US5] AUD-002 unresolved references with every using location in `…ApiDelta.Core/Audit/Validity/UnresolvedReferenceRule.cs` with tests
- [X] T101 [P] [US5] AUD-003 unreachable components, SAFE only after complete reference analysis and no keep-pattern match, in `…ApiDelta.Core/Audit/Validity/UnusedComponentRule.cs` with tests
- [X] T102 [P] [US5] AUD-004 path-template and path-parameter mismatch in both directions in `…ApiDelta.Core/Audit/Validity/PathParameterRule.cs` with tests
- [X] T103 [P] [US5] AUD-005 missing and duplicate operation identifiers in `…ApiDelta.Core/Audit/Validity/OperationIdRule.cs` with tests
- [X] T104 [P] [US5] AUD-006 invalid and inconsistent response definitions in `…ApiDelta.Core/Audit/Validity/ResponseDefinitionRule.cs` with tests
- [X] T105 [P] [US5] AUD-007 unstated request-body requiredness, REVIEW_REQUIRED, in `…ApiDelta.Core/Audit/Validity/RequestBodyRequirednessRule.cs` with tests
- [X] T106 [P] [US5] AUD-008 nullability constructs against the declared version in `…ApiDelta.Core/Audit/Validity/NullabilityVersionRule.cs` with tests
- [X] T107 [P] [US5] AUD-009 type and format combinations, heuristics reported as suggestions, in `…ApiDelta.Core/Audit/Validity/TypeFormatRule.cs` with tests
- [X] T108 [P] [US5] AUD-010 empty and misleading fields in `…ApiDelta.Core/Audit/Validity/EmptyFieldRule.cs` with tests

### Redundancy rules (AUD-100 to AUD-107)

- [X] T109 [P] [US5] AUD-100 repeated inline parameters, grouped with count, locations and estimated saving, in `…ApiDelta.Core/Audit/Redundancy/RepeatedParameterRule.cs` with tests
- [X] T110 [P] [US5] AUD-101 repeated header sets, with the reusable or path-level alternative, in `…ApiDelta.Core/Audit/Redundancy/RepeatedHeaderSetRule.cs` with tests
- [X] T111 [P] [US5] AUD-102 repeated responses including the standard error set in `…ApiDelta.Core/Audit/Redundancy/RepeatedResponseRule.cs` with tests
- [X] T112 [P] [US5] AUD-103 repeated request bodies and media types in `…ApiDelta.Core/Audit/Redundancy/RepeatedRequestBodyRule.cs` with tests
- [X] T113 [P] [US5] AUD-104 duplicate schemas, exact and near kept in separate groups and differently named duplicates never SAFE, in `…ApiDelta.Core/Audit/Redundancy/DuplicateSchemaRule.cs` with tests
- [X] T114 [P] [US5] AUD-105 duplicate examples and headers in `…ApiDelta.Core/Audit/Redundancy/DuplicateExampleRule.cs` with tests
- [X] T115 [P] [US5] AUD-106 duplicate enums, never automatically merged, in `…ApiDelta.Core/Audit/Redundancy/DuplicateEnumRule.cs` with tests
- [X] T116 [P] [US5] AUD-107 components differing only in documentation, with the differing fields named, in `…ApiDelta.Core/Audit/Redundancy/EquivalentAliasRule.cs` with tests

### Consistency and formatting rules (AUD-200 to AUD-210)

- [X] T117 [P] [US5] AUD-200 canonical formatting departures in `…ApiDelta.Core/Audit/Consistency/FormattingRule.cs` with tests
- [X] T118 [P] [US5] AUD-201 unstable ordering, never reordering a semantically ordered array, in `…ApiDelta.Core/Audit/Consistency/OrderingRule.cs` with tests
- [X] T119 [P] [US5] AUD-202 inconsistent response-key quoting in `…ApiDelta.Core/Audit/Consistency/ResponseKeyQuotingRule.cs` with tests
- [X] T120 [P] [US5] AUD-203 header casing variants, reported and never merged, in `…ApiDelta.Core/Audit/Consistency/HeaderCasingRule.cs` with tests
- [X] T121 [P] [US5] AUD-204 naming conventions in `…ApiDelta.Core/Audit/Consistency/NamingConventionRule.cs` with tests
- [X] T122 [P] [US5] AUD-205 deprecation phrase without the flag in `…ApiDelta.Core/Audit/Consistency/DeprecationMetadataRule.cs` with tests
- [X] T123 [P] [US5] AUD-206 date and date-time strings without a format in `…ApiDelta.Core/Audit/Consistency/DateFormatRule.cs` with tests
- [X] T124 [P] [US5] AUD-207 escaped markup, suspicious encoding, duplicated descriptions and placeholders in `…ApiDelta.Core/Audit/Consistency/DescriptionQualityRule.cs` with tests
- [X] T125 [P] [US5] AUD-208 required-array consistency and arrays without items in `…ApiDelta.Core/Audit/Consistency/RequiredArrayRule.cs` with tests
- [X] T126 [P] [US5] AUD-209 inconsistent object openness in `…ApiDelta.Core/Audit/Consistency/AdditionalPropertiesRule.cs` with tests
- [X] T127 [P] [US5] AUD-210 known vendor-extension structures against their configured schema, unknown ones left opaque, in `…ApiDelta.Core/Audit/Consistency/ExtensionStructureRule.cs` with tests

### Maintainability and governance rules (AUD-300 to AUD-305)

- [X] T128 [P] [US5] AUD-300 missing configured standards in `…ApiDelta.Core/Audit/Governance/MissingStandardsRule.cs` with tests
- [X] T129 [P] [US5] AUD-301 the same header required in some comparable operations and optional in others in `…ApiDelta.Core/Audit/Governance/InconsistentHeaderRequirednessRule.cs` with tests
- [X] T130 [P] [US5] AUD-302 security overridden or disabled, including an empty requirement, in `…ApiDelta.Core/Audit/Governance/SecurityOverrideRule.cs` with tests
- [X] T131 [P] [US5] AUD-303 server and environment placeholders in `…ApiDelta.Core/Audit/Governance/PlaceholderRule.cs` with tests
- [X] T132 [P] [US5] AUD-304 gateway configuration consistency, reported without alteration, in `…ApiDelta.Core/Audit/Governance/VendorConfigurationRule.cs` with tests
- [X] T133 [P] [US5] AUD-305 dead metadata in `…ApiDelta.Core/Audit/Governance/DeadMetadataRule.cs` with tests

### Delivery for User Story 5

- [X] T134 [US5] Wire `AuditStep` in `…ApiDelta.Core/Steps/AuditStep.cs`, registering every rule explicitly in the DI extensions and letting `CompositionRootTests` catch one that was written but never wired
- [X] T135 [US5] Publish the audit report artifact from `…ApiDelta.Core/Reporting/MarkdownReportWriter.cs`, carrying sections 2, 3, 12, 13, 17, 18 and 19 and nothing from the contract delta (FR-411)
- [X] T136 [US5] Extend the JSON report with `ruleOutcomes`, `findings` and `redundancyGroups` per [contracts/artifacts.md](./contracts/artifacts.md) §4
- [X] T137 [US5] Add the findings views in `…ApiDelta.Presentation/Rendering/FindingsView.cs`: grouped by rule, paged, with the rendering of [contracts/artifacts.md](./contracts/artifacts.md) §3
- [X] T138 [US5] Surface rules that could not be evaluated on the summary panel, not buried in a drill-down (FR-418)
- [X] T139 [US5] Add the golden audit report for `redundant.yaml` in `tests/DevToolbox.Tests/Tools/ApiDelta/Golden/`

**Checkpoint**: the audit stands on its own, and says honestly what it did not check.

**Notes on this phase, recorded while doing it**

- The 35 rules run in **one pass**. The engine walks the tree once, labels every node with the kind of
  OpenAPI object it is, and hands each rule only the nodes of the kinds it declares. Rules are therefore
  stateless and safe as singletons, and 35 rules cost one traversal rather than 35 — which is the
  difference between a 5-second job and a 90-second one on a 15 MB file.
- Labelling a node needed one distinction the metamodel does not make: the table describes a *field* by
  what it contains, so `responses` announces a response. The node carried by that field is the container,
  not the member. Conflating the two would file a `responses` map and each of its responses under the same
  kind, and a rule asking for responses would get both.
- **Found by running it, not by reading it.** The first full run over the corpus produced five classes of
  false positive, each of which would have discredited the whole report: `bearerAuth` called unreachable
  although the document's `security` block names it (a security requirement is not a `$ref`, so the
  reference graph never sees it); `libelle` called a date because it ends in the letters of `le`; the keys
  of `components.responses` counted as bare response codes; two schemas with nothing in common called
  near-duplicates because they "differ only in `properties` and `required`"; and AUD-006 and AUD-300
  reporting the same missing error codes on the same operations. All five are fixed, and each fix has a
  test that fails without it.
- AUD-006's duplicate-semantic-code check was **removed rather than kept as a safety net**: the loader
  resolves scalars before it looks for duplicate keys, so `'400'` and `400` in one map is already refused
  at read time and no such document reaches the audit. A guard over an unreachable state suggests the
  audit is what protects against it. Recorded as AUD-006a, with AUD-006b for the AUD-300 overlap.
- Three ruleset additions were needed by rules the spec describes as configurable and had nowhere to read
  from: `KeepComponentPatterns` (AUD-003's keep rule), `Naming` (AUD-204's conventions) and
  `KnownExtensionFields` (AUD-210's declared shapes). Two of the three default to **silence**: a tool that
  imposed its own naming taste would produce hundreds of findings on first run, which is how an audit
  becomes something nobody opens.
- `AutofixAvailable` is **computed, not set**: a finding is fixable only if its remedy is proven safe *and*
  equivalence was established under the strict profile. A rule cannot declare itself applicable by
  accident.
- The rule catalogue is compared to `spec.md` **on disk**, row by row, severity and safety. A transcription
  kept by hand in the test would drift silently, which is the defect the test exists to prevent.

---

## Phase 8: User Story 6 — Know what cleanup would be worth, before doing any of it (Priority: P3)

**Goal**: a plan with measured savings, break-even counts and safety classes, which changes nothing.

**Independent Test**: produce a plan for `redundant.yaml`; every proposal carries occurrences, current and projected size, saving, break-even count and safety class, and the production file is byte-identical afterwards.

### Tests for User Story 6

- [X] T140 [P] [US6] Savings tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Optimization/SavingsTests.cs`: every field present; a group at the break-even count reports a saving of zero or less rather than a recommendation; a plan-only run leaves production byte-identical (SC-007)

### Implementation for User Story 6

- [X] T141 [US6] Implement serialized-size measurement from byte spans in `…ApiDelta.Core/Fingerprinting/SizeMeasurement.cs`
- [X] T142 [US6] Implement `SavingsEstimate` with absolute and percentage saving and break-even count in `…ApiDelta.Core/Optimization/SavingsEstimate.cs` (FR-403)
- [X] T143 [US6] Assemble `RedundancyGroup`s with occurrences, sizes and tier in `…ApiDelta.Core/Optimization/RedundancyGrouper.cs`
- [X] T144 [US6] Produce `Transformation` proposals in the `Proposed` state from redundancy groups in `…ApiDelta.Core/Optimization/TransformationPlanner.cs`, each carrying its rule, origins, destination, evidence fingerprint and reversal description
- [X] T145 [US6] Implement `OptimizationPlan` and the growth check in `…ApiDelta.Core/Optimization/OptimizationPlan.cs` (FR-555)
- [X] T146 [US6] Wire `PlanStep` and publish the optimization plan artifact
- [X] T147 [US6] Fill report sections 13 to 16 and the JSON `optimization.transformations` and `sizes`
- [X] T148 [US6] Add the redundancy-group and proposal views in `…ApiDelta.Presentation/Rendering/ProposalsView.cs`, separating safe from review-required
- [X] T149 [US6] Add the golden optimization plan for `redundant.yaml` plus the assertion that the input file's hash is unchanged after the run

**Checkpoint**: the developer can decide about cleanup without doing any of it.

**Notes on this phase, recorded while doing it**

- **The rule measures, the plan records.** The first design had the plan re-measure each occurrence by
  resolving the finding's locations in the tree. It is wrong for AUD-101, whose locations are *operations*
  while the repeated thing is the header set inside them: the plan announced fourteen whole operations as
  recoverable. A finding now publishes `OccurrenceBytes` and `ReferenceBytes` as part of its evidence, which
  also makes the saving checkable by a reader and makes it impossible for sections 12 and 13 to disagree.
  Recorded as FR-403c.
- **The destination family is read from the location, not from the rule.** AUD-103 reports repeated request
  bodies *and* repeated media types; a media type inside `responses` is not a request body. Mapping the rule
  to a family proposed moving response media types into `components.requestBodies` — a description that
  would not reparse. The family now comes from the pointer, and a pointer designating no family yields no
  proposal. That is also why nothing is proposed for repeated media types, duplicate named schemas,
  identical enumerations or equivalent aliases: the tool has found them and cannot transform them, and the
  finding says so. Recorded as FR-403b.
- **`schemas` is deliberately absent from the family-to-rule table.** Extracting an inline schema is
  OPT-006, disabled by default because it renames types in generated clients. Listing it with a per-rule
  setting that defaults to enabled would have turned it on without anyone asking.
- **A group reported is not a group recommended.** `SavingsEstimate.Recommended` is false whenever the
  saving is not positive or the safety class is not actionable, so a near-duplicate and a two-occurrence
  group below its break-even count both appear in the table, chiffrés, without being advised. The data
  model's invariant said "at or below the break-even count", which is arithmetically false — at the
  break-even count the saving is positive by construction. Corrected, and recorded as FR-403a.
- **`Uncovered` exists because an empty proposal list is ambiguous.** It reads as a clean file when it may
  mean the tool does not know how to act on what it found. The count is on the first screen and in
  section 16.
- **AuditStep and PlanStep were missing from `CompositionRootTests`.** Phase 7 wired AuditStep into the tool
  but not into the two assertions that resolve every step from the real graph. Both are there now.
- **Found by running it, not by reading it.** Both of the first two notes came from dumping the real plan for
  `redundant.yaml` before writing a single assertion. Neither was visible in review.

---

## Phase 9: User Story 7 — Apply only what is provably safe, to a copy, verified (Priority: P3)

**Goal**: an optimized copy that the tool has proved contract-equivalent to the original, with a manifest, and nothing published if the proof fails.

**Independent Test**: apply safe optimizations to `redundant.yaml`; the output reparses, revalidates and verifies equivalent, production is untouched, every vendor extension survives, review-required transformations stay proposals. Then inject a contract-altering transformation and confirm nothing is published.

### Tests for User Story 7

- [X] T150 [P] [US7] Application tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Optimization/ApplicationTests.cs`: verified output published; injected contract change publishes nothing and reports `outputFailure` (SC-008); review-required never applied without its rule identifier; every extension retained (SC-010)

### Implementation for User Story 7

- [X] T151 [US7] Implement canonical emission in `…ApiDelta.Infrastructure/Emitting/CanonicalEmitter.cs`: configured indentation, line width, quoting, line endings, final newline and key order, deterministic for identical input (OPT-001)
- [X] T152 [US7] Implement tree-editing primitives in `…ApiDelta.Core/Optimization/TreeEdits.cs`: replace a node with a reference, insert a component, remove a component, each returning a new tree
- [X] T153 [P] [US7] OPT-002 extract strictly equivalent repeated parameters, preserving override behaviour and insertion scope, in `…ApiDelta.Core/Optimization/Rules/ExtractParametersRule.cs` with tests
- [X] T154 [P] [US7] OPT-003 extract strictly equivalent repeated responses in `…ApiDelta.Core/Optimization/Rules/ExtractResponsesRule.cs` with tests
- [X] T155 [P] [US7] OPT-004 extract strictly equivalent repeated request bodies in `…ApiDelta.Core/Optimization/Rules/ExtractRequestBodiesRule.cs` with tests
- [X] T156 [P] [US7] OPT-005 extract strictly equivalent repeated headers and examples in `…ApiDelta.Core/Optimization/Rules/ExtractHeadersAndExamplesRule.cs` with tests
- [X] T157 [P] [US7] OPT-006 inline schema extraction, **disabled by default** and REVIEW_REQUIRED, in `…ApiDelta.Core/Optimization/Rules/ExtractInlineSchemasRule.cs` with tests including the test that it does not run by default
- [X] T158 [P] [US7] OPT-007 remove unreachable components, SAFE only with an external-reference index and REVIEW_REQUIRED without one, in `…ApiDelta.Core/Optimization/Rules/RemoveUnusedComponentsRule.cs` with tests
- [X] T159 [P] [US7] OPT-008 hoist parameters shared by every operation of a path, only where override behaviour is unchanged, in `…ApiDelta.Core/Optimization/Rules/HoistPathParametersRule.cs` with tests
- [X] T160 [P] [US7] OPT-009 add the deprecation flag under a configured phrase policy, **disabled by default**, in `…ApiDelta.Core/Optimization/Rules/AddDeprecationFlagRule.cs` with tests
- [X] T161 [US7] Enforce OPT-010 as a constraint over every rule in `…ApiDelta.Core/Optimization/ExtensionProtection.cs`: no transformation removes, merges or relocates `x-` data, with a test running every rule against `baseline-30.yaml` and asserting extension survival
- [X] T162 [US7] Implement `IContractEquivalenceVerifier` in `…ApiDelta.Core/Optimization/ContractEquivalenceVerifier.cs`: reparse the serialized bytes, revalidate, resolve references, compare under the `contract` profile, and report what equivalence found when it fails (OPT-011, R14)
- [X] T163 [US7] Implement the manifest writer in `…ApiDelta.Core/Optimization/ManifestWriter.cs` per [contracts/artifacts.md](./contracts/artifacts.md) §6, written in the same step as the optimized document (OPT-012)
- [X] T164 [US7] Implement the apply path in `…ApiDelta.Core/Optimization/OptimizationApplier.cs`: SAFE only, REVIEW_REQUIRED only when its identifier was explicitly allowed, UNSAFE never, and no behaviour changed to reduce size nor deprecated operation removed (FR-551, FR-554)
- [X] T165 [US7] Implement the in-place write in `…ApiDelta.Infrastructure/Files/InPlaceWriter.cs`: explicit request, explicit confirmation naming the file, a backup unless declined, and refusal when the file changed on disk since it was read (FR-550)
- [X] T166 [US7] Make verification failure publish nothing and resolve the outcome to `outputFailure` in `…ApiDelta.Core/Steps/WriteStep.cs` (FR-552)
- [X] T167 [US7] Add the optimization intent prompt and the three confirmations of [contracts/console-experience.md](./contracts/console-experience.md) §5 in `…ApiDelta.Presentation/Prompts/`
- [X] T168 [US7] Add the golden optimized document for `redundant.yaml` plus the assertion that the measured saving is within 10% of the estimate (SC-012)

**Checkpoint**: the optimizer proves its own work, or publishes nothing.

**Notes on this phase, recorded while doing it**

- **The comparison had to stop comparing `components`.** The first real apply run failed verification on
  every transformation at once: extraction adds a component, OPT-007 removes one, and a whole-document
  contract comparison reads both as contract changes. Once references are resolved, the component store
  carries the *filing* of the contract, not the contract. What escapes that — a security scheme, named by
  key rather than by reference — became a referential-integrity check beside unresolved references.
  Recorded as OPT-011a.
- **The verification runs on the serialized bytes, which needed a reader that takes bytes.** `ISpecReader`
  gained `ReadBytes`. Verifying the edited tree instead would have passed over precisely the defects that
  writing introduces.
- **OPT-002 and OPT-008 compete, and the competition was silent.** Both target the same parameters; applied
  after the extraction, the hoist finds only `$ref` nodes and reports that no path qualifies. The applier
  now tracks touched locations by containment and declines the loser by name, ordering by safety class so
  the proven remedy wins — not by rule identifier, which would have given the same answer by accident.
  Recorded as OPT-002a.
- **Two defects found by the test OPT-010 demanded.** Running every rule against every extension-bearing
  location of `baseline-30.yaml` exposed that two extractions sharing a destination overwrite each other —
  leaving the first one's references pointing at the second one's content — and that an origin containing
  its own destination carries away what was just inserted, the document root being the limiting case. Both
  are now refused. Recorded as OPT-002b.
- **Removing vendor data is not relocating it.** OPT-007 removing an unreachable component that carries an
  `x-` key is a loss, whatever the prefix; an extraction moving one is not, because resolving the reference
  finds it again. `ExtensionProtection.Guards` takes the distinction, and `IOptimizationRule.Removes`
  declares which side a rule is on — one rule says true, and a test holds that number. Recorded as OPT-010a.
- **The replacement confirmation is asked once, before anything is written.** The store gained
  `PrepareAsync`, and the write step hands it the complete list of file names by yielding each publisher
  with its name instead of reconstituting the list — three prompts for three reports is a ceremony, and a
  ceremony is answered without being read. `ConfirmOverwrite` split into `ConfirmReplace` (reports) and
  `ConfirmInPlaceWrite` (the one answer that does not undo by deleting a file).
- **The optimized copy keeps the configured key order, which sorts by default.** Not changed: OPT-001 says
  the order is configured, FR-556 warns that regenerated output does not preserve the original order, and a
  team that wants a readable diff sets `keyOrder: preserve`. Changing the shipped default would have made
  FR-556's warning untrue.
- **`AuditStep` and `PlanStep` are in the composition-root assertions, and so are the eight optimization
  rules** — the last via their own catalogue test, read from `spec.md` on disk, because a rule written and
  never wired would make the applier decline a transformation the plan had proposed.

---

## Phase 10: User Story 8 — Make the rules the team's own (Priority: P3)

**Goal**: severities, thresholds, enablement and suppressions configurable by identifier, with expired suppressions reported and suppressed findings listed.

**Independent Test**: run the same audit with and without a ruleset that lowers one severity, disables one rule and raises one occurrence threshold; then suppress a finding without a justification (invalid configuration), and with a justification and a past expiry (reported anyway).

### Tests for User Story 8

- [X] T169 [P] [US8] Configuration tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Configuration/RulesetTests.cs`: threshold change takes effect; a disabled rule is reported as disabled; an unknown rule identifier or top-level key is invalid configuration; an unjustified suppression prevents the run from starting; an expired suppression reports both finding and expiry

### Implementation for User Story 8

- [X] T170 [US8] Implement `IRulesetStore` in `…ApiDelta.Infrastructure/Files/RulesetStore.cs` reading the YAML of [contracts/configuration.md](./contracts/configuration.md) §1, failing on an unknown rule identifier or top-level key, and never falling back to defaults silently (FR-650, FR-655)
- [X] T171 [US8] Apply typed per-rule settings and thresholds in `…ApiDelta.Core/Configuration/RuleSettingsResolver.cs`, with severity never able to change a safety class (FR-412)
- [X] T172 [US8] Implement `ISuppressionStore` and pointer-glob matching in `…ApiDelta.Infrastructure/Files/SuppressionStore.cs` and `…ApiDelta.Core/Configuration/PointerGlob.cs` (FR-652)
- [X] T173 [US8] Enforce `requireJustification` before the run starts in `…ApiDelta.Core/Configuration/SuppressionValidator.cs` (FR-652, FR-655)
- [X] T174 [US8] Implement expiry and unmatched reporting in `…ApiDelta.Core/Configuration/SuppressionApplier.cs`: an expired suppression is not honoured, an unmatched one is reported (FR-653)
- [X] T175 [US8] Add report section 17 and the JSON `suppressions` array, listing every suppressed finding with justification and owner (FR-654, FR-608)
- [X] T176 [US8] Add ruleset and suppression prompts with discovery of conventionally named files beside the production description in `…ApiDelta.Presentation/Prompts/`
- [X] T177 [US8] Implement the configurable failure threshold and the full outcome-code resolution in `…ApiDelta.Core/OutcomeResolver.cs`, adding `thresholdExceeded` and `breakingChange` to the codes T024 established (FR-707, FR-708)
- [X] T178 [US8] Display the outcome code when the run ends and write it to the JSON report, with a test asserting it is **not** returned as the process exit code and that `ExitCodes` gained no member (FR-708a)
- [X] T179 [US8] Fill the shipped default values in `…ApiDelta.Core/Configuration/`: the `x-sg-` protected prefix, the deprecation phrases, the `$(catalog.host)` placeholder pattern and the standard header and response lists, with a test that the tool produces usable results with no configuration file present (FR-656)

**Checkpoint**: the audit is the team's, and the tool still works out of the box.

**Notes on this phase, recorded while doing it**

- **The configuration is read by the tool's own tree builder, not by a deserializer.** Every diagnostic
  therefore carries a line and a column, and an unknown key can be *named*. A tolerant deserialization is
  precisely what produces a configuration file whose misspelled section does nothing for a year.
- **Reading and validating happen in the Select step, before any document is read.** An unjustified
  suppression discovered mid-audit would leave two bad answers — honour it and silence an unmotivated
  finding, or ignore it and report a finding the team believed silenced. Refusing to start is the third.
  The steps that follow read the ruleset from the context rather than falling back to the shipped values,
  because falling back would apply a configuration nobody wrote.
- **Suppressions are applied after the rules, never inside them.** A rule's outcome keeps counting what it
  found, and the findings list carries only what remains to be dealt with; section 17 explains the gap.
  Folding suppression into the engine would have made "the rule found nothing" and "we told it to be
  quiet" indistinguishable, which is the same class of error as reporting an unevaluated rule as clean.
- **All sixteen sections of the contract's §1 are read.** Seven of them (`limits`, `references`,
  `canonicalFormatting`, `versioning`, `namingConventions`, `environmentPlaceholders`, `extract`) had types
  on `Ruleset` but no way in from the file. Leaving them out would have made the contract false in one of
  two ways: refusing a documented key as unknown, or — worse — accepting it and doing nothing.
  `references.allowRemote` without a non-empty `remoteHosts` is refused, as the contract requires.
- **An unreadable `expires` is an error, not an absent expiry.** Treating it as "no expiry" would make a
  suppression perpetual by typo, which is the opposite of what its author wrote.
- **`CompositionRootTests` caught the two unregistered stores** before anything else did — the step would
  not construct. That is the test working as designed, for the second phase running.
- **The outcome code is a word, and the test says so.** `OutcomeCodeNames` publishes `outputFailure`, not
  the `ToString().ToLowerInvariant()` of the enum member, and a test asserts no code's published name
  parses as an integer and that `ExitCodes` still has exactly its four shell-level members.

---

## Phase 11: User Story 9 — Take one route out, as a description you can actually use (Priority: P3)

**Goal**: the developer picks operations out of a long description and gets a standalone, dependency-complete, verified OpenAPI description of just those — with what they inherit, without what they did not choose, and without touching the source.

**Independent Test**: select one operation that shares its path with others, inherits path-level parameters, is covered by the global security requirement and references a chain of schemas; confirm the extract contains it and nothing else, resolves completely, keeps the inherited parameters and the scheme, and is contract-identical in effective form to the same operation in the source.

**Depends on**: US2 (the read) and the dependency closure built in US4 (T083 to T086). Independent of the comparer, the version engine, the audit and the optimizer — it can be pulled forward right after the MVP if the need is immediate.

### Tests for User Story 9

- [X] T180 [P] [US9] Extraction correctness tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Extraction/ExtractTests.cs`: a single operation taken from a four-operation path yields only that method; its inherited path-level parameters are present; the global security requirement and only the scheme it names are carried; every schema in the chain appears exactly once; no unselected path or component is present; `info.version` is unchanged; the source is byte-identical afterwards — then the exhaustive pass, extracting **every** selectable operation of `baseline-30.yaml` one run each and asserting all of it for each (SC-017)
- [X] T181 [P] [US9] Extraction refusal tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Extraction/ExtractRefusalTests.cs`: an empty selection is refused before any work; a builder deliberately broken to drop a path-level parameter fails verification, names the operation and the parameter, and publishes nothing; a selected operation whose only dependency is an external reference is refused while external references are disabled

### Implementation for User Story 9

- [X] T182 [P] [US9] Add `OperationSelector`, `SelectableOperation`, `ExtractRequest`, `ExtractProvenance`, `InheritanceRecord`, `ExtractResult` and `ExtractVerification` in `…ApiDelta.Core/Extraction/` ([data-model.md](./data-model.md) §8b)
- [X] T183 [US9] Implement `IOperationCatalog` in `…ApiDelta.Core/Extraction/OperationCatalog.cs`: one walk over the paths returning path, method, summary, tags, deprecation and pointer, carrying no schema and no example so that listing a thousand operations costs a thousand short strings (FR-902)
- [X] T184 [US9] Generalize the root set of `…ApiDelta.Core/Dependencies/DependencyResolver.cs` so one closure engine serves both the changed set of a patch and the chosen set of an extract, with US4's behaviour unchanged and its tests still green (FR-904)
- [X] T185 [US9] Implement `EffectiveOperation` assembly in `…ApiDelta.Core/Extraction/EffectiveOperation.cs`: merge path-item parameters under the override rules, resolve the servers in force at document and path level, and resolve the security requirement in force — this is the unit the verifier compares, and the reason an extracted endpoint cannot quietly lose what protected it (FR-905)
- [X] T186 [US9] Implement `IExtractBuilder` in `…ApiDelta.Core/Extraction/ExtractBuilder.cs`: the version field, `info` verbatim, the selected paths carrying only the selected methods plus their inherited parameters, the servers in force, the global security requirement with only the schemes it names, the tag definitions used, and the closure's components once each — and nothing the selection does not require (FR-903, FR-906, FR-907, FR-908)
- [X] T187 [US9] Carry vendor extensions into the extract unchanged, document-level ones included, and raise a finding — never an edit — for a carried extension that names a path or operation the extract does not contain, in `…ApiDelta.Core/Extraction/ExtensionCarryOver.cs` (FR-909)
- [X] T188 [US9] Implement the provenance stamp in `…ApiDelta.Core/Extraction/ProvenanceStamp.cs`: one `x-apidelta-extract` member at the document root carrying source path, source hash, timestamp, the selected operations and the partial-description note, enabled by default and switchable through `extract.stampProvenance`, touching no extension that was already there (FR-911)
- [X] T189 [US9] Implement `IExtractVerifier` in `…ApiDelta.Core/Extraction/ExtractVerifier.cs`: reparse the emitted bytes, revalidate, resolve every reference, assert nothing unselected is present, and compare each selected operation in effective form against the source under the contract profile — reusing `IContractEquivalenceVerifier`'s machinery rather than a second comparison path (FR-910)
- [X] T190 [US9] Wire `ExtractStep` in `…ApiDelta.Core/Steps/ExtractStep.cs` between `Plan` and `Write`, publish the extract artifact only on a fully true verification, skip the version step for this mode, and make the in-place path unavailable here (FR-908, FR-910, FR-912)
- [X] T191 [US9] Implement the operation picker in `…ApiDelta.Presentation/Prompts/OperationPicker.cs` per [contracts/console-experience.md](./contracts/console-experience.md) §2b: filter by tag or path fragment first, then a paged `MultiSelectionPrompt` grouped by tag with per-operation selection, refusal on an empty selection, and the pre-build confirmation naming what will be carried along. Confirm whether `MultiSelectionPrompt` in Spectre.Console 0.57.2 offers built-in search and use it if so — the filter step is required either way (FR-901, FR-902, SC-018)
- [X] T192 [US9] Add the extract result view in `…ApiDelta.Presentation/Rendering/ExtractView.cs`, the report's Extract section and the JSON `extract` object per [contracts/artifacts.md](./contracts/artifacts.md) §7: operations taken, what each inherited, closure size, carried tags, dangling extension references, verification verdict
- [X] T193 [US9] Persist the selectors as labelled checkpoint entries in `…ApiDelta.Presentation/ApiDeltaCheckpointFactory.cs`, and make resuming re-read the source and refuse — naming the operation — when a selected operation is no longer present (FR-913, FR-705)

**Checkpoint**: a developer can take one route out of a four-hundred-operation description and get something they can hand to someone else.

**Notes on this phase, recorded while doing it**

- **An inherited parameter written as `$ref` was neither named nor compared.** `baseline-30.yaml` declares
  its path-level parameter as a reference, so the inheritance record came out empty — and, far worse, an
  operation that redeclared the same parameter inline would not have masked it, leaving two parameters
  where the document sees one. `EffectiveOperation` now follows local references before taking a
  parameter's identity. Found by the first assertion that looked at the inheritance record.
- **The extract has its own report.** Section 20 had nowhere to go: in extract mode there is no candidate,
  so the comparison report is not written at all. Publishing the comparison report with eleven sections
  saying "Aucun" would read as a comparison that found nothing rather than one that never happened — the
  same reasoning that gave the audit its own report in Phase 7. Recorded as FR-910a.
- **The operation picker needed its own port.** The catalogue comes from the document, and the document is
  read after the mode is chosen, so the question cannot be asked in `AskForSelection`.
  `IOperationSelectionPrompt` lets the extract step ask once the document is in hand, and a resumed
  selection skips the question entirely.
- **`MultiSelectionPrompt` in Spectre.Console 0.57.2 has no incremental search**, so the filter step is not
  a convenience but the only thing that makes four hundred operations choosable. It would stay necessary
  anyway: a filter says what it kept, where a search only shows what it found.
- **Two extracts of two copies of the same file differ, legitimately.** The provenance stamp carries the
  source path. The determinism assertion is therefore two runs on the *same* source, which is what FR-604
  actually promises.
- **The "nothing unselected" gate needed a document without shared components to be tested at all.** With
  components to resolve, smuggling in a neighbouring operation fails the *reference* gate first, and the
  test would have passed while saying nothing about the gate it named.
- **One closure engine serves both the patch and the extract**, as T184 asked: passing the source as both
  candidate and production gives the same roots-to-closure walk, and US4's tests stayed green untouched.

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: the success criteria that span stories, and the documents that now describe something untrue.

- [X] T194 [P] Performance test in `tests/DevToolbox.Tests/Tools/ApiDelta/PerformanceTests.cs`: the generated 1,000-operation, 10,000-schema document audits in under 30 s with per-step timings recorded (SC-003)
- [X] T195 [P] Secret-leak test in `tests/DevToolbox.Tests/Tools/ApiDelta/SecretsTests.cs`: after a full run over `secrets.yaml`, the literal token and password appear in no artifact, no log file and no checkpoint, while the finding naming their locations is present (SC-016, FR-607)
- [X] T196 [P] Full determinism test in `tests/DevToolbox.Tests/Tools/ApiDelta/Golden/DeterminismTests.cs`: two complete runs into separate directories differ only inside the run-metadata block (SC-004)
- [X] T197 [P] Resume-equivalence test in `tests/DevToolbox.Tests/Tools/ApiDelta/ResumeTests.cs`: an interrupted and resumed run reaches artifacts identical to an uninterrupted one, and a resume whose input hash changed is refused with its reason (SC-015, FR-705)
- [X] T198 [P] Limit and robustness tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Loading/LimitTests.cs`: `deep.yaml` refused with the depth limit named rather than a stack overflow, `cyclic.yaml` completing, an oversized document refused with the size named (SC-011, FR-010)
- [X] T199 [P] Complete the mutation matrix in `tests/DevToolbox.Tests/Tools/ApiDelta/Mutation/`: no breaking mutation classified non-breaking **and** no transformation over one classified SAFE, across requiredness, nullability, enum membership, type, format, security and constraints (SC-002)
- [X] T200 [P] Degradation tests in `tests/DevToolbox.Tests/Tools/ApiDelta/Presentation/DegradationTests.cs` with `Spectre.Console.Testing`: no colour, no Unicode and a narrow terminal, with severity and safety carried by words rather than colour
- [X] T201 [P] Run `dotnet test DevToolbox.slnx --collect:"XPlat Code Coverage"` and close the gaps it shows in `…ApiDelta.Core`, where the whole engine is reachable without a console, a network or a file system
- [X] T202 Update `README.md`: three tools, apidelta described, and the prerequisites table corrected — a domain-joined machine, the corporate network and read access to a collection are requirements of the Azure DevOps tools, not of the toolbox, and apidelta is the tool that proves it
- [X] T203 Update `CLAUDE.md`: the third tool and the OpenAPI tab, the two new packages, and the design rule this feature adds to the list of things easy to break by accident — **nothing we publish is ever serialized from the semantic model**, because that is what keeps an unknown extension intact
- [X] T204 Record the resolved open items in `research.md`: the confirmed reading API of Microsoft.OpenApi 3.10.2 and what it preserves, the YamlDotNet member names, and the alias and anchor policy as implemented
- [X] T205 Reconcile specification and code: if implementation changed any described behaviour, amend `spec.md` first as a dated clarification, then the code — a set of artifacts that quietly stops matching the code is worse than no artifacts
- [X] T206 Walk [quickstart.md](./quickstart.md) end to end, all ten scenarios, and correct whatever it describes inaccurately
- [X] T207 Review every log statement in the tool against Principle VI: structured templates, a `RunId` scope, no document content, and the tool's value-bearing types registered with `RedactionRegistry`
- [X] T208 Confirm the architecture and composition tests cover the finished shape: Core free of both new packages, Presentation free of Infrastructure, every rule resolvable from the real graph, and the checkpoint shape guard still failing on a widened record
- [X] T209 Final gate: `dotnet build DevToolbox.slnx` with zero warnings, `dotnet test DevToolbox.slnx` green, `NuGetAudit` clean, and every golden artifact approved and reproducing byte-for-byte

**Notes on this phase, recorded while doing it**

- **The performance test found a crash, not a slowness.** A generated description whose ten thousand schemas
  each reference the next made `ReferenceGraph` descend ten thousand stack frames and die of a stack
  overflow — no diagnostic, no position, nothing said about what had been read. Such a shape is plausible: a
  linked list of types produces it. The walk now carries a depth, stops at four hundred links and reports
  the limit, which is what FR-010 asks of every budget. With that fixed, 1 000 operations and 10 000 schemas
  read, resolve, validate, audit and cost in **5.7 s** against a 30 s budget. Recorded in research as R18.
- **`SpecPointer.Json` was rebuilt on every call**, including inside the comparison used to sort the walked
  nodes. The pointer is immutable, so memoising it cannot lie; on a multi-megabyte description it removed
  millions of reconstructions of the same string.
- **The secret test found the tool leaking.** AUD-303 quoted the whole server URL in its message, password
  included — a report that would have been a second leak. The rule now names the location and not the value,
  which is what the rest of the audit already did. FR-607 is now enforced by an assertion over *every* file
  a run writes, not by discipline.
- **`ValueSummary` was missing from the redaction registry.** It exists to carry, bounded, what the document
  said; it had no business being loggable. Registered, and asserted in `CompositionRootTests`.
- **`nullable` carried the value rule written for `required`, whose polarity is the opposite.** Adding
  `nullable: true` *loosens* — a caller who sent null is now accepted — and the tool called it breaking with
  the change id `PARAMETER-REQUIRED-ADDED`. A `PermissionFlag` rule now holds the inverted polarity. Found
  by widening the mutation matrix to nullability, which is exactly what T199 asked for.
- **Two of my own test premises were wrong, and the code was right.** Adding `nullable: false` to a field
  that had none restates OpenAPI 3.0's default and changes nothing; an added enum value is policy-dependent
  by FR-304 and major by the shipped policy. Both assertions were rewritten to say what is true — the
  nullability one by comparing two mutations rather than one against the original.
- **The comparer is the wrong instrument for proving a transformation harmless**, and the test now says so.
  It compares what is *written*, and an extraction replaces an object with a reference to it. Proving
  nothing broke requires comparing what is *meant*, references resolved — which is why OPT-011 exists as a
  separate mechanism rather than a call to the comparer.
- **ANSI cannot be switched off in the degradation tests**, because Spectre refuses a selection prompt
  without it and the shell already refuses a non-interactive terminal with its own exit code. What the
  requirement is about — colour and Unicode — is switched off, and the meaning survives in words.
- **`ApiDelta.Core` sits at 91 % line coverage.** The only two zeroes were `RunModeNames` and the write
  step's patch publication path; both now have tests, the second asserting that every reference a patch
  carries designates a component the patch also carries.
- **T022 and T024 are closed here**, as Phase 2 said they would be: every port named by the contract now
  exists, and `ApiDeltaRun` carries the differences, the audit, the plan, the application and the extract.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (Phase 1)**: no dependencies. T001 first — if the audit gate fails, R1 or R2 reopens before any code exists.
- **Foundational (Phase 2)**: depends on Setup. **Blocks every story.** T010 before anything that writes a file; T011 to T014 move together, since the record, the store's allow-list, the prompt and varcompare's factory are one change; T015 to T018 move together for the same reason.
- **US2 (Phase 3)**: depends on Foundational. Blocks every other story — nothing can be compared, audited or optimized before it can be read.
- **US1 (Phase 4)**: depends on US2. With US2 it is the MVP.
- **US3 (Phase 5)**: depends on US1 — it consumes classified differences.
- **US4 (Phase 6)**: depends on US1 for the differences and on US3 for the version it carries.
- **US5 (Phase 7)**: depends on US2 only. **Independent of US1, US3 and US4**, and can be built in parallel with them by a second developer.
- **US6 (Phase 8)**: depends on US5's fingerprints and groups.
- **US7 (Phase 9)**: depends on US6's plan.
- **US8 (Phase 10)**: depends on US5 for something to configure. The shipped defaults in T023 are what let US5 be built without it.
- **US9 (Phase 11)**: depends on US2 and on US4's closure tasks (T083 to T086), and on nothing else. It is the one P3 story that could ship immediately after the MVP, because extraction has nothing to do with comparing two files.
- **Polish (Phase 12)**: depends on the stories it tests. T202 to T206 depend on everything being settled.

### Within each story

- Tests first, failing, then implementation.
- In US1: metamodel types → rows → walk → classification → reporting → rendering.
- In US5: canonicalizer → fingerprinter → profiles → buckets → engine → rules → delivery. **The rules depend on the engine and on nothing else, which is why all 35 are parallel.**
- In US7: emitter and edit primitives → rules → protection invariant → verifier → apply path.

### Parallel opportunities

- **Phase 1**: T004 to T008 together.
- **Phase 2**: T019 to T023 together, once the shared prerequisites land.
- **US5 rules**: T099 to T133 — **35 tasks, 35 files, no shared state.** The widest parallel front in the feature.
- **US7 rules**: T153 to T160 — eight tasks, eight files.
- **US9**: T180 and T181 together, then T182 alongside them; T183 to T189 are sequential within the builder.
- **Polish**: T194 to T201 together.
- **Across stories**: after US2, one developer can take US1 → US3 → US4 while another takes US5 → US6 → US7. US8 joins once US5 lands, and US9 as soon as US4's closure exists.

---

## Parallel Example: the audit rules

```bash
# After T098, the full rule front opens. Each is one file plus its tests:
Task: "T099 AUD-001 invalid structure in …ApiDelta.Core/Audit/Validity/InvalidStructureRule.cs"
Task: "T101 AUD-003 unreachable components in …ApiDelta.Core/Audit/Validity/UnusedComponentRule.cs"
Task: "T109 AUD-100 repeated inline parameters in …ApiDelta.Core/Audit/Redundancy/RepeatedParameterRule.cs"
Task: "T119 AUD-202 response-key quoting in …ApiDelta.Core/Audit/Consistency/ResponseKeyQuotingRule.cs"
Task: "T130 AUD-302 security overridden or disabled in …ApiDelta.Core/Audit/Governance/SecurityOverrideRule.cs"
# … 30 more
```

---

## Implementation Strategy

### MVP first (US2 + US1)

1. Phase 1: Setup — and stop immediately if `NuGetAudit` fails T001.
2. Phase 2: Foundational — the three shared prerequisites and the spine.
3. Phase 3: US2 — it reads faithfully or it refuses.
4. Phase 4: US1 — it says what changed and what breaks.
5. **Stop and validate**: quickstart scenarios 1, 2 and 3. This is a releasable tool with no audit engine at all, and it is the half that a text diff cannot do.

### Incremental delivery

1. MVP → the delta, classified, with artifacts.
2. US3 → the version number and its reasons.
3. US4 → a patch you can merge by hand.
4. US5 → the audit, which stands alone and is the other half of the product.
5. US6 → what cleanup would be worth.
6. US7 → cleanup applied to a copy, verified.
7. US8 → the rules become the team's.
8. US9 → one route out, as a description you can hand to someone.

Each step is demonstrable, and each leaves production untouched.

**If extraction is the urgent one**, it can go third: MVP → US4's closure (T083 to T086) → US9. It needs
the reader and the closure, and nothing from the version engine, the audit or the optimizer.

### Parallel team strategy

Two developers, after Phase 3:

- **A**: US1 → US3 → US4 — the contract half.
- **B**: US5 → US6 → US7 — the quality half, starting with the 35-wide rule front.
- Either takes US8 once US5 is in, and US9 once US4's closure is in.

The two halves meet only in the report writers and the summary panel, which is where to expect the merge conflicts.

---

## Notes

- `[P]` means a different file and no dependency on an incomplete task.
- Comments in French, in the repository's register, with **no specification identifiers in code or
  comments** — `AUD-101` belongs in a rule's `RuleId` property, which is its output contract, not in a
  comment pointing at a document.
- One public type per file, named for the file; methods at 60 lines or fewer. The analyzers enforce both.
- `ConfigureAwait(false)` in every library assembly; a `CancellationToken` on every async public member.
- Commit per task or per logical group. Stop at any checkpoint and validate the story on its own.
- A behaviour change goes into `spec.md` first, as a clarification, and only then into the code.

## Phase 13 : La production corrigée (FR-500 à FR-518)

Ouverte le 2026-10-21, après la clarification qui a retiré le fragment. La phase est en cours : les tâches
cochées sont construites et éprouvées, les autres restent à faire. `tasks.md` dit donc où en est le code,
et non où en est l'intention.

### Le socle d'écriture

- [X] T210 Les types de la sélection et du plan dans `tools/ApiDelta/DevToolbox.Tools.ApiDelta.Core/Correction/` : `RouteState`, `DifferenceSelector`, `CorrectionSelection`, `CorrectionRoute`, `CorrectionPlan`, `PlannedDifference`, `DeclinedCorrection`
- [X] T211 `CorrectionCatalog` : la liste par route, routes sans rien à reporter comprises (FR-502)
- [X] T212 `Splice`, `SpliceKind` et le port `ISplicingEmitter` dans `Abstractions/ISplicingEmitter.cs`
- [X] T213 `SplicingEmitter` dans `tools/ApiDelta/DevToolbox.Tools.ApiDelta.Infrastructure/Emitting/SplicingEmitter.cs` : réassemble les octets de la production autour de ce qui change (FR-506)
- [X] T214 [P] `SplicingTests` : rien n'est touché hors des épissures, chevauchement refusé, JSON refusé en le disant, cible absente nommée
- [X] T215 `ByteOffsets` et la traduction caractère → octet dans `YamlTreeBuilder` — défaut préexistant, voir les notes
- [X] T216 [P] `SpecReaderTests.Every_scalar_span_designates_that_scalar_in_the_bytes` : l'épreuve qui tient la garantie que le va-et-vient ne pouvait pas tenir
- [X] T217 `CorrectionMark`, `CorrectionRequest`, `BuiltCorrection` et le port `ICorrectionBuilder`
- [X] T218 `CorrectionBuilder` : routes entières, différences une par une, composants de la fermeture, version, déclaration du document (FR-503 à FR-513)
- [X] T219 [P] `CorrectionBuilderTests` : sélection vide refusée, route portée, production intacte mot pour mot, déclaration présente, origine tracée, route déjà portée écartée en le disant, un plan n'écrit rien, une différence seule ne change que son champ

### La chaîne, de la sélection à l'écriture

- [X] T220 `CorrectionVerifier` et le port `ICorrectionVerifier` : relire, revalider, résoudre, établir que ce qui n'était pas sélectionné est identique à l'octet et que ce qui l'était ne diffère que comme demandé (FR-514)
- [X] T221 [P] Essais du vérificateur, dont un constructeur délibérément cassé qui touche une route non sélectionnée
- [X] T222 `ICorrectionSelectionPrompt` et l'écran de sélection dans `Presentation/Prompts/`, groupé par route, avec effet et confiance (FR-502, FR-505)
- [X] T223 L'annonce avant écriture, et l'acceptation ou le retour (FR-505)
- [X] T224 L'étape `Correct` dans `ApiDeltaStepNames` et `Steps/CorrectStep.cs`, après `Version` et avant `Write`
- [X] T225 `RunSelection.Correction`, la projection dans le point de reprise et la reprise sans nouvelle question (FR-517)
- [X] T226 `RunMode.Patch` renommé `Correct`, `RunModeNames`, `ArtifactKind.CorrectedDocument`, `ArtifactNames.CorrectedDocument`
- [X] T227 Retrait du fragment : `PatchWriter`, `PatchRequest`, `IPatchWriter`, `FragmentEntry`, `IPatchValidator`, `PatchValidator`, `ISpecEmitter.EmitFragment` et la section du rapport qui le nommait
- [X] T228 La section du rapport qui nomme le document, liste la sélection et nomme les routes sans rien à reporter (FR-516)
- [X] T229 Enregistrement dans `ServiceCollectionExtensions` des deux projets, et `CompositionRootTests`
- [X] T230 [P] Déterminisme : mêmes entrées et même sélection donnent des octets identiques hors bloc de métadonnées (FR-518)
- [X] T231 [P] Qu'aucune entrée ne produise une écriture sur la production (FR-515)
- [X] T232 Mettre à jour `README.md` et `quickstart.md` : la sortie est la production corrigée, et il n'y a plus de fragment

### Notes de cette phase, consignées en la faisant

**Un défaut préexistant, trouvé en exécutant.** L'épisseur écrivait `requirtruealse` au lieu de
`required: true`. Cause : `ByteSpan` se documente en octets et `YamlTreeBuilder` y mettait l'index de
caractère de l'analyseur. Sur `baseline-30.yaml`, quatre caractères accentués précédaient le nœud, d'où
quatre octets de décalage. Toute étendue située après la première lettre accentuée était donc fausse — ce
qui atteint la mesure des tailles, donc **les économies que l'optimiseur annonce**, et non seulement cette
phase. Corrigé par `ByteOffsets`.

**L'épreuve qui prétendait l'attraper ne pouvait pas.** `An_unmodified_tree_rewrites_itself_byte_for_byte`
dit en commentaire qu'« une étendue mal calculée se voit ici comme une différence d'octets ». C'est faux :
elle concatène les étendues dans l'ordre du document et les intervalles absorbent le décalage. Un arbre
dont chaque étendue serait décalée se réécrit quand même à l'identique. T216 prend chaque étendue
isolément ; vérifié en neutralisant la traduction, auquel cas elle échoue en nommant les nœuds.

**Un second défaut, de la même famille.** Le repère de fin d'une association de bloc est posé là où le
jeton suivant commence : l'étendue de `paths` finissait après la ligne `components:`, et la première
version de l'insertion plaçait la route neuve **sous `components`** — un document valide, décrivant autre
chose. `ContentEnd` recule sur les blancs.

**Ce que l'épissure permet et que le fragment interdisait.** `CarriedObject` énonce qu'une différence sur un
champ oblige à emporter l'opération entière, parce qu'un fragment ne peut pas porter un objet incomplet.
Une épissure remplace exactement l'emplacement visé : retenir une seule différence change donc ce champ et
rien d'autre, ce que FR-503 demande et qu'un fragment ne savait pas faire. `CarriedObject` reste employé
pour ce à quoi il sert vraiment ici, établir la fermeture.

**Une limite, consignée dans la spécification plutôt que découverte.** L'écriture exige une production en
YAML (FR-500a). L'émetteur canonique n'écrit que du YAML, et reformater un JSON en entier enterrerait le
changement demandé.
