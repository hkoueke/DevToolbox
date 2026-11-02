# Research: Authoring a route instead of deriving one

Decisions taken before writing anything, each with what was rejected and why. Nothing here is marked
NEEDS CLARIFICATION: the two open questions of the specification were answered on 2026-10-28, and the
remainder are decisions this document makes.

---

## 1. Where authoring sits in the ordered chain

**Decision**: a new `AuthorStep`, placed immediately after `Correspond` and before `Validate`.

**Rationale**: authoring needs three things that only exist by then — the contract read (`ReadWsdl`), the
projection with its derived references, and production read (`ReadTarget`) so that insertion points and
reusable headers can be collected. It must come before `Compare`, because what it produces changes what
there is to compare. Placing it after `Correspond` keeps the pairing of *covered* operations where it is,
and leaves authoring to answer the question pairing cannot: what a route production lacks should be.

**Alternatives rejected**:

- *Inside `CorrespondStep`.* It would make one step ask two unrelated kinds of question and would couple
  coverage reporting to authoring. Principle II is explicit about interfaces that grow unrelated
  operations, and the same reasoning applies to a step.
- *Inside `CorrectStep`, at writing time.* Too late: the developer would be asked for a path after the
  comparison had already described the change in terms of a path nobody had settled.
- *Replacing `CorrespondStep` outright.* Coverage — what production covers of the contract — is built on
  the pairing, and is a published promise of feature 004 (WR-107). Removing it to make room would be a
  silent scope loss.

---

## 2. What the developer is asked, and in what order

**Decision**: per kept operation, in this order — (1) path and verb, pre-filled with the derivation;
(2) where in the production document the route goes; (3) the placement of each field, pre-set to the
per-verb default; (4) required or not, for each placed field except those in the path; (5) which reusable
headers to attach. The description is never asked for.

**Rationale**: each question narrows the next. The verb decides the default placement, so asking it first
means the placements arrive already mostly right. Insertion point comes second because it is a single
choice and the developer still has the route in mind. Requiredness is asked per field rather than per
operation because it genuinely varies, and because the contract offers no signal to default from.

**Alternatives rejected**:

- *Ask placements before the verb.* The default could not then be derived from the verb, so every field
  would start unplaced.
- *Ask requiredness once per operation.* It would force one answer onto fields that differ, and the tool
  would be writing a constraint the developer did not mean.
- *Ask for the description.* Rejected by the specification, and rightly: a description typed in a terminal
  prompt, under time pressure, in the middle of 21 operations, is worse than a placeholder a reviewer can
  see is unwritten.

---

## 3. The per-verb default placement

**Decision**: `GET` and `DELETE` place every scalar field in the query; `POST`, `PUT` and `PATCH` place the
operation's input in the body carrying its derived reference. A composite field under `GET` or `DELETE` is
left unplaced and reported as needing a decision, not silently moved to the body.

**Rationale**: it matches what the two verb families mean, and it is the default that needs the fewest
corrections on the real pair. Leaving a composite unplaced rather than quietly putting it in the body is
the honest form: a `GET` whose input is a composite object is a design question for the developer, not a
placement the tool can settle.

**Alternatives rejected**:

- *`GET` with a body.* Legal in OpenAPI, widely broken in practice, and never what a developer migrating
  from SOAP wants by default.
- *Everything in the body regardless of verb.* It would reproduce the SOAP shape the developer is leaving,
  which is precisely the complaint that opened this feature.
- *Flatten a composite into query parameters.* Rejected as inventing structure. The refusal names the
  field and its type instead (RA-013).

---

## 4. The description placeholder

**Decision**: a fixed string, `À COMPLÉTER`, written as the `description` of the authored operation.

**Rationale**: it must be identical everywhere so one search finds all of them, conspicuous enough that a
reviewer notices it, and valid OpenAPI so the document the tool writes stays a document. Accented French
matches the rest of what this tool shows its user, and the phrase cannot be mistaken for a real
description. The report states how many there are (RA-020), so the count is known without reading the
file.

**Alternatives rejected**:

- *An extension member such as `x-apidelta-todo`.* Forbidden by the rule this repository adopted three
  days ago: the written file carries only OpenAPI. A placeholder in an extension member would also sit
  where nobody looks.
- *An empty string.* It passes schema validation and disappears from a reader's eye. "Empty" would be
  indistinguishable from "nobody has noticed yet".
- *Omitting the description member entirely.* The developer then has nothing to find and replace, and the
  count of unwritten descriptions cannot be stated.
- *Reusing the contract's `wsdl:documentation`.* Tempting and wrong twice over: most WCF contracts carry
  none, and where they do it describes a SOAP operation, not a REST route. A sentence nobody wrote about
  this route is the thing a reader cannot tell from a sentence somebody did.

