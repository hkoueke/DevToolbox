# Quickstart: validating a WSDL candidate

Runnable scenarios that prove the feature end to end. Shapes live in [data-model.md](data-model.md); the
projection table lives in [contracts/projection.md](contracts/projection.md). Nothing is duplicated here.

## Prerequisites

```bash
dotnet build DevToolbox.slnx        # warnings are errors
dotnet test DevToolbox.slnx
```

No server, no credentials, no network — except the scenarios that are explicitly about a URL, which use a
loopback listener rather than the internet.

## The corpus, and why it is written first

Under `tests/DevToolbox.Tests/Tools/ApiDelta/Corpus/Wsdl/`. **The hostile half precedes the reader** —
003's stack overflow on a 10,000-link reference chain was found long after its reader was written, and the
lesson is cheap to apply here.

| Document | What it is for |
|----------|----------------|
| `catalogue.wsdl` + `catalogue-types.xsd` + `commun.xsd` | the ordinary case: a graph of three documents |
| `catalogue-inline.wsdl` | the same contract, schemas inlined — must project identically (SC-001) |
| `catalogue-prefixes.wsdl` | the same contract with `s0:` for `tns:` — must project identically |
| `one-way.wsdl` | an operation with no output; must project without a response body (WR-115) |
| `rpc-encoded.wsdl` | reported as a binding whose message shape the schema does not determine |
| `substitution.wsdl` | an abstract head with concrete members — a `NotCarried` gap |
| `attributes.wsdl` | 400+ `xsd:attribute`, to prove grouping (WR-111, SC-004) |
| `enormous-enum.xsd` | tens of thousands of `enumeration` facets — a real code-list shape |
| **`hostile/doctype.wsdl`** | a `DOCTYPE` — refused before anything resolves (WR-002) |
| **`hostile/billion-laughs.wsdl`** | nested entities — closed by the same refusal |
| **`hostile/chain-400.wsdl`** | a 400-document import chain — must exhaust a **named** budget |
| **`hostile/cycle.wsdl`** | a legal cycle — read and recorded, never followed forever |
| **`hostile/traversal.wsdl`** | a `schemaLocation` of `../../../etc` — refused and named |
| **`hostile/same-name/`** | two documents whose last segment is `types.xsd` (WR-012) |
| `production.yaml` | the OpenAPI production the WSDL is compared against |
| `correspondance.yaml` | a declared mapping, for WR-105 |

## Scenario 0 — the bet, checked before anything else

**Why first.** The whole estimate rests on reusing 003's splicer with a synthetic candidate node. If that
is false, the plan is wrong — and it is better to know in an hour than in a week.

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~SyntheticGraftTests'
```

**Expected**: a hand-built `MappingNode` with no byte extent, grafted into production through the real
`SplicingEmitter`, produces a document that reparses, and every byte of production outside the graft is
unchanged.

## Scenario 1 — read a graph, or refuse to pretend

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~WsdlReaderTests'
```

**Expected**: `catalogue.wsdl` yields three documents, each with path, size and hash, the confinement root
stated, no diagnostic. Remove `commun.xsd` and the run fails naming the missing `schemaLocation`, the
document that imported it, and its line.

## Scenario 2 — the hostile set is refused by name

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~WsdlHostileTests'
```

**Expected**, for each document under `hostile/`: a refusal that **names the limit or the reason**, no
crash, no hang, no external fetch, no entity expansion, and no partial document left behind. A test that
passed by timing out would not be passing.

## Scenario 3 — the same contract, written differently, projects identically

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~ProjectionStabilityTests'
```

**Expected**: `catalogue.wsdl`, `catalogue-inline.wsdl` and `catalogue-prefixes.wsdl` produce identical
synthetic trees, identical gaps and identical figures. Under the same selection, the generated documents
are byte-identical (SC-001). Without this, the tool proposes changes nobody made.

## Scenario 4 — the projection table, row by row

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~ProjectionTableTests'
```

**Expected**: one assertion per row of [contracts/projection.md](contracts/projection.md) — carried,
approximate or not carried — plus one test establishing that the rule set covers the table **exactly**,
with no rule the table does not list and no row no rule implements. Same shape as 003's
`RuleCatalogueTests`, and for the same reason: a published table and its code cannot be allowed to drift.

## Scenario 5 — nothing compares before confirmation

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~CorrespondenceTests'
```

