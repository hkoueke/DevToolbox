# Feature Specification: apidelta — what a candidate OpenAPI description would do to production, and what production has accumulated

**Feature Branch**: `003-openapi-delta-audit`

**Created**: 2026-10-16

**Status**: Draft

**Input**: User description: "A new feature for the toolbox landed in the specs folder
(openapi-semantic-delta-audit-optimizer-prd.md)"

**Source document**: `specs/openapi-semantic-delta-audit-optimizer-prd.md`, version 3.0. That product
requirements document is the input to this specification, not a substitute for it. Where the two differ,
this specification is the authority and the difference is recorded under Assumptions or Clarifications.

## Clarifications

### Session 2026-10-16

- Q: The source document wants the run's outcome code usable by a continuous-integration pipeline, which
  means a process exit code and a run driven entirely by supplied arguments. The toolbox is an interactive
  shell whose tools are chosen from a menu, so that path does not exist and adding it changes code every
  tool shares. Is non-interactive invocation in scope here? → A: **No — interactive only for this release.**
  The outcome code is still specified and still reported, on screen and in the machine-readable report, so
  the vocabulary is fixed; what is deferred is the surface that exposes it to a pipeline. Putting a
  non-interactive entry path inside this feature would mean this tool modifying shared shell behaviour and
  restating every confirmation — the in-place write of FR-550, the out-of-directory write of FR-709 — as
  arguments rather than prompts, which is a second feature wearing this one's clothes. A per-tool exception
  was rejected for the same reason: it would be the precedent the fourth and fifth tools inherit. Recorded
  as FR-708 and FR-708a.
- Q: A developer often wants one route out of a large description rather than the whole of it — can the tool
  let them choose the operations they care about and hand back a usable description of just those? → A:
  **Yes, as a mode of its own.** It is nearly free: the dependency closure that makes a corrected
  description complete (FR-200) is the same machinery, run over a set of roots the developer chose rather
  than the set that changed, and the reader, the emitter and the self-verification are already there. What
  it is *not* is a trimmed copy produced by deletion. Three things make the difference between a usable
  extract and a plausible-looking one, and all three are stated as requirements rather than left to the
  implementation: what an operation inherits from its context must come with it, or an authenticated
  endpoint quietly becomes an open one (FR-905); the version must not move, because a view is not a change
  (FR-908); and the tool must verify that each extracted operation is still contract-identical in its
  *effective* form before it publishes anything (FR-910). Recorded as User Story 9 and FR-900 to FR-913,
  with FR-700 and FR-701 extended to carry the new mode.

### Session 2026-10-17 — raised during implementation

- Q: FR-305 makes the recommendation the highest impact detected, over a lattice of major, minor and patch.
  But FR-114 admits a fourth classification — unknown — and the implementation has to put it somewhere:
  a changed vendor extension is unknown by construction, and the reference corpus carries several. What does
  an unclassifiable change do to the recommended version? → A: **It raises the floor to minor, and it is
  reported as requiring review.** Three options were weighed. Treating it as breaking would make any
  touched `x-` block force a major release, which trains a team to stop reading the recommendation —
  the failure mode that costs the tool its usefulness. Treating it as compatible would put the one mistake
  that is discovered by a caller rather than a reviewer into the default path. Letting it set no floor at
  all would let a patch be recommended for a change nobody could classify. The floor plus a named review
  list is the only one of the four that neither overstates nor hides. Recorded as FR-305a.


### Session 2026-10-21 — the output becomes the document itself

- Q: Should the comparison keep stopping at a report and a fragment the developer merges by hand? → A:
  **No. The output is production itself, corrected.** FR-500 now requires the complete corrected
  description and forbids a fragment. The reasoning is the one the fragment was chosen for, turned around:
  a fragment is what a reviewer *can* merge, but it makes them perform by hand the merge the tool can do
  **and prove**, and it can pass every check while describing a file nobody has assembled. The old FR-500
  to FR-504 are gone as an output format; their substance — completeness, the transitive closure, removals
  only when asked, no cleanup mixed in, it must reparse and resolve — survives as FR-508 to FR-511, where
  it applies to a whole document and is therefore stronger than it was.
- Q: Who chooses what gets carried, and when? → A: **The developer chooses, after the comparison, from
  what it found** (FR-501). Choosing before would buy nothing — the dependency closure resolves over the
  whole document anyway, because schemas are shared between routes — and would throw away the impact and
  confidence that are the point of comparing at all. The selectable list is organised by route and includes
  routes with nothing to carry, marked as such (FR-502), so that a developer looking for one always finds
  it and is told why there is nothing, instead of having to tell "it already matched" from "the tool missed
  it" by themselves.
- Q: At what granularity? → A: **A whole route, or one single difference on a route** (FR-503), any number
  at once (FR-504). A developer who wants a parameter's requiredness carried but not a type change on the
  same operation must be able to say exactly that.
- Q: What is guaranteed about the parts nobody selected? → A: **They are byte-identical, and the tool
  proves it before writing** (FR-506, FR-514). Verification asserts that what the selection did not name is
  unchanged and that what it did name differs *only* as selected — no more and no fewer. That pair is what
  makes the result reviewable: the diff a developer reads is the selection they made, and nothing arrived
  alongside it. FR-512 therefore records what each change *replaced*, not only what it became, and FR-513
  marks a carried breaking change as breaking so it cannot be taken unknowingly. FR-515 gives the document
  no in-place path at all: it never overwrites production, under any flag.
- Q: La production corrigée est écrite en recopiant les octets de la production autour de ce qui change.
  Que se passe-t-il quand la production est en JSON ? → A: **Le mode est refusé, en le disant.** FR-500a
  l'énonce. Insérer dans du JSON demanderait de le réécrire en entier — l'émetteur canonique de cet outil
  n'écrit que du YAML —, et une réécriture complète enterrerait le changement demandé sous des milliers de
  lignes, ce qui est exactement ce que FR-506 existe pour empêcher. La comparaison, l'examen et le
  chiffrage continuent de fonctionner sur une production en JSON ; seule l'écriture de la production
  corrigée l'exige en YAML.
- Note: this is new implementation work, not a correction to what exists. `tasks.md` closed at 209 of 209
  with a fragment builder behind it. A selection step, an assembler writing from production's syntax tree,
  a third verifier of the same shape as `ContractEquivalenceVerifier` and `ExtractVerifier`, the mode, the
  report sections and the tests are a phase of their own. Until it lands, this specification is ahead of
  the code, and that gap is recorded here rather than left to be discovered.
## User Scenarios & Testing *(mandatory)*

### User Story 1 - Know what this candidate would do to production (Priority: P1)

A developer has a freshly generated OpenAPI description and the one currently in production. They point the
tool at both and get an ordered answer: what was added, what was removed, what changed, and for each change
whether it breaks existing consumers, is backward compatible, or only touches documentation.
Reindentation, reordered keys, requoted response codes and changed line endings produce nothing at all,
because none of them change the contract.

**Why this priority**: This is the question the tool exists to answer, and the one a text diff answers
badly. A generated description differs from production on almost every line; the developer needs the
handful of lines that mean something. Everything else in this feature is either a prerequisite for trusting
this answer or a consequence of having it.

**Independent Test**: Take a production description, reserialize it with different indentation, key order,
quoting and line endings, and confirm the tool reports no API difference. Then apply one known breaking
change and one known additive change, and confirm each is reported once, located, and classified correctly.

**Acceptance Scenarios**:

1. **Given** a candidate that differs from production only in formatting, **When** the comparison runs,
   **Then** no API difference is reported and no version increment is recommended.
2. **Given** a candidate that removes a response, narrows a constraint or makes a parameter required,
   **When** the comparison runs, **Then** each such change is reported as breaking, with its location, its
   previous and new value, and the reason it breaks.
3. **Given** a candidate that adds an optional parameter and a new operation, **When** the comparison runs,
   **Then** both are reported as backward-compatible additions.
4. **Given** a candidate that only corrects a description, **When** the comparison runs, **Then** the change
   is reported as documentation-only.
5. **Given** a change the tool cannot confidently classify, **When** the comparison runs, **Then** it is
   reported as unknown with its rationale, and MUST NOT be presented as safe.

---

### User Story 2 - Refuse to reason about a description it could not read faithfully (Priority: P1)

Before comparing or auditing anything, the tool establishes that it read both documents as their authors
meant them. Duplicate keys, unresolved references, an unsupported OpenAPI version or a parse error are
reported with their location, and the run stops there rather than producing a confident report built on a
document it silently repaired.

**Why this priority**: A duplicate mapping key is the clearest case: most parsers keep one value and discard
the other without a word, so a comparison computed afterwards is a comparison of something nobody wrote. An
answer derived from a misread document is worse than no answer, because it is indistinguishable from a good
one. This shares P1 because Story 1 is unsafe without it.

**Independent Test**: Feed a document containing a duplicate mapping key, one with an unresolved reference,
and one declaring an unsupported OpenAPI version. Confirm each is refused with a located diagnostic and that
no report, corrected description or optimized output is produced.

**Acceptance Scenarios**:

1. **Given** a document with a duplicate mapping key, **When** it is loaded, **Then** the duplicate is
   reported with its location and the run fails, before either value has been lost.
2. **Given** a document with an unresolved internal reference, **When** it is loaded, **Then** the reference
   and every location using it are reported, and no comparison result is published.
3. **Given** a document declaring an OpenAPI version outside the supported range, **When** it is loaded,
   **Then** the tool states which versions it supports and stops.
4. **Given** a document containing a reference cycle between schemas, **When** it is loaded, **Then** the
   cycle is accepted as legal and the tool neither fails nor loops.
5. **Given** a document whose extension does not match its content, **When** it is loaded, **Then** the
   actual format is detected and used.

---

### User Story 3 - Be told which version number to publish, and why (Priority: P2)

Taking the production version as the baseline, the tool recommends the next version — patch, minor or major
— and lists every change that contributed to the recommendation. The developer can see that one newly
required property is what forced a major, and can override the number deliberately if they disagree.

**Why this priority**: The classification in Story 1 already tells the developer whether they are breaking
anything; this turns that into the number they must publish, and into an auditable reason for it. Valuable
and small, but Story 1 alone is already usable.

