# Research: A WSDL as a candidate for apidelta

Decisions, their reasons, and what was rejected. Written before the code, so that the reasons survive the
afternoon that produced them.

## R-01 — No new tool: a WSDL is an input format

**Decision.** The work lands in the three `tools/ApiDelta/*` projects. `RunMode` gains nothing; the
candidate's kind is detected from its content.

**Rationale.** The spec fixes production as OpenAPI and the output as OpenAPI, and binds the entire
selection-and-generation half to 003 FR-501 to FR-518. A second tool would therefore have to either
duplicate that half or depend on the first tool's internals. The user's own framing settled it: *"tout
comme le json"* — the WSDL is another thing you can hand over, not another thing the box does.

**Alternatives considered.** A `tools/WsdlDelta/` triplet implementing `ITool`: rejected, because its
first act would be to reach into apidelta for the comparer, the splicer and the verifier, and the
layering tests would be right to refuse it. A shared `ApiDelta.Contracts` library extracted from 003:
rejected as premature — one consumer does not justify a seam, and the seam that matters (`ISpecReader`,
`IDocumentComparer`) already exists.

## R-02 — Project the WSDL into a synthetic OpenAPI document

**Decision.** `Projector` returns a `SpecDocument` whose tree is built with the existing `SpecNode` types,
plus a list of `ProjectionGap`. Everything downstream — comparison, catalogue, selection, splice, proof —
runs against that document unmodified.

**Rationale.** It is the only design where the reused half is reused *literally* rather than adapted. The
comparer already classifies differences between two `SpecDocument`s with impact and confidence; the
catalogue already lists routes; the splicer already grafts a candidate subtree into production's bytes.
Projecting into the shape those parts already speak means the new code is the projection and nothing else.

**What it costs, and why it is acceptable.** A synthetic tree has no byte extents, so it cannot be emitted
verbatim and its nodes cannot be measured for size. Neither is needed: nothing writes WSDL, and the only
measurements the feature reports are counts and proportions. `SpecFormat.Projected` makes the absence
explicit in the type rather than implicit in a convention — see R-03.

**Alternatives considered.** A parallel comparer over a neutral model shared by both languages: rejected,
because it would mean a second classification engine, a second impact vocabulary and a second set of
mutation tests, for a result the existing one already produces. Comparing a WSDL against OpenAPI directly,
rule by rule: rejected for the same reason, with the added cost that every rule would have to know both
languages instead of one table doing it once.

**The risk this creates is R-01 in the plan**, and it is retired before anything else is built: a
hand-built node grafted through the real splicer, as the first task.

## R-03 — `SpecFormat.Projected`, and no byte spans on the WSDL side

**Decision.** Add a third `SpecFormat` member. A projected document's nodes carry `ByteSpan`-free
positions; the WSDL documents themselves keep line and column for diagnostics and never get extents.

**Rationale.** 003's most expensive reading machinery is byte-extent fidelity, and the two defects that
cost the most in its last phase were both extent arithmetic — characters counted as bytes, and a block
collection's end mark overshooting its content. That machinery exists to let the tool *write* a document
by splicing its own bytes. This feature never writes a WSDL, so paying for extents would be paying for a
capability with no consumer, and inheriting a class of defect with no benefit.

**Alternatives considered.** Reusing `ByteSpan` with zero lengths: rejected, because a zero span is
indistinguishable from a real one at the point of use, and someone would eventually splice against it.
Naming the format `Wsdl`: rejected, because the document is not WSDL — it is the OpenAPI shape derived
from one, and calling it WSDL would invite someone to try writing it back out.

## R-04 — `XmlReader` for WSDL, `XmlSchemaSet` for XSD

**Decision.** Read the WSDL with `XmlReader` over a `MemoryStream`, with
`DtdProcessing.Prohibit` and `XmlResolver = null`. Compile the schema half with `XmlSchemaSet`, also with
`XmlResolver = null`, fed only from documents already materialised under the policy.

**Rationale.** WSDL 1.1's own structure is small and well bounded — `definitions`, `types`, `message`,
`portType`, `binding`, `service` — and hand-reading it costs less than adapting a general model, while
giving exact line and column for every diagnostic through `IXmlLineInfo`. XSD is the opposite: particles,
derivation, substitution groups and facets are a genuine type system, and `XmlSchemaSet`'s
post-compilation model is precisely what the projection needs to read. Both are in the base class library,
so `Directory.Packages.props` gains nothing.

