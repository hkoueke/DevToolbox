# Feature Specification: A WSDL as a candidate for apidelta

**Feature Branch**: `004-wsdl-delta-audit`

**Created**: 2026-10-21

**Status**: Draft

**Input**: User description: "Le développeur doit pouvoir aussi fournir un contrat d'interface écrit en
wsdl, pour la même finalité que ApiDelta promet." — narrowed over four clarifications the same day to: one
production YAML, one WSDL; compare the WSDL against production; detect what production lacks and what could
be added from the WSDL; ask which operations the developer wants added as routes; generate a YAML that is
production plus those additions.

## Context

[003](../003-openapi-delta-audit/spec.md) already does all of this for a candidate written in OpenAPI. It
compares the candidate against the production description, classifies every difference, states what it will
insert, and writes **production plus exactly what the developer selected** as one complete corrected
document — its FR-500 to FR-518.

This feature changes one thing: **the candidate may be a WSDL.** Everything downstream is unchanged and is
deliberately not restated — the selection, the statement before writing, the untouched-by-default
guarantee, the marking, the proof before publishing, the reports, the outcome codes, the resumability.
Production is always an OpenAPI YAML or JSON document. The output is always an OpenAPI document. A WSDL is
never written, never compared against another WSDL, and never treated as a contract the team owns.

The work is therefore in two places only, and both are about getting a WSDL *into* a comparison that
already exists.

**Reading it.** A WSDL contract is not a file but a graph of files: one `.wsdl` and typically three to
twenty `.xsd` documents bound by `wsdl:import`, `xsd:import`, `xsd:include` and `xsd:redefine`. In OpenAPI a
reference leaving the document was the exception that policy refused by default; here it is the ordinary
shape, so the default has to differ. The contract may also arrive as a URL rather than a file, because that
is how a WSDL actually circulates. And one contract can be written with different namespace prefixes and
split across different files while being the same contract — so two deliveries of one WSDL must give the
same result, or the tool would propose changes nobody made.

**Projecting it.** XSD and JSON Schema do not correspond. An element in a `sequence` maps to an object
property, `minOccurs="0"` to absence from `required`, a facet to its keyword — but an `xsd:attribute`, a
substitution group, a namespace, a binding style and the order of a `sequence` have no counterpart at all.
A single verdict over that would be mostly "undetermined", and a report that is mostly undetermined is read
once. So this feature answers two questions kept apart: **what production does not cover**, which is
certain, and **whether what it does cover still fits**, which is partial. It groups what it could not
compare by reason instead of enumerating it, and states as a figure how much of the WSDL it compared at
all. What the projection cannot carry becomes, in the generated document, an explicit unfilled marker that
the tool refuses to guess at.

## Clarifications

### Session 2026-10-21

The feature was specified and then narrowed four times in one day. The record is kept because the narrowing
*is* the design: each step removed something that had looked reasonable and was not.

- **Q: Which pairings must a comparison accept?** → First full mixed comparison in both directions; then a
  WSDL candidate against the contract in service; then, finally, **a WSDL candidate against a production
  OpenAPI description, and nothing else.** WSDL against WSDL does not exist here. The requirements that
  governed it were removed rather than kept without a subject: semantic comparison of two WSDL documents,
  versioning by target namespace, the cleanup catalogue and its nine transformations, the twenty-five
  examination rules, and extraction of a chosen operation. The XSD semantics they carried were not lost —
  knowing that an element added to a `sequence` is required, what a facet constrains and what `minOccurs`
  means is exactly what the projection needs, and it lives in WR-100 to WR-115 now.
- **Q: Does the tool follow a relative `schemaLocation` to a sibling file by default?** → **Yes, confined to
  a declared root, and announced.** This diverges deliberately from 003, where external references are
  refused by default. The divergence is recorded in WR-005 with its reason — the shape of the input is
  different, not the risk appetite — and WR-006 carries what pays for it: an open default must not be a
  silent one. Remote resolution stays closed.
- **Q: Can the WSDL be read from a URL the developer types?** → **Yes, by materialising it.** The tool
  retrieves the entry URL, writes every document it obtained to disk, then reads only those files (WR-010,
  WR-011). Materialising is what lets a new kind of input obey rules already in force: a materialised
  document has a path, a size and a hash, so provenance and resumability need no exception, and a resumed
  run re-reads the files rather than retrieving again (WR-018) — a generated `?wsdl` endpoint may answer
  with different bytes on every call, and re-retrieving would make every resume refuse for a change that is
  not one.
- **Q: Does the comparison output a document carrying production plus the changes?** → **Yes, and it became
  003's own output.** The question was first answered inside this feature and then propagated: 003 dropped
  its fragment entirely and now writes the complete corrected description. This feature therefore specifies
  no selection mechanics, no marking discipline and no verification of its own. It specifies only what a
  WSDL candidate adds: a derived element must say that it was derived and from where, and a gap the
  projection cannot carry must be an unfilled marker rather than a plausible schema — because a plausible
  schema is the one output from which a reader cannot recover the fact that nobody knows.

