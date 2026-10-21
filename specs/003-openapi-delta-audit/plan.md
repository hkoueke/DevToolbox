# Implementation Plan: apidelta — what a candidate OpenAPI description would do to production, and what production has accumulated

**Branch**: `003-openapi-delta-audit` | **Date**: 2026-10-16 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-openapi-delta-audit/spec.md`

## Summary

Point the tool at a candidate OpenAPI description and the one in production, and it answers what changed,
what breaks, which version to publish and why — and then, separately, what production has accumulated:
what is invalid, repeated, unreachable or inconsistent, what cleaning it up would save, and which of those
cleanups it can prove safe enough to apply to a copy. Production is never written by default, and nothing
optimized is published until the tool has reparsed its own output and proved it contract-equivalent.

Technical approach: a `Core` / `Infrastructure` / `Presentation` trio under `tools/ApiDelta/`, registered
with two lines in `Program.ConfigureServices`, reusing the shell, the step runner, the recovery prompt, the
checkpoint store, the file logging sink and the redaction registry. Two new packages — YamlDotNet and
Microsoft.OpenApi — centrally versioned.

The decision that shapes everything is **R2 and R7: we own the syntax layer**. Each input is parsed once
into a lossless node tree that carries positions, spans and scalar styles; the diff, the audit, the
fingerprints, the patch and the optimized output all work on that tree, guided by an OpenAPI metamodel
table we own. `Microsoft.OpenApi` is used behind a port for structural validation and reference resolution
only, and never to serialize anything we publish. That is what makes "an unknown vendor extension survives
verbatim" (FR-806) a structural fact rather than a hope, and what makes the optimizer's self-verification
meaningful.

One capability arrived after the first plan was written and is folded in here: **extraction** (User Story
9, FR-900 to FR-913). A developer chooses operations and gets a standalone description of just those. It
needs almost no new machinery — the dependency closure that makes a patch complete runs over the chosen
operations instead of the changed ones, and the reader, the emitter and the self-verification already
exist. What it does need is the inheritance a naïve trim loses (path-level parameters, servers, the global
security requirement) and a verification that compares each operation in its *effective* form, which is
what stops an extract silently dropping the thing that protected an endpoint.

Three findings came out of planning that touch code this feature does not own, and all three are
prerequisites rather than nice-to-haves:

1. **The shared checkpoint record is varcompare-shaped** and its store refuses widened shapes at runtime,
   so it cannot express this tool's selection. It is generalized to labelled string entries (R10).
2. **The shell cannot start without an Azure DevOps server address**, which it prompts for before the host
   is even built. apidelta is the first tool that talks to nothing, so server resolution becomes lazy
   behind an additive `ITool.RequiresServer` default member (R11).
3. **`AppPaths.CombineConfined` accepts a sibling-directory escape** (`C:\out` admits `C:\outside\x`).
   Harmless with today's callers; this feature would be the first to pass it a developer-chosen root, so it
   is fixed first (R12).

This feature is also, deliberately, the first one in the repository large enough that P1 is a release on
its own. P1 — a faithful read of both documents and a classified comparison — is a usable tool with no
audit engine at all.

## Technical Context

**Language/Version**: C# (`LangVersion latest`) on .NET 10 (`net10.0`), inherited from
`Directory.Build.props`. The PRD's stated .NET 8 target does not apply: the host decides the runtime.

**Primary Dependencies**: two new, both centrally versioned in `Directory.Packages.props`:

- **YamlDotNet 18.1.0** (MIT, targets `net10.0`, no dependencies) — low-level YAML parsing for the syntax
  layer. JSON needs nothing new: `Utf8JsonReader` is in the framework.
- **Microsoft.OpenApi 3.10.2** (MIT, `net8.0`, no dependencies) — structural validation and reference
  resolution, behind a port, confined to `ApiDelta.Infrastructure`.

Deliberately **not** taken: `Microsoft.OpenApi.YamlReader`, because it brings SharpYaml and would put two
independent YAML parsers in one process (R3). Also unchanged: Spectre.Console, `Microsoft.Extensions.*`,
`System.Text.Json`. No HTTP client, no resilience pipeline, no authentication — this tool makes no outbound
call.

**Storage**: local files only, and this is the first tool that writes files the developer chose. Artifacts
go to a developer-chosen output directory, confined by `AppPaths.CombineConfined` (R12) and confirmed
before any write outside it (FR-709). Run recovery in `%LOCALAPPDATA%\DevToolbox\runs\` holds only the
selection — mode, the two paths, their content hashes, the output directory, the ruleset path — with the
same 7-day expiry as every other tool. Logs in `%LOCALAPPDATA%\DevToolbox\logs\`. **No document content,
no finding and no example value is ever written to a checkpoint or a log.**

**Testing**: xUnit · NSubstitute · FluentAssertions 7.2.2 · NetArchTest.Rules · coverlet ·
`Spectre.Console.Testing`, all already referenced. New tests land in
`tests/DevToolbox.Tests/Tools/ApiDelta/`, with additions to the existing `Architecture/` folder. Four
shapes of test, matching the spec's own guarantees: unit tests per rule and per metamodel row; golden-file
tests over a corpus of descriptions with approved report, patch, JSON and optimized output; a **mutation
suite** that varies requiredness, nullability, enum membership, type, format, security and constraints and
asserts that no breaking mutation is ever classified non-breaking and no transformation touching one is
ever SAFE (SC-002); and property tests for canonicalization idempotence and round-trip fidelity.

**Target Platform**: Windows 11 console, offline. No network, no domain join, no server. The tool runs on a
machine that has never been configured for Azure DevOps — which is why R11 exists.

**Project Type**: Console application (interactive, menu-driven), single solution, multi-project.

**Performance Goals**: 1,000 operations and 10,000 schemas audited within 30 s on a developer workstation
(SC-003), with the run reporting its own duration. Parse once (FR-011); fingerprint once, cached; one pass
over the tree for all rules rather than one pass per rule; near-duplicate comparison bucketed, never
pairwise (R8).

**Constraints**: warnings-as-errors · Spectre.Console the only renderer · **no outbound call of any kind**
· external references disabled by default, remote resolution behind a second opt-in with a host allow-list
· production byte-identical unless an in-place write is explicitly requested *and* confirmed · nothing
optimized published until it reparses and verifies · artifacts byte-identical for identical inputs outside
one delimited run-metadata block · every async API takes and propagates a `CancellationToken` · no
unbounded accumulation and no unbounded concurrency.

**Scale/Scope**: one tool, 10 named steps, 35 audit rules, 12 optimization rules, 120 functional
requirements. Three prerequisite changes to shared code, all named above. No file under
`tools/VarCompare/` is edited except the mechanical checkpoint-factory change R10 forces.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| # | Principle | Gate | Initial | Post-design |
|---|-----------|------|:-------:|:-----------:|
| I | Clean Architecture Boundaries | `ApiDelta.Core` holds the syntax tree, the metamodel, the comparer, the rules and the ports — no Spectre, no file system, no package beyond the BCL; YamlDotNet and Microsoft.OpenApi are confined to `ApiDelta.Infrastructure`; `Presentation` never references `Infrastructure`. `LayeringTests` currently covers only the four shared assemblies, so this feature adds rows for its own three — including "`ApiDelta.Core` depends on neither new package", which is the assertion that keeps R7 true | PASS | PASS |
| II | SOLID, explicit dependencies, testability | Constructor injection only; one sealed class per rule behind `IAuditRule`/`IOptimizationRule`, registered explicitly; `ITool` + `AddApiDelta()` keeps the addition additive; the whole engine is testable from a string input with no console, network or file system | PASS | PASS |
| III | Security by default (STRIDE + OWASP) | STRIDE table below; input treated as hostile with size, depth and reference-count limits; no remote fetch by default and SSRF guards when enabled; path confinement fixed before use (R12); no secret in any artifact, log or checkpoint, redacted at the sink; `NuGetAudit` at `low` over two new packages | PASS | PASS |
| IV | Zero-warning builds, enforced analysis | Inherits `Directory.Build.props` unchanged; no new suppression planned; MA0048 and MA0051 shape the rule catalogue into one small type per file (R16); the two new packages are centrally versioned | PASS | PASS (with the pre-existing drift named in R16, inherited and not widened) |
| V | Spectre.Console-only terminal experience | Only Spectre renders; a new **OpenAPI** tab, which the shell derives from the tool list for free; Back on every screen and the shared `NavigationLabels`; cooperative `Ctrl+C`; `Progress` on every long step; summary-first rendering with drill-down (R17); degrades without colour or Unicode | PASS | PASS |
| VI | Structured, auditable logging | `ILogger<T>` with a source-generated `Log` class; `RunId` scope; per-step outcomes, rule counts and artifact paths logged — **never a description, an example, a server URL or a finding message**; the tool's value-bearing types registered with `RedactionRegistry` | PASS | PASS |
| VII | Resilience on every outbound call | Vacuously satisfied and worth stating: this tool makes no outbound call. If remote reference resolution is ever enabled (FR-006), it MUST go through a typed client from `IHttpClientFactory` with the standard pipeline, and it is a `GET`, so the banned-verb list holds by construction | PASS | PASS |
| VIII | Resumable and restartable execution | 10 named steps (R9, plus `Extract`), checkpoint after each success, four recovery choices, cross-launch resume; resuming re-reads both inputs and refuses when a content hash no longer matches (FR-705); every step idempotent, `Write` being the only one that touches the world | PASS | PASS |

### STRIDE pass (required by Principle III)

Both inputs are files of unknown provenance, which is the whole threat model: this tool's job is to read
documents it did not write.

| Threat | Vector | Mitigation |
|---|---|---|
| **Spoofing** | A `$ref` that points somewhere the developer did not intend — a UNC path, a sibling directory, a URL | External references disabled by default (FR-006); local resolution opt-in and confined to a configured root (FR-006b) with the confinement bug fixed first (R12); remote resolution a separate opt-in with a host allow-list |
| **Tampering** | A crafted description that makes the tool overwrite something outside the output directory, via a traversal in a suggested component name or an output file name | Every artifact path goes through confinement; names derived from document content are sanitized to a conservative character set before they reach a file name; writes outside the output directory are confirmed (FR-709); production untouched unless in-place is requested, confirmed, and the file is unchanged since it was read (FR-550) |
| **Repudiation** | "What did it change in my specification?" | Structured logs with a correlation identifier per run (FR-710); the optimization manifest records every transformation with origin, destination, fingerprint and reversibility (OPT-012); artifacts record input hashes and sizes before and after (FR-602) |
| **Information disclosure** | A description carries credentials in an example or a server URL; the tool then copies them into a report, a log or a checkpoint | Redaction at the sink boundary per Principle VI; value-bearing types registered with `RedactionRegistry`; checkpoints hold only paths, hashes and step names; optional secret-pattern auditing of examples and server URLs (FR-607) — reported as a finding, never echoed in full |
| **Denial of service** | A billion-laughs alias expansion, a 2 GB file, a 10,000-deep nesting, a reference cycle, a quadratic duplicate scan | Configured maximum size, nesting depth and reference count, refused with the limit named (FR-010); alias expansion bounded and counted as part of that budget; cycles terminated by a visited set keyed on canonical reference identity (FR-201); near-duplicate work bucketed and capped (R8); cancellable throughout |
| **Elevation of privilege** | Executing something the document carries | Nothing in a document is ever executed (FR-012); extensions are data; no shell invocation anywhere in this tool; no deserialization into polymorphic types — `System.Text.Json` with explicit contracts only |

A note on the honest limit: a `$ref` that resolves *within* the file cannot be a security problem, but a
file reference that resolves to a device path or a symbolic link out of the root can. Confinement is
therefore applied to the resolved, canonical path, after symlink resolution, not to the text of the
reference.

## Project Structure

### Documentation (this feature)

```text
specs/003-openapi-delta-audit/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── application-ports.md
│   ├── artifacts.md
│   ├── configuration.md
│   └── console-experience.md
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
tools/ApiDelta/
├── DevToolbox.Tools.ApiDelta.Core/              # references Application + Domain only
│   ├── ApiDeltaOptions.cs
│   ├── Syntax/                                  # the lossless tree: nodes, spans, styles, pointers
│   ├── Metamodel/                               # the OpenAPI field-policy table (R6)
│   ├── Loading/                                 # format detection, duplicate keys, limits, diagnostics
│   ├── Comparison/                              # the walk, change records, classification
│   ├── Versioning/                              # baseline, policy, recommendation
│   ├── Dependencies/                            # transitive closure, cycles, conflicts
│   ├── Fingerprinting/                          # canonical form, profiles, hashes, savings
│   ├── Audit/
│   │   ├── Validity/                            # AUD-001…AUD-010
│   │   ├── Redundancy/                          # AUD-100…AUD-107
│   │   ├── Consistency/                         # AUD-200…AUD-210
│   │   └── Governance/                          # AUD-300…AUD-305
│   ├── Optimization/                            # OPT-001…OPT-012, plan, manifest, verification
│   ├── Configuration/                           # ruleset, suppressions, policy resolution
│   ├── Reporting/                               # report models, ordering, determinism
│   ├── Extraction/                              # operation catalogue, subset builder, verification
│   ├── Steps/                                   # the 10 named steps
│   └── Abstractions/                            # ports this tool owns
├── DevToolbox.Tools.ApiDelta.Infrastructure/    # references Core + shared Infrastructure
│   ├── Yaml/                                    # YamlDotNet-backed reader → syntax tree
│   ├── Json/                                    # Utf8JsonReader-backed reader → syntax tree
│   ├── Semantic/                                # Microsoft.OpenApi adapter: validate, resolve
│   ├── Emitting/                                # canonical YAML/JSON writer from the tree
│   ├── Files/                                   # confined reads and writes, backups, hashes
│   └── ServiceCollectionExtensions.cs
└── DevToolbox.Tools.ApiDelta.Presentation/      # references Core + shared Presentation
    ├── ApiDeltaTool.cs                          # ITool, RequiresServer => false
    ├── ApiDeltaCheckpointFactory.cs
    ├── ApiDeltaSession.cs
    ├── ResumeCoordinator.cs
    ├── Prompts/                                 # inputs, mode, output directory, confirmations
    ├── Rendering/                               # summary-first, drill-down, progress
    └── ServiceCollectionExtensions.cs

