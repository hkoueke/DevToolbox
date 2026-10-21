# Data model — apidelta

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Date**: 2026-10-16

Shapes, not storage. Everything here lives in `DevToolbox.Tools.ApiDelta.Core` unless the heading says
otherwise, and everything is a sealed `record` with init-only members: nothing in this model mutates after
the step that produced it. Where an invariant is what makes a promise in the spec structural rather than
conventional, it is marked **Invariant** and belongs in a test.

---

## 1. The document and its syntax

### `SpecDocument`

One input, in its role.

| Member | Type | Notes |
|---|---|---|
| `Role` | `DocumentRole` | `Candidate` or `Production` |
| `Path` | `string` | absolute, canonical, confined |
| `Format` | `SpecFormat` | `Json`, `Yaml` — detected from content, not from the extension (FR-001) |
| `DeclaredVersion` | `OpenApiVersion` | `V30`, `V31`; anything else is refused before this type exists (FR-002) |
| `ByteLength` | `long` | what the savings arithmetic and the report are measured against |
| `ContentHash` | `string` | SHA-256, lowercase hex; the identity used by the checkpoint and the report |
| `Root` | `SpecNode` | the lossless tree |

**Invariant**: a `SpecDocument` exists only if it was read faithfully. Parse failures, duplicate keys,
unresolved references and unsupported versions produce `Diagnostic`s and no document, which is how FR-009
is enforced — downstream code cannot be handed a half-read document because none can be constructed.

### `SpecNode`

The tree the whole tool works on. A discriminated shape: `MappingNode`, `SequenceNode`, `ScalarNode`.

| Member | Type | Notes |
|---|---|---|
| `Location` | `SpecPointer` | where this node is |
| `Span` | `ByteSpan` | start and end offset in the source; what makes byte-exact savings possible (FR-403) |
| `Position` | `SourcePosition` | line and column, 1-based, for every diagnostic and finding |
| `Kind` | `NodeKind` | mapping, sequence, scalar |

`MappingNode` adds `Entries` as an *ordered* list of `(KeyScalar, SpecNode)` — order is preserved because
AUD-201 reports on it, and because a mapping's written order is a fact even where it carries no meaning.
`SequenceNode` adds `Items`. `ScalarNode` adds `Text` (as written), `Style` (plain, single-quoted,
double-quoted, literal, folded), `Tag`, and `Resolved` — a `ResolvedScalar` carrying the YAML 1.2
core-schema value and its kind (`Text`, `Integral`, `Number`, `Boolean`, `Null`, or `Ambiguous` with the
reason, per R3).

**Invariant**: the tree is lossless with respect to meaning *and* spelling. Re-emitting an unmodified tree
reproduces the input byte-for-byte. This is the property the round-trip property test asserts, and it is
what FR-806 and FR-110 stand on.

### `SpecPointer`

| Member | Type | Notes |
|---|---|---|
| `Json` | `string` | RFC 6901 JSON Pointer, with `~0`/`~1` escaping — the machine location in every record |
| `Display` | `string` | what a human reads: `POST /documents → parameters[0] (X-serviceEmetteur)` |

Members that hold one are named `Location`, not `Pointer`: CA1720 reads `Pointer` as a type name, and
`Location` reads better anyway. The type keeps its name.

Two representations of one location, because FR-112 and FR-410 require both and because deriving the
display form at the point of rendering loses the context only the walk has.

### `Diagnostic`

| Member | Type | Notes |
|---|---|---|
| `Code` | `string` | stable, e.g. `LOAD-DUPLICATE-KEY`, `LOAD-UNRESOLVED-REF` |
| `Severity` | `DiagnosticSeverity` | `Error`, `Warning`, `Information` |
| `Message` | `string` | already fit to display |
| `Location` | `SpecPointer?` | absent only for whole-file failures |
| `Position` | `SourcePosition?` | present whenever the parser gave one (FR-003) |
| `Related` | `IReadOnlyList<SourcePosition>` | the *other* occurrences — both halves of a duplicate key, every use of an unresolved reference |

---

## 2. The metamodel

### `MetamodelEntry`

The table described in R6. One row per field or collection in the OpenAPI surface.

