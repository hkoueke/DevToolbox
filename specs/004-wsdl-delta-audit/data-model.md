# Data Model: A WSDL as a candidate for apidelta

Every shape below names the requirement it answers. A shape without a requirement would be inventory, and
a requirement without a shape would be a promise.

Nothing here replaces anything in 003. The selection, the plan, the marks and the verification keep the
types they have; this feature adds what comes *before* them.

## The WSDL side, as read

### `WsdlContract` — WR-001, WR-004, WR-008

The unit the tool reads and reports on. Not the entry file: the set of documents reached under the policy.

| Field | Shape | Why |
|-------|-------|-----|
| `Documents` | `IReadOnlyList<WsdlDocument>` | every document opened, in the order opened |
| `Operations` | `IReadOnlyList<WsdlOperation>` | the units a correspondence pairs |
| `Types` | `IReadOnlyDictionary<XmlQualifiedName, XsdType>` | the compiled schema set, by qualified name |
| `Unresolved` | `IReadOnlyList<UnresolvedImport>` | what policy forbade, with the import that wanted it |
| `ConfinementRoot` | `string` | the root local resolution was confined to (WR-006) |
| `IsComplete` | `bool` (computed) | false when `Unresolved` is non-empty — WR-008 |

`IsComplete` is computed, never set: a contract that could not be read completely must not be able to
claim it was.

### `WsdlDocument` — WR-004, WR-018, WR-601

| Field | Shape | Why |
|-------|-------|-----|
| `Path` | `string` | where it is on disk, materialised or not |
| `ByteLength` | `long` | reported, and bounded by WR-009 |
| `ContentHash` | `string` | SHA-256; what a resumed run compares |
| `TargetNamespace` | `string?` | absent for a chameleon inclusion |
| `Kind` | `WsdlDocumentKind` | `Definitions` or `Schema` |
| `Origin` | `RetrievedOrigin?` | present only when retrieved |

### `RetrievedOrigin` — WR-018, WR-019

| Field | Shape | Why |
|-------|-------|-----|
| `RedactedUrl` | `string` | userinfo and query redacted at construction, not at use |
| `RetrievedAt` | `DateTimeOffset` | lives in the run-metadata block, so two runs over the same files do not differ |
| `RedirectChain` | `IReadOnlyList<string>` | redacted, each hop, as WR-015 requires |

Redaction happens in the constructor. A type that could hold an unredacted URL would eventually be logged
by someone, and the registry guard is a second line, not the first.

### `WsdlOperation` — WR-104, WR-115, WR-305

| Field | Shape | Why |
|-------|-------|-----|
| `Name` | `string` | the `portType` operation name |
| `Pattern` | `MessagePattern` | request-response, one-way, solicit-response, notification |
| `Input` / `Output` | `WsdlMessage?` | output absent for one-way — WR-115 |
| `Faults` | `IReadOnlyList<WsdlMessage>` | a correspondence, never an equivalence, with status codes |
| `Binding` | `WsdlBindingRef` | style, use, transport, SOAP version |
| `Location` | `WsdlLocation` | document + path + line + column |

### `WsdlLocation` — WR-003

Document identity plus a reviewer-followable path plus line and column. **No `ByteSpan`** — R-03. Nothing
writes WSDL, so an extent would be a capability with no consumer and a defect class with no benefit.

## The projection

### `ProjectionResult` — WR-110, WR-111, WR-603

| Field | Shape | Why |
|-------|-------|-----|
| `Synthetic` | `SpecDocument` | `Format = SpecFormat.Projected`; what the existing comparer runs against |
| `Gaps` | `IReadOnlyList<ProjectionGap>` | grouped by reason downstream, never one per occurrence |
| `Figures` | `ProjectionFigures` | what WR-603 states as a number |
| `Derivations` | `IReadOnlyList<RouteDerivation>` | operation to route, stated per WR-305 |

### `ProjectionGap` — WR-111, WR-112

| Field | Shape | Why |
|-------|-------|-----|
| `Reason` | `ProjectionGapReason` | the grouping key: attribute, substitution group, mixed content, namespace, binding style, particle order, XSD 1.1 construct |
| `Confidence` | `ProjectionConfidence` | `NotCarried` or `Approximate` — never `Carried`, or it would not be a gap |
| `Where` | `WsdlLocation` | so a reader can go and look |
| `Operations` | `IReadOnlyList<string>` | which operations a caller would feel it through |