### Session 2026-10-27, while building User Story 3

Two clarifications the implementation forced, both found at the end of the chain — where the generated
document must be **valid**, and not merely re-readable.

- **Q: Where does a derived *scalar* field say that it was derived?** → **In the document's own
  declaration, which names the field.** WR-300 requires every derived element to carry its WSDL origin,
  and OpenAPI allows an `x-` member only on an object: a field such as `summary` or `required` cannot hold
  one. The origin of such a field therefore goes in the `x-apidelta-correction` declaration, under
  `champsDerives`, naming the field, its document, its place and the confidence. Leaving it out would have
  made a derived field indistinguishable from a copied one, which is the one thing WR-300 forbids — and
  003's own remark already claimed this was done, without doing it.
- **Q: What does a projected `xsd:choice` branch contain?** → **The particles of that branch, projected by
  the ordinary particle rules.** The table row (`xsd:choice` → `oneOf`, `Approximate`) is unchanged; what
  is now specified is the branch content. The first implementation wrote a `$comment` holding the type
  name, which OpenAPI 3.0 does not allow — the generated document was refused by its own revalidation —
  and which told a reader nothing about the type expected. It also left the choice's particles to be
  projected a second time at the type level, where they were declared **all required at once**: the exact
  opposite of a choice, asserted with the confidence of a carried rule. Both defects were invisible until
  a projected schema had to be spliced into a production description and revalidated.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read the WSDL and its imports, or refuse to pretend (Priority: P1)

A developer points the tool at a `.wsdl`, or at the URL of one. It follows the imports policy allows, builds
one faithful picture of the whole contract, states the perimeter it gave itself, and either says it has read
all of it or names precisely what it could not read and stops.

**Why this priority**: every other answer rests on this one, and on a WSDL this is the bulk of the work. A
contract whose types live in a file that was not read is a contract the tool knows nothing about, and a
confident report built on it is indistinguishable from a good one — which makes it the worse of the two.

**Independent Test**: read a multi-document contract from the corpus and confirm every document opened is
reported with its path and hash and the root it confined itself to; then move one imported schema and
confirm the run refuses, naming the missing document and the import that wanted it.

**Acceptance Scenarios**:

1. **Given** a WSDL whose types are in two sibling `.xsd` files, **When** it is read, **Then** three
   documents are reported, each with its path and hash, the confinement root is stated, and no diagnostic is
   raised.
2. **Given** the same WSDL with one schema deleted, **When** it is read, **Then** the run fails, names the
   missing `schemaLocation`, names the document that imported it, and gives its line.
3. **Given** a WSDL carrying a `DOCTYPE` declaration, **When** it is read, **Then** the run refuses and says
   document type definitions are not processed, without having resolved anything.
4. **Given** an import pointing outside the declared root, **When** it is read, **Then** the run refuses and
   names the path it would have left.
5. **Given** a WSDL 2.0 document, **When** it is read, **Then** the run refuses and names the versions
   supported.
6. **Given** the URL of a `?wsdl` endpoint, **When** it is given as the input, **Then** the tool retrieves
   it, writes every document obtained into the output directory, reads only those files, and reports each
   with its path, hash and redacted origin.
7. **Given** a URL whose host resolves to a private address, **When** it is given, **Then** nothing is
   retrieved without explicit confirmation naming the address it resolved to.
8. **Given** a URL answering with a sign-in page under a success status, **When** it is retrieved, **Then**
   the run refuses by naming what arrived, and no document is left behind.
9. **Given** a URL carrying a token in its query string, **When** anything is written, **Then** the token
   appears in no report, log, checkpoint or console line.
10. **Given** a run whose input was a URL, **When** it is interrupted and resumed, **Then** the resumed run
   re-reads the materialised files and retrieves nothing again.

---

### User Story 2 - Know what production does not cover, and what could be added (Priority: P1)

A developer has the production YAML and a WSDL. The tool reports two things kept apart: which operations of
the WSDL production answers nowhere, and, where it does answer one, whether what production declares still
fits what that operation's callers send. It says plainly how much of the WSDL it was able to compare.

**Why this priority**: it is the question the feature exists for. The first half is what a developer acts
on; the second is what stops them acting wrongly.

**Independent Test**: compare the corpus WSDL against the production description written to answer it,
confirm coverage is exact against the documented expectation, and confirm the compatibility findings group
their unknowns rather than listing one per field.

**Acceptance Scenarios**:

1. **Given** a WSDL and the production description, **When** a comparison is requested, **Then** the tool
   proposes which production route answers which WSDL operation, states for each how it arrived at it, and
   compares nothing until the developer has confirmed or corrected it.
2. **Given** a confirmed correspondence, **When** the comparison runs, **Then** the report carries coverage
   and compatibility as separate sections and never merges a coverage gap into the breaking changes.