tests/DevToolbox.Tests/Tools/ApiDelta/
├── Loading/  Comparison/  Versioning/  Dependencies/
├── Fingerprinting/  Audit/  Optimization/  Configuration/
├── Mutation/                                    # SC-002: unsafe changes never classified safe
├── Golden/                                      # approved artifacts, environment-normalized
└── Corpus/                                      # the example descriptions, as embedded resources
```

Shared code touched — all three are prerequisites, and all three are the subject of a Complexity Tracking
row:

```text
src/DevToolbox.Application/Abstractions/ITool.cs                  # + bool RequiresServer => true;
src/DevToolbox.Application/Abstractions/PersistedRun.cs           # selection generalized (R10)
src/DevToolbox.Application/Abstractions/PersistedSelectionEntry.cs # new, replaces PersistedGroupSelection
src/DevToolbox.Infrastructure/Storage/FileRunCheckpointStore.cs    # shape allow-list narrowed
src/DevToolbox.Infrastructure/Platform/AppPaths.cs                 # confinement fixed (R12)
src/DevToolbox.Presentation/Shell/ResumePrompt.cs                  # displays labelled entries
src/DevToolbox.Console/Program.cs                                  # lazy server resolution + 2 lines
tools/VarCompare/.../VarCompareCheckpointFactory.cs                # mechanical: emits entries
```

**Structure Decision**: the established tool trio under `tools/<Name>/`, exactly as `tools/VarCompare/`
does it — `Core` referencing only `Application` and `Domain`, `Infrastructure` referencing `Core` and the
shared `Infrastructure`, `Presentation` referencing `Core` and the shared `Presentation` and holding the
`ITool` implementation. The PRD's suggested solution layout (eleven `OpenApiTool.*` projects) is not
adopted: it describes a standalone product, and its separations map onto folders inside `Core` rather than
onto assemblies. Three projects enforce the one boundary that matters — the engine must not be able to
reach a terminal or a file — and eleven would enforce eight boundaries nobody is at risk of crossing.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Changing the shared `PersistedRun` shape, which forces a mechanical edit inside `tools/VarCompare/` — against the rule that adding a tool modifies no existing tool | apidelta's selection is a mode, two paths and two content hashes; the record is varcompare-shaped (`Collection`, `Project`, `SelectedGroups`) and `FileRunCheckpointStore` refuses at runtime to write a widened shape, so FR-705 cannot be met without touching it | A second checkpoint store owned by apidelta duplicates the store, the expiry sweep and the resume glue, and the two tools would then expire runs by different rules. An additive nullable member is a union type two tools read differently. Reusing `Collection` for the source path makes the resume prompt lie |
| Two new packages where prqueue added none | A structural validator and a reference resolver for two OpenAPI versions are months of work and nobody's differentiator; a YAML parser with positions and scalar styles is not in the BCL | Writing either ourselves costs more and is worse. Taking `Microsoft.OpenApi.YamlReader` instead of YamlDotNet would add SharpYaml and put two YAML parsers in one process (R3) |
| An OpenAPI metamodel table — a layer of data the PRD does not mention | FR-111's "ignore the non-semantic, preserve the meaningful order", FR-112's classification and FR-402's three profiles are all the same knowledge about the same fields; as a table it is reviewable row by row | Spreading it across a comparer, three canonicalizers and fifty rules is the same knowledge in fifty places, drifting one commit at a time. Reflecting over the library's model types describes somebody else's classes rather than the specification, and unknown keywords have no properties to reflect over |
| `Presentation` keeps its own session, resume coordinator and checkpoint factory rather than reusing varcompare's | They are per-tool by design in this repository, and the shared `IRunCheckpointFactory` is explicitly documented as per-tool | Hoisting them into the shared layer would make the shell know what a specification file is |

## Working agreements

- **Comments in French, in the repository's register.** No `FR-`, `AUD-`, `OPT-`, `SC-` or task
  identifiers in code or comments: a comment explains why the code is as it is, and a specification
  identifier is a cross-reference nobody can follow from a source file.
- **One public type per file, named for the file; methods at 60 lines or fewer.** The analyzers enforce
  both. The rule catalogue is one small sealed class per identifier.
- **`ConfigureAwait(false)` in every library assembly**, `CancellationToken` on every async public member,
  `Result`/`Result<T>` for expected failures, exceptions only for defects.
- **Nothing that can reach a document value may reach a log or a checkpoint.** Value-bearing types are
  registered with `RedactionRegistry` at composition; the checkpoint carries paths, hashes and step names.
- **Determinism is a test, not an intention.** Every golden artifact is compared byte-for-byte after the
  run-metadata block is normalized, and a second run on a different machine must match.
- **The specification is the authority.** A behaviour change goes into `spec.md` first, as a clarification,
  before the code moves.

## Open items

These are tracked here rather than resolved, because none of them blocks `/speckit-tasks`:

1. ~~**The exact reading API of `Microsoft.OpenApi` 3.10.2**~~ — **resolved while building US2.**
   `OpenApiDocument.Parse` returns `ReadResult { Document, Diagnostic }`, and errors carry a pointer that
   maps onto our nodes. The model preserves both unknown vendor extensions and unrecognized schema
   keywords, so the library is a better validator than the plan assumed. See R1.
2. ~~**YamlDotNet member names for position and scalar style**~~ — **resolved while building US2**, with
   two traps recorded in R2: `TagName.Value` throws on a non-specific tag, and a malformed document can
   leave the parser through a plain `InvalidOperationException` with no position.
3. **Alias and anchor policy** — **decided while building US2, against the plan's position.** Aliases are
   refused rather than expanded: an expanded alias has no byte span of its own, so the round-trip invariant
   that proves the tree covers the document could not hold where it matters most. Recorded on T041. If a
   real description in the corpus turns out to rely on anchors, that becomes a clarification in `spec.md`
   and a comment-preserving expansion design, not a quiet change here.
4. **Whether `$(catalog.host)`-style placeholders should ship enabled.** The spec makes them replaceable
   defaults; whether the default list is the one in the PRD or empty is a one-line decision for
   `/speckit-tasks` to surface, not a design question.
5. **The pre-existing analyzer drift** named in R16 (`AnalysisLevel`, `AsyncFixer` versus the
   constitution's named threading analyzer). Inherited, not widened, not this feature's to fix.
6. **`NuGetAudit` is the gate on the two new packages.** It runs at `low` with `NuGetAuditMode=all`, so a
   single known advisory on YamlDotNet 18.1.0 or Microsoft.OpenApi 3.10.2 — including transitively — fails
   the build and reopens R1 or R2 rather than being waived. The first task in the dependency area is
   therefore adding both `PackageVersion` entries and running a restore, before any code is written
   against them.
