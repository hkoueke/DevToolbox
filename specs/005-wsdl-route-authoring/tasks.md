# Tasks: Authoring a route instead of deriving one

**Feature**: `005-wsdl-route-authoring` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

**Created**: 2026-10-28

## Format: `[ID] [P?] [Story] Description`

- `[P]` — parallelisable: a different file, with no dependency on an unfinished task.
- `[US1]`…`[US4]` — the user story the task serves. Setup, foundational and polish tasks carry none.

## Path Conventions

| Shorthand | Real path |
|---|---|
| `Core/` | `tools/ApiDelta/DevToolbox.Tools.ApiDelta.Core/` |
| `Infrastructure/` | `tools/ApiDelta/DevToolbox.Tools.ApiDelta.Infrastructure/` |
| `Presentation/` | `tools/ApiDelta/DevToolbox.Tools.ApiDelta.Presentation/` |
| `Tests/` | `tests/DevToolbox.Tests/Tools/ApiDelta/` |

**Tests are not optional here.** The repository's gate is zero warnings and a green suite, and
`CONCEPTION.md` now requires that every guard be seen to fail before it is trusted. Each guard-bearing
task below names the test that holds it, and the polish phase contains the mutation pass.

---

## Phase 1: Setup

No project is created and no package is added: the work lands in three projects that exist. Setup is
limited to the corpus the tests need.

- [X] T001 [P] Create `Tests/Corpus/Authoring/rpc-production.yaml` — a production whose paths carry a
  prefix and keep the operation name, grouped by family, with five header parameters declared under
  `components/parameters` and referenced by every route. This is the shape no naming rule reproduces, and
  every assertion about insertion order and reusable headers rests on it.
- [X] T002 [P] Create `Tests/Corpus/Authoring/rpc-contract.wsdl` — a contract of six operations: two whose
  input is flat scalars, two whose input is a single composite, one with no input at all, and one whose
  name yields no known verb. Six is the smallest set that covers every placement branch.
- [X] T003 Register both corpus files as embedded resources in `tests/DevToolbox.Tests/DevToolbox.Tests.csproj`

---

## Phase 2: Foundational (blocking prerequisites)

These block every story. The order matters: the records come before the rules that validate them, and the
splicer capability comes before anything can be written where it was asked to go.

- [X] T004 [P] Create `Core/Authoring/ParameterLocation.cs` — `Path`, `Query`, `Body`, `Header`,
  `Nowhere`, with the remark on why `Nowhere` is a member and not an absence (data-model)
- [X] T005 [P] Create `Core/Authoring/InsertionPoint.cs` — `AfterPath` nullable, with the remark on why it
  is a neighbour's name and not an index (RA-008, RA-009)
- [X] T006 [P] Create `Core/Authoring/OfferedHeader.cs` — component name, wire name, reference count
- [X] T007 Create `Core/Authoring/FieldPlacement.cs` with its three invariants enforced in the
  constructor: a path placement is required, a composite cannot go to path/query/header, a dropped field
  cannot be required (RA-013, RA-014)
- [X] T008 Create `Core/Authoring/AuthoredRoute.cs` — operation, path, method, placements, headers,
  insertion point, suggestion worth, whether the suggestion was taken (depends on T004–T007)
- [X] T009 Create `Core/Authoring/AuthoringRecord.cs` with `Dropped()`, `Placeholders()`,
  `Collisions(production)` and `Empty` (depends on T008)
- [X] T010 Create `Core/Authoring/RouteSyntax.cs` — the six path rules and the verb check, each failure
  naming the rule it broke and its position, none repairing the input (RA-004, RA-005)
- [X] T011 [P] Create `Tests/Authoring/RouteSyntaxTests.cs` — one case per rule, plus the JSON Pointer
  rule that came out of the STRIDE pass: a path carrying `~` must be refused **before** a pointer is built
  from it (depends on T010)
- [X] T012 Create `Core/Authoring/PlacementDefaults.cs` — `GET`/`DELETE` propose the query, the write verbs
  propose the body, a composite under a read verb arrives at `Nowhere` **flagged as needing a decision**
  (RA-011)