| Member | Type | Notes |
|---|---|---|
| `Location` | `LocationPattern` | a pointer pattern, e.g. `/paths/*/{method}/parameters` |
| `Versions` | `OpenApiVersions` | which declared versions the row applies to |
| `Collection` | `CollectionKind` | `None`, `SetLike`, `Ordered`, `KeyedMap` — what FR-111 needs to know |
| `Identity` | `IdentityRule` | how members are paired: by key, by `(name, in)` for parameters, by status code, by index |
| `Contractual` | `FieldRole` | `Contract`, `Documentation`, `Vendor` — drives profile membership (FR-402) |
| `Narrowing` | `ImpactRule` | what a narrowing, a widening, an addition and a removal mean here (FR-301…FR-303) |
| `Policy` | `PolicyKey?` | set where the impact is configurable, e.g. enum addition, response addition (FR-304) |

**Invariant**: an unmatched location resolves to the safe default — compared as an unordered mapping,
`FieldRole.Vendor`, impact `Unknown`, included in every profile, never eligible for automatic
transformation. A missing row can therefore make the tool *less* decisive, never wrong. That is what makes
FR-114 structural.

### `EquivalenceProfile`

`Strict`, `Contract`, `Shape` (FR-402). A profile is a predicate over `MetamodelEntry`, not a separate
canonicalizer. **Invariant**: only `Strict` can qualify a transformation for automatic application
(FR-413), asserted by a test over the optimization rules rather than by review.

---

## 3. Comparison

### `Difference`

| Member | Type | Notes |
|---|---|---|
| `ChangeId` | `string` | stable identifier of the kind of change |
| `Location` | `SpecPointer` | the most specific location that explains it (FR-113) |
| `Kind` | `DifferenceKind` | `Added`, `Removed`, `Modified` |
| `Before` / `After` | `ValueSummary?` | bounded summaries, never whole subtrees |
| `Classification` | `ChangeImpact` | `Breaking`, `NonBreaking`, `DocumentationOnly`, `Unknown` |
| `Confidence` | `Confidence` | `Certain`, `Probable`, `Uncertain` |
| `Rationale` | `string` | why it is classified so |
| `RecommendedAction` | `string` | what to do about it |

**Invariant**: one underlying change yields exactly one `Difference` (FR-113). A changed property does not
also produce a `Modified` for each ancestor. The walk emits at the deepest node whose metamodel row
explains the change, and a test over the corpus asserts no ancestor-plus-descendant pairs.

**Invariant**: `ValueSummary` is bounded in length and redacted through the same path as logs. A diff
record is a thing that gets written to a file; it must not become the leak that FR-607 forbids.

### `ValueSummary`

`Text` (truncated, with an explicit elision marker), `Kind`, `FullLength`. Deliberately not the node: a
summary that can grow to the size of the document would make reports unreadable and would carry example
payloads into artifacts.

---

## 4. Versioning

### `VersionRecommendation`

| Member | Type | Notes |
|---|---|---|
| `Baseline` | `SemanticVersion?` | production's version; `null` when unparseable (FR-300) |
| `BaselineProblem` | `string?` | reported rather than guessed at |
| `Increment` | `VersionIncrement` | `None`, `Patch`, `Minor`, `Major` (FR-305, FR-306) |
| `Recommended` | `SemanticVersion?` | what to publish |
| `Contributors` | `IReadOnlyList<SpecPointer>` | the differences that set the level (FR-303) |
| `ResolvedPolicy` | `IReadOnlyDictionary<PolicyKey, ImpactRule>` | every configurable choice, as resolved (FR-308) |
| `Override` | `VersionOverride?` | supplied version, whether it is below the recommendation, and whether it was forced (FR-307) |

---

## 5. References and dependencies

### `ReferenceEdge`

`From` (pointer), `To` (`ReferenceTarget`), `IsExternal`, `Resolution` (`Resolved`, `Unresolved`,
`DisallowedByPolicy`). `ReferenceTarget` is a canonical identity — normalized URI plus fragment — which is
the key the visited set uses so cycles terminate (FR-201).

### `DependencyClosure`

`Required` (the set of component pointers), `ExternalPreserved` (FR-203), `Cycles` (reported, not failed,
when they are legal for schemas — FR-005), `Conflicts`.

### `ComponentConflict`

`Name`, `Kind`, `CandidatePointer`, `ProductionPointer`, `DifferingFields`. **Invariant**: a conflict is
reported and never resolved by overwriting (FR-204, FR-802).

---

## 6. Audit

### `Finding`

The spec's FR-410 field set, exactly.