**Independent Test**: Run against pairs containing, respectively, only documentation changes, one additive
change, and one breaking change; confirm the recommendation is patch, minor and major, and that each names
the contributing changes.

**Acceptance Scenarios**:

1. **Given** changes of mixed impact, **When** the version is recommended, **Then** the highest impact wins
   and the report names every change that contributed at that level.
2. **Given** no semantic change at all, **When** the version is recommended, **Then** the current version is
   retained and the recommendation is reported as none.
3. **Given** a production version that is not a valid semantic version, **When** the version is recommended,
   **Then** that is reported rather than guessed at, and a fallback applies only if one was explicitly
   configured.
4. **Given** an explicit version lower than the recommendation, **When** it is supplied, **Then** the tool
   requires a deliberate override and states prominently in the report that the published version is below
   what the changes require.

---

### User Story 4 - Get production back with the chosen changes already in it (Priority: P1)

The developer has the classified differences. They pick, from them, the routes and the individual changes
they want brought into production. The tool states exactly what it is about to insert, they accept, and it
writes production as it was with precisely that applied — the new version, the added and modified objects,
and every component those objects depend on, resolved transitively, so nothing points at something that
does not exist. Each change is marked with where it came from and what it replaced. Everything they did not
pick comes through untouched, and the tool proves that before a byte is written. Removals appear only if
asked for, and cleanup never arrives mixed in with contract changes.

**Why this priority**: a classified list tells the developer what happened; the corrected document is the
work itself. It is P1 because without it the tool stops one step short, and that step is the one that takes
the afternoon. There is no fragment output: a fragment made a reviewer perform by hand the merge the tool
can do and prove, and it could pass every check while describing a file nobody had assembled.

**Independent Test**: compare the breaking candidate against the baseline, select two added routes and one
single parameter change on an existing route, and confirm the result carries exactly those three things,
that every other byte of production is unchanged, that the transitive closure of what was carried is
present and resolves, and that the tool refuses to publish when a deliberately broken builder touches an
unselected route.

**Acceptance Scenarios**:

1. **Given** a completed comparison, **When** the developer asks for the corrected description, **Then** the
   selectable list is organised by route, includes every route of the candidate — those with nothing to
   carry marked as such — and shows each difference with the impact and confidence the comparison
   established.
2. **Given** that list, **When** the developer selects two routes and one difference on a third, **Then**
   the tool states those three items before writing anything and lets them accept or go back.
3. **Given** an accepted selection, **When** the document is written, **Then** production's own bytes come
   through unchanged everywhere the selection did not name, and the selected content comes from the
   candidate's own bytes with nothing synthesised.
4. **Given** a new operation referencing schemas that themselves reference others, **When** it is carried,
   **Then** the full transitive closure is present and no internal reference is left dangling.
5. **Given** a route production already carries whose candidate counterpart made one parameter required and
   changed another one's type, **When** the developer selects only the requiredness, **Then** that one
   change is carried and the type is exactly as production had it.
6. **Given** a component present in both documents under the same name but with different meanings, **When**
   the document is written, **Then** the conflict is reported and production's component is not silently
   overwritten.
7. **Given** a selected change the comparison classified breaking, **When** the document is written, **Then**
   it is marked breaking in the document as well as in the report, so it cannot be carried unknowingly.
8. **Given** any corrected description, **When** it is written, **Then** every added or changed element
   carries a mark naming the operation it came from and what it replaced, and a reader can tell production's
   content, the candidate's and the overwritten apart without opening the report.
9. **Given** removals were not requested, **When** the document is written, **Then** it contains none; **and
   given** safe cleanup opportunities exist, **Then** they are absent unless explicitly combined, so the
   review stays about the contract change.
10. **Given** a deliberately broken builder that alters an unselected route, **When** the document is
   verified, **Then** nothing is published, the run is unsuccessful, and the verification names the route
   and what it changed.
11. **Given** any corrected description, **When** it is written, **Then** it declares itself a proposed
   correction rather than a published contract, it sits beside the reports under its own name, and no flag,
   mode or confirmation causes it to be written over production.
12. **Given** an empty selection, **When** the run completes, **Then** no document is written.
13. **Given** a selection the developer accepted, **When** the run is interrupted and resumed, **Then** the
   resumed run re-reads its inputs and rebuilds the same document without asking again.

---

### User Story 5 - See what production has accumulated (Priority: P2)

Without any candidate description, the developer audits production alone. The tool reports what is invalid,
what is inconsistent, what is repeated, what is unreachable and what is undocumented — each finding carrying
a stable rule identifier, a location, a severity, a safety class and a recommended action. Findings are
reported separately from contract changes, never merged into them.

**Why this priority**: This is the second half of the product and stands on its own: a description can be
perfectly valid and still be unmaintainable. It is P2 rather than P1 because a team that cannot yet see what
its next release breaks has a more urgent problem than a team with duplicated headers.

**Independent Test**: Audit a production description containing a repeated inline header, an unreachable
component, an operation whose description says it is obsolete without the deprecation flag, and an optional
request body; confirm one finding per occurrence group, with the identifiers, severities and safety classes
this specification fixes.

**Acceptance Scenarios**:

1. **Given** an identical inline parameter repeated across many operations, **When** the audit runs, **Then**
   one finding groups every occurrence, counts them, locates them and estimates the bytes a shared component
   would save.
2. **Given** a component no path, security requirement or other component can reach, **When** the audit
   runs, **Then** it is reported as unreachable, and its removal is classified safe only after complete
   reference analysis and absent any configured keep rule.
3. **Given** an operation that defines a request body without stating that it is required, **When** the audit
   runs, **Then** it is reported as review-required, never corrected automatically.
4. **Given** an operation whose description contains a configured deprecation phrase while the deprecation
   flag is absent, **When** the audit runs, **Then** the mismatch is reported and the flag is not added on
   its own.
5. **Given** two headers differing only in casing, **When** the audit runs, **Then** both spellings are
   reported and the tool MUST NOT merge them.

---

### User Story 6 - Know what cleanup would be worth, before doing any of it (Priority: P3)

The developer asks what could be reduced and what it would buy: for each redundancy group, how many
occurrences exist, how large they are now, how large a shared component plus references would be, the
absolute and percentage saving, the number of occurrences at which extraction breaks even, and the safety
class of doing it. Nothing is changed; the result is a plan.

**Why this priority**: Deciding whether to clean up is cheaper than cleaning up, and the estimate is what
makes the decision possible. It precedes Story 7 because a proposal nobody applies is still useful, while an
application nobody could evaluate is not.

**Independent Test**: Produce an optimization plan for a description with known duplication and confirm every
proposal carries occurrences, current size, projected size, saving, break-even count and safety class, and
that the production file is byte-identical afterwards.

**Acceptance Scenarios**:

1. **Given** duplicated parameters, responses and request bodies, **When** the plan is produced, **Then**
   each group appears with its measured savings and its safety class.
2. **Given** two structurally equivalent schemas with different names, **When** the plan is produced,
   **Then** merging them is proposed as review-required, with the differing fields named.
3. **Given** a plan-only run, **When** it completes, **Then** no file other than the requested artifacts has
   been written, and production is unchanged.

---

### User Story 7 - Apply only what is provably safe, to a copy, verified (Priority: P3)

The developer accepts the safe part of the plan. The tool writes a new optimized description, then proves its
own work: it reparses the output, revalidates it, resolves every reference and compares it against the
pre-optimization document under contract equivalence. If equivalence cannot be established, nothing is
published. A manifest records every transformation, where it came from, where it went, and how to reverse it.

**Why this priority**: This is the most dangerous capability in the feature, so it arrives last and behind an
explicit request. The value is real — repeated definitions are what make these files unreviewable — but an
optimizer trusted without verification is a contract-breaking machine.

**Independent Test**: Apply safe optimizations to a description with exact duplicates; confirm the optimized
output is contract-equivalent to the original and that production is untouched. Then inject a transformation
that alters the contract and confirm no optimized artifact is published.

**Acceptance Scenarios**:

1. **Given** exactly equivalent repeated definitions, **When** safe optimizations are applied, **Then** the
   optimized copy references shared components, reparses, revalidates and verifies as contract-equivalent.
2. **Given** verification fails, **When** the run completes, **Then** no optimized file exists, the failure
   is reported, and the run's outcome is unsuccessful.
3. **Given** a review-required transformation, **When** safe optimizations are applied, **Then** it is not
   applied, and remains a proposal.
4. **Given** vendor extensions anywhere in the document, **When** safe optimizations are applied, **Then**
   every one of them survives unchanged, unless a rule that explicitly targets it was enabled.
5. **Given** optimization was applied, **When** the run completes, **Then** a manifest lists each
   transformation with its origin, destination, fingerprint and reversibility.

---

### User Story 8 - Make the rules the team's own (Priority: P3)

A team adjusts severities per rule, disables rules it disagrees with, sets the minimum occurrence count that
makes repetition worth reporting, declares which vendor-extension prefixes are protected, states its
versioning policy for the cases where policy is a choice, and suppresses individual findings with a
justification, an owner and an optional expiry. Expired suppressions are reported rather than silently
honoured, and suppressed findings are listed rather than hidden.

**Why this priority**: Without this, the audit is somebody else's opinion, and gets switched off wholesale.
It is P3 because the shipped defaults are usable on day one.

**Independent Test**: Run the same audit with and without a ruleset that lowers one severity, disables one
rule and raises one occurrence threshold, and confirm each takes effect; then suppress a finding and confirm
it moves to the suppressed list with its justification rather than disappearing.

**Acceptance Scenarios**:

1. **Given** a ruleset that disables a rule, **When** the audit runs, **Then** that rule produces no finding
   and the report states which rules were disabled.
2. **Given** a suppression without a justification while justification is required, **When** the run starts,
   **Then** the suppression is rejected as invalid configuration rather than applied.
3. **Given** a suppression whose expiry has passed, **When** the audit runs, **Then** the finding is reported
   again and the expiry is reported.
4. **Given** suppressed findings, **When** the report is produced, **Then** they appear in their own section
   with rule, location, justification and owner.

---

### User Story 9 - Take one route out, as a description you can actually use (Priority: P3)

