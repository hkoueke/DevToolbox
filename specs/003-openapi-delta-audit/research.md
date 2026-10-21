# Research — apidelta

**Feature**: [spec.md](./spec.md) · **Date**: 2026-10-16

Decisions taken before design, each with what was rejected. Verified facts carry their source; anything
that still has to be confirmed against an installed package says so in as many words, because a plan that
cannot be told apart from a guess is the expensive kind of document.

---

## R1 — The semantic layer: `Microsoft.OpenApi`, for validation and references only

**Decision**: take `Microsoft.OpenApi` 3.10.2 (MIT, last updated 2026-08-20, targets `net8.0` and
`netstandard2.0`, no dependencies on `net8.0`) and use it for exactly three things: structural validation
against the declared version, reference-graph resolution, and the version vocabulary. It is reached through
a port (`ISpecValidator`, `IReferenceGraph`) and nothing above Infrastructure names the package.

Its `OpenApiSpecVersion` enumeration carries `OpenApi2_0`, `OpenApi3_0`, `OpenApi3_1` and `OpenApi3_2`,
which covers the 3.0.x and 3.1.x range FR-002 requires.

**Rationale**: a structural validator for two OpenAPI versions is months of work and is nobody's
differentiator. Keeping it behind a port means that if it proves too lossy or too strict, it can be
replaced without touching the diff, audit or optimization engines.

**Alternatives rejected**: writing our own validator (cost, and it would be worse); `Microsoft.OpenApi.Hidi`
as a subprocess (a CLI is not a library, and shelling out forfeits structured diagnostics); running
Spectral or `oasdiff` as a child process (both require a runtime — Node, Go — that the PRD and the
constitution refuse to make a prerequisite of the built-in checks).

**Confirmed at implementation time**, from the installed package's own API documentation:
`OpenApiDocument.Parse(string, string?, OpenApiReaderSettings?)` returns a `ReadResult` carrying `Document`
and `Diagnostic`, and `OpenApiDiagnostic` exposes `Errors`, `Warnings`, `SpecificationVersion` and `Format`,
with each `OpenApiError` carrying a `Message` and a `Pointer` — which is what lets its verdicts be mapped
back onto our nodes and given a line and a column. **The model does preserve what we feared it might
drop**: `IOpenApiSchema.UnrecognizedKeywords` and `IOpenApiExtensible.Extensions` both exist, so unknown
schema keywords and unknown vendor extensions survive a load. That makes the library a better validator
than expected and changes nothing about R7: nothing we publish is serialized from its model, which is why
the answer never had the power to sink the design.

---

## R2 — The syntax layer is ours, over YamlDotNet

**Decision**: parse each input exactly once, with our own code, into a lossless node tree
(`SpecNode`: mapping, sequence, scalar) that carries for every node its line, its column, its byte span,
and — for scalars — how it was written (plain, single-quoted, double-quoted, block). YAML goes through
YamlDotNet 18.1.0 (MIT, last updated 2026-06-26, targets `net10.0` among others, no dependencies, and
documented as providing "low level parsing and emitting of YAML as well as a high level object model").
JSON goes through `Utf8JsonReader`, already in the framework.

**Rationale**: half of this specification cannot be answered by a semantic object model.

| Requirement | Needs the syntax layer because |
|---|---|
| FR-003 | parse errors carry line and column |
| FR-007 | duplicate mapping keys must be seen *before* anything binds them |
| FR-008 | suspicious encoded text must be reported without being decoded |
| AUD-200, AUD-201, AUD-202 | indentation, ordering and quoting are physical facts, absent from any model |
| AUD-203, AUD-207 | header casing and escaped markup are spellings, not values |
| FR-403 | savings are measured in bytes, which only the serialized form has |
| FR-110, FR-806 | an extension the tool does not understand must survive verbatim |
| FR-410 | every finding carries a location, including findings about things a model drops |

A semantic model is a lossy projection of the document. Everything this feature promises about *fidelity*
has to stand on the unprojected form.

**Alternatives rejected**: the library's object model as the only representation (loses positions, styles,
byte sizes and anything it does not model — which is exactly what FR-806 forbids losing); a raw-text
line-based approach (cannot answer a single contract question, which is the other half of the tool).