| Member | Type | Notes |
|---|---|---|
| `RuleId` | `string` | `AUD-101`; part of the output contract (FR-419) |
| `Severity` | `FindingSeverity` | `Error`, `Warning`, `Information`, `Opportunity` (FR-411) |
| `Safety` | `SafetyClass` | `Safe`, `ReviewRequired`, `Unsafe` (FR-412) |
| `Message` | `string` | |
| `Location` | `SpecPointer` | the primary location |
| `Locations` | `IReadOnlyList<SpecPointer>` | every other location in the group |
| `EstimatedBytesSaved` | `long?` | `null` where saving is not the point |
| `SuggestedComponentName` | `string?` | sanitized before it can reach a file name or a `$ref` |
| `AutofixAvailable` | `bool` | **Invariant**: `true` only when `Safety is Safe` and strict equivalence was established (FR-416) |
| `RecommendedAction` | `string` | |
| `Evidence` | `FindingEvidence?` | occurrence count, fingerprint, differing fields — what the claim rests on |

### `RuleOutcome`

What a rule reports about *itself*, which is how FR-418 is kept: `RuleId`, `State`
(`Evaluated`, `NotEvaluated`, `Truncated`, `Disabled`, `Suppressed`), `Reason`, `FindingCount`.

**Invariant**: the audit result is a complete list of `RuleOutcome`s, not just a list of findings. A rule
that could not run appears as `NotEvaluated` with its reason, and the report cannot claim a clean audit
while one exists. This is the varcompare discipline — undetermined is not absent — carried into a second
tool.

---

## 7. Redundancy and fingerprints

### Fingerprints

A fingerprint is the SHA-256 of a canonical form, as lowercase hex, and it is always carried next to the
profile under which it was taken: the pair is what makes it meaningful, and neither half means anything
alone. It is a string rather than an entity because nothing consumes a canonical length.

### `RedundancyGroup`

| Member | Type | Notes |
|---|---|---|
| `RuleId` | `string` | the audit rule that established the repetition |
| `Fingerprint` | `string` | what makes the members provably the same |
| `Profile` | `string` | under which profile that proof was established |
| `Kind` | `CandidateKind` | parameter, response, request body, schema, header, example, enum, other |
| `Occurrences` | `IReadOnlyList<Occurrence>` | pointer plus serialized size, per occurrence |
| `Tier` | `DuplicateTier` | `Exact`, `Near` — never mixed in one group (AUD-104) |
| `DifferingFields` | `IReadOnlyList<string>` | populated for `Near` and for AUD-107, empty for `Exact` |
| `SuggestedComponentName` | `string?` | already sanitized, from the rule |
| `StrictEquivalenceEstablished` | `bool` | what lets a transformation be applied automatically |
| `Savings` | `SavingsEstimate` | |

**Invariant** (FR-403c): a group exists only for a finding whose rule published both of its measurements.
The rule measures, because only the rule knows what it looked at — a repeated header set is measured on the
headers, not on the operations carrying them. The plan records and computes; it never measures again.

### `SavingsEstimate`

`CurrentBytes`, `ProjectedBytes`, `AbsoluteSaving`, `PercentSaving`, `BreakEvenOccurrences`, `Safety`
(FR-403). **Invariant** (FR-403a): `BreakEvenOccurrences` is computed, and a group *below* it reports a
saving of zero or less rather than a recommendation — extraction that saves nothing is not an improvement.
`Recommended` is false whenever the saving is not positive or the safety class is not one the tool can act
on, so a group can be reported without ever being advised.

---

## 8. Optimization

### `Transformation`

| Member | Type | Notes |
|---|---|---|
| `RuleId` | `string` | `OPT-002`, read from the family the origin designates (FR-403b) |
| `FromRuleId` | `string?` | the audit rule the proposal proceeds from |
| `Safety` | `SafetyClass` | taken from the finding; a plan never invents a safety class |
| `Origins` | `IReadOnlyList<SpecPointer>` | what it replaces |
| `Destination` | `SpecPointer?` | the component it creates, absent for a move such as OPT-008 |
| `Evidence` | `string?` | the strict fingerprint that justifies it |
| `Reversal` | `ReversalDescription` | how to undo it, and whether undoing restores the exact state (OPT-012) |
| `Summary`, `ReviewReason` | `string` | what it does, and why a relecture is needed when one is |
| `EstimatedBytesSaved`, `BreakEvenOccurrences` | | from the group's `SavingsEstimate` |
| `State` | `TransformationState` | `Proposed`, `Applied`, `Rejected`, `Blocked` |