A developer needs one endpoint, not the four-hundred-operation file it lives in — to hand to a client team,
to feed a generator, to register in a gateway, or to reason about on its own. They point the tool at the
description, narrow a long list down to the routes they want, pick them, and get back a standalone OpenAPI
description containing exactly those operations and everything they depend on: the schemas, the parameters
they inherit from their path, the servers, the security scheme that protects them, the tags they use.
Nothing else. The source file is untouched, and the extract says on its face that it is a part rather than
a whole.

**Why this priority**: it is the most obviously useful thing in this feature that has nothing to do with
comparing two files, and it is the cheapest, because the dependency closure that makes a corrected
description complete is the same work over a different set of roots. It is P3 rather than P2 because a team
that cannot see what its next release breaks has a more urgent problem — but its dependencies are thin
enough that it can be pulled forward right after the comparison lands if the need is immediate.

**Independent Test**: select one operation that shares its path with others, inherits path-level
parameters, is covered by a global security requirement and references a chain of schemas. Confirm the
extract contains that operation and no other, resolves completely, keeps the inherited parameters and the
security scheme, and is contract-identical in effective form to the same operation in the source.

**Acceptance Scenarios**:

1. **Given** a description of several hundred operations, **When** the developer looks for a route,
   **Then** they can narrow the list before choosing, and select operations across several paths in one
   pass.
2. **Given** a selected operation that references a chain of schemas, **When** the extract is produced,
   **Then** every schema in the chain is present and no reference is left unresolved.
3. **Given** a selected operation whose path declares parameters shared by all its operations, **When** the
   extract is produced, **Then** those parameters are still in force for it.
4. **Given** a selected operation covered by a global security requirement, **When** the extract is
   produced, **Then** the requirement and the scheme it names are carried over, and the operation is not
   left unprotected.
5. **Given** a path with four operations of which one was selected, **When** the extract is produced,
   **Then** the other three are absent, and so is every component only they needed.
6. **Given** an extract has been produced, **When** the source description is inspected, **Then** it is
   byte-identical to what it was, and its version is unchanged in both files.
7. **Given** the tool cannot establish that a selected operation survived extraction unchanged, **When** the
   run completes, **Then** no extract is published and the failure says which operation and what differed.
8. **Given** an extract, **When** it is opened, **Then** it states its own provenance — which description
   it came from and which operations were chosen — so it cannot be mistaken for the whole API.

---

### Edge Cases

- A document that is valid YAML but not valid OpenAPI at all — refused by structural validation, not treated
  as an empty description.
- A path template placeholder with no matching path parameter, and a declared path parameter absent from the
  template: both directions are reported.
- A reference cycle between schemas, which is legal, versus a cycle through non-schema components, which is
  not — the first must not be reported as an error, the second must.
- A reference that resolves to a fragment of another file, while external references are disabled by default.
- A candidate and production declaring different OpenAPI minor versions, where nullability expressed the 3.0
  way and the 3.1 way means the same thing and must not read as a contract change.
- An operation whose success-response schema changes shape while remaining the same declared type.
- Security scope lists differing only in order, which is not a change, versus security requirement
  alternatives whose order may matter, which is.
- Two components identical except for a description — under one name that is a documentation-only
  difference, under two names it is a review-required merge, never an automatic one.
- A deduplication candidate occurring exactly at the break-even count, where extraction saves nothing.
- An enum that loses a value, which is breaking, versus one that gains a value, whose classification is
  policy.
- A response code written unquoted in one place and quoted in another, which is one code, not two.
- HTML-escaped text such as `&gt;` inside a description, reported and never silently decoded.
- An unknown vendor extension whose meaning the tool cannot know, which is preserved and treated as
  potentially behaviour-affecting.
- A document deep enough to exhaust a recursive traversal, which must be traversed without exhausting it.
- Cancellation midway through a long audit, which must leave no partial artifact that looks finished.
- An optimized output that would be larger than the original, or that exceeds a configured growth limit.
- A target file that is read-only, missing, or changed on disk between being read and being written back.
- Both inputs being the same file.
- A candidate or production file that is empty, or that contains only comments.
- A selected operation sharing its path with operations that were not selected — the path item keeps only
  the selected methods, and keeps the parameters they inherit from it.
- Two selected operations depending on the same component, which must appear once rather than twice.
- A selection of every operation in the description, which yields the whole description minus whatever
  nothing referenced, and must still verify.
- A selection of no operations at all, refused before any work is done.
- A document-level vendor extension that names paths the extract does not contain — reported, never
  edited.
- A selected operation whose only dependency is an external reference, while external references are
  disabled by default.


### Session 2026-10-20 — raised during implementation

- Q: (The requirement identifiers below are those of the time; the fragment output they governed was removed
  on 2026-10-21 and their substance now lives in FR-508 to FR-511.) FR-501 requires the patch to carry
  *complete modified objects*, and FR-502 requires it to carry removals *only when explicitly requested*.
  One case satisfies neither reading: an operation whose only change is an internal removal — the reference
  corpus has one, a `'404'` response that disappears from `GET /documents/{id}`. Carrying the operation
  whole means the merge drops that response, which looks like honouring a removal nobody asked for. Not
  carrying it means the patch silently omits the breaking change it exists to communicate. → A: **The
  operation is carried whole, and the entry is commented with what disappears, whether or not removals were
  requested.** The deciding argument is what each option does when it is wrong. Omitting the operation
  produces a patch that passes every check and fails to mention a breaking change, which is the one defect
  this feature cannot have. Carrying it produces a patch that is complete and whose only risk — a reviewer
  not noticing the response is gone — is removed by naming it in a comment on the entry. FR-502 is therefore
  about the *subject* of a patch: removing a path, an operation or a component is never done unasked. The
  internal shape of an object the patch already carries is whatever the new file says it is. Recorded at the
  time as FR-501a and FR-502a; since the removal of the fragment output on 2026-10-21 the same rules are
  FR-509 and FR-510.

- Q: A change confined to a component — `maxLength` tightened on `Document.titre`, which the corpus also
  has — produces no added or modified path, so a patch assembled from path differences alone would be
  empty, and a component whose meaning changed under an unchanged name would never be compared against its
  production twin. → A: **A difference located under a component puts that component into the patch's
  dependency closure directly**, alongside the components reached by following references from changed
  operations. Without this, both the patch and the same-name conflict report are silently empty for the
  most common kind of schema change. Recorded as FR-200a.

- Q: AUD-006 is specified to report, among other things, *duplicate semantic status codes* — `'400'` and
  `400` as two keys of the same `responses` map. The loader resolves scalars before it looks for duplicate
  keys, so it already refuses such a document as having a duplicate key, and no document carrying the defect
  ever reaches the audit. → A: **AUD-006 does not look for it.** A rule that guards a state the loader makes
  unreachable is a guard that guards nothing, and keeping it would suggest the audit is what protects
  against it. The defect stays reported, by `LOAD-DUPLICATE-KEY`, at the moment the file is read. Recorded
  as AUD-006a.

- Q: AUD-006 also reports *inconsistent standard error responses*, and AUD-300 reports operations that do
  not declare the configured standard error responses. On the reference corpus both fire on the same
  operations, for the same codes, which doubles a reviewer's work without adding anything. → A: **AUD-006
  considers only error responses the ruleset does not declare standard**; the configured standard is
  AUD-300's business alone. What remains in AUD-006 is the usage a document has given itself without any
  ruleset asking for it, which is a deduction and travels with its reasoning. Recorded as AUD-006b.

- Q: The data model states that a group *at or below* its break-even count reports a saving of zero or less.
  The break-even count is the first occurrence count at which extraction starts to pay, so at that count
  the saving is positive by construction, and the invariant as written is arithmetically false. → A: **the
  invariant reads "below it"**, and the data model is corrected. A group below its break-even count reports
  a saving of zero or less and is never a recommendation; a group at or above it reports a positive saving.
  The published rendering already says "break-even at 2 occurrences" for a group of 14 that saves 2.1 KB,
  so the rendering was right and the invariant's wording was not. Recorded as FR-403a.

- Q: Which component family does an extraction target? The audit rule that finds a repetition is the
  obvious source — one rule, one family — but AUD-103 reports both repeated request bodies and repeated
  *media types*, and a media type repeated inside `responses` is not a request body. Taking the family from
  the rule proposed moving response media types into `components.requestBodies`, which would produce a
  description that does not reparse. → A: **the family is read from the location of the repeated thing, not
  from the rule that found it.** A location from which no family can be read yields no proposal, which is
  how the tool stays silent about repetitions it has found and cannot transform — sharing the schema behind
  a repeated media type is sound advice, and the finding already gives it. Recorded as FR-403b.

- Q: Who measures an occurrence? The plan could re-measure from the locations a finding carries, which
  keeps the audit unchanged. But AUD-101's locations are *operations*, and the repeated thing is the header
  set inside them, so re-measuring announced fourteen whole operations as the recoverable size. → A: **the
  rule measures, the plan records.** A finding publishes the two sizes its number rests on — what one
  occurrence occupies and what the reference replacing it would occupy — so the saving in the findings
  section and the saving in the chiffrage section cannot disagree, and a reader can check the arithmetic.
  Recorded as FR-403c.

- Q: OPT-011 compares the optimized output against the original under the `contract` profile. Compared
  whole, every transformation fails it: extraction *adds* a component, and removing an unreachable
  component *removes* one, so the two things the optimizer exists to do both read as contract changes. →
  A: **the comparison excludes the component store, references resolved on both sides.** Once every
  reference has been replaced by what it designates, `components` no longer carries contract information —
  it carries the way the contract is filed. What escapes that reasoning is a security scheme, which a
  requirement names by key rather than by reference, so resolution never replaces it anywhere: that is
  checked as referential integrity alongside unresolved references, not as a contract change. Recorded as
  OPT-011a.

- Q: AUD-100 proposes extracting each repeated parameter; AUD-101 proposes hoisting the repeated header
  *set* to the path item. Both target the same objects. Applied in sequence, the second finds only `$ref`
  nodes and does nothing. → A: **two remedies for the same locations do not cumulate, and the proven one
  wins.** The applier tracks what has been touched — by containment, since a hoist's origins are operations
  and an extraction's are the parameters inside them — and declines the loser by name, with its reason.
  Application is ordered by safety class first, so the transformation the tool can prove always wins over
  the one that needs review; ordering by rule identifier gave the same answer on the reference corpus, but
  by accident of numbering. Recorded as OPT-002a.