3. **Given** a WSDL operation production answers nowhere, **When** the comparison runs, **Then** it is
   reported as a coverage gap naming the operation and what production would need to carry — not as a
   breaking change, because nothing is broken; something is missing.
4. **Given** a correspondence guessed from name similarity alone, **When** the report is written, **Then**
   it is marked inferred, and the report states that every compatibility finding beneath it is only as good
   as the correspondence above it.
5. **Given** a WSDL with four hundred `xsd:attribute` declarations, which OpenAPI has no counterpart for,
   **When** the comparison runs, **Then** one grouped entry states how many were not comparable and why,
   and four hundred findings are not produced.
6. **Given** a type the projection carries only approximately — an `xsd:choice` against `oneOf` — **When**
   it is reported, **Then** the finding says it is approximate and carries uncertain confidence.
7. **Given** any comparison, **When** the report is written, **Then** it states as a figure what proportion
   of the WSDL had a comparable counterpart, so a reader can tell a clean report from a silent one.
8. **Given** a correspondence the developer corrected by hand, **When** the run is interrupted and resumed,
   **Then** the corrected correspondence is what the resumed run uses, and the inputs are re-read.

---

### User Story 3 - Choose which operations become routes, and get production back with them in it (Priority: P1)

The developer picks the operations they want production to carry. The tool states what it will insert, they
accept, and it writes production plus exactly those routes — each derived element saying where in the WSDL
it came from, and each gap the projection could not carry left as a marker the tool refuses to fill.

**Why this priority**: it is the output the developer opens. The selecting, the statement before writing,
the untouched-byte guarantee and the proof are 003 User Story 4 and are not restated here; this story is
only about what a WSDL candidate makes different.

**Independent Test**: select two WSDL operations as new routes and confirm the generated YAML is production
unchanged plus those two, that every derived element names its WSDL origin, and that every non-projectable
place holds an unfilled marker rather than a plausible schema.

**Acceptance Scenarios**:

1. **Given** a completed comparison, **When** the developer is asked what to add, **Then** the selectable
   items are the WSDL's operations with their coverage state alongside the impact and confidence 003 already
   requires, and an item resting on an inferred correspondence says so where it is shown.
2. **Given** that list, **When** the developer selects several operations, **Then** all of them are carried,
   and selecting one never excludes another.
3. **Given** an accepted selection, **When** the document is written, **Then** it is production's own bytes
   everywhere the selection did not name, plus the selected operations expressed as routes.
4. **Given** a selected operation the projection can fully derive, **When** the document is written,
   **Then** every derived element names the WSDL document and location it came from and the confidence of
   the rule that produced it, so a derived element and one copied from production are never
   indistinguishable.
5. **Given** a selected operation whose response type uses `xsd:attribute`, **When** the document is
   written, **Then** that place holds an explicit unfilled marker naming what is missing and why, and not a
   plausible schema.
6. **Given** a document carrying unfilled markers, **When** it is examined, **Then** it says so at its own
   level, states the count, and remains parseable — recognisable as unfinished from the document alone,
   without the report.
7. **Given** a WSDL operation whose projected path would collide with one production already carries,
   **When** it is selected, **Then** the collision is reported and production's route is not silently
   overwritten.
8. **Given** any generated document, **When** it is written, **Then** it is never written over production,
   under any flag, mode or confirmation.

---

### User Story 4 - The same WSDL, written differently, gives the same result (Priority: P2)

A WSDL delivered with other namespace prefixes, other attribute order, or its schemas split across other
files produces the same coverage, the same findings and the same generated document.

**Why this priority**: without it the tool would propose changes nobody made. A second delivery of one
contract that produced a different draft would destroy trust in every draft.

**Independent Test**: compare production against two semantically identical deliveries of one WSDL that
differ in prefixes, attribute order, element form, comments, whitespace and document decomposition, and
confirm the coverage, the findings and the generated bytes are identical.

**Acceptance Scenarios**:

1. **Given** a delivery using `s0:` where another uses `tns:` for the same namespace URI, **When** both are
   compared, **Then** the coverage and the findings are identical.
2. **Given** a delivery with all schemas inlined that another imports from three files, **When** both are
   compared, **Then** the coverage and the findings are identical.
3. **Given** two such deliveries and the same selection, **When** a document is generated from each,
   **Then** they are byte-identical outside the run-metadata block.
4. **Given** a delivery where the order of elements inside an `xsd:sequence` differs, **When** it is
   compared, **Then** there **is** a difference, because that order is part of what the contract requires.

---

### User Story 5 - Make the reading and the projection the team's own (Priority: P3)

The import policy, the URL policy, the limits and the projection's configurable choices are read and
validated before any document is read, and the tool works with none of them set.

**Why this priority**: the shipped defaults are a working configuration, so this is what makes them the
team's rather than what makes the tool usable.

**Independent Test**: run the same comparison with and without a configuration that raises one limit and
enables remote resolution for one host; then supply a configuration with an unknown key and confirm the run
does not start.