**Invariant**: nothing on a `Transformation` can hold transformed content, so a plan cannot become a vehicle
for a write. `ApplicableWithoutPermission` is computed from the safety class and the presence of evidence,
never set.

### `OptimizationPlan`

`Groups`, `Transformations`, `TotalEstimatedSaving`, `AppliedCount`, `GrowthCheck`
(`ProjectedBytes`, `Limit`, `Exceeded` — FR-555), and `Uncovered`.

The plan carries the groups as well as the proposals because the two answer different questions: the first
says what the file has accumulated, the second what the tool knows how to do about it. `Uncovered` is the
difference, and it is what stops an empty proposal list from reading as a clean file.

### `VerificationResult`

`Reparsed`, `Revalidated`, `ReferencesResolved`, `ContractEquivalent`, `Differences` (what equivalence
found, when it failed). **Invariant**: no optimized artifact is written unless all four flags are true
(FR-552, OPT-011), and the verification runs against the *serialized bytes*, not the edited tree (R14).

### `OptimizationManifest`

`RunId`, `InputHash`, `OutputHash`, `Entries` (one per applied transformation), `Reversible`.

---

## 8b. Extraction

### `OperationSelector`

| Member | Type | Notes |
|---|---|---|
| `Path` | `string` | the path template exactly as the document writes it |
| `Method` | `HttpOperationMethod` | one of the eight; selection is per operation, not per path (FR-901) |

Equality is by both members, ordinally. A selector is what the developer picked, independent of where it
sits in the tree — which is what lets it survive a restart as two strings and be re-resolved against a
freshly read document (FR-913).

### `SelectableOperation`

What the picker shows: `Selector`, `Summary`, `Tags`, `Deprecated`, `Location`. Built by walking the paths
once; it carries no schema and no example, so listing four hundred operations costs four hundred short
strings.

### `ExtractRequest`

`Selectors`, `StampProvenance`, `OutputName`. **Invariant**: an empty `Selectors` is a usage error refused
before any work (FR-701) — there is no such thing as an empty extract.

### `EffectiveOperation`

The operation after inheritance is applied: its own fields, plus the path item's parameters merged under
the override rules, plus the servers in force, plus the security requirement in force. **Invariant**: this
is the unit FR-910 compares, on both sides. Comparing the written operation rather than the effective one
would pass an extract that had quietly dropped a path-level parameter or a global security requirement —
which is the exact failure this mode has to be unable to produce.

### `ExtractResult`

| Member | Type | Notes |
|---|---|---|
| `Document` | `SpecNode` | the built extract, before emission |
| `Closure` | `DependencyClosure` | what came along, and why (FR-904) |
| `CarriedTags` | `IReadOnlyList<string>` | FR-906 |
| `InheritedContext` | `IReadOnlyList<InheritanceRecord>` | per selector: which path parameters, servers and security requirement were carried, so the report can show it |
| `DanglingExtensionReferences` | `IReadOnlyList<Finding>` | carried extensions naming paths the extract does not contain (FR-909) |
| `Provenance` | `ExtractProvenance` | source path, source hash, selectors, tool version |
| `Verification` | `ExtractVerification` | |

### `ExtractVerification`

`Reparsed`, `Revalidated`, `ReferencesResolved`, `ContainsNothingUnselected`, `OperationsEquivalent`, plus
`Differences` for what equivalence found when it failed. **Invariant**: no extract is written unless all
five are true (FR-910), and the comparison runs on the emitted bytes, as in §8's `VerificationResult`.

---

## 9. Configuration

### `Ruleset`

`Rules` (`IReadOnlyDictionary<string, RuleSetting>`), `ProtectedExtensionPrefixes`, `DeprecationPhrases`,
`NamingConventions`, `StandardHeaders`, `StandardResponses`, `EnvironmentPlaceholders`,
`CanonicalFormatting`, `ReferencePolicy`, `Limits`, `VersioningPolicy`.

### `RuleSetting`

`Enabled`, `Severity?`, `MinimumOccurrences?`, `Thresholds` (per-rule, typed on read).

### `Suppression`

`RuleId`, `LocationPattern`, `Justification`, `Owner`, `Expires?` (FR-652). **Invariant**: a suppression
missing a justification while justification is required is *invalid configuration*, rejected before the run
starts (FR-655) — not an ignored suppression, which would silently change what the audit reports.

### `SuppressionOutcome`

`Suppression`, `State` (`Applied`, `Expired`, `Unmatched`), `SuppressedFindingCount`. Expired and unmatched
both reach the report (FR-653, FR-608).