- Q: What stops two extractions from writing to the same destination? Two groups can suggest the same
  component name, and a suggested name can collide with a component that already exists. → A: **an
  extraction refuses a destination that is already occupied**, and refuses an origin that contains its own
  destination — in the limiting case, the document root, where replacing the origin would carry away the
  whole document. Both were found by running every rule against every extension-bearing location of the
  reference corpus, which is also the test that OPT-010 demanded. Recorded as OPT-002b.

- Q: Does OPT-010 forbid an extraction that moves an object carrying a vendor extension? The data is still
  in the document, and any reader that resolves references finds it. → A: **the criterion is not the same
  for a move and for a removal.** A move loses nothing, so only the configured protected prefixes forbid it
  — that is what declaring a prefix is for: somewhere, something reads the file without resolving
  references. A removal makes the data unfindable however one reads, so *any* vendor extension forbids it,
  protected or not: the tool does not know what an extension is worth, which is exactly why it does not
  throw it away. An unreachable component carrying an `x-` key is therefore reported and never removed.
  Recorded as OPT-010a.

- Q: Where are the ruleset and the suppressions read? Reading them in the step that audits would be the
  obvious place, since that is where they are used. → A: **in the selection step, before any document is
  read.** An unjustified suppression found mid-audit leaves only bad answers: honour it and silence a
  finding nobody motivated, or ignore it and report a finding the team believed silenced. The steps that
  follow read the resolved ruleset from the run's context and never fall back to the shipped values, since
  falling back would apply a configuration nobody wrote. Recorded as FR-655a.

- Q: Does suppression belong inside the audit engine, where a rule could skip what is silenced? → A: **no
  — suppressions are applied to the findings after the rules have run.** A rule's outcome keeps counting
  what it found, and the findings list carries only what remains to deal with. Folding it into the engine
  would make "the rule found nothing" and "we told it to be quiet" indistinguishable, which is the same
  defect as reporting an unevaluated rule as clean. Recorded as FR-654a.

- Q: The extract contract says the delta report gains an Extract section. But in extract mode there is no
  candidate, so the comparison report is never written and the section has nowhere to go. Writing the
  comparison report anyway would fill eleven sections with "Aucun". → A: **the extract gets its own
  report**, carrying input metadata, validation, the extract section, the artifacts and the limitations —
  the same shape as the audit report, for the same reason: a comparison that says nothing reads as a
  comparison that found nothing, not as one that never happened. Recorded as FR-910a.

- Q: A path-level parameter is often written as a `$ref`. Taking a parameter's identity from the written
  node leaves such a parameter nameless in the inheritance record — and, worse, an operation that
  redeclares the same parameter inline does not mask it. → A: **a parameter's identity is taken after
  following a local reference.** Without it the effective operation would carry two parameters where the
  document sees one, and the comparison that FR-910 rests on would compare the wrong thing. Recorded as
  FR-905a.

- Q: `nullable` was given the value-aware rule written for `required`. The two have opposite polarity:
  `required: true` restricts, `nullable: true` permits. The tool was therefore announcing a loosening as a
  breaking change, under the change id `PARAMETER-REQUIRED-ADDED`. → A: **a permission flag is its own
  rule.** Losing the permission to be null is breaking; gaining it is not. Adding `nullable: false` to a
  field that had none restates OpenAPI 3.0's default and is correctly non-breaking — which is why it is not
  a tightening in the mutation matrix. Recorded as FR-301a.

- Q: AUD-303 quoted the whole server URL in its message, and a server URL sometimes carries a credential in
  its authority. The report was a second leak. → A: **a finding names the location and the pattern, never
  the value.** The pointer already locates it, which is what a pointer is for. FR-607 is now enforced by an
  assertion over every file a run writes, and `ValueSummary` — which exists to carry, bounded, what the
  document said — is registered with the redaction registry. Recorded as FR-607a.

- Q: A chain of ten thousand schemas each referencing the next made the reference walk recurse ten thousand
  frames deep and die of a stack overflow. The shape is plausible — a linked list of types produces it. →
  A: **the reference walk carries its own depth bound and reports it**, as FR-010 requires of every budget.
  It is distinct from the reference *count*: one counts references, the other measures how many are chained.
  The rules that depend on the graph then declare themselves unevaluated, which is the tool's own discipline
  applied to its own limit. Recorded as FR-010a.

## Requirements *(mandatory)*

### Functional Requirements

Rule identifiers in this section are part of the product's output contract: they appear in reports, in
configuration and in suppressions, so they are requirements rather than numbering. Identifiers follow the
source product requirements document so that the two can be read together. `FR-0xx` covers reading the
inputs, `FR-1xx` comparison, `FR-2xx` dependency resolution, `FR-3xx` versioning, `FR-4xx` fingerprinting,
`FR-5xx` the corrected description and optimization behaviour, `FR-6xx` reporting, `FR-7xx` running,
`FR-8xx` the boundaries of the feature, `FR-9xx` extracting a chosen subset. `AUD-xxx` names an audit rule
and `OPT-xxx` an optimization rule.

**Reading the inputs**

- **FR-001**: The tool MUST load descriptions written as JSON, YAML or YML, and MUST detect the actual format
  from the content when the file extension disagrees with it.
- **FR-002**: The tool MUST support OpenAPI 3.0.x and 3.1.x, and MUST refuse any other declared version with
  a diagnostic naming the versions it supports.
- **FR-003**: The tool MUST report parse errors with file and, where the parser exposes them, line and
  column.
- **FR-004**: The tool MUST validate required fields, permitted shapes, path templates, response keys,
  component names and reference structure against the declared OpenAPI version.
- **FR-005**: The tool MUST detect unresolved, structurally invalid and disallowed references, and MUST NOT
  treat a reference cycle that is legal for schemas as an error.
- **FR-006**: External references MUST be disabled by default. Resolution of local-file references and
  resolution of network references MUST each require separate, explicit enablement, and network resolution
  MUST additionally require a host allow-list.
- **FR-006a**: When network resolution is enabled, the tool MUST restrict schemes, apply timeouts and
  response-size limits, and MUST NOT follow references into loopback, link-local or private address ranges
  without explicit confirmation.
- **FR-006b**: The tool MUST reject file references that resolve outside the configured root unless that was
  explicitly allowed.
- **FR-007**: The tool MUST detect duplicate mapping keys before the document is turned into an object
  model, and MUST treat them as errors. Many parsers silently keep one value, so detection after
  deserialization is not detection at all.
- **FR-008**: The tool MUST report suspicious encoded text, such as HTML entities, in fields where decoded
  text is expected, and MUST NOT silently decode or rewrite it.
- **FR-009**: When either input cannot be read faithfully under FR-003, FR-004, FR-005 or FR-007, the tool
  MUST NOT publish a comparison, an audit, a corrected description or an optimized description. A confident
  report built on a misread document is indistinguishable from a correct one, which makes it worse than no
  report.
- **FR-010**: The tool MUST treat both inputs as untrusted and MUST enforce configured maximum document
  size, nesting depth and reference count, reporting the limit it refused on.
- **FR-010a**: The reference walk MUST enforce a maximum chain depth, distinct from the reference count,
  and MUST report it when reached. Exhausting it MUST produce a diagnostic naming the limit, never a stack
  overflow.
- **FR-011**: The tool MUST parse each input exactly once per run.
- **FR-012**: The tool MUST NOT call any endpoint described by either document, and MUST NOT execute
  anything carried in a vendor extension or embedded in the document.

**Comparing the two descriptions**

- **FR-100**: The comparison MUST cover the whole document: the OpenAPI version, `info`, `servers`, `paths`,
  `components`, security, `tags`, `externalDocs` and every specification extension.
- **FR-101**: The comparison MUST cover title, description, terms of service, contact, license, version and
  extensions within `info`. The version field is the version engine's input and MUST NOT mask any other
  change in `info`.
- **FR-102**: The comparison MUST detect added, removed and modified paths, and added, removed and modified
  operations for GET, PUT, POST, DELETE, OPTIONS, HEAD, PATCH and TRACE.
- **FR-103**: For each operation the comparison MUST cover tags, summary, description, operation id,
  parameters, request body, responses, callbacks, the deprecation flag, security, servers and extensions.
- **FR-104**: Parameter identity MUST be the pair of name and location. Once identified, the comparison MUST
  cover description, requiredness, deprecation, empty-value handling, style, explode, reserved-character
  handling, schema, example, examples, content and extensions.
- **FR-105**: Request-body comparison MUST cover requiredness, description, media types, schemas, encoding,
  examples and extensions.
- **FR-106**: Response comparison MUST cover status codes, the default response, descriptions, headers,
  links, media types, schemas, examples and extensions.
- **FR-107**: Schema comparison MUST cover type, format, title, description, properties, required sets,
  enums, constants, defaults, examples, nullability, read-only and write-only flags, deprecation,
  discriminator, XML metadata, external documentation, numeric, string, array and object constraints, and the
  composition and conditional keywords available in the declared version.
- **FR-108**: Component comparison MUST cover schemas, parameters, headers, request bodies, responses,
  security schemes, examples, links, callbacks, and path items where the declared version supports them.
- **FR-109**: Security comparison MUST cover global and operation-level requirements, every scheme and every
  flow and scope. A difference in the order of a scope list MUST NOT be reported as a change.
- **FR-110**: Every vendor extension MUST be compared as a structured value, not as text. Unknown extensions
  MUST be treated as potentially behaviour-affecting.
- **FR-111**: The comparison MUST ignore differences with no defined semantic effect — indentation,
  comments, line endings, mapping order, quoting style and the order of set-like collections — and MUST
  preserve order where order carries meaning, including security requirement alternatives and composition
  arrays whose order tooling may depend on.
- **FR-112**: Every reported difference MUST carry a stable change identifier, a machine-readable location,
  a human-readable location, a summary of the previous value, a summary of the new value, a classification
  of breaking, non-breaking, documentation-only or unknown, a confidence, a rationale, and a recommended
  action.