**Acceptance Scenarios**:

1. **Given** a configuration with an unknown key, **When** the run starts, **Then** it does not start, and
   the file, the key, its line and its column are named.
2. **Given** no configuration at all, **When** a comparison runs, **Then** it produces usable results.
3. **Given** a configuration enabling remote resolution with an empty host list, **When** the run starts,
   **Then** it is refused rather than treated as enabling everything.

---

### Edge Cases

- A WSDL that imports a schema which imports the WSDL's own namespace back — a legal cycle that must be read
  and reported, not refused and not followed forever.
- Two imported schemas declaring the same `targetNamespace` with different content, which is legal and means
  their declarations merge; and the same with *conflicting* declarations, which is not.
- `xsd:redefine`, which changes the meaning of a document already read.
- An import chain hundreds of documents long, which must exhaust a named budget rather than a stack.
- An inline schema with no `targetNamespace` — chameleon inclusion, where the including document's namespace
  applies.
- Two documents in one retrieved graph whose last path segment is the same, which cannot both be materialised
  under that name.
- A relative `schemaLocation` in a document that was retrieved rather than read from disk, which must be
  resolved against the URL it came from and never against the directory it was materialised into.
- A URL answering with a portal sign-in page under a success status, redirecting to another host mid-chain,
  or returning a document whose declared encoding contradicts its bytes.
- A one-way, notification or solicit-response operation: WSDL 1.1 allows four message patterns, and an
  operation with no output has no response to project.
- `rpc`/`encoded` bindings, whose message shape is not derivable from the schema alone.
- A substitution group whose head is abstract, where the concrete members are the contract.
- An enormous enumeration — tens of thousands of `xsd:enumeration` facets — which is a real shape in
  code-list-driven contracts.
- Mixed content, `xsd:any` and `xsd:anyAttribute`, which make a type open and change what an addition to it
  means.
- A WSDL operation whose projected path collides with one production already carries.

## Requirements *(mandatory)*

Identifiers are part of the published output contract: they appear in reports and in configuration. `WR-0xx`
covers reading the WSDL, `WR-1xx` projecting and comparing it, `WR-2xx` the document graph, `WR-3xx` what a
WSDL candidate adds to the generated document, `WR-6xx` reporting, `WR-7xx` running.

**Reading the WSDL**

- **WR-001**: The tool MUST read WSDL 1.1 documents and the XSD schemas they reach, and MUST refuse any
  other declared WSDL version with a diagnostic naming the versions it supports.
- **WR-002**: The tool MUST NOT process document type definitions and MUST NOT resolve external entities. A
  document carrying either MUST be refused before anything is resolved, with a diagnostic saying so.
- **WR-003**: Every location the tool reports MUST identify both the document and the position within it,
  the position expressed as a path a reviewer can follow and, where the parser provides them, a line and a
  column.
- **WR-004**: The tool MUST report every document it opened, with its path, its size and its content hash. A
  contract is the set of documents read, and a report naming only the entry point would not say what was
  examined.
- **WR-005**: Local resolution — following a relative `schemaLocation` to a file on disk — MUST be enabled by
  default, confined to a declared root, with traversal outside that root refused and named. A WSDL whose
  types live in a sibling schema is the ordinary shape of the input, so a closed default would refuse nearly
  every real contract before anything could be configured. This diverges deliberately from
  [003](../003-openapi-delta-audit/spec.md), where a reference leaving the document is the exception.
- **WR-006**: Because that default is open, it MUST NOT be silent. Every run MUST state the root it confined
  resolution to and MUST list every document it opened, so the developer can see the perimeter the tool gave
  itself rather than infer it.
- **WR-007**: Remote resolution of an import MUST be disabled unless explicitly enabled with a non-empty
  host allow-list, and MUST be refused for any scheme other than `https` unless the developer explicitly
  opts into `http`.
- **WR-008**: A resolution policy forbade MUST be reported as unresolved, and MUST make the comparison
  declare itself incomplete. The tool MUST NOT substitute an assumption for a document it did not read, and
  MUST NOT project a type it could not resolve.
- **WR-009**: The tool MUST treat every input as hostile, MUST enforce configured maximum document size,
  nesting depth, import-chain depth and total document count, and MUST report the limit it refused on rather
  than failing by exhaustion.
- **WR-010**: The developer MUST be able to give the entry WSDL as a URL. This is the address the developer
  typed, and it is the only address the tool ever retrieves on its own initiative: it MUST NOT call any
  endpoint the contract describes, MUST NOT fetch a `soap:address` location, and MUST NOT execute anything
  carried in the documents.
- **WR-011**: A URL input MUST be **materialised** before it is read. The tool MUST write every document it
  retrieved into the output directory and MUST then read only those files. Everything else here applies to a
  materialised document with no exception — it has a path, a size and a content hash, it can be re-read, and
  the run is reproducible from it. A document held only in memory would make WR-004, WR-601 and WR-702
  unsatisfiable and leave the developer unable to say what they compared.