- [X] T013 [P] Create `Tests/Authoring/PlacementDefaultsTests.cs` — including the case that matters most:
  accepting the defaults wholesale on a composite under `GET` must not silently drop it (depends on T012)
- [ ] T014 Create `Core/Authoring/PlacementRules.cs` — the refusals, separate from the proposals, naming
  field and declared type for a composite and naming the placeholder for a mismatch (RA-013, RA-015)
- [ ] T015 [P] Create `Tests/Authoring/PlacementRulesTests.cs` — composite refused for each of the three
  locations, placeholder with no field, duplicate effective name (depends on T014)
- [ ] T016 Add `AfterKey` to `Splice` in `Core/Correction/Splice.cs`, documented as the sibling after which
  a member is added, with null keeping today's behaviour
- [ ] T017 Change `Infrastructure/Emitting/SplicingEmitter.cs` so `Adding` computes its insertion from
  `AfterKey`'s content end rather than from `mapping.Entries[^1]`, and **refuses** an `AfterKey` naming no
  existing member rather than appending (contract: application-ports) (depends on T016)
- [ ] T018 [P] Extend `Tests/Correction/SplicingEmitterTests.cs` — a member added after a named sibling
  lands there with the siblings' order preserved; an unknown `AfterKey` is refused; a null `AfterKey`
  behaves exactly as before (depends on T017)
- [ ] T019 Create `Core/Abstractions/IRouteAuthoringPrompt.cs` with `AuthoringSubject`, and the remark on
  why it is a new port rather than an extension of `ICorrespondencePrompt` (Principle II)

**Checkpoint**: at the end of phase 2, `dotnet build` is clean and the new unit tests pass. Nothing is
wired into the chain yet, so the application behaves exactly as before.

---

## Phase 3: User Story 1 — Say which route an operation becomes (P1) 🎯 MVP

**Goal**: the developer settles the path and the verb for each operation they keep, pre-filled with the
derivation, and chooses where in production it goes. No route is written that nobody settled.

**Independent test**: integrate the corpus pair, keep three operations, give each a path and verb no naming
rule would produce, and verify the written document carries exactly those three routes, at the chosen
positions, with every pre-existing path still in place.

- [ ] T020 [US1] Create `Core/Authoring/ReusableHeaders.cs` — find header components production declares
  and counts how many operations reference each, most-referenced first, empty when none (RA-021, RA-023)
- [ ] T021 [P] [US1] Create `Tests/Authoring/ReusableHeadersTests.cs` — the five headers of the corpus
  found, counted and ordered; a production declaring none yields nothing (depends on T020)
- [ ] T022 [US1] Create `Core/Steps/AuthorStep.cs` — placed after `Correspond`, runs in `Consolidate` only,
  skipped when the companion is not a contract or the developer kept nothing, writes the record to the
  context, establishes collisions before anything is written (RA-003, RA-006)
- [ ] T023 [US1] Add the step to `Core/Steps/ApiDeltaStepNames.cs` — its label in the developer's words,
  its place in `InOrder`, and its mode in `Includes` (depends on T022)
- [ ] T024 [US1] Register the step and the prompt in `Presentation/ServiceCollectionExtensions.cs`, **and
  add it to the chain** — the omission that left two steps written, registered and unreachable for two
  releases (depends on T022)
- [ ] T025 [US1] Create `Presentation/Prompts/RouteAuthoringPrompt.cs` — the path and verb questions, one
  method per question to stay under the 60-line ceiling, suggestion pre-filled with its worth named
  (RA-001, RA-002)
- [ ] T026 [US1] Add the insertion-point question to `Presentation/Prompts/RouteAuthoringPrompt.cs`, offering production's paths in the
  document's own order plus "à la fin" (RA-008, RA-009) (depends on T025)
- [ ] T027 [US1] Change `Core/Correction/CorrectionBuilder.cs` to carry the authored path, verb and
  insertion point into the splice instead of the projected route's own (depends on T016, T022)
- [ ] T028 [US1] Add the authoring record to `Core/Correction/CorrectionRequest.cs` and to
  `Core/RunSelection.cs` (depends on T009)
- [ ] T029 [P] [US1] Create `Tests/Authoring/AuthoringRecordTests.cs` — collisions between two authored
  routes, and between an authored route and a production route, each naming both sides (depends on T009)