**Measured rather than assumed, and the assumption was backwards.** This decision first recorded that
`XmlSchemaSet` resolves imports on its own and had to be disarmed. It does not: `XmlResolver` defaults to
**null** since .NET Core, so without a resolver it follows no `schemaLocation` at all — the corpus test
refused to compile the catalogue until one was supplied. The posture is closed by default. The work is
therefore to provide a *controlled* resolver that answers only from the already-materialised set, and the
test that proves it is unchanged: an import of an unreachable host must be refused **by name**, not by
timeout.

**Alternatives considered.** `System.ServiceModel.Description.ServiceDescription`: gone from .NET, and it
resolved and fetched on its own terms. `XDocument` with `SetLineInfo`: adequate for WSDL but it loads the
whole graph eagerly with no size or depth budget, which is the opposite of WR-009. A third-party WSDL
parser: rejected on principle — a new package here means a new audit surface for a format whose structure
is a page long.

## R-05 — DTD and external entities are off under every configuration

**Decision.** `DtdProcessing.Prohibit`, `XmlResolver = null`, `MaxCharactersFromEntities = 0`. No setting
re-enables any of it.

**Rationale.** This closes XXE and entity-expansion together, and it is the one place where a
configuration knob would *be* the vulnerability rather than mitigate it. The constitution's "input is
hostile" is a rule about validation; this is stronger, and deliberately so.

**Alternatives considered.** Allowing DTD for documents under the declared root: rejected. A local DTD can
still declare external entities, and "the file is nearby" is not a trust statement.

## R-06 — Materialise a retrieved graph, then read only files

**Decision.** `ContractFetcher` retrieves the entry URL and every import the policy allows, `Materialiser`
writes each one into the run's output directory, and the reader then opens only those files.

**Rationale.** It is what lets a new kind of input obey rules already in force instead of needing
exceptions carved for it. A materialised document has a path, a size and a content hash, so provenance
(WR-004), reporting (WR-601) and resumability (003 FR-702) need no special case. And a resumed run
re-reads the files rather than retrieving again — which matters concretely: a generated `?wsdl` endpoint
commonly answers with different bytes on every call (timestamps, regenerated prefixes), and re-retrieving
would make every resume refuse for a change that is not one.

**Two defects materialising introduces, each with its own requirement.** Two documents whose last path
segment is the same cannot both be written under that name (WR-012) — distinct names derived from the
origin, with the mapping recorded. And a relative `schemaLocation` in a retrieved document must resolve
against the URL it came from, never against the directory it landed in (WR-013); materialising changes
where a document sits, and must not change what its imports mean.

**Alternatives considered.** Reading from memory: rejected — it makes WR-004, WR-601 and resumability
unsatisfiable, and leaves the developer unable to say what they compared. Caching under `%LOCALAPPDATA%`:
rejected, because the artifacts of a run belong with the run, and `AppPaths` exists for state, not inputs.

## R-07 — SSRF guard: validate the resolved address, bound the redirects

**Decision.** `AddressGuard` validates scheme before any request, resolves the host and refuses loopback,
link-local, private and otherwise internal ranges without explicit confirmation naming the resolved
address, and re-validates at every redirect hop against a configured maximum.

**Rationale.** The constitution requires exactly this, and a URL typed by a developer is the one input in
this feature that can point anywhere. Validating the *resolved* address rather than the hostname is the
part that matters: a name that looks external can resolve inward.

**A limit worth stating rather than pretending.** Validating the resolved address does not close DNS
rebinding — the name can resolve differently between the check and the connection. Closing that properly
means connecting to the validated address while keeping the original host header, which the typed-client
pipeline does not express cleanly. The guard reduces the surface and the confirmation names what it
resolved to; the residual is recorded here rather than left for someone to discover.

**Alternatives considered.** Hostname allow-list only: rejected, it checks the wrong thing. No guard
because "the developer typed it": rejected — the developer typed a partner's URL, not a decision to probe
their own network.

## R-08 — The projection table is the contract, and it is code

**Decision.** `ProjectionTable` holds one `IProjectionRule` per row of WR-110. A rule declares what it
carries and with what confidence: carried, approximate, or not carried.