---

## 10. The run

### `RunSelection`

Everything the developer chose, and the only thing that survives the process.

| Member | Type | Notes |
|---|---|---|
| `Mode` | `RunMode` | `Compare`, `Patch`, `Audit`, `Optimize`, `Extract`, `Everything` (FR-700) |
| `Extract` | `ExtractRequest?` | present only for the extract mode (§8b) |
| `CandidatePath` / `ProductionPath` | `string?` / `string` | candidate absent for audit and optimize (FR-701) |
| `CandidateHash` / `ProductionHash` | `string?` / `string` | what makes resuming safe (FR-705) |
| `OutputDirectory` | `string` | confined; artifacts land here |
| `RulesetPath` / `SuppressionsPath` | `string?` | |
| `ApplySafeOptimizations` | `bool` | |
| `AllowedReviewRequiredRules` | `IReadOnlyList<string>` | FR-414 |
| `IncludeRemovals`, `IncludeSafeOptimizationsInPatch`, `InPlace` | `bool` | FR-502, FR-503, FR-550 |

**Invariant**: `RunSelection` is projected to the shared checkpoint as labelled strings and **cannot reach
document content**. There is no member here, directly or transitively, that holds a node, a finding or a
value — the same absence that `PersistedRun` relies on, asserted by a test that serializes a checkpoint and
inspects the JSON.

### `ApiDeltaRun`

`RunId`, `Selection`, `Steps` (name, status, duration, skipped-with-reason), `Diagnostics`,
`Differences`, `VersionRecommendation`, `RuleOutcomes`, `Findings`, `Plan`, `Verification`, `Artifacts`,
`Outcome` (`Success`, `Failure`, `Cancelled`), `OutcomeCode`, `Duration`.

### `OutcomeCode`

The fixed vocabulary of FR-708: `Clean`, `ThresholdExceeded`, `BreakingChange`, `InvalidUsage`,
`LoadFailure`, `OutputFailure`, `InternalError`. **Invariant** (FR-708a): it is displayed and written to
the machine-readable report, and it is *not* returned as the process exit code — the shell owns
`ExitCodes`, and this release adds nothing to it.

### `Artifact`

`Kind` (`DeltaReport`, `Patch`, `DeltaJson`, `AuditReport`, `OptimizationPlan`, `OptimizedDocument`,
`Manifest`, `Extract`), `Path`, `ByteLength`, `Hash`. **Invariant**: an `Artifact` is recorded only once the file is
durable; a cancelled or failed run records none for the step that did not finish (FR-704, FR-552).

---

## 11. State transitions

The ten steps of R9, in order, with what each may do:

```text
Select → ReadTarget → ReadSource → Validate → Compare → Version → Audit → Plan → Extract → Write
```

| Step | Skipped when | Produces | Touches disk |
|---|---|---|---|
| `Select` | never | `RunSelection` | no |
| `ReadTarget` | never | production `SpecDocument` | reads |
| `ReadSource` | mode is audit, optimize or extract | candidate `SpecDocument` | reads |
| `Validate` | never | `Diagnostic`s, reference graph | no |
| `Compare` | no candidate | `Difference`s | no |
| `Version` | no candidate, or mode is extract (FR-908) | `VersionRecommendation` | no |
| `Audit` | mode is compare, patch or extract | `RuleOutcome`s, `Finding`s, `RedundancyGroup`s | no |
| `Plan` | mode is compare, patch or extract | `OptimizationPlan` | no |
| `Extract` | mode is not extract | `ExtractResult` | no |
| `Write` | never | `Artifact`s, `VerificationResult`, `OptimizationManifest` | writes |

The extract mode's own selection step is where the operations are chosen, so `Select` is the only step
whose work differs by mode rather than being skipped by it.

**Invariant**: a skipped step is recorded as `Skipped` with its reason, never as `Succeeded`. A run's step
list is therefore a truthful account of what happened, which is what the resume prompt and the report both
read.

**Invariant**: every step is idempotent. `Write` achieves it by writing whole artifacts to a temporary name
in the output directory and moving them into place, so a retried `Write` cannot leave a half-written report
that looks finished.

**Recovery** (FR-703), offered through the shared `IRecoveryPrompt`: retry the failed step, continue from
it, restart the run, abort. Abort states what exists on disk. On the next launch, an interrupted run is
offered for resume; resuming re-reads both inputs and refuses if either content hash has changed
(FR-705) — **resuming re-reads, it never restores**.
