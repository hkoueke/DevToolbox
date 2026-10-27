# Implementation Plan: A WSDL as a candidate for apidelta

**Branch**: `004-wsdl-delta-audit` | **Date**: 2026-10-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-wsdl-delta-audit/spec.md`

## Summary

The developer has one production OpenAPI YAML and one WSDL. The tool must compare them, report what
production does not cover, ask which operations should become routes, and write production plus those
routes.

[003](../003-openapi-delta-audit/spec.md) already does every part of that for an OpenAPI candidate, and
ships it: comparison, selection, the statement before writing, the byte-faithful splice, the proof, the
reports, resumability. **This feature adds an input format, not a tool.** Nothing downstream of the
projection changes.

The approach, in one sentence: **read the WSDL graph, project it into a synthetic OpenAPI document, and let
the existing machinery run against that — while keeping a record of everything the projection could not
carry.** The record is what the two new report sections are made of, and it is what stops the tool from
presenting a partial translation as a comparison.

Two consequences worth stating up front, because they are what makes the estimate small:

- The existing `DocumentComparer`, `CorrectionCatalog`, `CorrectionBuilder`, `SplicingEmitter` and
  `CorrectionVerifier` are reused **unmodified**. The splicer renders grafted nodes through the canonical
  emitter and reads byte spans only from *production*, so a synthetic candidate node — which has no
  meaningful span — works as-is. **This is established, not assumed**: `SyntheticGraftTests` grafts a
  hand-built node with a zero-length span through the real splicer and the result reparses with production
  intact. It was the plan's one load-bearing assumption, and it was checked before the plan was reported.
- Nothing is ever written in WSDL, so this feature needs **no verbatim WSDL emission and no WSDL byte
  spans**. That removes the single most expensive part of 003's reading layer. Positions (line, column)
  are still required for diagnostics; byte extents are not.

## Technical Context

**Language/Version**: C# 13 / .NET 10, same as the rest of the box.

**Primary Dependencies**: none new. `System.Xml` and `System.Xml.Schema` are in the base class library;
`Microsoft.Extensions.Http` and `Polly.Core` are already referenced for the one retrieval this feature
performs. `Directory.Packages.props` gains nothing, so `NuGetAudit` has no new surface and
`DevToolbox.Domain` keeps its zero package references.

**Storage**: files only. A retrieved document graph is materialised into the run's output directory and
read back from there; nothing else persists but the existing checkpoint.

**Testing**: xUnit, FluentAssertions, NSubstitute, Spectre.Console.Testing — unchanged. A new WSDL corpus
is embedded in the test assembly beside the OpenAPI one, and `CorpusLoader` already normalises line
endings for both.

**Target Platform**: Windows x64, single-file self-contained, as published today.

**Project Type**: CLI tool inside an existing per-tool clean architecture. **No new tool projects**: the
work lands in the three `tools/ApiDelta/*` projects that exist.

**Performance Goals**: 200 operations and 5,000 type declarations across 20 documents read, resolved,
projected and compared in under 30 seconds on a developer machine (SC-002). The projection is a single
pass over the compiled schema set; the comparison cost is the existing one.

**Constraints**: no network when the inputs are files (WR-700); no DTD and no external entity resolution
under any configuration (WR-002); no credentials acquired or sent (WR-017); local import resolution open
by default but confined and announced (WR-005, WR-006); production never written over, under any flag
(003 FR-515).

**Scale/Scope**: 56 functional requirements, 5 user stories, 16 success criteria. Roughly three new
namespaces in `ApiDelta.Core` (`Wsdl`, `Projection`, `Correspondence`), two in `ApiDelta.Infrastructure`
(`Wsdl`, `Http`), and one prompt plus one report extension in `ApiDelta.Presentation`.

## Constitution Check

*GATE: passed before Phase 0, re-checked after Phase 1 design — see the bottom of this file.*

| Principle | How this feature satisfies it |
|-----------|-------------------------------|
| I. Clean architecture boundaries | The WSDL syntax model, the projection and the correspondence are pure and live in `ApiDelta.Core`. The XML reader, the schema compiler and the retrieval live in `ApiDelta.Infrastructure`. The correspondence screen lives in `ApiDelta.Presentation`, which keeps referencing only `Core`. No new tool, so no new composition seam. |
| II. SOLID, explicit dependencies | Four new ports: `IWsdlReader`, `IWsdlProjector`, `ICorrespondenceProposer`, `ICorrespondencePrompt`, plus one typed client. Every one is constructor-injected and substitutable; the projection is pure and takes no clock, no filesystem, no console. |
| III. Security by default | A STRIDE pass follows. This is the first feature in the box that retrieves a URL, so the pass is not a formality. |
| IV. Zero-warning builds | Nothing here needs a `#pragma`. The two existing ones stay where they are. |
| V. Spectre.Console-only terminal | The correspondence screen is a Spectre prompt; the report gains rows in the existing tables. No `System.Console`. |
| VI. Structured, auditable logging | Reading, retrieval and projection log through the existing `Log` source-generated methods. `WsdlContract`, `ProjectionGap` and any type able to hold a retrieved value are registered with `RedactionRegistry`. |
| VII. Resilience for every outbound call | The one retrieval goes through a typed `IHttpClientFactory` client with the standard resilience handler: timeout, bounded retries on transient status, circuit breaker. No `new HttpClient()`. |
| VIII. Resumable and restartable | The correspondence and the draft selection are strings only and join the checkpoint beside the existing entries. A resumed run re-reads its inputs, re-reads the materialised files, and never retrieves again (WR-018). |

### STRIDE pass (required by Principle III)

The new surface is XML parsing and one outbound retrieval. Both are classically dangerous, and neither
existed in the box before.

**Spoofing.** A `schemaLocation` can name any host. Remote resolution stays off unless explicitly enabled
with a non-empty host allow-list (WR-007); `https` is required unless the developer opts into `http`
(WR-014). Certificate validation is never weakened — platform default, no callback, no flag.

**Tampering.** A retrieved document is written to disk and read back from there (WR-011), so what was
projected is what is on disk and the run is reproducible from it. Every document is reported with its
content hash (WR-004). A resumed run refuses when a hash no longer matches.

**Repudiation.** Every document carries its path, size, hash and — when retrieved — its redacted origin
and the instant of retrieval (WR-018). The report states the root local resolution was confined to
(WR-006), so the perimeter the tool gave itself is on the record rather than inferred.

**Information disclosure.** Three distinct leaks are possible and each has a named guard. A URL's
userinfo or query string can carry a token → redacted wherever the URL is reported, including a
materialised document's provenance (WR-019). A `soap:address` can carry a credential → reported by
location, never reproduced as a value (WR-605). A derived element could carry a value from the WSDL into
the generated document → the mark records the location it came from, and `ValueSummary` is already
bounded and already registered as never reaching a log sink.

**Denial of service.** This is the heaviest item, because XML is the richest DoS surface in the box.
DTD processing and external entity resolution are **off under every configuration** — not a knob, since a
knob able to re-enable them would itself be the vulnerability (WR-002). That closes billion-laughs and
XXE together. Beyond it: configured maxima on document size, nesting depth, import-chain depth and total
document count, each reported by name when reached (WR-009); a bounded retrieval timeout and response
size, leaving no partial document behind (WR-016); bounded redirects re-validated at every hop (WR-015);
and legal import cycles recorded rather than followed (WR-201). 003's own lesson is carried over
directly: a reference chain must exhaust a *named budget*, never a stack.

**Elevation of privilege.** No credential is acquired or sent (WR-017), so there is nothing to escalate
with. A URL requiring authentication is refused with a diagnostic telling the developer to download the
document and pass it as a file. Local resolution is confined to a declared root and traversal outside it
is refused and named (WR-005).

## Project Structure

### Documentation (this feature)

```text
specs/004-wsdl-delta-audit/
├── spec.md              # the what and the why, with the four clarifications of 2026-10-21
├── plan.md              # this file
├── research.md           # the decisions, their reasons, and what was rejected
├── data-model.md         # the shapes, and which requirement each one answers
├── contracts/
│   ├── application-ports.md   # the four new ports and the typed client
│   ├── projection.md          # the WSDL/XSD → OpenAPI table, as a contract
│   └── console-experience.md  # the correspondence screen and the two report sections
├── quickstart.md         # runnable scenarios that prove the feature end to end
└── checklists/
    └── requirements.md   # the spec quality gate, already passed
```

### Source Code (repository root)

No new project. The work lands in the three that exist, and the layering tests keep holding.

```text
tools/ApiDelta/
├── DevToolbox.Tools.ApiDelta.Core/            # no package references beyond today's
│   ├── Wsdl/                                   # NEW — the syntax model, pure
│   │   ├── WsdlContract.cs                     #   the document graph as one unit
│   │   ├── WsdlDocument.cs                     #   one .wsdl or .xsd: path, size, hash, origin
│   │   ├── WsdlOperation.cs                    #   portType entry + binding + messages + faults
│   │   ├── MessagePattern.cs                   #   request-response, one-way, solicit, notification
│   │   ├── WsdlPart.cs  WsdlBinding.cs  WsdlService.cs
│   │   └── XsdType.cs  XsdParticle.cs  XsdFacet.cs
│   ├── Projection/                             # NEW — the only place that knows both languages
│   │   ├── IProjectionRule.cs                  #   one rule per row of the published table
│   │   ├── ProjectionTable.cs                  #   the table of WR-110, as code
│   │   ├── Projector.cs                        #   WSDL contract → synthetic SpecDocument + gaps
│   │   ├── ProjectionResult.cs                 #   the synthetic tree, the gaps, the figures
│   │   ├── ProjectionGap.cs                    #   what could not be carried, and why
│   │   ├── ProjectionConfidence.cs             #   carried / approximate / not carried
│   │   └── RouteDerivation.cs                  #   operation → path + method, deterministic (WR-305)
│   ├── Correspondence/                         # NEW
│   │   ├── Correspondence.cs  CorrespondenceEntry.cs  CorrespondenceOrigin.cs
│   │   ├── CoverageState.cs  CoverageReport.cs
│   │   └── SimilarityProposer.cs               #   token-based, pure, explainable
│   ├── Abstractions/                           # + IWsdlReader, IWsdlProjector,
│   │                                           #   ICorrespondenceProposer, ICorrespondencePrompt
│   └── Steps/                                  # + ReadWsdlStep, CorrespondStep
├── DevToolbox.Tools.ApiDelta.Infrastructure/
│   ├── Wsdl/                                   # NEW (les limites et la politique vivent, elles,
│   │   ├── WsdlReader.cs                       #   XmlReader with line info, DTD off, resolver null
│   │   ├── SchemaCompiler.cs                   #   XmlSchemaSet, imports under policy
│   │   ├── ImportPolicy.cs                     #   the confined root, the host allow-list
│   │   └── WsdlLimits.cs                       #   size, depth, chain, document count
│   └── Http/                                   # NEW
│       ├── ContractFetcher.cs                  #   typed client + resilience handler
│       ├── AddressGuard.cs                     #   scheme, private ranges, redirects (SSRF)
│       └── Materialiser.cs                     #   writes the graph, names collisions apart
└── DevToolbox.Tools.ApiDelta.Presentation/
    ├── Prompts/CorrespondencePicker.cs         # NEW — confirm or correct, before anything compares
    └── Rendering/CoverageView.cs               # NEW — coverage, comparability, the proportion
```

Tests mirror it, under `tests/DevToolbox.Tests/Tools/ApiDelta/`:
`Wsdl/`, `Projection/`, `Correspondence/`, `Http/`, and a `Corpus/Wsdl/` of hand-written contracts —
including the hostile set, which is the part that must exist before the reader does.

## Complexity Tracking

| Risk | Why it matters | How it is retired, and when |
|------|----------------|------------------------------|
| ~~**R-01** The existing splicer may not accept a synthetic candidate node~~ | The whole estimate rested on reusing `CorrectionBuilder` and `SplicingEmitter` unchanged. | **Retired on 2026-10-21, before the plan was reported.** `SyntheticGraftTests` grafts a hand-built `MappingNode` whose span length is zero into production through the real `SplicingEmitter`: the result reparses, the synthetic route is present, and every line of production survives. The bet is no longer a bet. |
| **R-02** ~~`XmlSchemaSet` resolves imports itself~~ — **the risk was stated backwards** | The plan assumed an eager resolver reaching the filesystem or the network behind the policy's back. | **Corrected on 2026-10-21 by measurement.** `XmlSchemaSet.XmlResolver` defaults to **null** since .NET Core: without a resolver it follows no `schemaLocation` at all, and the corpus failed to compile until the test supplied one. The posture is therefore closed by default, and the work is to provide a *controlled* resolver answering only from the materialised set — not to disarm an eager one. The test that proves it still matters, unchanged: an import of an unreachable host must be refused **by name**. |
| **R-03** The projection is where silent wrongness lives | A rule that quietly carries something it should not makes every downstream finding confident and wrong. | The table of WR-110 is the contract: one test per row, asserting carried / approximate / not carried, and a test that the set of rules covers the table exactly — the same shape as 003's `RuleCatalogueTests`. |
| **R-04** A wrong correspondence produces a confident report about the wrong pair | Named in the spec as the worst failure available to this feature. | Nothing compares before confirmation (WR-104); every entry records its origin; the report states that each compatibility finding is only as reliable as the correspondence above it. Tested by a deliberately wrong mapping that must surface as inferred. |
| **R-05** Hostile XML | DoS and XXE are the richest surface here, and the box has never had it. | The hostile corpus is written **first**: a DTD, a billion-laughs entity, a 400-document chain, a cycle, a 10 MB enumeration, a `schemaLocation` traversing out of the root. Each must be refused by a named limit. |
| **R-06** Two retrieved documents whose last path segment is the same | They would overwrite each other, and the second would silently win. | WR-012: distinct names derived from the origin, the mapping recorded. One corpus case, one test. |
| **R-07** A relative `schemaLocation` in a retrieved document | Resolved against the output directory it would mean something else entirely. | WR-013: resolved against the URL it came from. One test with a two-level remote graph. |

## Working agreements

These are the decisions that would otherwise be re-litigated halfway through.

**A WSDL candidate is a format, not a mode.** `RunMode` gains nothing. `SelectStep` detects the candidate's
kind from its content — a `definitions` root in the WSDL namespace — exactly as `FormatDetector` already
distinguishes JSON from YAML. A developer who picks "comparer" and hands over a `.wsdl` gets a comparison;
they are not asked which kind of comparison they want, because the file answers that.

**The projection produces a synthetic `SpecDocument`, and says so.** Its `Path` is the WSDL's, its
`Format` is a new `SpecFormat.Projected`, and its nodes carry no byte extents. Every consumer that needs
bytes reads them from *production*, which is real. Making this explicit in the type is what stops someone
from later calling `VerbatimEmitter` on it and getting nonsense.

**The gaps are first-class, not diagnostics.** A `ProjectionGap` is data the report sections are built
from, grouped by reason and counted (WR-111). Folding them into the diagnostic collector would mix "the
tool could not read this" with "the two languages do not have this in common", which are different news
for the reader.

**Coverage is computed from the correspondence alone.** Not from the comparer, not from the projection.
That is what lets WR-108 promise certainty for coverage while compatibility stays partial, and it is why
the two live in separate types and separate report sections.

**Nothing in this feature writes WSDL.** No verbatim emitter, no splicer, no byte spans for the WSDL side.
If a later feature wants to write one, it starts by adding what this one deliberately left out.

**The hostile corpus precedes the reader.** 003 learned this the expensive way: its stack overflow on a
10,000-link reference chain was found by a test that refused to assume, long after the reader was written.
Here the refusals come first.

## Open items

None blocking. Two things are deliberately deferred and recorded so they are not mistaken for oversights:

- **The similarity proposer's algorithm is a detail, not a decision.** `research.md` picks token-based
  overlap over edit distance and says why, but the port exists precisely so it can be replaced without
  touching anything else. A poor proposal costs the developer a correction on one screen; it cannot
  produce a wrong result, because nothing compares before confirmation.
- **XSD 1.1 constructs are reported, not supported.** `assert`, `alternative` and `openContent` make a
  type's meaning depend on something `XmlSchemaSet` will not compile. Such a type is reported as not
  projectable rather than projected approximately — the honest failure, and it is in the assumptions.

## Constitution re-check, after Phase 1 design

Re-read against the artifacts produced below. No principle is violated and no deviation needs
justification, so the Complexity Tracking table above holds risks rather than exemptions.

The one place worth a second look is Principle I. The projection knows both contract languages, which
makes it the most coupled type in the feature. It stays in `Core` and stays pure: it takes a
`WsdlContract` and returns a `ProjectionResult`, with no reader, no clock and no console. The coupling is
to two *vocabularies*, which is the feature's subject, not to two layers.
