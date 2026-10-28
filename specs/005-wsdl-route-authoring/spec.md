# Feature Specification: Authoring a route instead of deriving one

**Feature Branch**: `005-wsdl-route-authoring`

**Created**: 2026-10-28

**Status**: Draft

**Input**: User description: "Pour wsdl vers yaml, ne serait-il pas mieux de demander directement quelle
route l'utilisateur souhaite intégrer, quel verbe utiliser ? […] j'ajouterai que l'app pourrait aussi
demander si les paramètres détectés dans le wsdl vont dans la route, le body ou autre."

## Context

Feature 004 lets a developer bring a service contract (WSDL/WCF) as the companion of a production OpenAPI
description. It derives a REST route for each operation from the operation's name — verb plus pluralised
subject — says what it based itself on, and asks the developer to confirm or correct the pairing.

Run against a real pair, that derivation is not useful. A WCF contract of 21 operations and a production
description of 20 routes produced 19 projected routes, **none** of which landed on its real counterpart:

| What the contract declares | What the derivation produced | What production actually carries |
|---|---|---|
| `AjouterDocuments` | `POST /documents` | `POST /Dossier/AjouterDocuments` |
| `ObtenirTypesDocuments` | `GET /typesdocuments` | `GET /Dossiers/` |
| `PasserEnRecu` | `POST /enrecus` | `POST /Document/PasserEnRecu` |

The derivation cannot know this house's path conventions — a `/Dossier` or `/Document` prefix, and the
operation name kept inside the path. It is an RPC-shaped API, and no naming rule recovers it. The tool is
already honest about this: such routes are labelled *fallback* or *partial*, never *derived*. But honesty
about a guess does not make the guess worth the developer's time: every one of the 19 had to be corrected
by hand, through a pairing screen whose purpose is to confirm, not to author.

The same investigation established what a WSDL **can** and **cannot** say.

**References can be derived, clearly.** XSD types are named and resolvable, and become `$ref` entries
pointing at the projected schemas. That part works and is not in question here.

**Parameter placement cannot be derived at all**, for three independent reasons:

1. **A SOAP operation has exactly one input: a body.** Nothing in a WSDL says a field travels in the path,
   the query, or a header. `GetDocument(identifiant, date, groupes, categorieDossier)` can faithfully
   become `GET /documents/{identifiant}?date=…&groupes=…` or `POST /documents/recherche` carrying a body.
   The information that separates the two is not in the file.
2. **Transport and governance headers are absent from the contract.** The production description carries
   five header parameters on all twenty of its routes — `X-idCorrelation`, `X-serviceEmetteur`,
   `X-serviceInitial`, `X-utilisateur`, `X-MARCHE`. Searched in the WSDL: zero occurrences, for all five.
   They are conventions of the API gateway, not of the service contract.
3. **Requiredness is not stated.** Every element is `minOccurs="0" nillable="true"`, the usual signature of
   a WCF `DataContract`. Even with the right path, `required: true` on a path parameter follows from
   nothing in the contract.

So the shape of this feature is: **stop inventing what cannot be known, ask for it once, and keep deriving
what can be known.** The asymmetry worth exploiting is that what the service contract cannot say, the
production description often already does — those five headers are declared there as reusable components.

### What already exists and must not be reinvented

- The projection of a contract into a synthetic OpenAPI-shaped document, its `$ref` graph, and the record
  of what it could not carry.
- The pairing step, which proposes one pair per operation, says what it based itself on, and remembers the
  confirmed pairing in the checkpoint.
- A **declared correspondence file** (WR-105): its format, a corpus example, a proposer that gives it
  precedence over any proposal, a context key documented as *"the pairing the developer declared in a
  file"*, and a report marker reading `[correspondance déclarée]`.

That last item is a live defect, not a foundation: **no reader fills that context key.** No production code
mentions the file's root key, and the corpus example is touched only by a test asserting the file is
reachable, never that it is read. The capability is announced end to end except the link that feeds it.
This feature must decide what becomes of it.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Say which route an operation becomes (Priority: P1)