- **FR-113**: A single underlying change MUST be reported once, attributed to the most specific location
  that explains it. A changed property MUST NOT also be reported as a change to each object containing it.
- **FR-114**: A difference the tool cannot classify confidently MUST be classified unknown with its
  rationale, and MUST NOT be reported as non-breaking.

**Resolving what a change depends on**

- **FR-200**: For every operation included in a corrected description, the tool MUST recursively collect
  every referenced parameter, schema, request body, response, header, example, link, callback and security
  scheme, including nested references.
- **FR-200a**: A difference located under a component MUST put that component into the dependency closure
  directly, in addition to the components reached by following references.
- **FR-201**: Traversal MUST use a visited set keyed by canonical reference identity so that cycles
  terminate.
- **FR-202**: A generated corrected description and a generated optimized description MUST contain no
  unresolved internal reference.
- **FR-203**: An allowed external reference that remains external MUST be preserved exactly as written and
  MUST be listed in the dependency report.
- **FR-204**: When both documents define the same component name with different semantics, the tool MUST
  report the conflict and MUST NOT silently overwrite the production component.
- **FR-205**: A document within the configured size and depth limits MUST NOT cause the run to fail through
  traversal depth.

**Recommending the version**

- **FR-300**: The production version is the baseline. The tool MUST parse major, minor and patch components,
  and MAY accept pre-release and build metadata. An unparseable baseline MUST be reported, and a fallback
  MUST apply only when one was explicitly configured.
- **FR-301**: The tool MUST recommend a patch increment for documentation-only and behaviour-neutral
  metadata changes.
- **FR-301a**: A permission flag and a requirement flag have opposite polarity and MUST NOT share a
  classification rule. Losing the permission to be null is breaking; gaining it is not; and stating a
  version's own default explicitly changes nothing.
- **FR-302**: The tool MUST recommend a minor increment for backward-compatible additions, including a new
  path or operation, an optional parameter or request property, a new response that does not replace an
  existing success contract, a new schema used only by new functionality, and a demonstrably compatible
  widening of accepted input.
- **FR-303**: The tool MUST recommend a major increment for breaking or restrictive changes, including a
  removed or renamed path or operation, a newly required parameter, body or property, a removed response or
  media type, a narrowed constraint, a removed enum value, an incompatible type or format change, a stronger
  security requirement, a changed parameter location, and an incompatible change to a success-response
  schema.
- **FR-304**: Where the impact of a change depends on policy rather than on the specification — notably an
  added enum value and an added response — its classification MUST be configurable.
- **FR-305**: The recommendation MUST be the highest impact detected: major over minor, minor over patch.
- **FR-305a**: A change whose impact could not be established (FR-114) MUST NOT be allowed to produce a
  patch recommendation, and MUST NOT by itself produce a major one. It raises the floor to minor — something
  changed, and it was not documentation — and the recommendation MUST additionally be reported as requiring
  review, naming every unclassified change. Treating the unknown as breaking would make a changed vendor
  extension force a major release and teach the team to ignore the tool; treating it as compatible would be
  the one mistake that is discovered by a caller rather than by a reviewer.
- **FR-306**: When no semantic change exists, the tool MUST retain the current version and report the
  recommendation as none.
- **FR-307**: A generated corrected or transformed description MUST carry the recommended version unless a
  version was explicitly supplied. A supplied version lower than the recommendation MUST require a
  deliberate override and MUST be reported prominently.
- **FR-308**: The report MUST state which versioning policy was in effect, including every configurable
  choice it resolved.

**Auditing production**

- **FR-410**: Every finding MUST carry its rule identifier, severity, safety class, message, a
  machine-readable location, a human-readable location, every other location belonging to the same group,
  an estimate of the bytes a remedy would save where that is meaningful, whether an automatic fix exists,
  and a recommended action.
- **FR-411**: Severities MUST be error, warning, information or opportunity. Audit findings MUST be reported
  separately from contract changes and MUST NOT be merged into them.
- **FR-412**: Every finding and every proposed transformation MUST carry exactly one safety class:
  - **SAFE** — the tool holds evidence that applying the remedy preserves the API contract and every vendor
    extension.
  - **REVIEW_REQUIRED** — the remedy is probably useful but may change generated clients, documentation,
    governance or gateway behaviour.
  - **UNSAFE** — reported only. The tool MUST NOT generate an automatic transformation for it. A finding
    with no automatic remedy is classified this way.
- **FR-413**: Only SAFE transformations MAY be applied when application is requested.
- **FR-414**: A REVIEW_REQUIRED transformation MUST NOT be applied unless it was allowed by naming its rule
  identifier explicitly.
- **FR-415**: An UNSAFE finding MUST NOT produce an automatic transformation under any configuration.
- **FR-416**: A rule MUST NOT state as fact what it has not established. Occurrences MUST be counted and
  equivalence MUST be verified before an automatic fix is offered; a heuristic conclusion MUST be reported as
  a suggestion with its reasoning.
- **FR-417**: The audit MUST run against a single production description, with no candidate description
  present.
- **FR-418**: When a rule cannot be evaluated — because scope was unreadable, an external reference was not
  resolvable, or a limit was reached — the tool MUST report that the rule was not evaluated. It MUST NOT
  report a clean result it did not establish, and MUST NOT present the audit as complete.
- **FR-419**: Rule identifiers MUST remain stable across releases. A withdrawn rule's identifier MUST NOT be
  reused for a different rule.

*Validity and correctness rules*

| Rule | Reports | Default severity | Safety of the remedy |
|------|---------|------------------|----------------------|
| AUD-001 | Violations of the declared OpenAPI version's structure | error | report-only |
| AUD-002 | Every unresolved reference, and every location using it | error | report-only |
| AUD-003 | Components unreachable from paths, webhooks, security requirements and other components | warning | SAFE once reference analysis is complete and no keep rule matches, otherwise REVIEW_REQUIRED |
| AUD-004 | A path placeholder with no required path parameter, and a declared path parameter absent from the template | error | report-only |
| AUD-005 | Missing or duplicated operation identifiers, per configured policy | error for duplicates, warning for missing | report-only |
| AUD-006 | Operations with no responses, missing response descriptions, invalid response keys, duplicate semantic status codes, inconsistent standard error responses | warning | report-only |
| AUD-007 | Body-bearing operations that do not state that the body is required | warning | REVIEW_REQUIRED |
| AUD-008 | 3.0 nullability constructs in a 3.1 document, and 3.1 union-with-null constructs in a 3.0 document | warning | REVIEW_REQUIRED |
| AUD-009 | Invalid type and format combinations, and suspicious date, date-time, UUID, URI, email and binary declarations | warning | REVIEW_REQUIRED |
| AUD-010 | Empty external-documentation URLs, empty descriptions where policy requires content, empty scope descriptions, empty tags, empty servers, and placeholders left unresolved where they are not allowed | warning | REVIEW_REQUIRED |

- **AUD-006a**: AUD-006 MUST NOT look for two keys of the same `responses` map that resolve to the same
  status code. The loader refuses such a document as carrying a duplicate key before the audit sees it, so
  the defect is reported by `LOAD-DUPLICATE-KEY` at read time, not by a rule that could never fire.
- **AUD-006b**: AUD-006's report of inconsistent error responses MUST consider only codes the ruleset does
  not declare standard. Departure from the configured standard is AUD-300's subject, and the two rules
  MUST NOT report the same operation for the same code.

*Redundancy rules*

| Rule | Reports | Default severity | Safety of the remedy |
|------|---------|------------------|----------------------|
| AUD-100 | Identical inline parameters repeated across operations or paths, grouped by fingerprint, with occurrence count, locations and estimated saving | warning | SAFE under strict equivalence |
| AUD-101 | Identical groups of headers repeated across operations, with the reusable or path-level alternative | warning | SAFE where scope and override semantics remain equivalent |
| AUD-102 | Identical reusable response candidates, including the standard 400, 401, 403, 404 and 500 responses | warning | SAFE under strict equivalence |
| AUD-103 | Identical inline request bodies and media-type definitions | warning | SAFE under strict equivalence |
| AUD-104 | Exact semantic duplicate schemas, reported separately from near-duplicates | warning for exact, information for near | SAFE for an exact duplicate under the same name where policy excludes generator-naming impact; REVIEW_REQUIRED for differently named duplicates; report-only for near-duplicates |
| AUD-105 | Exact duplicate examples and headers suitable for shared components | information | SAFE under strict equivalence |
| AUD-106 | Identical enum definitions | information | REVIEW_REQUIRED — names may carry different domain meaning |
| AUD-107 | Components differing only in description, title, example or name, with the differing fields named | information | REVIEW_REQUIRED |

*Consistency and formatting rules*

| Rule | Reports | Default severity | Safety of the remedy |
|------|---------|------------------|----------------------|
| AUD-200 | Departures from the configured indentation, line width, final newline, trailing whitespace, quoting policy and line endings | information | SAFE |
| AUD-201 | Unstable ordering of top-level fields, paths, methods, response codes and components | information | SAFE, and MUST NOT reorder a semantically ordered array |
| AUD-202 | Inconsistent quoting of response keys | information | SAFE |
| AUD-203 | Header names differing only in casing | warning | report-only — the tool MUST NOT merge casing variants |
| AUD-204 | Path style, component names, operation identifiers, tags and property names that depart from configured conventions | information | report-only |
| AUD-205 | Descriptions containing a configured deprecation phrase while the deprecation flag is absent | warning | REVIEW_REQUIRED |
| AUD-206 | Likely date and date-time strings carrying no format | information | REVIEW_REQUIRED |
| AUD-207 | HTML-escaped markup, suspicious encoding, duplicated descriptions, obvious placeholders and configured terminology problems | information | report-only |
| AUD-208 | Required properties that do not exist, impossible requiredness and nullability combinations, and arrays without an item schema | error | report-only |
| AUD-209 | Inconsistent object openness, where generated contracts may differ unexpectedly | information | report-only |
| AUD-210 | Known vendor-extension structures that depart from their configured schema; unknown extensions remain opaque | warning | report-only |

*Maintainability and governance rules*