**Confirmed at implementation time**: `YamlDotNet.Core.Mark` carries `Index`, `Line` and `Column`; every
`ParsingEvent` carries `Start` and `End` marks; `Scalar` carries `Value`, `Style` (`Plain`,
`SingleQuoted`, `DoubleQuoted`, `Literal`, `Folded`), `Anchor` and `Tag`. Two traps found by building on
it: `TagName.Value` **throws** on a non-specific tag rather than returning null, so a tag must be tested
with `IsEmpty`/`IsNonSpecific` first; and a malformed document does not always surface as a `YamlException`
— some states leave through a plain `InvalidOperationException` with no position at all. Since both inputs
are files the tool did not write, that second one is handled as a refusal rather than a crash, and a test
pins it.

---

## R3 — No `Microsoft.OpenApi.YamlReader`: one YAML parser, one truth

**Decision**: do not take the YAML reader package. Project our own syntax tree to `JsonNode` and hand
*that* to the semantic layer, so the library only ever sees JSON.

**Rationale**: `Microsoft.OpenApi.YamlReader` 3.10.2 depends on **SharpYaml** (≥ 2.1.5), not on YamlDotNet.
Taking it would put two independent YAML parsers in one process, and the tool would then validate what
SharpYaml read while diffing what YamlDotNet read. Any disagreement between them — scalar resolution,
duplicate handling, anchors — becomes a class of bug where the audit is confidently about a document that
is not the one being compared. Projecting one tree to JSON removes the whole class by construction, and
drops a transitive dependency and its audit surface with it.

The projection is not extra work: FR-400 already requires a canonical JSON representation for
fingerprinting, so the tree-to-JSON mapping has to exist either way.

**Alternatives rejected**: using SharpYaml for the syntax layer as well (one parser, but then the syntax
layer is built on the transitive dependency of a reader we would otherwise not need, and YamlDotNet targets
`net10.0` directly while being the more widely used of the two); accepting both parsers and testing that
they agree (a test that two libraries agree on the inputs we thought of is not a guarantee about the ones
we did not).

**Consequence to specify**: the YAML-to-JSON projection resolves scalars under the **YAML 1.2 core
schema**, and an ambiguous scalar — `y`, `no`, `on`, `~`, `0644`, a sexagesimal, an unquoted date — is
reported rather than silently coerced. Non-string mapping keys are a structural error under FR-004, since
OpenAPI has none.

---

## R4 — Duplicate keys: detected by us, with every occurrence located

**Decision**: duplicate detection happens in our tree builder, which reports *every* duplicate with both
positions, rather than failing on the first. For JSON we additionally set .NET 10's
`JsonDocumentOptions.AllowDuplicateProperties = false` wherever a document is bound, as a second line of
defence.

**Rationale**: FR-007 exists because most parsers keep one value and discard the other silently. A check
that lives in our builder cannot be switched off by a library default, and it is the only way to satisfy
FR-410's demand that a finding name every location. .NET 10 added
`JsonSerializerOptions.AllowDuplicateProperties` and `JsonDocumentOptions.AllowDuplicateProperties`
(default `true`; set to `false`, a duplicate throws `JsonException`), which is a real guard but reports one
duplicate as an exception rather than all of them as findings.

**Alternatives rejected**: relying on YamlDotNet's deserializer, which has thrown on non-unique keys since
12.2.1 — the throw happens during binding, which is after FR-007's deadline, and it yields an exception
rather than a located finding; relying on the `Strict` preset alone for JSON, for the same reason.

---

## R5 — Canonical form and fingerprints

**Decision**: canonicalization produces a JSON byte sequence from a node subtree by: dropping position and
style metadata; sorting mapping keys by UTF-16 code unit; normalizing set-like arrays by sorting their
canonical children; leaving semantically ordered arrays alone; normalizing numbers to a single form; and
keeping every validation keyword and vendor extension unless the active profile excludes it. The
fingerprint is SHA-256 over those bytes, in `Convert.ToHexStringLower`, cached per `(node, profile)` for
the run.