- **WR-012**: Where two retrieved documents would be materialised under the same name, the tool MUST give
  each a distinct name derived from its origin, MUST record which origin each name came from, and MUST NOT
  let one overwrite the other.
- **WR-013**: A relative `schemaLocation` in a retrieved document MUST be resolved against the URL that
  document came from, never against the directory it was materialised into. Materialising changes where a
  document sits; it MUST NOT change what its imports mean.
- **WR-014**: A URL MUST be validated against an allow-list of schemes before any request is made. `https`
  MUST be required, and `http` MUST be refused unless the developer explicitly opts into it. Any other
  scheme MUST be refused by name.
- **WR-015**: The tool MUST NOT retrieve a URL whose host resolves to a loopback, link-local, private or
  otherwise internal range without explicit confirmation naming the address it resolved to. Redirects MUST
  be bounded by a configured maximum, MUST be re-validated against WR-014 and this requirement at every hop,
  and the chain actually followed MUST be reported.
- **WR-016**: A retrieval MUST be bounded by a configured timeout and a configured maximum response size,
  MUST report the limit it refused on, and MUST leave no partially written document behind. A response that
  is not a well-formed XML document MUST be refused by naming what arrived rather than by failing to parse —
  a portal's sign-in page returned with a success status is the ordinary failure here, not an exotic one.
- **WR-017**: The tool MUST NOT acquire or send credentials. A URL requiring authentication MUST be refused
  with a diagnostic saying so and saying the document can be downloaded and passed as a file. A feature that
  holds no credential cannot leak one.
- **WR-018**: Every materialised document MUST record, alongside its path, size and hash, the URL it came
  from in redacted form and the instant it was retrieved, the instant living in the run-metadata block so it
  does not make two runs over the same files differ. A resumed run MUST re-read the materialised files and
  MUST NOT retrieve anything again.
- **WR-019**: A URL's userinfo, query string and any secret-shaped part of it MUST be redacted wherever the
  URL is reported — reports, logs, checkpoints, console, and a materialised document's provenance. The
  address a developer pasted is as likely to carry a token as a `soap:address` is.

**Projecting the WSDL and comparing it with production**

- **WR-100**: Namespace prefixes MUST be resolved to namespace URIs before anything is projected, and the
  prefix itself MUST NOT affect any result.
- **WR-101**: Two schema documents declaring the same `targetNamespace` MUST be merged. Conflicting
  declarations of the same name within one namespace MUST be reported as an error naming both documents.
- **WR-102**: The projection MUST NOT be affected by how the contract is written or decomposed. Namespace
  prefix choice, attribute order, self-closing versus paired element form, insignificant whitespace,
  comments, CDATA versus escaped text, the order of top-level WSDL declarations and the distribution of
  declarations across documents MUST all leave the result identical.
- **WR-103**: The order of particles inside an `xsd:sequence` MUST be carried by the projection. The order
  inside an `xsd:all`, and the order of members of a `choice`, MUST NOT be.
- **WR-104**: A comparison MUST rest on an explicit **correspondence** between the WSDL's operations and
  production's routes. The tool MUST propose one, MUST present it for confirmation or correction, and MUST
  compare nothing before it is confirmed. No convention bridges the two languages, so a correspondence the
  tool invented and never showed would make every finding beneath it unfalsifiable.
- **WR-105**: The tool MUST accept a correspondence declared by the developer as a file, and MUST be able to
  propose one from the two documents. Each entry MUST record which of the two it came from, and a proposed
  entry MUST record what it was based on.
- **WR-106**: Every report MUST carry the correspondence it used, entry by entry, with each entry's origin,
  and MUST state that each compatibility finding is only as reliable as the correspondence above it. A wrong
  correspondence does not produce a wrong finding here and there; it produces a confident report about the
  wrong pair.
- **WR-107**: The tool MUST report **coverage** and **compatibility** as two separate sections and MUST NOT
  merge either into the other. They answer different questions, carry different certainty, and are acted on
  by different people.
- **WR-108**: Each operation of the WSDL MUST carry exactly one coverage state: covered, absent, or
  ambiguous — ambiguous meaning more than one candidate counterpart and no confirmation. Coverage MUST be
  established with certainty, because it rests on the confirmed correspondence and on nothing else.
- **WR-109**: An operation production does not cover MUST be reported as a coverage gap, naming the
  operation and, in terms a developer can act on, what production would have to carry to answer it. It MUST
  NOT be reported as a breaking change: nothing is broken, something is missing, and the two belong on
  different lists.