| Rule | Reports | Default severity | Safety of the remedy |
|------|---------|------------------|----------------------|
| AUD-300 | Operations that do not use the configured standard headers, security, error responses or tags | information | report-only |
| AUD-301 | The same header required in some comparable operations and optional in others | warning | report-only |
| AUD-302 | Operations that unexpectedly override or disable global security, including an empty security requirement | warning | report-only |
| AUD-303 | Configured server and environment placeholders that are missing, malformed or unknown | error or warning by policy | report-only |
| AUD-304 | Gateway configuration, catalog names, property references and target-URL placeholders that are inconsistent, reported without altering them | warning | report-only |
| AUD-305 | Unused tags, unreferenced security schemes, and components that have no effect until referenced | information | report-only |
| AUD-306 | Secret-shaped values in examples, defaults and server URLs — the location and the pattern named, the value never | warning | report-only |

**Deciding that two things are the same**

- **FR-400**: For duplicate detection, each candidate MUST be reduced to a canonical representation that
  removes source-location metadata, normalizes mapping order, normalizes set-like arrays, preserves
  semantically ordered arrays, resolves local references only where the selected comparison mode requires
  structural equivalence, and retains every validation keyword and vendor extension by default.
  Documentation-only fields MAY be excluded for near-duplicate analysis and MUST NOT be excluded when
  establishing the strict equivalence that qualifies a transformation as SAFE.
- **FR-401**: Each canonical representation MUST produce a stable fingerprint, equal for canonically
  identical candidates, different for others, and collision-resistant enough to group duplicates on
  fingerprint alone.
- **FR-402**: Three equivalence profiles MUST exist: `strict`, covering every field and extension;
  `contract`, excluding clearly non-contractual documentation fields while retaining constraints, extensions
  and examples according to policy; and `shape`, comparing structure for suggestion purposes only. Only
  `strict` equivalence MAY qualify a transformation for automatic application by default.
- **FR-403**: Every deduplication candidate MUST report its current occurrence count, its current estimated
  serialized size, the estimated size of a shared component plus its references, the estimated absolute and
  percentage saving, the occurrence count at which extraction breaks even, and its safety class.
- **FR-403a**: A candidate *below* its break-even count MUST report a saving of zero or less and MUST NOT be
  recommended. A candidate whose occurrences are no longer than the reference replacing them MUST report no
  break-even count at all, because extraction never pays for it.
- **FR-403b**: The component family an extraction targets MUST be read from the location of the repeated
  object, not from the rule that reported it. A location designating no family MUST yield no transformation
  proposal.
- **FR-403c**: The rule that establishes a repetition MUST publish the two sizes its saving rests on — what
  one occurrence occupies and what its replacing reference would occupy — and the plan MUST derive its
  figures from them rather than measuring again.

**The corrected description**

The output of a comparison is production itself, corrected — one complete document carrying what the
developer chose. There is no fragment output. A fragment was a weaker form of the same thing: it made a
reviewer do by hand the merge the tool can do and prove, and it could pass every check while describing a
file nobody had assembled.

- **FR-500**: The tool MUST produce **the complete corrected production description** — production exactly
  as it was, plus the changes the developer selected — and MUST NOT produce a fragment. The whole document
  is what gets reviewed, committed and published, so the whole document is what the tool is responsible
  for.
- **FR-500a**: The corrected description MUST be written by reassembling production's own bytes around what
  changes, which requires production to be written in YAML. A production written in JSON MUST be refused for
  this mode alone, with a diagnostic saying why: inserting into it would mean rewriting it entirely, and a
  complete rewrite would bury the requested change — which is what FR-506 exists to prevent. Comparison,
  audit and costing remain available on a JSON production.
- **FR-501**: The corrected description MUST be built **after** the comparison, from a selection the
  developer makes over what it found. Comparing first costs nothing extra — the dependency closure resolves
  over the whole document anyway, because schemas are shared between routes — and it is what lets the
  developer choose in full view of each difference's impact and confidence. A selection made before the
  comparison would throw away the one thing the comparison is for.
- **FR-502**: The selectable list MUST be organised by route and MUST include every route of the candidate,
  including those that carry no difference, each marked with what it would bring. A developer looking for a
  route MUST always find it and be told why there is nothing to carry, rather than having to tell "it
  already matched" from "the tool missed it" by themselves.
- **FR-503**: The selection MUST admit a whole route, and MUST also admit **one difference at a time** on a
  route. A developer who wants a parameter's requiredness brought over but not a type change on the same
  operation MUST be able to say exactly that; a tool that carried the operation wholesale would be deciding
  on their behalf what it was not asked to decide.
- **FR-504**: The selection MUST admit any number of items at once — several routes, several differences, or
  a mixture — and selecting one MUST NOT exclude another. An empty selection MUST produce no document rather
  than a document of everything.
- **FR-505**: Before anything is written, the tool MUST state what it is about to insert, item by item, and
  the developer MUST be able to accept or go back. FR-550 already makes writing over production require an
  explicit confirmation and FR-553 already makes an applied optimization record what it did; what is new is
  that the statement comes *before* the write and is the thing the developer accepts.
- **FR-506**: Everything production said MUST come through unchanged **except at the points the selection
  names**. The document MUST be written from production's own syntax tree rather than from any semantic
  model, and every vendor extension MUST be exactly as production had it. A corrected description whose
  untouched parts had quietly moved would be worse than none, because its reader would have no way to know
  which parts to check.
- **FR-507**: The selected content MUST be carried from the candidate's own bytes. Both inputs are OpenAPI
  descriptions, so nothing is synthesised and nothing is approximated; a document that invented a schema
  would be a defect, not an honest gap.
- **FR-508**: Whatever the selection carries, the result MUST be complete: the recommended version under
  FR-307, every new dependency its contents require under FR-200, and complete objects wherever carrying a
  field alone would be ambiguous. A reference that does not resolve within the document MUST NOT exist, per
  FR-202.
- **FR-509**: A selected operation whose only change is an internal removal MUST be carried whole, and what
  disappears MUST be stated, because omitting a breaking change is the one defect this feature cannot have.
- **FR-510**: Removals MUST be carried only when removals were explicitly requested. This governs the
  *subject*: a path, operation or component present in production and absent from the candidate MUST NOT be
  removed unless asked for. It does not govern the internal shape of an object the selection already
  carries.
- **FR-511**: Audit and optimization changes MUST NOT be carried unless they were explicitly combined in.
  Functional change and cleanup stay separate by default, so that a review remains about one thing.
- **FR-512**: Every element the tool added or changed MUST be individually marked, by a single named
  extension member, carrying the operation it came from and — where it replaced something production
  already said — what was replaced. A reader MUST be able to tell, element by element and without
  consulting the report, what came from production, what came from the candidate, and what was overwritten.
  A modification that recorded only its result would leave a reviewer unable to see what it cost.
- **FR-513**: A selected change the comparison classified breaking MUST be marked as breaking in the
  document as well as in the report. A developer may legitimately want it; they MUST NOT be able to carry
  it without having been told.
- **FR-514**: Before the document is published, the tool MUST verify it against the bytes it would write:
  reparse it, revalidate it against its declared version, resolve every reference, assert that everything
  the selection did **not** name is byte-identical to what production said, assert that every point the
  selection **did** name differs only in the ways that selection named — no more and no fewer — and assert
  that every added or changed element carries its mark. If any of that cannot be established, nothing is
  published and the run is unsuccessful. This is what makes the result reviewable: the diff a developer
  reads is the selection they made, and nothing has arrived alongside it.
- **FR-515**: The document MUST declare at its own level that it is a proposed correction and not yet a
  published contract. It MUST be written beside the reports under its own name, and it MUST NEVER be written
  over production — there MUST be no in-place path for it under any flag, mode or confirmation.
- **FR-516**: The document MUST carry no verdict, no outcome code and no finding. The report remains the
  only place a verdict is said, and it MUST name the document, list the selection item by item, and name
  every route selected with nothing to carry.
- **FR-517**: The selection MUST be part of the run's selection for the purposes of FR-702: it MUST survive
  a restart, a resumed run MUST re-read its inputs and rebuild the same document rather than ask again, and
  the checkpoint MUST NOT hold any description content.
- **FR-518**: For identical inputs and an identical selection, the document MUST be byte-identical across
  runs apart from the one delimited run-metadata block that governs every other artifact.

**Optimizing production**

| Rule | Transformation | Default | Safety |
|------|----------------|---------|--------|
| OPT-001 | Serialize canonically, with configured indentation, quoting, line endings and key order | enabled | SAFE |
| OPT-002 | Extract strictly equivalent repeated parameters into shared components, preserving override behaviour and insertion scope | enabled | SAFE |
| OPT-003 | Extract strictly equivalent repeated responses into shared components | enabled | SAFE |
| OPT-004 | Extract strictly equivalent repeated request bodies into shared components | enabled | SAFE |
| OPT-005 | Extract strictly equivalent repeated headers and examples into shared components | enabled | SAFE |
| OPT-006 | Extract inline schemas into shared components | disabled | REVIEW_REQUIRED — client generators may derive different type names |
| OPT-007 | Remove components proven unreachable and matched by no keep pattern | enabled | SAFE only with an external-reference index; REVIEW_REQUIRED without one |
| OPT-008 | Move parameters shared by every operation of a path up to the path item | enabled | SAFE only where override behaviour and the generated result stay equivalent |
| OPT-009 | Add the deprecation flag where a configured deprecation phrase is present | disabled | REVIEW_REQUIRED — it changes published metadata |
| OPT-010 | Never remove, merge or relocate vendor-extension data unless a vendor-specific rule proves equivalence | always in force | constraint on every other rule |
| OPT-011 | Verify after transforming: reparse, revalidate, resolve every reference, and compare against the pre-optimization document under the contract profile | always in force | constraint on every other rule |
| OPT-012 | Write a manifest of every transformation, with origin, destination, fingerprint and reversibility | always in force | constraint on every other rule |

- **OPT-002a**: Two transformations whose origins overlap MUST NOT both be applied. The one the tool can
  prove safe takes precedence, and the other MUST be reported as declined with its reason.
- **OPT-002b**: An extraction MUST refuse a destination that already exists, and MUST refuse an origin that
  contains its own destination.
- **OPT-010a**: A transformation that *relocates* an object MUST refuse it when it carries an extension
  under a configured protected prefix. A transformation that *removes* an object MUST refuse it when it
  carries any vendor extension at all.