A developer integrating a service contract keeps a handful of its operations. For each one kept, the tool
shows the operation, shows what it would have guessed, and asks for the path and the HTTP verb. The guess
is pre-filled, so a contract that does follow REST conventions costs one keystroke per operation, and a
contract that does not costs one typed line.

**Why this priority**: It is the whole of the complaint, and it is independently valuable. With only this
story, the 19 operations of the real pair land on the paths the developer intends instead of on 19 invented
ones. Everything else in this feature refines what happens *after* the route is settled.

**Independent Test**: Integrate a contract against a production description, keep three operations, give
each a path and a verb that no naming rule would produce, and verify the written document carries exactly
those three routes under exactly those paths and verbs.

**Acceptance Scenarios**:

1. **Given** an operation named `AjouterDocuments` and a production carrying `/Dossier/…` paths, **When**
   the developer is asked for the route, **Then** the suggestion `POST /documents` is pre-filled and
   editable, and accepting a typed `POST /Dossier/AjouterDocuments` results in that route being written.
2. **Given** an operation whose name yields no known verb, **When** the route is asked for, **Then** the
   suggestion is presented as a fallback and labelled as such, and no verb is pre-selected as though it had
   been derived.
3. **Given** a developer who keeps none of the operations, **When** the run reaches the authoring step,
   **Then** nothing is asked and the run completes having written nothing.
4. **Given** a path typed by the developer that production already carries with the same verb, **When** it
   is submitted, **Then** the collision is named and the developer is asked for another path or to drop the
   operation — production is never overwritten.
5. **Given** a production whose paths are grouped by hand, **When** the route is settled, **Then** the
   developer is asked beside which existing path it goes, the choices appearing in the document's own
   order, and the written document carries it there with every existing path in its original place.
6. **Given** an authored route, **When** it is written, **Then** its description is a conspicuous
   placeholder the developer replaces by hand, and the tool has composed no sentence of its own.

---

### User Story 2 - Say where each field goes (Priority: P1)

The path and verb settled, the developer is shown the fields the contract declares for that operation and
places each one: in the path, in the query, in the body, in a header, or nowhere. A default placement is
proposed from the verb, and every field is accounted for — a field placed nowhere is recorded as dropped,
not silently lost.

**Why this priority**: Without it, story 1 produces routes whose input is still a single SOAP body, which
is the shape the developer is migrating away from. The two stories together are the feature; either alone
leaves the output needing manual editing.

**Independent Test**: Take an operation with four flat fields, give it a `GET` verb and a path containing
one placeholder, place one field in the path and two in the query, drop the fourth, and verify the written
route declares exactly one path parameter, two query parameters, no body, and that the dropped field is
named in the report.

**Acceptance Scenarios**:

1. **Given** a verb of `GET` or `DELETE` and an operation whose fields are all scalar, **When** placement
   is proposed, **Then** the default places every field in the query and no body is produced.
2. **Given** a verb of `POST`, `PUT` or `PATCH`, **When** placement is proposed, **Then** the default
   places the operation's input in the body, carrying the reference the projection derived for it.
3. **Given** a field whose type is composite, **When** the developer places it in the path or the query,
   **Then** the tool refuses by naming the field and its type, and does not flatten it into invented
   scalars.
4. **Given** a path containing a `{placeholder}`, **When** no field is placed in the path under that name,
   **Then** the mismatch is named before anything is written.
5. **Given** a field placed in the path, **When** the route is written, **Then** that parameter is marked
   required without the question being asked, because a path parameter cannot be optional.
6. **Given** a field placed in the query, the body or a header, **When** it is placed, **Then** the
   developer is asked whether it is required, and the answer is what gets written — the contract declaring
   every field optional and nillable, neither answer could have been derived.
7. **Given** a field the developer places nowhere, **When** the run completes, **Then** the report names it
   as dropped, with its operation.

---

### User Story 3 - Reuse the headers production already declares (Priority: P2)

The developer is offered the header parameters their production description already declares as reusable
components, so a new route carries the same governance headers as its twenty neighbours without retyping
them.