- **WR-110**: Compatibility MUST be established over a projection of both sides onto what they have in
  common, and the tool MUST publish what that projection does and does not carry. At minimum:

  | WSDL / XSD | OpenAPI / JSON Schema | Carried |
  |------------|-----------------------|---------|
  | Element in a `sequence` | Object property | Yes |
  | `minOccurs="0"` | Absent from `required` | Yes |
  | `maxOccurs` greater than one | `type: array` with bounds | Yes |
  | Facets: `maxLength`, `pattern`, `enumeration`, numeric bounds | The same keywords | Yes |
  | `xsd:int`, `long`, `decimal`, `boolean`, `dateTime` | `type` with `format` | Yes, the format correspondence named as a convention rather than an equivalence |
  | `nillable` | `nullable`, or `null` in a type union | Yes, by version of the production description |
  | `xsd:choice` | `oneOf` | Approximately, and reported as approximate |
  | Derivation by extension | `allOf` | Approximately, and reported as approximate |
  | `xsd:any`, `processContents` | `additionalProperties` | Approximately |
  | SOAP fault | Status code and response body | A correspondence, never an equivalence |
  | SOAP header | A header parameter | A correspondence, never an equivalence |
  | `xsd:attribute` | — | No counterpart |
  | Substitution group, abstract head | — | No counterpart |
  | Mixed content | — | No counterpart |
  | Namespace of a declaration | — | No counterpart |
  | Binding `style`, `use`, transport | — | No counterpart |
  | Particle order inside a `sequence` | — | No counterpart: JSON object members are unordered |

- **WR-111**: Where the projection carries no counterpart, the tool MUST report one grouped entry per
  reason, carrying the count and the reason, and MUST NOT emit one finding per occurrence. Four hundred
  findings that all say the same thing tell a reader one thing once and cost them four hundred lines.
- **WR-112**: Where the projection carries a counterpart only approximately, the finding MUST say so and
  MUST carry uncertain confidence. An approximation presented as an equivalence is worse than a refusal.
- **WR-113**: The impact and confidence vocabularies of [003](../003-openapi-delta-audit/spec.md) FR-103
  MUST be used unchanged for compatibility findings. Coverage MUST carry its own states from WR-108 and MUST
  NOT borrow the impact vocabulary. One vocabulary per question; a new question gets a new one rather than a
  stretched one.
- **WR-114**: The tool MUST NOT recommend a version from a WSDL comparison. The version to publish is
  production's, and 003 FR-300 to FR-308 own that recommendation.
- **WR-115**: An operation with no output message MUST be projected without a response body rather than with
  an empty one, and an `rpc`/`encoded` binding MUST be reported as a binding whose message shape the schema
  does not determine rather than projected as if it did.

**The document graph**

- **WR-200**: The tool MUST resolve, for any declaration, the set of operations that reach it, and MUST
  close the transitive set of declarations a chosen set of operations depends on, across documents and
  namespaces, recording alongside it what policy left unresolved.
- **WR-201**: Legal cycles — a schema importing a namespace that imports it back, a type referencing itself
  — MUST be read, recorded and reported, never refused and never followed indefinitely.
- **WR-202**: `xsd:redefine` MUST be either supported with its effect applied, or refused with a diagnostic
  naming it. It MUST NOT be read and silently ignored.

**What a WSDL candidate adds to the generated document**

The selection, the statement before writing, the untouched-by-default guarantee, the marking discipline and
the verification are [003](../003-openapi-delta-audit/spec.md) FR-501 to FR-518 and apply here unchanged. A
divergence from them would be a defect. What follows is only what a WSDL candidate adds.

- **WR-300**: The selected content MUST be **derived** through the projection of WR-110, not copied — which
  is the one place a WSDL candidate differs from an OpenAPI one, where 003 FR-507 has the content come from
  the candidate's own bytes. Every derived element MUST carry, in addition to what 003 FR-512 requires of it,
  the WSDL document and location it came from and the confidence of the projection rule that produced it. A
  derived element and a copied one MUST NOT be indistinguishable.
- **WR-301**: Where the projection carries no counterpart, the document MUST carry an explicit unfilled
  marker naming what is missing and why, and the tool MUST NOT fill it with a guess. An operation whose
  request or response could not be fully derived MUST carry such a marker rather than a plausible schema: a
  plausible schema is the one output from which a reader cannot recover the fact that nobody knows.
- **WR-302**: A document carrying any unfilled marker MUST say so at its own level and MUST state the count,
  extending the declaration 003 FR-515 already requires. It MUST remain parseable and valid so ordinary
  tooling can open it, and it MUST NOT be valid by accident: an unfinished document MUST be recognisable as
  unfinished from itself alone.
- **WR-303**: A selectable item MUST be presented with its coverage state from WR-108 alongside the impact
  and confidence 003 FR-502 already requires, and an item resting on an inferred rather than a declared
  correspondence MUST say so where it is shown. A developer selecting under an inferred correspondence is
  choosing on a weaker footing and MUST be told at the moment of choosing, not afterwards.
- **WR-304**: Where a selected operation's projected path would collide with a path production already
  carries, the collision MUST be reported and production's route MUST NOT be silently overwritten.
- **WR-305**: The projected path, method and operation identifier derived from a WSDL operation MUST be
  deterministic and MUST be stated in the report, so the same WSDL always proposes the same route and a
  developer can see the derivation rather than infer it.