- **OPT-011a**: The equivalence comparison MUST resolve local references on both sides and MUST exclude the
  component store. Every security scheme a requirement names MUST still be declared, and a scheme that is
  not MUST be reported as an unresolved reference rather than as a contract change.

- **FR-550**: The production file MUST be left byte-identical by default. Writing over it MUST require both
  an explicit in-place request and an explicit confirmation naming the file, MUST create a backup unless a
  backup was explicitly declined, and MUST be refused if the file changed on disk since it was read.
- **FR-551**: Applying optimizations MUST apply SAFE transformations only, MUST apply a REVIEW_REQUIRED
  transformation only when its rule identifier was explicitly allowed, and MUST never apply an UNSAFE one.
- **FR-552**: When the verification of OPT-011 cannot establish equivalence, the tool MUST NOT publish an
  optimized description, MUST report why, and MUST report the run as unsuccessful. A partial optimized output
  MUST NOT be emitted as a successful artifact.
- **FR-553**: When optimization is applied, the tool MUST write the manifest of OPT-012 alongside the
  optimized description.
- **FR-554**: No transformation MAY change API behaviour in order to reduce size, and none MAY remove a
  deprecated operation on the grounds that it is deprecated.
- **FR-555**: When the optimized description would exceed a configured maximum size or growth allowance, the
  tool MUST report that and MUST NOT publish it.
- **FR-556**: The report MUST state that regenerated output does not preserve comments, anchors or the
  original key order, and that the original file is therefore the only place those survive.

**Reporting**

- **FR-600**: The human-readable report MUST contain, in this order: an executive summary; input metadata and
  hashes; validation status; the recommended version change; breaking changes; added operations; removed
  operations; modified operations; component changes; security changes; vendor-extension changes; production
  audit findings; redundancy groups; safe optimization proposals; review-required proposals; estimated file
  size reduction; suppressed findings; generated artifacts; and limitations and assumptions.
- **FR-601**: The machine-readable report MUST carry run metadata, diagnostics, differences, classifications,
  the version recommendation, audit findings, optimization proposals, artifact paths, and before-and-after
  hashes and sizes, under a stable field layout.
- **FR-602**: Both reports MUST identify each input by path, size and content hash.
- **FR-603**: Differences MUST appear in both reports with the fields of FR-112, and findings with the
  fields of FR-410.
- **FR-604**: For identical inputs and identical configuration, every artifact MUST be byte-identical across
  runs and machines, except within one clearly delimited run-metadata block that may carry the time, the
  paths and the correlation identifier of the run.
- **FR-605**: Artifacts MUST be written under their documented default names — a human-readable delta
  report, the corrected description, a machine-readable delta report, an audit report, an optimization plan,
  and, only when application was requested, an optimized description — into the chosen output directory.
- **FR-606**: Every report MUST state its limitations, including what was not evaluated under FR-418 and
  which references were left unresolved by policy.
- **FR-607**: No credential, token or secret MUST appear in any artifact or log. Redaction MUST happen where
  the artifact is written, not at each call site, and the tool MUST offer optional auditing for
  secret-shaped values in examples and server URLs.
- **FR-607a**: A finding about a secret-shaped value MUST name its location and the pattern recognised, and
  MUST NOT reproduce the value. No rule message MUST quote a value from the document.
- **FR-608**: Every report MUST state which rules were disabled, which suppressions were applied, and which
  suppressions have expired.

**Configuring and suppressing**

- **FR-650**: Every rule MUST be configurable by identifier, at least for severity, enablement and the
  thresholds the rule defines, such as a minimum occurrence count.
- **FR-651**: Configuration MUST cover protected vendor-extension prefixes, deprecation phrases, naming
  conventions, standard headers and responses, environment placeholders, canonical formatting options,
  external-reference policy, size and growth limits, and the versioning choices of FR-304.
- **FR-652**: A suppression MUST carry a rule identifier, an exact or pattern location, a justification, an
  owner and an optional expiry. When justification is required by configuration, a suppression lacking one
  MUST be rejected as invalid configuration before the run begins.
- **FR-653**: An expired suppression MUST NOT be honoured, and MUST be reported.
- **FR-654**: A suppressed finding MUST be listed with its justification and owner, never silently dropped.
- **FR-655**: Invalid configuration MUST be reported before any work begins, naming the file and the
  offending entry. The tool MUST NOT fall back to defaults silently.
- **FR-654a**: Suppression MUST be applied to findings after the rules have run. A rule's outcome MUST keep
  reporting what the rule found, so that a silenced finding and an unevaluated rule stay distinguishable.
- **FR-655a**: Configuration MUST be read and validated before any description is read. Steps after the
  selection MUST use the resolved ruleset and MUST NOT fall back to the shipped values.
- **FR-656**: The tool MUST produce usable results with no configuration file present.

**Running it**

- **FR-700**: The tool MUST offer the modes compare, correct, audit, optimize, extract, and everything. The
  everything mode MUST NOT apply optimizations unless application was explicitly requested, and MUST NOT
  extract: an extract requires a selection, and there is no selection to infer.
- **FR-701**: Compare and correct MUST require both a candidate and a production description; audit and
  optimize MUST require only production; extract MUST require one description and a non-empty selection of
  operations. A missing or unreadable input, or an empty selection, MUST be reported as a usage error
  before any work begins.
- **FR-702**: A run MUST be an ordered sequence of named, individually recoverable steps rather than one
  monolithic operation.
- **FR-703**: On failure the developer MUST be offered, at least: retry the failed step, continue from the
  failed step, restart the whole run, and abort. Abort MUST leave a described, consistent state.
- **FR-704**: Long steps MUST report progress, and the run MUST remain cancellable throughout. Cancellation
  MUST leave no artifact that appears complete.
- **FR-705**: An interrupted run MUST be discoverable and resumable on the next launch. Resuming MUST re-read
  both inputs; it MUST NOT restore document content, differences or findings from the checkpoint. A
  checkpoint MUST hold only the selection — the inputs' identity and content hashes, the mode and the
  options — and MUST NOT hold document content, findings or secrets. When an input's hash no longer matches
  the checkpoint, resuming MUST be refused and a fresh run offered.
- **FR-706**: Stale checkpoints MUST expire under a documented retention policy.
- **FR-707**: Each run MUST report its outcome as success, failure or cancellation, with its duration. The
  threshold at which findings make a run unsuccessful MUST be configurable over at least: never, on warning,
  on error, and on a breaking change.
- **FR-708**: Each run MUST resolve a documented, stable outcome code, distinguishing at least: nothing at
  or above the threshold; the threshold exceeded; a breaking change where that is configured to fail;
  invalid usage or a missing file; a parsing or reference-resolution failure; an output or verification
  failure; and an unexpected internal error. The code MUST be displayed when the run ends and MUST appear in
  the machine-readable report.
- **FR-708a**: This release is driven through the toolbox's own interface. The tool MUST NOT require a
  non-interactive entry path, and MUST NOT present its outcome code as the process exit code. The vocabulary
  of FR-708 is fixed now precisely so that automated invocation, when it is specified as its own feature,
  adds a surface rather than a meaning.
- **FR-709**: Every write outside the chosen output directory MUST be confirmed explicitly.
- **FR-710**: Each run MUST be identifiable by a correlation identifier that appears in its logs and in its
  machine-readable report, so that one execution can be reconstructed afterwards.

**Extracting a chosen subset**

- **FR-900**: The tool MUST be able to produce, from one description and a developer-chosen set of
  operations, a standalone OpenAPI description containing those operations and nothing that is not required
  by them.
- **FR-901**: Selection MUST be possible per operation — a path and a method — and not only per path, and
  MUST accept several operations across several paths in one pass.
- **FR-902**: The tool MUST present the selectable operations with their path, method, summary and tags,
  and MUST let the developer narrow a long list before choosing. A description of a thousand operations is
  not a list to be scrolled.
- **FR-903**: The extract MUST be a valid OpenAPI description of the same declared version as its source,
  carrying the version field, `info`, and the selected paths.
- **FR-904**: The extract MUST contain the transitive closure of everything the selected operations
  reference, resolved under FR-200, and MUST contain no unresolved internal reference.
- **FR-905**: The extract MUST preserve what each selected operation inherits from its context: the
  parameters declared on its path item, the servers declared at document and path level, and the global
  security requirement together with the schemes it names. **An operation that was authenticated MUST NOT
  become unauthenticated by being extracted.**
- **FR-905a**: A parameter's identity, for deciding what an operation inherits and what it masks, MUST be
  taken after following a local reference. A path-level parameter written as a reference MUST be named in
  the report and MUST be masked by an operation that redeclares the same name and location inline.
- **FR-906**: The extract MUST carry the tag definitions the selected operations use, and MUST NOT carry
  those they do not.
- **FR-907**: The extract MUST NOT contain any path, operation or component that the selection does not
  require. What was not chosen is not shipped.
- **FR-908**: The extract MUST preserve `info.version` unchanged, and the version engine MUST NOT run for
  this mode. An extract is a view of a description, not a change to it.
- **FR-909**: Vendor extensions MUST be carried over unchanged, including those at document level. Where a
  carried extension refers to a path or operation the extract does not contain, the tool MUST report that
  as a finding and MUST NOT edit the extension — FR-806 stands here as everywhere.
- **FR-910**: Before publishing an extract, the tool MUST verify its own work: the extract reparses,
  revalidates, resolves every reference, contains nothing unselected, and every selected operation is
  contract-equivalent — in its effective form, after inheritance is applied on both sides — to the same
  operation in the source. If equivalence cannot be established, no extract is published, the run is
  unsuccessful, and the report names the operation and what differed.
- **FR-910a**: The extract's own report MUST be a separate artifact carrying input metadata, validation
  status, the extract section, the generated artifacts and the limitations — never the comparison report,
  which in this mode would have nothing to compare. It MUST be written even when verification failed,
  because it is what explains the refusal.
- **FR-911**: The run's reports MUST record the extract's provenance: the source path, the source content
  hash, and every operation selected. The extract document itself MUST also carry that provenance, under a
  single clearly named vendor extension, so that a partial description cannot be mistaken for a whole one.
  Stamping MUST be configurable and MUST be enabled by default.