**Why this priority**: It removes the most repetitive part of the work, and it is the clearest case of
reading an answer rather than inventing one — the tool proposes these headers because it found them in the
developer's own file. It is not required for a correct route, which is why it sits below the first two.

**Independent Test**: Against a production description whose routes all reference five header components,
integrate one operation and verify the offered headers are exactly those five, that accepting them produces
references to the existing components rather than redeclared parameters, and that declining them produces a
route with no headers.

**Acceptance Scenarios**:

1. **Given** a production whose routes reference the same header components, **When** a new route is
   authored, **Then** those headers are offered, each with the count of existing routes carrying it.
2. **Given** the developer accepts them, **When** the route is written, **Then** it references the existing
   components and no parameter is redeclared.
3. **Given** a production that declares no reusable header, **When** a route is authored, **Then** nothing
   is offered and no header is invented.

---

### User Story 4 - Not lose a long sitting (Priority: P2)

The authoring decisions survive an interruption, so a sitting cut short by a closed terminal or a stopped
machine resumes where it left off rather than starting over.

**Why this priority**: Authoring 21 operations is a long sitting, and losing it to an interruption would
make the feature expensive enough to avoid. The pairing step already sets this precedent.

**Independent Test**: Author three operations of five, interrupt the run, resume it, and verify the three
settled ones are not asked about again and the two remaining ones are.

**Acceptance Scenarios**:

1. **Given** a run interrupted after some operations were authored, **When** it is resumed, **Then** the
   settled routes and placements return and only the unsettled ones are asked for.
2. **Given** a resumed run whose production or contract file changed since, **When** the resume is
   attempted, **Then** it is refused by saying which file changed — the existing rule, unchanged.
3. **Given** a sitting completed and a fresh run on the same pair, **When** authoring begins, **Then**
   every operation is asked about again: decisions live in the checkpoint and the checkpoint is consumed.

---

### Edge Cases

- An operation whose input wrapper is a single composite field, under a `GET` verb: there is no scalar to
  place in the query, and the developer must change the verb or drop the operation.
- A path typed with two placeholders of the same name.
- A path typed that is not a valid OpenAPI path — no leading slash, unbalanced braces, whitespace inside.
- A verb typed that OpenAPI does not define for a path item.
- A field placed in a header whose name collides with one of the reusable headers offered.
- An operation kept whose projection carried nothing for its response, so the route would have a request
  and no described response.
- Two operations authored onto the same path with different verbs — legal, and it must produce one path
  item carrying two operations rather than two path items.
- Two operations authored onto the same path **and** verb — a collision between two authored routes, which
  must be named before anything is written.
- A contract of several hundred operations, where authoring every one is not a reasonable sitting.
- A developer who chooses to place a route beside a path that another authored route is also joining.
- A production description carrying no path at all, so there is no neighbour to choose from.
- A developer who replaces the placeholder description during the same sitting by editing the file the
  tool is about to rewrite.

## Requirements *(mandatory)*

### Functional Requirements

#### Authoring the route

- **RA-001**: The tool MUST ask the developer, for each operation they keep, the path and the HTTP verb the
  operation becomes. It MUST NOT write a route whose path or verb the developer has not settled.
- **RA-002**: The tool MUST pre-fill that question with the route it would have derived, and MUST say which
  of *derived*, *partial* or *fallback* the suggestion is, so that a suggestion never passes for a fact.
- **RA-003**: The tool MUST ask only about operations the developer keeps. An operation not kept MUST cost
  no question.
- **RA-004**: The tool MUST validate a typed path as an OpenAPI path — leading slash, balanced braces,
  no whitespace, no duplicate placeholder name — and MUST refuse an invalid one by naming what is wrong,
  without silently repairing it.
- **RA-005**: The tool MUST validate a typed verb against the verbs OpenAPI defines for a path item.
- **RA-006**: The tool MUST refuse an authored route that collides with a route production already carries,
  or with another route authored in the same run, by naming both sides of the collision before anything is
  written.
- **RA-007**: The tool MUST allow two authored operations to share a path under different verbs, producing
  one path item.