---

## 5. Adding a member somewhere other than the end

**Decision**: `Splice` gains an optional *after this sibling key* and `SplicingEmitter.Adding` computes its
insertion from that sibling's content end instead of from the mapping's last entry. Absent the sibling, the
behaviour is exactly today's.

**Rationale**: this is the one capability RA-008 needs and the splicer lacks. `Adding` currently reads
`mapping.Entries[^1]` and inserts after it, so every new path lands at the end of `paths`. The change is
four lines and leaves the indentation computation, the byte-span arithmetic and the whitespace back-off
untouched — the three things in that method that were hard to get right.

**Alternatives rejected**:

- *Re-emit the whole `paths` mapping in the chosen order.* It would rewrite bytes the developer did not
  change, which the verification would then correctly report as drift. The entire point of splicing is
  that untouched bytes stay untouched.
- *Sort paths on the way out.* Rejected three days ago for the output as a whole, for the same reason: a
  document relied on by people must keep the order they gave it.
- *Ask the developer to move the route by hand afterwards.* That is the current behaviour and the thing
  being fixed.

---

## 6. Finding the reusable headers production declares

**Decision**: collect the entries of `#/components/parameters` whose `in` is `header`, count how many of
production's operations reference each, and offer those with a count of at least one, most-referenced
first.

**Rationale**: it reads an answer instead of inventing one. On the real pair it finds exactly the five
governance headers, each referenced by all twenty routes — which is also the signal that they are house
convention rather than one route's peculiarity. The count is shown so the developer can see the difference
between a header every route carries and one that appears once.

**Alternatives rejected**:

- *Offer every parameter component, not only headers.* Path and query components belong to the routes that
  define them; offering them would invite attaching a parameter that means nothing here.
- *Infer the headers from the gateway's configuration or from a convention list.* Nothing in the inputs
  carries it, and a built-in list of "usual" headers would be this tool inventing another house's rules.
- *Attach them without asking.* They are not universal — a production that declares none must be offered
  none — and silently adding five parameters to a route is a change the developer did not make.

---

## 7. How authoring reaches the checkpoint

**Decision**: one checkpoint entry per authored route and one per placement, both as strings, under two new
selection kinds. A placement entry reads `operation · field · location · required`.

**Rationale**: Principle VIII and the standing rule that `PersistedRun` carries strings only — a rule the
checkpoint store enforces at runtime by inspecting the type's shape and refusing to write if it has been
widened. Strings also keep the resume prompt able to show what was settled without knowing what the words
mean.

**Alternatives rejected**:

- *A nested object per route.* It would widen `PersistedRun` and the store would refuse to write it, which
  is the guard working as designed.
- *A file beside production.* Forbidden by RA-025, and the reason is in the specification: the declared
  correspondence file was exactly this idea, and it rotted into a capability with no reader.

---

## 8. Withdrawing WR-105 rather than completing it

**Decision**: delete the remains — the context key, the proposer's `declared` parameter,
`CorrespondenceOrigin.Declared`, its rendering, and the corpus example.

**Rationale**: the developer decided against the file. Leaving the pieces in place would leave a third
instance of the defect `CONCEPTION.md` names twice: a capability announced end to end except the link that
feeds it. A reader of `SimilarityProposer` would keep seeing a parameter whose only caller passes null, and
would reasonably assume a file reader exists somewhere.

**Alternatives rejected**:

- *Leave them, harmless.* They are not harmless. A declared-and-unfed capability is what this repository
  has now spent two releases removing.
- *Keep the enum member for checkpoint compatibility.* Unnecessary: a checkpoint this version cannot read
  already stops offering a resume cleanly, and that path has a test.

---

## 9. What is validated in a typed path, and when

**Decision**: `RouteSyntax` validates before the value is used for anything — leading slash, balanced
braces, no whitespace, no duplicate placeholder name, no empty segment, and no character that would change
the meaning of a JSON Pointer segment once escaped.

**Rationale**: this came out of the STRIDE pass and is the one security-relevant finding of this feature.
`SpecPointer` segments are escaped for JSON Pointer, so a path containing `~` or an unescaped `/` in the
wrong place can build a pointer that designates a different node than the one intended. Validating first
means no malformed value ever reaches pointer construction, splicing or emission.

**Alternatives rejected**:

- *Validate at write time.* Too late to tell the developer usefully, and it would let a malformed path
  travel through the plan and the report first.
- *Rely on the emitter's quoting.* The emitter protects the document's syntax, not the pointer's meaning.
  They are different problems and only one of them is the emitter's.