- **FR-912**: The source description MUST be left byte-identical. An extract is always a new artifact, and
  the in-place path of FR-550 MUST NOT be available for this mode.
- **FR-913**: The selection MUST survive a restart as part of the run selection under FR-705, and resuming
  MUST re-read the source and refuse, naming it, if a selected operation is no longer present.

**The boundaries of this feature**

- **FR-800**: The tool MUST NOT deploy a description, publish it to a gateway, or call an endpoint it
  describes.
- **FR-801**: The tool MUST NOT claim that a running implementation conforms to a description.
- **FR-802**: The tool MUST NOT resolve a genuine business conflict. A conflict is reported for a human to
  settle.
- **FR-803**: The tool MUST NOT overwrite a production description by default.
- **FR-804**: The tool MUST NOT treat two similar components as interchangeable when their observable
  semantics differ.
- **FR-805**: The tool MUST NOT remove a deprecated operation merely because it is deprecated.
- **FR-806**: The tool MUST NOT delete or alter an unknown vendor extension.
- **FR-807**: The tool MUST NOT claim that regenerated output preserves comments, anchors or original key
  order.

### Key Entities

- **Description**: one OpenAPI document in a role — candidate or production — identified by its path, its
  size, its content hash and its declared OpenAPI version.
- **Difference**: one semantic change between the two descriptions, with the fields of FR-112. It belongs to
  the contract, and never mixes with findings.
- **Classification**: the impact of a difference — breaking, non-breaking, documentation-only or unknown —
  with its confidence and its rationale.
- **Version recommendation**: the baseline version, the recommended increment, the resolved policy, and the
  differences that contributed to it.
- **Dependency closure**: the set of components reachable from the objects a corrected description carries,
  plus the external references that were left external.
- **Finding**: one audit result, with the fields of FR-410. Several occurrences of one repetition form one
  finding, not many.
- **Fingerprint**: the stable identity of a canonicalized candidate under a named equivalence profile; what
  makes two things provably the same rather than apparently similar.
- **Redundancy group**: the occurrences sharing one fingerprint, with their measured sizes, their projected
  size, their saving and their break-even count.
- **Safety class**: SAFE, REVIEW_REQUIRED or UNSAFE, as defined in FR-412. It qualifies a remedy, and it is
  what decides whether anything may be applied automatically.
- **Transformation**: one proposed or applied change to production, carrying its rule, its origin, its
  destination, its fingerprint evidence and its reversibility.
- **Optimization manifest**: the record of every applied transformation, written so that an application can
  be understood and undone.
- **Ruleset**: the configured severity, enablement and thresholds per rule identifier, plus the conventions
  and policies of FR-651.
- **Suppression**: a rule identifier, a location, a justification, an owner and an optional expiry.
- **Operation selector**: one chosen operation, identified by its path and its method — what the developer
  picked, independent of where it sits in the document.
- **Extract**: a standalone description built from a set of selectors, with its closure, its inherited
  context, its provenance and its verification. It is an artifact, never an edit.
- **Run**: one execution — its correlation identifier, its mode, its inputs' identity, its ordered steps,
  its artifacts, its duration and its outcome.
- **Artifact set**: the files a run produced, each named, located and hashed in the report.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A production description reserialized with different indentation, key order, quoting and line
  endings, compared against its original, yields zero reported differences and a version recommendation of
  none.
- **SC-002**: Across a mutation suite that varies requiredness, nullability, enum membership, type, format,
  security and constraints, no breaking mutation is ever classified non-breaking and no transformation
  touching one is ever classified SAFE — zero escapes, measured over the whole suite on every run.
- **SC-003**: A description of at least 1,000 operations and 10,000 schemas is audited within 30 seconds on
  a developer workstation, and the run reports its own duration so the claim stays checkable.
- **SC-004**: Identical inputs and identical configuration produce byte-identical artifacts across repeated
  runs and across machines, outside the delimited run-metadata block.
- **SC-005**: 100% of reported differences carry a location, a classification, a rationale and a recommended
  action; 100% of findings carry a rule identifier, a severity, a safety class, a location and a recommended
  action.
- **SC-006**: Every generated corrected description and every generated optimized description contains zero
  unresolved internal references, over every case in the maintained example set.
- **SC-007**: After any run that was not an explicit, confirmed in-place write, the production file is
  byte-identical to what it was before the run — verified on every run of the example set.
- **SC-008**: No optimized description is ever published without passing contract-equivalence verification;
  on an injected contract-altering transformation, zero artifacts are published and the run reports failure.
- **SC-009**: A duplicate mapping key is detected and reported in 100% of cases, with no value silently
  discarded.
- **SC-010**: Every vendor extension present in the input is present and unchanged in any optimized output —
  100% retention, including extensions the tool does not recognize.
- **SC-011**: A description containing reference cycles completes its audit without failure or
  non-termination.
- **SC-012**: For applied safe deduplication, the estimated saving is within 10% of the saving actually
  measured between the input and the output.
- **SC-013**: A corrected description generated without explicitly combining cleanup contains zero audit or
  optimization changes.
- **SC-014**: A developer who has two files and has read nothing reaches a report they can act on in under
  two minutes.
- **SC-015**: An interrupted run is resumable on the next launch, and the resumed run reaches the same
  artifacts as an uninterrupted one — identical outside the run-metadata block.
- **SC-016**: Zero credentials, tokens or secret-shaped values appear in any artifact or log across the
  example set, including descriptions and server URLs that contain them in the input.
- **SC-017**: Extracted one at a time, **every** selectable operation of the example description yields an
  extract that reparses, validates, resolves completely, contains no unselected path or component, and is
  contract-equivalent in effective form to the same operation in the source — zero exceptions over the
  whole set, with the source byte-identical afterwards every time.
- **SC-018**: In a description of 1,000 operations, a developer can find and select a route they can name
  in under 30 seconds, without reading the whole list.
- **SC-019**: A draft built from a chosen selection leaves every byte of production that the selection did
  not name identical to what it was, and carries every point it did name changed only as selected —
  verified element by element over the whole example set, not by sampling.
- **SC-020**: Selecting one difference out of several on the same route carries that one and leaves the rest
  of that route byte-identical, verified over every difference of the example pair one at a time.
- **SC-021**: An injected builder fault that touches an unselected route causes nothing to be published and
  the run to report an output failure.
- **SC-022**: Every element a draft added or changed carries its mark naming its origin and what it
  replaced; every selected change classified breaking is marked as breaking in the draft; and no run writes
  a draft over production under any input.

## Assumptions

- **This is a tool in the toolbox, not a second program.** The source document describes a standalone
  command-line application with its own solution layout and runtime. The toolbox hosts it as one tool among
  others, discovered and registered like the existing ones, and the host's governance decides the runtime,
  the interface and the project structure. None of the capability requirements above change as a result;
  only the delivery shape does. Where the source document and the host's constitution disagree, the
  constitution wins and this specification follows it.
- **The tool is named `apidelta`**, following the existing lowercase compound naming of the other tools.
  Nothing in this specification depends on the name; renaming it changes no requirement.
- **Adding it modifies no existing tool.** It is the third tool, and the first two should not need to change
  for it to exist.
- **Both inputs are local files the developer points at.** Fetching a description from a repository, a
  gateway or a URL is not part of this feature. External references are a separate, disabled-by-default
  concern governed by FR-006.
- **The output is the complete corrected description, and an overlay is not offered.** The source document
  offers an overlay-compatible output as an option; it is deferred, because the whole document is what gets
  reviewed and committed, and an overlay would add a second output contract to maintain before anyone has
  asked for it. The fragment output this specification originally described is gone rather than deferred —
  see the clarification of 2026-10-21.
- **The built-in rules stand alone.** The source document mentions a possible adapter for an external lint
  engine; that is out of scope here, and the built-in checks must never require another runtime to be
  installed.
- **Comment-preserving editing is out of scope.** Optimized output is a newly serialized artifact, and the
  original file is the only place comments, anchors and original key order survive. FR-556 exists so the
  report says so rather than leaving it to be discovered.
- **The shipped defaults are a working configuration.** The organization-specific values in the source
  document — a protected `x-sg-` extension prefix, a French deprecation phrase such as `Endpoint obsolète`,
  catalog and host placeholders of the form `$(catalog.host)` — ship as replaceable defaults, so the tool is
  useful before anyone writes a ruleset and is not tied to one organization afterwards.
- **Rule identifiers are an output contract**, because they appear in reports, rulesets and suppressions.
  That is why FR-419 fixes them rather than treating the catalog as documentation.
- **Delivery follows the priorities, not the source document's milestones.** The stories above are ordered
  so that P1 alone is a usable tool: a trustworthy reading of both documents and a classified comparison.
  The source document's five milestones map onto P1 and P2 (comparison, versioning, the corrected
  description), P2 (audit) and P3 (optimization, configuration); the release the source document describes
  is complete only when all of its acceptance criteria are met, which this specification covers in full.
- **An extract is a view, not a change.** FR-908 and FR-912 are what keep it one: the version does not
  move, the source is not touched, and the extract is a new file. The temptation to treat extraction as
  "production, trimmed" is what would eventually have it writing back.
- **Selecting what to keep, not what to drop.** A developer names the routes they want; an
  "everything except these" selection is out of scope for this release. Both are reasonable, but only one
  of them makes the verification of FR-910 straightforward, because the thing to prove is a property of
  what was chosen.
- **The provenance stamp is on by default.** A description trimmed to three routes looks exactly like a
  description of an API with three routes, and the difference matters the first time one is published or
  reviewed in the belief that it is complete. One clearly named extension says what the file is; nothing
  else in the document is touched, and a team that disagrees can turn it off (FR-911).
- **Automated invocation is deferred, not rejected.** FR-708 fixes the outcome codes and their meanings
  while FR-708a keeps the process-level surface out of this feature, so a pipeline-facing entry path can be
  specified later without reopening what the codes mean.
- **The 30-second target of SC-003 is for local files on a developer workstation**, with external reference
  resolution disabled, which is the default.
- **Determinism is a property of content, not of the clock.** FR-604 carves out one run-metadata block
  because a report that states when it ran cannot otherwise be byte-identical, and the example-based tests
  need something stable to compare.