**Reporting**

- **WR-600**: The artifacts, their names, their sections and their field layout MUST follow
  [003](../003-openapi-delta-audit/spec.md) FR-600 to FR-608, extended so every location names its document.
  A team reading a report about an OpenAPI candidate and one about a WSDL candidate MUST be reading the same
  shape of document.
- **WR-601**: Both reports MUST identify every input document by path, size and content hash, and a
  materialised document additionally by its redacted origin.
- **WR-602**: The report MUST carry a correspondence section, a coverage section and a comparability section
  stating the proportion of WR-603 and the grouped non-comparable entries of WR-111. These MUST appear in a
  fixed order and MUST be present even when empty: a section that disappears when it has nothing to say
  leaves a reader unable to tell silence from absence.
- **WR-603**: The report MUST state, as a figure and not as prose, what proportion of the WSDL had a
  comparable counterpart — operations corresponded, and fields projected. A reader MUST be able to tell a
  clean report from a silent one.
- **WR-604**: Every report MUST state its limitations, including every import left unresolved by policy and
  every projection that could not be established.
- **WR-605**: No credential, token or secret MUST appear in any artifact or log. Redaction MUST happen where
  the artifact is written, not at each call site. A `soap:address` location MUST be reported by location and
  never reproduced as a value.
- **WR-606**: For identical inputs and identical configuration, every artifact MUST be byte-identical across
  runs apart from one clearly delimited run-metadata block.

**Running**

- **WR-700**: The tool MUST run with no network at all when its inputs are files, and MUST never require a
  server address or any credential. The one retrieval it performs is the entry URL of WR-010, when the
  developer gave one.
- **WR-701**: An invalid request — a missing document, a production description that is not OpenAPI, a WSDL
  that could not be read — MUST be reported as such before any work begins.
- **WR-702**: Every run MUST be resumable, MUST re-read its inputs on resume, and MUST refuse to resume when
  any input's content hash no longer matches. A checkpoint MUST hold only the selection and the
  correspondence, and MUST NOT hold contract content.
- **WR-703**: Every run MUST resolve an outcome code from the same fixed vocabulary as
  [003](../003-openapi-delta-audit/spec.md) FR-708, MUST display it, MUST write it to the machine-readable
  report, and MUST NOT present it as the process exit code.
- **WR-704**: Configuration MUST be read and validated before any document is read, MUST name an unknown key
  with its file, line and column, and MUST NOT fall back silently to the shipped values.

### Key Entities

- **WSDL contract**: the set of documents reached from one entry WSDL or URL under the policy in force,
  together with what each declares. It is the unit the tool reads and reports on — not the entry file.
- **Document**: one WSDL or XSD file, with its path, size, content hash, target namespace, the declarations
  it carries and, when it was retrieved, its redacted origin and the instant of retrieval.
- **WSDL operation**: the unit a correspondence pairs and a selection selects. Carries its message pattern —
  request-response, one-way, solicit-response, notification — its messages, its parts and its faults.
- **Correspondence**: the explicit pairing between the WSDL's operations and production's routes, entry by
  entry, each recording whether it was declared by the developer or proposed by the tool and on what basis.
  It is an input to the comparison, not a conclusion of one; it is part of the run's selection and survives a
  restart.
- **Coverage state**: what the comparison establishes for one WSDL operation — covered, absent or ambiguous
  — carrying its own vocabulary rather than borrowing the impact vocabulary, because it answers a different
  question: not "what did this break" but "what is not answered yet".
- **Projection**: what the two contract languages have in common, onto which both sides are mapped before
  compatibility is assessed, together with the record of what it could not carry and why. Its published
  table is what makes a comparison falsifiable rather than merely plausible.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Two semantically identical deliveries of one WSDL — differing in prefixes, attribute order,
  element form, comments, whitespace and document decomposition — yield identical coverage, identical
  findings, and, under the same selection, byte-identical generated documents.
- **SC-002**: A WSDL contract of 200 operations and 5,000 type declarations across 20 documents is read,
  resolved, projected and compared in under 30 seconds on a developer machine.
- **SC-003**: Coverage is exact against the documented expectation for every operation of the example pair —
  every covered, absent and ambiguous one as recorded — and no coverage gap appears in the list of breaking
  changes.
- **SC-004**: Where the projection carries no counterpart, the number of findings is bounded by the number of
  distinct reasons and not by the number of occurrences, verified on a contract with at least four hundred
  non-projectable declarations.
- **SC-005**: Every report states, as a figure, the proportion of the WSDL that had a comparable counterpart,
  and no WSDL comparison recommends a version.
- **SC-006**: A WSDL contract the tool cannot read completely produces no comparison and no generated
  document — only diagnostics naming each document it could not read and the import that wanted it.
- **SC-007**: In every generated document, every operation production already carried is byte-identical to
  what production said, every derived element names the WSDL document, location and projection confidence it
  came from, and no copied element carries such a mark — verified element by element, not by sampling.