- **RA-008**: The tool MUST ask the developer **where in the production document** the authored route is
  placed — beside which existing path, or at the end — and MUST splice it there. A production description
  is read by people, and its paths are grouped by hand; appending every new route at a position the tool
  chose would scatter a family of routes that belong together.
- **RA-009**: The tool MUST offer that choice in the order the production document declares its paths, so
  that the developer picks a neighbour they can see rather than an index they must count.

#### Placing the fields

- **RA-010**: The tool MUST present, for each authored operation, every field the contract declares for its
  input, and MUST let the developer place each one in the path, the query, the body, a header, or nowhere.
- **RA-011**: The tool MUST propose a default placement derived from the verb: query for `GET` and
  `DELETE`, body for `POST`, `PUT` and `PATCH`. The default MUST be a proposal the developer can change,
  never a decision.
- **RA-012**: The tool MUST ask, for every field the developer places, **whether it is required**, and MUST
  write what the developer answered. The contract states nothing on the matter — everything in it is
  optional and nillable — so the answer cannot come from anywhere else.
- **RA-013**: The tool MUST refuse to place a composite field in the path, the query or a header, naming
  the field and its type. It MUST NOT flatten a composite into scalars it has invented.
- **RA-014**: The tool MUST mark a field placed in the path as required without asking, a path parameter
  being required by OpenAPI. This is the only placement whose requiredness is not a question.
- **RA-015**: The tool MUST establish that every placeholder in the authored path is filled by a field
  placed in the path under that name, and MUST name any mismatch before writing.
- **RA-016**: The tool MUST carry, for a field placed in the body, the reference the projection derived for
  it, rather than restating its schema inline.
- **RA-017**: The tool MUST record every field placed nowhere as dropped, with its operation, and MUST name
  them in the report. A field MUST NOT disappear without being named.

#### What is left for the developer to write

- **RA-018**: The tool MUST leave the description of an authored route **empty, carrying a conspicuous
  placeholder** for the developer to replace by hand. It MUST NOT compose a description from the operation
  name, the contract's documentation annotations, or anything else: a plausible sentence nobody wrote is
  the one thing a reader cannot tell from a sentence somebody did.
- **RA-019**: That placeholder MUST be a plain OpenAPI `description`, not an extension member. The written
  file carries only OpenAPI, and a placeholder a developer is meant to find and replace must sit where
  they will look for it.
- **RA-020**: The report MUST list every placeholder left in the document, with its route, so that the
  count of descriptions still to write is known without reading the file.

#### Reusing what production declares

- **RA-021**: The tool MUST offer the header parameters that production declares as reusable components and
  that its routes reference, with the number of existing routes carrying each.
- **RA-022**: An accepted header MUST be written as a reference to the existing component, never as a
  redeclared parameter.
- **RA-023**: The tool MUST NOT offer a header that production does not declare, and MUST offer nothing
  when production declares none.

#### Keeping the work

- **RA-024**: Authoring decisions MUST survive an interruption: a resumed run MUST ask only about what was
  not settled.
- **RA-025**: Authoring decisions MUST live in the checkpoint and nowhere else. The tool MUST NOT read
  authoring decisions from a file, and MUST NOT write them to one. A fresh run asks again.
- **RA-026**: **WR-105 is withdrawn.** The declared correspondence file is abandoned, and its remains MUST
  be removed rather than left in place: the context key that is read and never written, the proposer
  parameter that honours it, the corpus example, and the report marker. A capability announced end to end
  except the link that feeds it is the defect this repository has now named twice; leaving it in place
  after deciding against it would be the third.

#### Reporting

- **RA-027**: The report MUST carry, for each written route, the operation it came from, the path and verb
  the developer settled, and the placement of every field — including those dropped.
- **RA-028**: The report MUST distinguish a route the developer authored from a route the tool derived, so
  that a reader can tell a decision from a guess.

### Key Entities

- **Authored route**: the path and verb a developer settled for one operation, where in the production
  document it is placed, and the origin of the suggestion it replaced or accepted.