Scalar and number serialization follows RFC 8785 (JSON Canonicalization Scheme) where it applies, so a
fingerprint in an optimization manifest is reproducible by something other than this tool.

**Rationale**: FR-401 needs equality that cannot be fooled by key order, and collision resistance strong
enough that grouping on the fingerprint alone is safe — SHA-256 is that, and the cost is irrelevant beside
parsing. Number normalization matters: `maximum: 1.0` and `maximum: 1` are the same constraint and must
share a fingerprint, while the syntax tree still remembers the two spellings so AUD-200 can report on them.

**Alternatives rejected**: structural equality over the object graph (no stable key for grouping, and
O(n²) comparisons — see R8); a non-cryptographic hash (grouping on it would need a confirmation pass, and
"these are the same" is exactly the claim that qualifies a transformation as SAFE under FR-413); hashing
the raw bytes of the region (defeats the entire point — formatting would create false differences).

---

## R6 — Equivalence profiles are a field-policy table, not three code paths

**Decision**: one table — the OpenAPI metamodel — maps every location in the document to its policy:
whether its collection is set-like or ordered, whether a field is contractual or documentation, whether a
narrowing of it breaks consumers, and which profiles include it. `strict`, `contract` and `shape` (FR-402)
are three readings of that one table.

**Rationale**: the alternative is the same knowledge spread across a comparer, three canonicalizers and
forty-seven rules, drifting apart one commit at a time. As data it is reviewable, testable row by row, and
it is also what FR-111 (ignore the non-semantic, preserve the meaningful order) and FR-112's classification
need. Unknown locations get the safe default: compared as an unordered mapping, classified `unknown` per
FR-114, preserved verbatim, never eligible for automatic transformation.

**Alternatives rejected**: reflection over the library's model types (the table would then describe
somebody else's class shapes rather than the specification, and unknown keywords have no properties to
reflect over); hand-written comparers per node kind (forty-odd near-copies, and FR-113's "report a change
once, at the most specific location" becomes accidental rather than structural).

---

## R7 — The diff and the audit walk our tree, driven by the metamodel

**Decision**: comparison, auditing, fingerprinting, patch generation and optimized output all operate on
the syntax tree with the metamodel as their guide. The semantic model is consulted for validation verdicts
and for the resolved reference graph, and its diagnostics — which are located by pointer — are mapped back
onto our nodes so that every error reaches the screen with a line and a column.

**Rationale**: this is what makes the library replaceable, FR-806 structural rather than aspirational, and
OPT-011's verification meaningful. A verifier that compares two documents through a projection can only
prove the projections match; the contract lives in the bytes.

**Alternatives rejected**: diffing the object model and auditing the text (two models of what the document
says, and every finding would need reconciling across them); generating output by reserializing the
library's model (it would silently drop whatever it does not model — the one failure mode this feature
exists to prevent).

**Consequence worth recording**: because output is produced by editing a node tree rather than by
reserializing a model, the comment-preserving editing that the PRD deferred to "a concrete-syntax-tree YAML
engine" becomes reachable later without a redesign. The spec does not promise it (FR-807, FR-556) and this
plan does not build it; it simply does not foreclose it.

---

## R8 — Near-duplicate detection must be bucketed, never pairwise

**Decision**: exact duplicates group by fingerprint, which is linear. Near-duplicate analysis (AUD-104's
near tier, AUD-107) first buckets candidates by a cheap shape key — kind, property-name set, child count,
size band — and compares only within a bucket, with a configured cap on bucket size. When a bucket exceeds
its cap, the tool reports that the comparison was truncated rather than reporting a clean result, as
FR-418 requires.

**Rationale**: arithmetic. SC-003 asks for 10,000 schemas in under 30 seconds; all-pairs comparison is
about 50 million comparisons of non-trivial trees, which no amount of micro-optimization rescues. Bucketing
turns it into a few thousand small comparisons. This is a design constraint, not a tuning exercise, which
is why it belongs here rather than in a later profiling pass.

**Alternatives rejected**: pairwise with an early-exit heuristic (still quadratic in the worst case, which
is a large generated file — precisely our target); skipping near-duplicates in v1 (they are the finding a
reviewer most wants, and AUD-107 is specified).