**Rationale.** Publishing the table is what makes "not comparable" a falsifiable claim instead of an
excuse, and the only way the published table and the code cannot drift is for the table to *be* the code,
with a test that the rule set covers it exactly. 003 already has this shape for its rule catalogue, and
that test is one of the two that caught `specs/` missing from the repository — it earns its keep.

**The rule that decides the feature's honesty.** `approximate` is a first-class outcome, not a shade of
carried. An `xsd:choice` against `oneOf`, or derivation by extension against `allOf`, are *correspondences
that nearly hold*; reporting them as equivalences would be worse than refusing them, because a reader
cannot recover the doubt from a confident finding.

**Alternatives considered.** A boolean carried/not-carried: rejected, it would force every approximation
into one of two wrong answers. Deriving the table from the spec at test time by parsing the markdown:
tempting, and rejected — 003 does exactly that for its rule catalogue and it is right there, because the
spec row carries severities the code must match. Here the row carries prose, and asserting on prose would
make the test fragile against rewording rather than against behaviour.

## R-09 — Correspondence: propose by token overlap, never compare before confirmation

**Decision.** `SimilarityProposer` splits both sides into tokens — camelCase and PascalCase boundaries,
path segments, namespace tails — and ranks candidates by overlap, keeping the best and any near-tie as
ambiguous. The developer confirms or corrects; nothing compares before.

**Rationale.** No convention bridges the two languages: nothing says `POST /documents` answers
`deposerDocument`. Token overlap is explainable — the screen can say *which* tokens matched, which is what
lets a developer correct it quickly — and it degrades gracefully: a bad proposal costs one correction on
one screen and cannot produce a wrong result, because confirmation gates the comparison.

**Why not edit distance.** Levenshtein and Jaro-Winkler score `deposerDocument` against `deposerDossier`
highly, which is precisely the confusion that matters here — two operations of the same family differing
by their subject. Token overlap separates them, because the differing token is the whole signal.

**Why ambiguity is a state and not a guess.** WR-108 gives coverage three states, and `ambiguous` exists
so the proposer can decline rather than pick between two near-ties. A proposer that always picked would
hide its uncertainty in the one place the report promises certainty.

**Alternatives considered.** A declared mapping file only, with no proposal: rejected — it makes the first
run on a 200-operation contract an afternoon of typing, which is the work the tool exists to remove. The
file remains supported (WR-105) and takes precedence, because a team that has written one has decided.

## R-10 — Route derivation must be deterministic and stated

**Decision.** `RouteDerivation` maps a WSDL operation to a path and a method by a fixed, documented rule,
and the report states the derivation for every operation it proposes.

**Rationale.** WR-305. The same WSDL must always propose the same route, or two runs disagree and the
generated document stops being reproducible (SC-001). Stating the derivation rather than leaving it
implicit is what lets a developer disagree with it usefully — they can see that `deposerDocument` became
`POST /documents` and say so.

**The shipped rule.** A verb-leading operation name is split into verb and subject; the verb maps to a
method through a small configurable table (`deposer` and `creer` to POST, `lire` and `obtenir` to GET,
`modifier` to PUT, `supprimer` to DELETE); the subject becomes a plural path segment. An operation whose
name yields no verb becomes POST on the operation name itself, which is honest for an RPC-shaped service
and reported as a fallback rather than as a derivation.

**Alternatives considered.** Always POST on the operation name: defensible, mechanical, and rejected
because it produces a REST-shaped document nobody would keep, which makes the whole draft worthless.
Asking the developer per operation: rejected for a 200-operation contract — the correspondence screen
already asks the one question that cannot be derived, and a second would turn the tool into a form.

## R-11 — Coverage comes from the correspondence alone

**Decision.** `CoverageReport` is computed from the confirmed correspondence and nothing else. The
comparer's differences feed compatibility; the projection's gaps feed comparability. Three inputs, three
sections, no sharing.

**Rationale.** It is what lets WR-108 promise certainty for coverage while compatibility stays partial. If
coverage were derived from the comparison, it would inherit the comparison's uncertainty and the report
would have no certain half left — which is the failure the whole two-section design exists to avoid.

**Alternatives considered.** One merged verdict per operation: rejected in the spec itself, for the reason
recorded there — a mostly-undetermined verdict is read once. Deriving coverage from whether the comparer
produced differences: rejected, because an operation with no counterpart produces no differences at all,
and silence would read as agreement.