- [ ] T030 [US1] Create `Tests/Wsdl/AuthoredRouteEndToEndTests.cs` — three operations authored onto
  unconventional paths, written there, at the chosen positions, with production's path order preserved and
  the existing verification passing (depends on T022–T028)
- [ ] T031 [US1] Extend `Tests/Application/CompositionRootTests.cs` — the built chain still equals the
  declared order, which is what catches a step registered but never wired (depends on T023, T024)

**Checkpoint**: user story 1 is independently demonstrable. A developer can integrate a contract and get
routes at the paths they chose, in the places they chose. Fields still all travel in the body.

---

## Phase 4: User Story 2 — Say where each field goes (P1)

**Goal**: every field of an authored operation is placed by the developer, with a per-verb default, and
every field is accounted for — placed or dropped.

**Independent test**: an operation with four flat fields, a `GET` verb, a path with one placeholder: one
field in the path, two in the query, one dropped. The written route declares one path parameter, two query
parameters, no body, and the report names the dropped field.

- [ ] T032 [US2] Add the placement question to `Presentation/Prompts/RouteAuthoringPrompt.cs` — the fields
  listed with their declared type and composite flag, pre-set from `PlacementDefaults`, a refusal naming
  the field and its type (RA-010, RA-011, RA-013) (depends on T012, T014, T025)
- [ ] T033 [US2] Add the requiredness question to `Presentation/Prompts/RouteAuthoringPrompt.cs` — asked once per placed field, never for a path parameter,
  default no, the question saying why it is asked (RA-012, RA-014) (depends on T032)
- [ ] T034 [US2] Change `Core/Correction/CorrectionBuilder.cs` to assemble the operation object from the
  placements: `parameters` for path, query and header; `requestBody` carrying the derived reference for the
  body; nothing for a dropped field (RA-016, RA-017) (depends on T027, T032)
- [ ] T035 [US2] Establish the placeholder/field agreement in `Core/Steps/AuthorStep.cs` before writing, naming any
  mismatch (RA-015) (depends on T014, T022)
- [ ] T036 [P] [US2] Extend `Tests/Wsdl/AuthoredRouteEndToEndTests.cs` — the four-field `GET` case above,
  plus a `POST` whose composite input becomes a body with its reference intact (depends on T034)
- [ ] T037 [P] [US2] Add the dropped-field case to `Tests/Wsdl/AuthoredRouteEndToEndTests.cs` — a field placed nowhere is absent from the document, named
  in the report, and counted (depends on T034)

**Checkpoint**: user stories 1 and 2 together are the feature. An authored route is usable without hand
editing, except for its description.

---

## Phase 5: User Story 3 — Reuse the headers production declares (P2)

**Goal**: a new route carries the same governance headers as its neighbours, by reference, without
retyping.

**Independent test**: against the corpus production, author one route, accept the offered headers, and
verify the written route references the existing components and redeclares no parameter; decline them and
verify the route carries none.

- [ ] T038 [US3] Add the header question to `Presentation/Prompts/RouteAuthoringPrompt.cs` — offered in the
  order `ReusableHeaders` returns, checked when every production route carries them, nothing shown when
  production declares none (RA-021, RA-023) (depends on T020, T025)
- [ ] T039 [US3] Write accepted headers as references to the existing components in `Core/Correction/CorrectionBuilder.cs`,
  never as redeclared parameters (RA-022) (depends on T034, T038)
- [ ] T040 [P] [US3] Extend `Tests/Wsdl/AuthoredRouteEndToEndTests.cs` — accepted headers become
  references; declined headers leave the route bare; a production with no header components offers nothing
  (depends on T039)

---

## Phase 6: User Story 4 — Not lose a long sitting (P2)

**Goal**: an interrupted sitting resumes where it left off.

**Independent test**: author three operations of six, interrupt, resume, and verify the three settled are
not asked about again and the three remaining are.

- [ ] T041 [US4] Add the two selection kinds to `Presentation/ApiDeltaSelectionKinds.cs` — one per authored
  route, one per placement, both named in the developer's words