- **SC-008**: No generated document contains a synthesised request or response schema where the projection
  had no counterpart; each such place carries an unfilled marker instead, and the count of those markers
  matches the count the report gives.
- **SC-009**: No run writes over the production description, under any flag, confirmation or mode — there is
  no input to the tool that produces that outcome.
- **SC-010**: Every document a run retrieved from a URL exists as a file on disk afterwards, with its path,
  size, hash and redacted origin in both reports, and the run is reproducible from those files with no
  network at all.
- **SC-011**: Across the hostile-URL set — a plain-`http` address, a private and a loopback address, a
  redirect chain exceeding the bound, a response over the size limit, a response that is not XML, an endpoint
  demanding authentication, a URL carrying a token, and two retrieved documents whose last path segment is
  the same — every case is refused, confirmed or disambiguated as its requirement says, no partial document
  is left behind, and no token appears in any artifact or log.
- **SC-012**: A run given only files makes no outbound call of any kind, verified by observation and not by
  inspection of the configuration.
- **SC-013**: Every configured limit, when reached, is reported with the limit named. No input in the
  hostile-input set produces a crash, a hang, an external fetch, or an entity expansion.
- **SC-014**: An interrupted run is resumable, and the resumed run reaches artifacts identical to an
  uninterrupted one outside the run-metadata block, having retrieved nothing again.
- **SC-015**: Zero credentials, tokens or secret-shaped values appear in any artifact or log across the
  example set, including contracts whose `soap:address` carries one.
- **SC-016**: A developer who knows the OpenAPI reports can read a WSDL comparison's reports without being
  told anything new about their structure: same sections, same vocabulary of impacts and confidences, same
  meaning, plus the three sections WR-602 adds.

## Assumptions

- **Production is always an OpenAPI description**, read under the rules of
  [003](../003-openapi-delta-audit/spec.md), including its reference policy — which is the closed one. WR-005
  opens local resolution for WSDL graphs because their shape demands it; it does not reach across and open it
  for the production side.
- **WSDL 1.1 only.** WSDL 2.0 is refused with a named diagnostic. Almost nothing in the field ships 2.0, and
  supporting it would double the metamodel for a case the team does not have.
- **XSD 1.0**, which is what WSDL 1.1 tooling emits and consumes. XSD 1.1 constructs (`assert`,
  `alternative`, `openContent`) are read and reported as not understood rather than ignored, and a type whose
  meaning depends on one is reported as not projectable rather than projected approximately.
- **SOAP 1.1 and SOAP 1.2 bindings are projected.** WSDL 1.1's HTTP `GET`/`POST` bindings and any other
  binding extension are read and reported as present, without claims about their message shape.
- **Document type definitions and external entities are never processed**, under any configuration. This is
  not a policy knob: it is the mitigation for the whole class of XML external-entity and entity-expansion
  attacks, and a knob able to disable it would be the vulnerability.
- **No authentication, for the entry URL or anything else.** A URL behind a sign-in is refused by WR-017 with
  a diagnostic telling the developer to download the document and pass it as a file. A feature holding no
  credential cannot leak one, and the cost is one manual download in the cases that need it.
- **WS-Policy is read structurally, not interpreted.** The tool reports that a policy assertion is present
  and that the projection does not carry it; it does not evaluate what a WS-SecurityPolicy assertion means at
  runtime. Claiming otherwise would be claiming to be a SOAP stack.
- **The reports, the vocabulary, the configuration shape, the artifact contract and the whole of the
  selection-and-generation half are inherited from 003 rather than reinvented.** WR-104 constrains the
  arrangement: one run now reads a WSDL graph and an OpenAPI description together, so whatever the
  architecture, something must hold both readings at once. That is a decision for the plan.
- **Nothing published is ever serialized from a semantic model.** Production's half of a generated document
  is written from production's own syntax tree. This is the project's standing rule rather than a choice this
  feature makes, and it is what keeps an unknown vendor extension in production intact.

## Out of Scope

- Comparing a WSDL against another WSDL. There is one WSDL and one production OpenAPI description. Breaking
  changes between two versions of a WSDL, versioning by target namespace, examining a WSDL against a team's
  own conventions, costing or applying a cleanup of a WSDL, and extracting an operation into a standalone
  WSDL were all specified and then removed when this became clear. They describe a WSDL the team owns; this
  feature is about one it was handed.
- Comparing an OpenAPI candidate against a WSDL. The direction is WSDL candidate towards production.
- Writing a WSDL. The tool reads WSDL and writes OpenAPI, never the reverse.
- Producing a publishable contract. The generated document is production plus chosen additions, with every
  derived element marked and every gap the projection could not carry left unfilled; completing and reviewing
  it is the developer's work, and the tool never claims otherwise.
- Calling the described service, generating a client, or validating a message instance against either
  contract.
- WS-* runtime behaviour: reliable messaging, transactions, addressing resolution, security token
  acquisition.