---

## R9 — Steps: ten named, individually recoverable

**Decision**: a run is `Select` → `ReadTarget` → `ReadSource` → `Validate` → `Compare` → `Version` →
`Audit` → `Plan` → `Extract` → `Write`. Steps absent from the chosen mode are skipped and recorded as
skipped, not as successful. Every step is idempotent, because every step either reads or writes a whole
artifact once.

**Rationale**: Principle VIII and FR-702 require named recoverable steps, and these are the boundaries
where failure actually happens: a file that disappeared, a reference that will not resolve, a verification
that failed. `Write` is the only step that touches the world, which is what makes "retry the failed step"
safe to offer everywhere else.

**Alternatives rejected**: one step per mode (nothing to resume into); a step per audit rule (hundreds of
checkpoints to express one pass over a tree, and no user-meaningful recovery point).

**Added with User Story 9**: `Extract` sits between `Plan` and `Write` because it produces a document and
`Write` publishes it, which keeps "the only step that touches the world" true. Extraction reuses
`IDependencyResolver` with the chosen operations as roots — **a patch closes over the changed set, an
extract closes over the chosen set** — so the only genuinely new work is the inheritance a trim loses
(path-level parameters, servers, the global security requirement) and the effective-form verification of
FR-910.

---

## R10 — The shared checkpoint has to stop being varcompare-shaped

**Decision**: generalize `PersistedRun` from `(RunId, ToolId, Collection, Project, SelectedGroups,
CompletedSteps, StartedAt, LastUpdatedAt)` to `(RunId, ToolId, Selection, CompletedSteps, StartedAt,
LastUpdatedAt)`, where `Selection` is `IReadOnlyList<PersistedSelectionEntry>` and an entry is two strings,
a label and a value. The runtime shape guard in `FileRunCheckpointStore` keeps doing its job over the new
allow-list — which gets *narrower*, since `int` and `PersistedGroupSelection` leave it.

apidelta's selection is then the mode, the two input paths with their content hashes, the output directory
and the ruleset path. FR-705's refusal to resume when a hash no longer matches is a comparison of two
strings.

**Rationale**: the current record cannot express this feature's selection, and
`FileRunCheckpointStore.SaveAsync` refuses to write a widened shape at runtime, so there is no quiet way
round it. `ResumePrompt.Ask` takes `IReadOnlyList<PersistedRun>` and is shared, so the generalization also
gives the resume prompt something it can display for any tool. prqueue (002, specified, not yet built)
needs the same thing for its remembered project subset, so one change serves three tools.