- **Field placement**: one field of an operation's input, where it goes — path, query, body, header, or
  nowhere — whether the developer declared it required, and the reason when the tool refused a placement.
- **Offered header**: a header parameter production declares as a reusable component, with the count of
  routes that reference it.
- **Authoring record**: the set of authored routes and placements for one contract, durable enough to
  survive an interruption and to be applied again at the next delivery.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer integrating the real 21-operation contract obtains routes that match their house
  conventions without editing the written file by hand afterwards.
- **SC-002**: No route is written whose path or verb the developer did not settle: zero invented routes in
  the output, against 19 out of 19 today.
- **SC-003**: Every field of every authored operation is accounted for in the report — placed or dropped —
  with no field unaccounted for.
- **SC-004**: Authoring the 21 operations of the real pair takes a developer under 15 minutes.
- **SC-005**: An interrupted session loses no settled decision: resuming asks only about what remained.
- **SC-006**: A developer reading the report can tell, for every route, whether its path and verb were
  settled by a person or suggested by the tool.
- **SC-007**: Every authored route lands where the developer asked it to land in the production document,
  and the routes that were already there keep their order.
- **SC-008**: No authored route carries a description the tool composed: the count of placeholders in the
  written document equals the count of routes authored, and the report states it.

## Assumptions

- **The derivation is kept, demoted to a suggestion.** Deleting it would make every operation cost a typed
  line, including those the convention does fit. It stops being a decision and becomes a default.
- **Placement is asked per field, with a per-verb default applied to all of them first.** Asking field by
  field with no default would be unusable on an operation with twenty fields; applying a default without
  showing it would hide a decision.
- **Requiredness is asked, not defaulted**, for every placement except the path. The contract states
  nothing — everything in it is optional and nillable — so neither answer could be derived, and defaulting
  silently would write a constraint on callers that nobody decided.
- **"In the route" is kept as two placements, path and query.** The request named three destinations —
  route, header, body. OpenAPI distinguishes a path parameter from a query parameter, and a document that
  confused them would not be valid, so the route destination is offered as those two. Anything placed in
  the path must also appear as a placeholder in the authored path, which is what RA-015 holds.
- **"Nowhere" is kept as a fourth answer.** A contract carries fields a REST route has no use for, and the
  developer must be able to say so — recorded as dropped, never silently omitted.
- **The body keeps the shape the projection derived**, including its `$ref`. This feature changes where
  input travels, not what the schemas say.
- **Only the request side is authored.** Responses keep what the projection derived; changing them is a
  separate question and is out of scope here.
- **Feature 004's pairing step remains** for the case it was built for: an operation that production
  already covers, where the question is which existing route it corresponds to. Authoring answers a
  different question — what a *new* route should be — and the two must not be merged without deciding what
  happens to coverage reporting.
- **A contract of several hundred operations is out of scope for interactive authoring.** The developer
  keeps a workable subset; the tool does not attempt to make authoring hundreds of routes pleasant.
- **Authoring decides what goes into the document and where; the existing splicing, verification and
  reporting carry it from there.** The one thing authoring adds to the writing side is a chosen insertion
  point, where today the splicer places a new route at a position of its own choosing.
- **The placeholder left in a description is a fixed, recognisable string**, identical for every authored
  route, so that a developer can find them all with one search and a reviewer can see at a glance what is
  still unwritten.
- **No pairing file, and none of its remains.** The declared correspondence file is abandoned rather than
  completed. What exists of it — a context key read and never written, a proposer parameter, a corpus
  example, a report marker — is removed in this feature. Keeping an unfed capability after deciding
  against it would repeat the defect twice already named in `CONCEPTION.md`.

## Out of Scope

- Deriving requiredness, path placement or headers from anything in the WSDL — this specification
  establishes that the information is not there.
- Composing a description for an authored route, from the contract's annotations or otherwise.
- Any file that declares authoring decisions, read or written.
- Carrying authoring decisions from one completed run to the next.
- Response shaping, status codes and error payloads for authored routes.
- Editing or re-authoring a route that production already carries.
- Any non-interactive or batch authoring surface.