- [ ] T042 [US4] Write and read them as strings in `Presentation/ApiDeltaCheckpointFactory.cs`, keeping
  `PersistedRun` string-only so the store's runtime shape check still passes (Principle VIII) (depends on
  T041)
- [ ] T043 [US4] Use a resumed record in `Core/Steps/AuthorStep.cs`, asking only about what is missing (RA-019) (depends
  on T022, T042)
- [ ] T044 [P] [US4] Extend `Tests/ApiDeltaCheckpointShapeTests.cs` — the new entries carry no field value
  and no typed content, and the shape check still refuses a widened type (depends on T042)
- [ ] T045 [P] [US4] Extend `Tests/ApiDeltaRecoveryTests.cs` — a sitting interrupted mid-authoring resumes
  without re-asking what was settled; a changed input file still refuses the resume by naming the file
  (depends on T043)

---

## Phase 7: The description placeholder

Its own phase because it cuts across every story and has one rule of its own.

- [ ] T046 Add the placeholder constant — `À COMPLÉTER` — in `Core/Authoring/AuthoredRoute.cs` or beside
  it, documented as fixed, searchable, and a plain `description` rather than an extension member
  (RA-018, RA-019, research §4)
- [ ] T047 Write it as the authored operation's `description` in `Core/Correction/CorrectionBuilder.cs`, composing nothing
  from the operation name or the contract's annotations (depends on T034, T046)
- [ ] T048 [P] Add the placeholder count to `Core/Reporting/MarkdownReportWriter.cs` and to the pre-accord summary in `Presentation/Prompts/CorrectionPicker.cs` (RA-020) (depends on
  T047)
- [ ] T049 [P] Extend `Tests/Wsdl/AuthoredRouteEndToEndTests.cs` — every authored route carries the
  placeholder, the count in the report equals the number of routes authored, and the document carries no
  `x-apidelta` member at all (depends on T047, T048)

---

## Phase 8: Reporting

- [ ] T050 Add the authored-routes section to `Core/Reporting/MarkdownReportWriter.cs` — one row per route
  with its operation, insertion point, suggestion worth and whether it was taken, then one placement table
  per route (RA-027, RA-028, contract: console-experience)
- [ ] T051 [P] Add the dropped-field total and the placeholder count as their own lines in `Core/Reporting/MarkdownReportWriter.cs`, so neither reading
  misses them (depends on T050)
- [ ] T052 [P] Add the same facts to `Core/Reporting/JsonReportWriter.cs` (depends on T050)
- [ ] T053 [P] Extend `Tests/Golden/AuditReportTests.cs` — the report names every authored route, every placement and every
  dropped field, and distinguishes a taken suggestion from a replaced one (depends on T050, T052)

---

## Phase 9: Withdrawing WR-105

Deletions, after the replacement works. Doing them earlier would leave the tool with neither.

- [ ] T054 Remove `DeclaredCorrespondence` from `Core/Steps/ApiDeltaContextKeys.cs`
- [ ] T055 Remove the `declared` parameter from `Core/Abstractions/ICorrespondenceProposer.cs` and
  `Core/Correspondence/SimilarityProposer.cs`, and the branch that honoured it (depends on T054)
- [ ] T056 Remove `CorrespondenceOrigin.Declared` in `Core/Correspondence/CorrespondenceOrigin.cs` and its
  rendering in `Core/Correspondence/CorrespondenceEntry.cs` (depends on T055)
- [ ] T057 Delete `Tests/Corpus/Wsdl/correspondance.yaml` and its entry in `WsdlCorpusTests.cs` and the
  test project file (depends on T056)
- [ ] T058 Record the withdrawal in `specs/004-wsdl-delta-audit/spec.md` as a dated clarification — a
  published requirement is being retired, and the reason belongs where the requirement was

---

## Phase 10: Polish and cross-cutting

- [ ] T059 **Mutation pass.** For each guard this feature adds, remove it, watch the named test go red,
  restore it: path validation, composite refusal, placeholder agreement, chosen insertion point,
  description placeholder, header references. A guard whose removal leaves the suite green is not tested,
  whatever the coverage says (quickstart)
- [ ] T060 Clear the screen on entering authoring in `Presentation/Prompts/RouteAuthoringPrompt.cs` if the sitting warrants it, following the rule adopted
  for apidelta's other prompts (`CONCEPTION.md`)