**Alternatives rejected**: a second checkpoint store owned by apidelta (duplicates the store, the expiry
sweep and the resume glue, and two tools would then expire runs by different rules); an additive nullable
member alongside the existing trio (a union type two tools read differently, which is the shape that rots);
putting the source path in `Collection` and the target path in `Project` (the resume prompt would lie, and
CLAUDE.md's invariant is about meaning, not about field types).

**Cost**: `VarCompareCheckpointFactory`, the resume coordinator and their tests change mechanically.
VarCompare's *behaviour* does not. Recorded in the plan's Complexity Tracking.

---

## R11 — The shell must be able to start without an Azure DevOps server

**Decision**: add `bool RequiresServer => true;` as a default interface member on `ITool`, return `false`
from apidelta, and move server resolution and validation from startup to the moment a tool that needs one
is chosen. The header renders the server when one is configured and says nothing about it otherwise.

**Rationale**: today `Program.BuildHostAsync` runs `ServerConfigurationResolver.ResolveAsync` *before the
host is built*, prompting on first launch for a base URL and a collection, and `Program.RunAsync` then
resolves `IOptions<AzureDevOpsServerOptions>` eagerly and exits with `InvalidConfiguration` if validation
fails. apidelta reads two local files and talks to nothing. Requiring a developer to invent a server
address before auditing an OpenAPI file would be the toolbox's first genuinely silly moment, and the
default interface member keeps the change additive: no existing tool is edited.

**Alternatives rejected**: leaving startup alone and accepting the prompt (a tool that needs no network
would inherit a network prerequisite, and the header would name a server it never contacts); keying the
decision off the tab name (a string comparison standing in for a capability — it would be wrong the first
time a server-backed tool lands under a new tab); a separate entry point for offline tools (two shells).

---

## R12 — `AppPaths.CombineConfined` must be fixed before it is relied on

**Decision**: fix the shared helper to compare against the root *with a trailing separator* (or via
`Path.GetRelativePath`) before apidelta uses it for a developer-chosen output directory, and cover the
escape with a test.

**Rationale**: it currently does `candidate.StartsWith(fullRoot, OrdinalIgnoreCase)`. With `fullRoot` =
`C:\out`, the path `C:\out\..\outside\x` normalizes to `C:\outside\x`, which starts with `C:\out` and is
therefore accepted. Today every caller passes a GUID-derived file name under a root the application owns,
so nothing exploits it. apidelta would be the first caller to pass it a user-chosen root and
user-influenced names (FR-006b, FR-709), which turns a latent defect into a live one.

**Alternatives rejected**: apidelta carrying its own confinement helper (two implementations of the same
security check, one of them wrong); treating it as out of scope (it becomes load-bearing for this feature's
threat model the moment we call it).

---

## R13 — Classification: cross-check against published prior art

**Decision**: implement FR-301 to FR-303 from the specification, then cross-check the rule set against
`oasdiff`'s published checks (`oasdiff checks --severity warn,error`) before calling the version engine
done.

**Rationale**: oasdiff documents over a hundred breaking-change kinds and splits them into `ERR`
("definite breaking changes") and `WARN` ("potential breaking changes which developers should be aware of,
but cannot be confirmed programmatically"). That second tier is independent confirmation that FR-114's
`unknown` classification with a confidence is the right shape rather than a hedge — a mature tool in this
space needed the same tier. Reading its catalogue is cheap insurance against a missing rule.

**Alternatives rejected**: depending on oasdiff at runtime (a Go binary as a prerequisite — refused by the
PRD); treating our list as complete because the PRD wrote it down (the PRD is an input, not an oracle).

**Cross-check performed, and partial.** The catalogue is not enumerable from the published documentation:
`BREAKING-CHANGES.md` names only a handful of examples and says to run `oasdiff checks` for the rest. Since
the binary is deliberately not a prerequisite here, the comparison was made against what the documentation
does name. Three findings:

- **One real gap.** oasdiff treats *removing a component schema* as breaking. Our metamodel classifies a
  removal under `/components/*` as `Unknown`, on the grounds that a component is only a contract through
  its uses. That stands, and for a reason the comparison itself supplies: a candidate that removes a
  still-referenced component produces an unresolved reference, which the reader refuses outright (FR-005,
  FR-009) — so the case oasdiff reports as breaking never reaches our classifier, because the document is
  rejected as invalid first. A component removed and *no longer referenced* is genuinely not a contract
  change, and the unreachable-component audit (AUD-003) is what has something to say about it.
- **Two pieces of prior art worth borrowing later**, both policy expressed as a vendor extension:
  `x-extensible-enum`, which lets a description declare that enum additions are not breaking, and
  `x-stability-level`, which lets an endpoint declare itself below stable and therefore free to change.
  Both are exactly FR-304's shape — the impact being a choice rather than a rule — but declared in the
  document rather than in a ruleset. Noted for the configuration slice (US8); neither is adopted now,
  since inventing support for someone else's extension before anyone asks is how a tool grows features
  nobody uses.
- **One limitation it declares and we share honestly**: callbacks are not checked. Ours resolve to
  `Unknown`, which says so rather than implying they were examined.

Being partial, this cross-check does not discharge FR-302 and FR-303 on its own. What does is the mutation
suite: twelve isolated tightenings, each asserted never compatible and never documentation-only.

---

## R14 — Verification is the comparer, pointed at our own output

**Decision**: OPT-011 writes the optimized document to a buffer, reparses *those bytes* through the same
loader, revalidates, resolves references, and compares against the pre-optimization tree under the
`contract` profile. Only then does anything reach disk. The manifest (OPT-012) records each
transformation's rule, origin pointer, destination component, fingerprint evidence and reversibility.

**Rationale**: the strongest verification available is the engine whose job is detecting contract change,
run against the artifact rather than against the intention. Verifying the in-memory tree we just edited
would skip serialization, which is where a quoting or anchor mistake would actually appear. Writing to a
buffer first is what makes FR-552's "never publish a partial artifact" true by construction rather than by
cleanup.

**Alternatives rejected**: trusting rule-level proofs (each rule's local argument is sound and the
composition still has to be checked); verifying after writing and deleting on failure (a window in which a
wrong file exists on disk under the name of a right one).

---

## R15 — The performance budget

**Decision**: for the SC-003 document (1,000 operations, 10,000 schemas, roughly 10–15 MB of YAML): parse
once, under 5 s; fingerprint every candidate subtree once, cached, under 5 s; exact-duplicate grouping
linear; near-duplicate work bucketed per R8; audit rules in a single pass over the tree, collecting
findings per rule rather than re-walking per rule. Progress is reported per step, and nothing accumulates
unboundedly: findings stream into the writer, grouped, with a configured cap per rule and an honest
"truncated" note when it is hit.

**Rationale**: the budget's only real risks are quadratic comparison (R8) and re-walking the tree once per
rule — forty-seven passes over a 15 MB tree is how a 5-second job becomes a 90-second one. One pass with a
rule registry keyed by node kind is the same code, organized by what it visits.

**Alternatives rejected**: parallelising the rules (bounded parallelism is permitted, but a single pass is
both faster and deterministic, and determinism is SC-004); lazy or streaming parse (the audit needs the
whole graph anyway — unused components cannot be found without it).

---

## R16 — What the repository's own rules impose on the shape of the code

**Decision**: accept that MA0048 (one public type per file, named for the file) and MA0051 (60-line
methods) mean the rule catalogue is roughly fifty small sealed classes, one per `AUD-`/`OPT-` identifier,
each implementing `IAuditRule`/`IOptimizationRule`, discovered by registration rather than by reflection.

**Rationale**: it is the house style and it suits the problem — a rule per file is a reviewable unit, and
FR-419's promise that identifiers are stable is easiest to keep when an identifier is a type. Registration
rather than assembly scanning keeps the composition root honest and `CompositionRootTests` able to resolve
the real graph.

**Also inherited, and unchanged**: `TreatWarningsAsErrors`, `NuGetAudit` at `low` with
`NuGetAuditMode=all`, central package versions, `ConfigureAwait(false)` in every library assembly, nullable
enabled, sealed by default.

**Pre-existing drift, named rather than fixed here**: `Directory.Build.props` sets
`AnalysisLevel=latest-recommended` where the constitution says `latest-all`, and the repository uses
`AsyncFixer` where the constitution names `Microsoft.VisualStudio.Threading.Analyzers`. Both predate this
feature, which inherits them without widening them. Reconciling code and constitution is a separate,
deliberate change — the constitution's own governance section says to fix one or amend the other rather
than leave both standing.

---

## R17 — Rendering thousands of findings in a terminal

**Decision**: the screen shows a summary first — counts by severity, by safety class and by rule, plus the
version recommendation and the measured savings — then lets the developer drill into one rule or one group
at a time, paged. The artifacts are the full record; the terminal is the index. Progress during long steps
uses the shared Spectre `Progress`, and Back exists on every screen per Principle V.

**Rationale**: a 4,000-row table in a terminal is not information. The report files already carry
everything (FR-600, FR-601), so the interactive job is navigation and triage.

**Alternatives rejected**: printing every finding (unreadable, and slow to render); an interactive
finding-by-finding wizard (hundreds of keystrokes to see what one file already says).

---

## R18 — What implementation settled, 2026-10-21

The open items this research left for the build, resolved by the build.

**Microsoft.OpenApi 3.10.2, the reading API as used.** `OpenApiModelFactory.Load` over a `MemoryStream`
with an explicit `OpenApiReaderSettings`, returning a `ReadResult` whose `Diagnostic.Errors` carry a
`Pointer` string. That pointer is what `PointerNavigator.Find` turns back into a node, which is how a
structural verdict acquires a line and a column. What it preserves is irrelevant to us and was never
tested: the semantic model is used to validate and to resolve, never to re-emit. **Nothing published by
this tool is serialized from it** — that single rule is what keeps a vendor extension intact, and it is
now recorded in `CLAUDE.md` rather than left as a habit.

**YamlDotNet 18.1.0, the member names relied on.** `Parser` over a `StringReader`; `StreamStart`,
`DocumentStart`, `DocumentEnd`, `MappingStart`, `SequenceStart`, `Scalar`, `AnchorAlias` consumed through
`Consume<T>` / `TryConsume<T>` / `Accept<T>`; `Scalar.Value`, `Scalar.Style`, `Scalar.Tag`,
`Scalar.IsQuotedImplicit`; positions from `ParsingEvent.Start` and `.End` as `Mark.Line`, `.Column`,
`.Index`. `Mark.Index` is what gives a node its byte span, and therefore what makes a saving measured
rather than estimated.

**Aliases and anchors, as implemented.** An alias is expanded into the node it names, and each expansion
counts against `LoadLimits.MaxAliasExpansions` — a YAML bomb is refused with the budget named. The
expansion is a *copy*, so a document using anchors reads as the document it denotes, which is what every
later stage needs. Consequence, and it is stated in the reports: a regenerated output has no anchors, and
the original file is the only place they survive (FR-556).

**The reference walk needed its own depth bound, and that was found by measuring.** A chain of ten
thousand schemas each referencing the next made `ReferenceGraph` descend ten thousand stack frames and
crash with a stack overflow — no diagnostic, no position, nothing said about what had been read. Such a
description is plausible: a linked list of types produces it. The walk now carries a depth, stops at four
hundred links, and reports the limit as a warning, leaving the rules that depend on the graph to declare
themselves unevaluated. Two distinct budgets for two distinct exhaustions: `MaxReferences` counts
references, this one measures how many are chained.

**`SpecPointer.Json` is memoised.** The pointer is immutable, so remembering its printed form cannot lie —
and it is asked for everywhere, notably by the sort that orders the walked nodes, where every comparison
was rebuilding two strings. On a ten-megabyte description that was millions of reconstructions of the same
result.

---

## Sources

- [Microsoft.OpenApi on NuGet](https://www.nuget.org/packages/Microsoft.OpenApi/) — 3.10.2, last updated
  2026-08-20, `net8.0`/`netstandard2.0`, no `net8.0` dependencies, not deprecated.
- [Microsoft.OpenApi.YamlReader on NuGet](https://www.nuget.org/packages/Microsoft.OpenApi.YamlReader/) —
  3.10.2, depends on `SharpYaml` ≥ 2.1.5, not on YamlDotNet.
- [OpenAPI.NET repository](https://github.com/microsoft/OpenAPI.NET) and its
  [v3 upgrade guide](https://github.com/microsoft/OpenAPI.NET/blob/main/docs/upgrade-guide-3.md) —
  `OpenApiSpecVersion` covers `OpenApi2_0`, `OpenApi3_0`, `OpenApi3_1`, `OpenApi3_2`; YAML reading lives in
  a separate package.
- [YamlDotNet on NuGet](https://www.nuget.org/packages/YamlDotNet/) — 18.1.0, MIT, last updated
  2026-06-26, targets include `net10.0`, no dependencies, low-level parsing plus a representation model.
- [What's new in .NET 10 libraries](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/libraries#serialization)
  and [`JsonDocumentOptions.AllowDuplicateProperties`](https://learn.microsoft.com/dotnet/api/system.text.json.jsondocumentoptions.allowduplicateproperties)
  — duplicate JSON property detection, default `true`, `JsonException` when disabled.
- [oasdiff](https://github.com/Tufin/oasdiff) and its
  [breaking-changes catalogue](https://github.com/tufin/oasdiff/blob/v1.10.16/BREAKING-CHANGES.md) — over
  a hundred checks, `ERR`/`WARN` severities, `--fail-on ERR`.
- [RFC 8785, JSON Canonicalization Scheme](https://www.rfc-editor.org/rfc/rfc8785) — scalar and number
  serialization rules reused by R5.