**Expected**: with an unconfirmed correspondence, no `Difference` is produced and no coverage is computed.
A declared mapping takes precedence over a proposal. A near-tie yields `Ambiguous` rather than a pick. A
deliberately wrong declared mapping is honoured — and every compatibility finding beneath it is reported
under a correspondence marked as its origin says.

## Scenario 6 — coverage is certain, compatibility is partial, and they stay apart

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~CoverageTests'
```

**Expected**: every WSDL operation carries exactly one of `Covered`, `Absent`, `Ambiguous`, matching the
corpus's documented expectation (SC-003). No coverage gap appears among the breaking changes. An absent
operation names what production would need to carry.

## Scenario 7 — four hundred gaps are three lines

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~GroupingTests'
```

**Expected**: `attributes.wsdl` produces one grouped entry per reason with a count, not one finding per
occurrence (SC-004). The number of entries is bounded by the number of distinct reasons.

## Scenario 8 — the proportion is a figure

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~ComparabilityReportTests'
```

**Expected**: the report states operations corresponded and fields projected as numbers (WR-603), and no
WSDL comparison recommends a version (WR-114). A reader can tell a clean report from a silent one.

## Scenario 9 — a URL, materialised

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~ContractFetcherTests'
```

Against a loopback listener, never the internet.

**Expected**: every retrieved document exists as a file afterwards, with path, size, hash and redacted
origin in both reports, and the run is reproducible from those files with no network at all (SC-010). A
relative `schemaLocation` in a retrieved document resolves against the URL it came from, not the output
directory (WR-013). Two documents whose last segment is the same get distinct names, with the mapping
recorded (WR-012).

## Scenario 10 — the hostile URL set

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~AddressGuardTests'
```

**Expected**, each refused, confirmed or disambiguated as its requirement says, with no partial document
and no token in any artifact or log (SC-011): plain `http`; a private address; a loopback address; a
redirect chain over the bound; a redirect that changes host mid-chain; a response over the size limit; a
response that is not XML — a sign-in page under a success status is the ordinary case, not the exotic one;
an endpoint demanding authentication; and a URL carrying a token in its query string.

## Scenario 11 — the whole chain, and the production left alone

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~WsdlEndToEndTests'
```

**Expected**: from `catalogue.wsdl` and `production.yaml`, select two absent operations as new routes and
one difference on a covered one, and the generated document is production unchanged plus those three
things. Every derived element names the WSDL document, location and projection confidence it came from,
and no copied element carries such a mark (SC-007). Every place the projection could not carry holds an
unfilled marker, and their count matches the report (SC-008). The production file is byte-identical
afterwards, under every flag (SC-009).

## Scenario 12 — interrupt it

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~WsdlResumeTests'
```

**Expected**: a run interrupted after the correspondence is confirmed resumes without asking again,
re-reads the materialised files, retrieves nothing, and reaches artifacts identical to an uninterrupted
run outside the run-metadata block (SC-014).

## Scenario 13 — scale

```bash
dotnet test DevToolbox.slnx --filter 'FullyQualifiedName~WsdlPerformanceTests'
```

**Expected**: a generated contract of 200 operations and 5,000 type declarations across 20 documents is
read, resolved, projected and compared in under 30 seconds (SC-002). Measure the phases separately — 003's
performance test found a stack overflow rather than a slowdown, and only the per-phase curve showed where.

## What a reviewer should check by hand

Three things no test asserts well.

1. **Open a generated document and read the marks.** Can you tell, element by element and without the
   report, what came from production, what was derived, and what was left unfilled? That is WR-300 and
   003 FR-512, and it is a judgement about legibility.
2. **Read a comparability section and decide whether you trust it.** If the proportion is 62 %, does the
   report make you feel informed or uneasy? Uneasy is correct.
3. **Correct a proposed correspondence.** Does the basis — the matched tokens — tell you enough to
   disagree quickly? If correcting is slower than typing a mapping file, the proposer has failed even
   where its tests pass.