- [ ] T061 [P] Add the authored-route vocabulary to `README.md`'s description of what apidelta writes,
  including the placeholder a developer must replace
- [ ] T062 [P] Add to `CONCEPTION.md` any rule this feature establishes that a compiler will not enforce —
  candidates: a typed value is validated before it builds a pointer; a placeholder belongs where the
  reader will look
- [ ] T063 Run the full gate: `dotnet build` with zero warnings, `dotnet test` green, and verify in a
  fresh clone that the corpus resources are embedded
- [ ] T064 Mark the tasks of `specs/005-wsdl-route-authoring/tasks.md` as done with their per-phase notes,
  as 004 did, so the record says what was learnt and not only what was built

---

## Dependencies & Execution Order

### Phase dependencies

```text
Phase 1 (setup corpus)
   └─> Phase 2 (records, rules, splicer, port)   ← blocks every story
          ├─> Phase 3 (US1: path, verb, where)   ← MVP
          │      ├─> Phase 4 (US2: placements)
          │      │      ├─> Phase 5 (US3: headers)
          │      │      ├─> Phase 7 (placeholder)
          │      │      └─> Phase 8 (reporting)
          │      └─> Phase 6 (US4: resume)
          └─> Phase 9 (withdraw WR-105)          ← after Phase 3 proves the replacement
                 └─> Phase 10 (polish)
```

### User story dependencies

- **US1** depends only on the foundational phase. It is the MVP and is demonstrable alone.
- **US2** depends on US1: there is no field to place until a route exists.
- **US3** depends on US2 — a header is a placement — and is otherwise independent.
- **US4** depends on US1 only: a sitting of paths and verbs is already worth not losing.

### Within each phase

Records before the rules that validate them; rules before the prompt that calls them; the prompt before
the builder that consumes its output; the builder before the report that describes it.

### Parallel opportunities

- **Phase 1**: T001 and T002 are different files and can be written together.
- **Phase 2**: T004, T005, T006 are three independent records. Each test task (T011, T013, T015, T018) runs
  parallel to the others once its subject exists.
- **Phase 3**: T021 and T029 are independent test files.
- **Phases 5, 7, 8** can proceed in parallel once phase 4 is done: headers, placeholder and reporting touch
  different files.
- **Phase 9** can be done by a second pair of hands while phase 8 is in progress — the deletions touch
  files no other phase changes.

## Parallel Example: Phase 2

```text
Together:  T004  ParameterLocation.cs
           T005  InsertionPoint.cs
           T006  OfferedHeader.cs

Then:      T007  FieldPlacement.cs      (needs T004)
           T008  AuthoredRoute.cs       (needs T007)
           T009  AuthoringRecord.cs     (needs T008)

Together:  T010 + T011  RouteSyntax and its tests
           T012 + T013  PlacementDefaults and its tests
           T014 + T015  PlacementRules and its tests
           T016 → T017 → T018  Splice, emitter, tests
```

## Implementation Strategy

**MVP is phase 3 alone.** It is where the complaint lives: 19 of 19 derived routes missed, and phase 3 is
what stops that. Shipping it with fields still travelling in the body would already remove the hand
editing of every path.

**Phase 4 is what makes it worth using**, and the two together are the feature as specified.

**Phases 5 to 8 are refinements**, each deliverable on its own: headers, the placeholder, the report.

**Phase 9 is a deletion and must come after phase 3 proves the replacement works.** Removing the declared
correspondence file before authoring exists would leave the developer with neither a file nor a prompt.

**Phase 10 is not optional.** The mutation pass of T059 is what distinguishes a guard from a comment, and
this repository has spent two releases learning the difference.

## Task count

| Phase | Tasks |
|---|---|
| 1 — Setup | 3 |
| 2 — Foundational | 16 |
| 3 — US1 (MVP) | 12 |
| 4 — US2 | 6 |
| 5 — US3 | 3 |
| 6 — US4 | 5 |
| 7 — Placeholder | 4 |
| 8 — Reporting | 4 |
| 9 — Withdraw WR-105 | 5 |
| 10 — Polish | 6 |
| **Total** | **64** |
