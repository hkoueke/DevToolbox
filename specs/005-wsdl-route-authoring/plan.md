# Implementation Plan: Authoring a route instead of deriving one

**Branch**: `005-wsdl-route-authoring` | **Date**: 2026-10-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-wsdl-route-authoring/spec.md`

## Summary

Feature 004 guesses a REST route from an operation's name and asks the developer to confirm it. Against a
real pair, 19 of 19 guesses missed. This feature stops guessing what cannot be known and asks instead:
for each operation the developer keeps, the path, the verb, where in the production document the route
goes, where each field of the operation's input travels, and whether each is required. The description is
left as a placeholder. What a WSDL *can* say — the schemas and their references — keeps being derived.

The technical shape is narrow and almost entirely additive. One step of the existing ordered chain is
replaced, one existing prompt is replaced, the splicing emitter gains the one capability it lacks (adding
a member somewhere other than at the end), and the correction builder learns to assemble an operation
object from placements rather than copying the projected one wholesale. Nothing outside `tools/ApiDelta/*`
changes, and no new package is referenced.

The feature also **removes** a capability rather than completing it: the declared correspondence file of
WR-105, whose format, corpus example, proposer support, context key and report marker all exist while no
reader ever fills the key. The decision is to withdraw it, and withdrawing it means deleting its remains.

## Technical Context

**Language/Version**: C# 13 / .NET 10, same as the rest of the box.

**Primary Dependencies**: none new. Spectre.Console is already the only terminal surface; the prompts this
feature needs (text with a pre-filled default, single selection, multi-selection) are the ones already in
use. `Directory.Packages.props` gains nothing, so `NuGetAudit` has no new surface and `DevToolbox.Domain`
keeps its zero package references.

**Storage**: the existing checkpoint, and nothing else. RA-024 and RA-025 forbid any file of authoring
decisions, read or written. Authoring joins the checkpoint as strings beside the entries already there,
which is what Principle VIII requires of it.

**Testing**: xUnit, FluentAssertions, NSubstitute, Spectre.Console.Testing — unchanged. The WSDL corpus
gains nothing; the authoring prompts are driven through `TestConsole` with pushed keys, as the resume
prompt now is.

**Target Platform**: Windows x64, single-file self-contained, as published today.

**Project Type**: CLI tool inside an existing per-tool clean architecture. **No new projects**: the work
lands in the three `tools/ApiDelta/*` projects that exist.

**Performance Goals**: none new. Authoring is bounded by how fast a person types; the pass over production
to collect insertion points and reusable headers is a single walk of a document already in memory.

**Constraints**: production is never written over (003 FR-515); the written document carries only OpenAPI,
so the description placeholder is a `description`, not an extension member (`CONCEPTION.md`, "Le fichier
que l'outil écrit ne porte que de l'OpenAPI"); a declared budget must bite and an assertion under a
condition proves nothing (`CONCEPTION.md`); every authored route must survive the existing verification,
which establishes that nothing outside the selection moved.

**Scale/Scope**: 28 functional requirements, 4 user stories, 8 success criteria. One new namespace in
`ApiDelta.Core` (`Authoring`), one new prompt in `ApiDelta.Presentation`, one changed step, one changed
emitter behaviour, one report section, and four deletions.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Verdict | How |
|---|---|---|
| I. Clean architecture boundaries | **Pass** | Authoring records and their validation live in `ApiDelta.Core`; the prompt lives in `ApiDelta.Presentation` behind an abstraction the core declares. No core type references Spectre. The splicer change is in `ApiDelta.Infrastructure`, reached only through the existing `ISplicingEmitter` port. |
| II. SOLID, explicit dependencies, testability | **Pass** | One new port (`IRouteAuthoringPrompt`), segregated to this one need rather than added to the existing correspondence prompt. Authoring validation is a pure function over records, reachable in a unit test with no console and no filesystem. |
| III. Security by default (STRIDE) | **Pass** | See the pass below. The new input is typed text that becomes a document path and a parameter name; it is validated against an allow-list before use, never concatenated into anything. |
| IV. Zero-warning builds | **Pass** | Nothing here needs a suppression. The 60-line method ceiling shapes the prompt into one method per question rather than one per operation. |
| V. Spectre.Console-only terminal | **Pass** | Three prompt shapes already in use. No `System.Console`, so `BannedSymbols.txt` is untouched. |
| VI. Structured, auditable logging | **Pass** | One log event per authored route, carrying the operation name, the path and the verb — all of which are already in the report. No field value and no typed content is logged. |
| VII. Resilience for outbound calls | **Not applicable** | Authoring makes no outbound call. |
| VIII. Resumable and restartable | **Pass** | Authoring records are strings only and join the checkpoint beside the existing entries (RA-019). A resumed run re-reads its inputs and re-verifies their fingerprints, unchanged. |

### STRIDE pass (required by Principle III)

The new surface is **typed text from the developer**, which ends up inside a written document: a path, a
verb, parameter names and locations, and required flags.

- **Spoofing**: not applicable. No identity is asserted or accepted here.
- **Tampering**: the risk is a typed path that escapes the document structure it is written into — a path
  carrying a newline, a quote, or YAML punctuation that terminates the member and opens another. The
  mitigation is that authored values never reach the output as text: they are placed in the syntax tree as
  scalar nodes and rendered by the canonical emitter, which quotes what needs quoting. Validation (RA-004,
  RA-005) refuses the malformed before that point, and the existing verification establishes afterwards
  that nothing outside the selection moved — a typed value that had escaped would show there as drift.
- **Repudiation**: every authored decision is in the report with the operation it belongs to (RA-027),
  and the report distinguishes a decision from a suggestion (RA-028). A route cannot appear without a
  record of who settled it.
- **Information disclosure**: the placeholder description must not carry the contract's file path, and the
  log must not carry typed field names beyond what the report already shows. Nothing read from the WSDL
  beyond names and types enters the authored route.
- **Denial of service**: a contract of several hundred operations is out of scope for authoring (spec),
  and the developer keeps a subset. The pass over production for insertion points and headers is a single
  walk, bounded by the existing `MaxBytes` and `MaxDepth` budgets.
- **Elevation of privilege**: not applicable. No privilege boundary is crossed.

**One STRIDE item becomes a requirement of this plan**: the authored path must be validated *before* it is
used to build a pointer, because `SpecPointer` segments are escaped for JSON Pointer and a path containing
`/` or `~` would otherwise build a pointer that designates something else. This is not hypothetical — path
templates contain `/` by construction.

## Project Structure

### Documentation (this feature)

```text
specs/005-wsdl-route-authoring/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── application-ports.md
│   └── console-experience.md
├── checklists/
│   └── requirements.md  # Written by /speckit-specify
└── tasks.md             # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
tools/ApiDelta/DevToolbox.Tools.ApiDelta.Core/
├── Abstractions/
│   └── IRouteAuthoringPrompt.cs        # new: the one question-asking port
├── Authoring/                          # new namespace
│   ├── AuthoredRoute.cs                # path, verb, insertion point, suggestion origin
│   ├── FieldPlacement.cs               # one field, where it goes, required or not
│   ├── ParameterLocation.cs            # path | query | body | header | nowhere
│   ├── AuthoringRecord.cs              # every authored route of one run
│   ├── RouteSyntax.cs                  # validates a typed path and verb (RA-004, RA-005)
│   ├── PlacementDefaults.cs            # the per-verb proposal (RA-011)
│   ├── PlacementRules.cs               # refusals: composite, placeholder mismatch (RA-013, RA-015)
│   ├── InsertionPoint.cs               # beside which existing path, or at the end
│   ├── OfferedHeader.cs                # a reusable header production declares, with its count
│   └── ReusableHeaders.cs              # finds them in production (RA-021, RA-023)
├── Correction/
│   └── CorrectionBuilder.cs            # changed: assemble from placements, carry the insertion point
├── Steps/
│   └── AuthorStep.cs                   # new: replaces CorrespondStep in the chain for new routes
└── Reporting/
    └── MarkdownReportWriter.cs         # changed: authored routes, placements, dropped fields, placeholders

tools/ApiDelta/DevToolbox.Tools.ApiDelta.Infrastructure/
└── Emitting/
    └── SplicingEmitter.cs              # changed: add a member after a named sibling, not only last

tools/ApiDelta/DevToolbox.Tools.ApiDelta.Presentation/
├── Prompts/
│   └── RouteAuthoringPrompt.cs         # new: the questions, one method per question
├── ApiDeltaSelectionKinds.cs           # changed: authored route and placement kinds
└── ApiDeltaCheckpointFactory.cs        # changed: write and read them as strings

tests/DevToolbox.Tests/Tools/ApiDelta/
├── Authoring/                          # new
│   ├── RouteSyntaxTests.cs
│   ├── PlacementRulesTests.cs
│   ├── PlacementDefaultsTests.cs
│   ├── ReusableHeadersTests.cs
│   └── AuthoringRecordTests.cs
├── Wsdl/
│   └── AuthoredRouteEndToEndTests.cs   # new: the real shape, through the chain
└── Golden/
    └── ArtifactDeterminismTests.cs     # changed: an authored route in the golden run
```

**Structure Decision**: the existing per-tool clean architecture is kept exactly. Authoring is a Core
concern with one Presentation port, which is what lets its rules be tested without a console. The only
Infrastructure change is to the splicer, behind a port the core already talks to.

### What is deleted

WR-105 is withdrawn (RA-026), and these are its remains:

| What | Where | Why it goes |
|---|---|---|
| `DeclaredCorrespondence` context key | `Steps/ApiDeltaContextKeys.cs` | Read once, written never |
| `declared` parameter of `Propose` | `Abstractions/ICorrespondenceProposer.cs`, `Correspondence/SimilarityProposer.cs` | Its only caller passes the key above, which is always null |
| `CorrespondenceOrigin.Declared` | `Correspondence/CorrespondenceOrigin.cs` | Unreachable once nothing declares |
| `[correspondance déclarée]` rendering | `Correspondence/CorrespondenceEntry.cs` | Describes a state that cannot occur |
| `Corpus/Wsdl/correspondance.yaml` | tests | Reached only by a test asserting it is reachable |

Deleting an enum member reachable from a checkpoint needs care: a checkpoint written by an earlier version
could name it. The existing rule covers this — a checkpoint this version cannot read stops offering a
resume, cleanly, which FR-705 and its test already hold.

## Complexity Tracking

> No constitution violation to justify. Two decisions are worth recording as deliberate costs.

| Decision | Why | Simpler alternative rejected because |
|---|---|---|
| A new port `IRouteAuthoringPrompt` rather than extending `ICorrespondencePrompt` | Principle II forbids a prompt interface that grows unrelated operations; the two answer different questions | Extending the correspondence prompt would make one interface serve pairing *and* authoring, and would couple the step that still pairs covered operations to the step that authors new ones |
| The splicer gains "add after this sibling" | RA-008 cannot be met otherwise: `Adding` inserts after the last entry of the target mapping, always | Writing the route at the end and asking the developer to move it by hand is what the feature exists to stop |

## Working agreements

- **The derivation stays, demoted.** `RouteDerivation` is not deleted; it becomes the source of the
  pre-filled suggestion, and its `RouteDerivationKind` becomes what RA-002 shows the developer.
- **Authoring replaces pairing only for routes production lacks.** `CorrespondStep` keeps answering "which
  existing route does this operation correspond to", which is what coverage reporting is built on. The new
  step answers "what should this new route be". Merging them is out of scope and would put coverage at
  risk.
- **Every refusal names its subject.** A composite field refused for the query names the field and its
  type; a placeholder with no field names the placeholder; a collision names both sides.
- **No budget, no flag and no limit is introduced without an application site and a test that was seen to
  fail.** This is now a rule of the repository, and this feature introduces no exception to it.

## Open items

- The exact placeholder string for a description (RA-018) is a wording decision, settled in research.md
  rather than left to the implementation, because it must be identical everywhere and searchable.
- Whether the report lists placements per route or in one table is a presentation decision, settled in
  `contracts/console-experience.md`.

## Constitution re-check, after Phase 1 design

Re-evaluated after writing the data model and the contracts: **no change in verdict**. The design adds one
Core namespace of records and pure functions, one Presentation prompt behind one Core port, and one
Infrastructure capability behind an existing port. Layering, dependency direction, prompt segregation,
checkpoint shape and the zero-warning gate are all unaffected. The STRIDE item raised above — validate a
typed path before building a pointer from it — is carried into the data model as a rule of `RouteSyntax`
rather than left as a note.