Grouping is the report's job, not the gap's: one gap per occurrence, one entry per reason. That is what
keeps four hundred attributes from becoming four hundred findings while still letting a reader ask which
ones.

### `ProjectionFigures` — WR-603

`OperationsTotal`, `OperationsCorresponded`, `FieldsProjected`, `FieldsNotCarried`. Four counts, so the
report states a proportion rather than describing one. A reader must be able to tell a clean report from a
silent one, and prose cannot do that.

## The correspondence

### `Correspondence` — WR-104, WR-105, WR-106

| Field | Shape | Why |
|-------|-------|-----|
| `Entries` | `IReadOnlyList<CorrespondenceEntry>` | one per WSDL operation considered |
| `IsConfirmed` | `bool` | nothing compares while false — WR-104 |

### `CorrespondenceEntry` — WR-105, WR-106, WR-303

| Field | Shape | Why |
|-------|-------|-----|
| `Operation` | `string` | the WSDL operation name |
| `Route` | `OperationSelector?` | production's route, null when nothing answers it |
| `Origin` | `CorrespondenceOrigin` | `Declared`, `Proposed` or `Corrected` — WR-105 |
| `Basis` | `string?` | for a proposal: which tokens matched, so a developer can disagree usefully |
| `Candidates` | `IReadOnlyList<OperationSelector>` | more than one when ambiguous |

**Checkpoint-safe by construction.** Every field is a string, an enum or an `OperationSelector`, which is
itself two strings. There is no path from here to a node or a value, which is what lets the correspondence
join the checkpoint beside the draft selection — the same invariant `RunSelection` already states, and the
same reason: resuming re-reads rather than restores.

### `CoverageReport` and `CoverageState` — WR-107, WR-108, WR-109

`CoverageState` is `Covered`, `Absent` or `Ambiguous`. Three states, and `Ambiguous` exists so the
proposer can decline between near-ties rather than guess — a guess would hide uncertainty in the one place
the report promises certainty.

`CoverageReport` carries, per operation, its state and — for `Absent` — what production would need to
carry to answer it, in terms a developer can act on (WR-109). It does **not** carry a `ChangeImpact`:
WR-113 keeps the vocabularies apart, because "what did this break" and "what is not answered yet" are
different questions, and one vocabulary stretched over both would answer neither.

## What this feature adds to 003's types

Three additions, each minimal and each required.

| Change | Where | Requirement |
|--------|-------|-------------|
| `SpecFormat.Projected` | `Core/Syntax/SpecFormat.cs` | R-03 — makes the absence of byte extents explicit in the type |
| `RunSelection.Correspondence` | `Core/RunSelection.cs` | WR-104 — joins the checkpoint, strings only |
| `ApiDeltaRun.Coverage` | `Core/ApiDeltaRun.cs` | WR-602 — what the two new report sections read |

`ApiDeltaRun.Coverage` is `CoverageOutcome?` — the coverage report, the projection figures and the grouped
gaps. Null for an OpenAPI candidate, present for a WSDL one, and the report section says "sans objet"
rather than disappearing: a section that vanishes when it has nothing to say leaves a reader unable to
tell silence from absence.

## State transitions

One sequence matters, and it is the feature's spine.

```text
WSDL path or URL
  → retrieved and materialised        (WR-011; skipped when the input is a file)
  → read                              (WR-001 … WR-009) → WsdlContract
  → projected                         (WR-110)          → ProjectionResult
  → corresponded                      (WR-104)          → Correspondence, unconfirmed
  → confirmed by the developer        (WR-104)          → Correspondence, confirmed
  → compared                          003 FR-100s       → IReadOnlyList<Difference>
  → covered                           (WR-107, WR-108)  → CoverageReport
  → selected                           003 FR-501 … 504
  → announced and accepted             003 FR-505
  → built                              003 FR-506 … 513
  → proved                             003 FR-514
  → written                            003 FR-515 … 518
```

Two invariants hold across it. **Nothing compares before the correspondence is confirmed** — the arrow is
one-way, and an unconfirmed correspondence makes every finding beneath it unfalsifiable. **Nothing is
written before the proof** — and the proof measures against the selection, so what the selection did not
name must come through byte-identical.

The first six steps are new. The last five are 003's, unmodified, and that is the plan's whole bet.
