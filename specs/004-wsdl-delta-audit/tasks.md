# Tasks: A WSDL as a candidate for apidelta

**Input**: design documents from `specs/004-wsdl-delta-audit/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md),
[data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests are requested.** The specification is written as acceptance scenarios, and the plan fixes an order
the project learned the hard way: **the hostile corpus precedes the reader**. 003's stack overflow on a
10,000-link reference chain was found long after its reader was written, and that is the cost this
ordering avoids.

## Format: `[ID] [P?] [Story] Description`

- **[P]** — parallelisable: a different file, no dependency on an unfinished task
- **[US1]…[US5]** — the user story the task serves; absent for setup, foundational and polish
- Every task names the file it touches

## Path Conventions

No new project. Everything lands in the three that exist, so the layering tests keep holding.

- `Core/` is `tools/ApiDelta/DevToolbox.Tools.ApiDelta.Core/`
- `Infra/` is `tools/ApiDelta/DevToolbox.Tools.ApiDelta.Infrastructure/`
- `Pres/` is `tools/ApiDelta/DevToolbox.Tools.ApiDelta.Presentation/`
- `Tests/` is `tests/DevToolbox.Tests/Tools/ApiDelta/`

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 Establish that the existing splicer accepts a node with no byte extent, in `Tests/Correction/SyntheticGraftTests.cs` — **done 2026-10-21, before the plan was reported**: plan risk R-01 retired, and the whole estimate rested on it
- [X] T002 Add `SpecFormat.Projected` in `Core/Syntax/SpecFormat.cs`, with the remark that a projected document carries no byte extents and must never be emitted verbatim (research R-03)
- [X] T003 [P] Create the ordinary contract in `Tests/Corpus/Wsdl/catalogue.wsdl`, `catalogue-types.xsd` and `commun.xsd` — three documents, a cycle between two schemas, one operation per message pattern
- [X] T004 [P] Create `Tests/Corpus/Wsdl/production.yaml` — the OpenAPI production the WSDL is compared against, answering some of its operations and not others
- [X] T005 [P] Create `Tests/Corpus/Wsdl/correspondance.yaml` — a declared mapping, for WR-105
- [X] T006 Register the new corpus files as embedded resources in `tests/DevToolbox.Tests/DevToolbox.Tests.csproj`

### The hostile corpus, written before the reader exists

- [X] T007 [P] Create `Tests/Corpus/Wsdl/hostile/doctype.wsdl` — a `DOCTYPE` declaration (WR-002)
- [X] T008 [P] Create `Tests/Corpus/Wsdl/hostile/billion-laughs.wsdl` — nested entity expansion (WR-002)
- [X] T009 [P] Generate the import chain in `Tests/Corpus/GeneratedWsdl.cs` via `Chain(directory, depth)` rather than embedding four hundred files — the precedent is `GeneratedDescription`, and the content is entirely mechanical (WR-009)
- [X] T010 [P] Create `Tests/Corpus/Wsdl/hostile/cycle.wsdl` — a legal cycle, to be recorded and never followed (WR-201)
- [X] T011 [P] Create `Tests/Corpus/Wsdl/hostile/traversal.wsdl` — a `schemaLocation` leaving the declared root (WR-005)
- [X] T012 [P] Create `Tests/Corpus/Wsdl/hostile/same-name/` — two documents whose last path segment is identical (WR-012)
- [X] T013 [P] Generate the enormous enumeration in `Tests/Corpus/GeneratedWsdl.cs` via `EnormousEnumeration(values)` — a ten-megabyte embedded file would be absurd for mechanical content
- [X] T014 [P] Generate the attribute payload in `Tests/Corpus/GeneratedWsdl.cs` via `ManyAttributes(count)` — more than four hundred `xsd:attribute`, to prove grouping (WR-111, SC-004)

**Checkpoint**: the corpus exists and nothing reads it. Every hostile document is a question the reader
will have to answer out loud.

---

## Notes de la phase 1, consignées en la faisant

**Un risque du plan était énoncé à l'envers, et la mesure l'a montré.** R-02 annonçait que `XmlSchemaSet`
résout ses imports tout seul et qu'il faudrait le désarmer. C'est faux : `XmlResolver` vaut **nul** par
défaut depuis .NET Core, si bien que sans résolveur il ne suit aucun `schemaLocation` — le corpus a refusé
de compiler jusqu'à ce que l'essai en pose un. La posture est donc fermée par défaut, et le travail
consiste à fournir un résolveur *contrôlé*, non à en brider un trop zélé. L'épreuve qui compte ne change
pas : un import vers un hôte injoignable doit être refusé **en le nommant**.

**Le corpus a été compilé avant d'être cru.** Onze types globaux, douze éléments, aucune erreur de
validation : les imports résolvent, le cycle entre `Document` et `Dossier` se compile, la dérivation par
extension et le groupe de substitution tiennent. Un corpus que personne n'a vérifié transforme chaque
échec ultérieur en enquête — on ne sait plus si c'est le lecteur qui se trompe ou le document qui est faux.

**Les documents hostiles sont éprouvés hostiles.** `doctype.wsdl` et `billion-laughs.wsdl` font lever
l'analyseur quand les définitions de type de document sont interdites. Sans cet essai, un
« doctype.wsdl » sans `DOCTYPE` serait un essai qui passe pour rien, et c'est le genre de corpus qui
endort une suite entière.

**Une bizarrerie de MSBuild qui coûte une heure.** Dans un nom de ressource embarquée, un **dossier**
`same-name` devient `same_name`, alors qu'un **fichier** `same-name.wsdl` garde son tiret. Les deux
cohabitent donc dans la même liste de ressources sous deux orthographes.

**Trois documents sont engendrés plutôt qu'embarqués** : la chaîne de 400 imports, l'énumération de vingt
mille valeurs, et la charge de 420 attributs. Leur contenu est entièrement mécanique, et `GeneratedWsdl`
suit le précédent de `GeneratedDescription`. Une chaîne d'imports écrit de vrais fichiers, puisque c'est
la résolution qu'elle éprouve.


## Phase 2: Foundational (Blocking Prerequisites)

**These block every story.** They are the shapes; nothing can be read or projected without them.

- [X] T015 [P] Create `Core/Wsdl/WsdlLocation.cs` — document identity, reviewer path, line, column, and **no `ByteSpan`** (WR-003, research R-03)
- [X] T016 [P] Create `Core/Wsdl/WsdlDocumentKind.cs` and `Core/Wsdl/MessagePattern.cs` — the two closed vocabularies
- [X] T017 [P] Create `Core/Wsdl/RetrievedOrigin.cs` — redacted URL, retrieval instant, redirect chain, with **redaction in the constructor** so no instance can hold an unredacted URL (WR-019)
- [X] T018 Create `Core/Wsdl/WsdlDocument.cs` — path, size, hash, target namespace, kind, optional origin (WR-004)
- [X] T019 [P] Create `Core/Wsdl/XsdType.cs`, `Core/Wsdl/XsdParticle.cs` and `Core/Wsdl/XsdFacet.cs` — the compiled type shapes the projection reads
- [X] T020 [P] Create `Core/Wsdl/WsdlMessage.cs`, `Core/Wsdl/WsdlPart.cs`, `Core/Wsdl/WsdlBinding.cs` and `Core/Wsdl/WsdlService.cs`
- [X] T021 Create `Core/Wsdl/WsdlOperation.cs` — name, pattern, input, output, faults, binding, location (WR-104, WR-115)
- [X] T022 Create `Core/Wsdl/WsdlContract.cs` — documents, operations, types, unresolved imports, confinement root, and `IsComplete` **computed** so a partly read contract cannot claim otherwise (WR-008)
- [X] T023 [P] Create `Core/Wsdl/UnresolvedImport.cs` — what policy forbade, and the import that wanted it
- [X] T024 [P] Create `Core/Configuration/WsdlLimits.cs` — document size, nesting depth, import-chain depth and document count, each named when reached (WR-009)
- [X] T025 [P] Create `Core/Configuration/ImportPolicy.cs` — confinement root, remote switch, host allow-list, scheme allow-list (WR-005, WR-007, WR-014)
- [X] T026 [P] Create `Core/Abstractions/IWsdlReader.cs` and `Core/Wsdl/WsdlReadResult.cs` — the shape of `SpecReadResult`: a contract exists only if it was read faithfully
- [X] T027 [P] Create `Core/Abstractions/IContractFetcher.cs` — the retrieval port, which stands on types the foundational phase already has
- [X] T027a **Ordering corrected while doing the work.** The three remaining ports of [contracts/application-ports.md](contracts/application-ports.md) — `IWsdlProjector`, `ICorrespondenceProposer`, `ICorrespondencePrompt` — name `ProjectionResult`, `Correspondence` and `CoverageReport`, which belong to User Story 2. A port cannot compile before the types it speaks of, so they move to Phase 4 beside them (T059a). Declaring them here with placeholder types would have been inventory.

**Checkpoint**: the shapes exist, nothing behaves yet. The build is green and the layering tests pass.

---

## Notes de la phase 2, consignées en la faisant

**Deux erreurs de ma propre liste de tâches, trouvées en l'exécutant.**

T024 et T025 plaçaient les limites et la politique d'import dans `Infrastructure`. Or `IWsdlReader` vit
dans `Core` et les prend en paramètre : les dépendances ne pointant que vers l'intérieur, l'essai
d'architecture aurait refusé. Elles vivent donc dans `Core/Configuration/`, aux côtés de `LoadLimits` et
`ReferencePolicy` qui y étaient déjà pour la même raison.

T027 demandait les quatre ports d'un coup. Trois d'entre eux nomment `ProjectionResult`, `Correspondence`
et `CoverageReport`, qui appartiennent à l'histoire 2 : un port ne compile pas avant les types dont il
parle. Ils sont déplacés en phase 4, et les déclarer ici avec des types fantoches aurait été de
l'inventaire.

**Un nom refusé par un analyseur, et le vocabulaire XSD donnait le bon.** `XsdAttribute` tombe sous CA1711,
qui réserve le suffixe aux vrais attributs .NET. La déclaration d'un attribut sur un type complexe
s'appelle une « attribute use » dans la spécification XSD : `XsdAttributeUse` dit donc plus juste que ce
que j'avais écrit, en plus de passer.

**Trois promesses sont calculées, jamais renseignées.** `WsdlContract.IsComplete` dérive de la liste des
imports non résolus ; `WsdlOperation.IsUnreachable` de l'absence de liaison ; `XsdType.HasUncarryableParts`
de ce que le type porte réellement. La raison est celle que `Finding.AutofixAvailable` avait déjà dans la
tranche précédente : un type capable d'affirmer le contraire de ce qu'il porte finirait par le faire, et
aucun essai ne rattrape une affirmation.

**Le caviardage a lieu dans le constructeur de `RetrievedOrigin`.** Aucune instance ne peut porter une
adresse non caviardée, chaîne de redirections comprise. Le registre de masquage du puits de journalisation
reste une seconde ligne : il suffit d'un message de diagnostic écrit à la hâte pour publier un jeton, et
une première ligne qui dépend de la discipline de l'appelant n'en est pas une. La chaîne de requête part
entière plutôt que par nom de paramètre, parce qu'une liste noire se contourne dès qu'un partenaire nomme
son paramètre autrement.

**`WsdlPort` ne porte pas l'adresse déclarée**, et c'est délibéré. Un `soap:address` porte parfois un
identifiant dans son autorité ; un type qui la retiendrait finirait par la journaliser. Les constats qui
la concernent nomment l'emplacement, et le lecteur va la lire dans le document — exactement la correction
qu'AUD-303 avait reçue dans la tranche précédente, après avoir recopié un mot de passe dans un rapport.


## Phase 3: User Story 1 — Read the WSDL and its imports, or refuse to pretend (P1) 🎯 MVP

**Goal**: point the tool at a `.wsdl` or at the URL of one, and get one faithful picture of the whole
contract — or a refusal that names what it could not read.

**Independent test**: read the corpus graph and confirm three documents are reported with path, hash and
confinement root; then move one schema and confirm the run refuses, naming the missing `schemaLocation`,
the document that wanted it, and its line.

### Tests for User Story 1

- [X] T028 [P] [US1] Create `Tests/Wsdl/WsdlHostileTests.cs` — one case per hostile document, each asserting a refusal that **names the limit or the reason**. A test that passed by timing out would not be passing (WR-002, WR-005, WR-009, WR-201)
- [X] T029 [P] [US1] Create `Tests/Wsdl/WsdlReaderTests.cs` — the ordinary graph, then the missing-schema refusal
- [X] T030 [P] [US1] Create `Tests/Wsdl/SchemaCompilerTests.cs` — a schema importing an unreachable host is refused **by name, not by timeout** (plan risk R-02)
- [X] T031 [P] [US1] Covered in `Tests/Wsdl/WsdlReaderTests.cs` rather than a file of its own — one-way, notification and solicit-response read correctly, and an operation with no output has no response (WR-115)

### Implementation for User Story 1, reading files

- [X] T032 [US1] Create `Infra/Wsdl/WsdlReader.cs` — `XmlReader` with DTD processing prohibited, the resolver null, entity characters capped at zero, and `IXmlLineInfo` for every location (WR-001, WR-002, WR-003)
- [X] T033 [US1] Create `Infra/Wsdl/SchemaCompiler.cs` — `XmlSchemaSet` with the resolver null plus one answering only from the already-read set, so imports cannot escape the policy (plan risk R-02)
- [X] T034 [US1] Add import resolution to `Infra/Wsdl/WsdlReader.cs` — local open by default and confined, traversal refused and named, remote closed unless allow-listed (WR-005, WR-006, WR-007)
- [X] T035 [US1] Merge same-namespace schemas and report a conflicting declaration naming both documents, in `Infra/Wsdl/SchemaCompiler.cs` (WR-101)
- [X] T036 [US1] Apply or refuse `xsd:redefine` by name, never read and silently ignore it, in `Infra/Wsdl/SchemaCompiler.cs` (WR-202)
- [X] T037 [US1] Report every document opened and the confinement root, in `Infra/Wsdl/WsdlReader.cs` (WR-004, WR-006)

### Implementation for User Story 1, reading a URL

- [X] T038 [P] [US1] Create `Tests/Http/AddressGuardTests.cs` — the hostile URL set of quickstart scenario 10, each refused or confirmed as its requirement says, and no token in any artifact (SC-011)
- [X] T039 [P] [US1] Create `Tests/Http/ContractFetcherTests.cs` — against a loopback listener: materialisation, name collision, a relative import resolved against its origin, a resume that retrieves nothing (SC-010, WR-012, WR-013)
- [X] T040 [US1] Create `Infra/Http/AddressGuard.cs` — scheme allow-list, resolved-address checks, bounded redirects re-validated at every hop, the chain reported (WR-014, WR-015)
- [X] T041 [US1] Create `Infra/Http/ContractFetcher.cs` — typed client from `IHttpClientFactory` with the standard resilience handler, bounded timeout and response size, a non-XML response refused by naming what arrived, no credential ever acquired or sent (WR-016, WR-017)
- [X] T042 [US1] Create `Infra/Http/Materialiser.cs` — writes the graph to the output directory, names collisions apart, records the mapping, leaves nothing partial behind (WR-011, WR-012)
- [X] T043 [US1] Resolve a retrieved document's relative `schemaLocation` against the URL it came from, in `Infra/Http/Materialiser.cs` (WR-013)
- [X] T044 [US1] Create `Core/Steps/ReadWsdlStep.cs` and add its name to `Core/Steps/ApiDeltaStepNames.cs` — detect a WSDL candidate from its content, read or retrieve, put the contract in the context
- [X] T045 [US1] Register the reader, compiler, guard, fetcher and materialiser in `Infra/ServiceCollectionExtensions.cs`, and the step in `Pres/ServiceCollectionExtensions.cs`

**Checkpoint**: a WSDL graph reads from disk or from a URL, or is refused by name.

> **Note, 2026-10-22.** `ReadWsdlStep` is written and registered, and `CompositionRootTests` resolves it
> from the real graph — but it is **not yet in the pipeline list**. The comment that governs that list
> forbids a run from containing a step that can do nothing, because the display would report it
> accomplished. Until the projection of User Story 2 exists, a read contract has no consumer, so the step
> joins the list with the slice that uses it. Nothing else of US1 is held back by this. **This is the MVP
boundary** — US1 ships at T037 with files only; T038 to T045 add the URL and are additive.

---

## Phase 4: User Story 2 — Know what production does not cover, and what could be added (P1)

**Goal**: two things kept apart — which WSDL operations production answers nowhere, and, where it answers
one, whether what it declares still fits.

**Independent test**: compare the corpus WSDL against `production.yaml`, confirm coverage is exact against
the documented expectation, and confirm the compatibility findings group their unknowns rather than listing
one per field.

### Tests for User Story 2

- [X] T046 [P] [US2] Create `Tests/Projection/ProjectionTableTests.cs` — one assertion per row of [contracts/projection.md](contracts/projection.md), plus one test that the rule set covers the table **exactly** (plan risk R-03)
- [X] T047 [P] [US2] Create `Tests/Correspondence/CorrespondenceTests.cs` — nothing compares unconfirmed, a declared mapping wins over a proposal, a near-tie yields ambiguous, and a deliberately wrong mapping is honoured yet marked inferred (plan risk R-04)
- [X] T048 [P] [US2] Create `Tests/Correspondence/CoverageTests.cs` — every operation carries exactly one state, matching the corpus expectation, and no coverage gap appears among the breaking changes (SC-003)
- [X] T049 [P] [US2] Create `Tests/Projection/GroupingTests.cs` — the attribute corpus yields one entry per reason with a count, bounded by reasons and not by occurrences (SC-004)
- [X] T050 [P] [US2] Create `Tests/Projection/RouteDerivationTests.cs` — the verb table, the partial derivation, and the fallback reported as a fallback (WR-305)

### Implementation for User Story 2

- [X] T051 [P] [US2] Create `Core/Projection/ProjectionConfidence.cs` and `Core/Projection/ProjectionGapReason.cs` — the two vocabularies, with approximate as a first-class member (WR-112)
- [X] T052 [P] [US2] Create `Core/Projection/ProjectionGap.cs` and `Core/Projection/ProjectionFigures.cs` — one gap per occurrence, grouping left to the report (WR-111, WR-603)
- [X] T053 [US2] Create `Core/Projection/IProjectionRule.cs` and `Core/Projection/ProjectionTable.cs` — one rule per published row, so the table and the code cannot drift
- [X] T054 [US2] Implement the carried rules under `Core/Projection/Rules/` — particles, occurrence bounds, facets, types and formats, nillability, `xsd:all`
- [X] T055 [US2] Implement the approximate rules under `Core/Projection/Rules/` — choice against `oneOf`, extension against `allOf`, `xsd:any`, SOAP fault and SOAP header, each carrying uncertain confidence (WR-112)
- [X] T056 [US2] Implement the not-carried rules under `Core/Projection/Rules/` — attributes, substitution groups, mixed content, namespaces, binding style, particle order, restriction, XSD 1.1 constructs
- [X] T057 [US2] Create `Core/Projection/RouteDerivation.cs` — deterministic, with the configurable verb table and the reported fallback (WR-305)
- [X] T058 [US2] Create `Core/Projection/Projector.cs` and `Core/Projection/ProjectionResult.cs` — build the synthetic document with the projected format, the gaps and the figures; pure, with no clock, no filesystem and no console (WR-110)
- [X] T059 [P] [US2] Create `Core/Correspondence/CoverageState.cs`, `CorrespondenceOrigin.cs`, `CorrespondenceEntry.cs` and `Correspondence.cs` — strings and enums only, so it joins the checkpoint (WR-104, WR-105)
- [X] T059a [US2] Create `Core/Abstractions/IWsdlProjector.cs`, `Core/Abstractions/ICorrespondenceProposer.cs` and `Core/Abstractions/ICorrespondencePrompt.cs` — moved here from T027, because a port cannot compile before the types it names
- [X] T060 [US2] Create `Core/Correspondence/SimilarityProposer.cs` — token overlap, the matched tokens kept as the basis, near-ties left ambiguous
- [X] T061 [US2] Create `Core/Correspondence/CoverageReport.cs` — computed from the confirmed correspondence **alone**, never from the comparer (WR-108)
- [X] T062 [US2] Create `Pres/Prompts/CorrespondencePicker.cs` and `Pres/Prompts/CorrespondenceSelectionPrompt.cs` — the four marks of [contracts/console-experience.md](contracts/console-experience.md), with declining as an answer
- [X] T063 [US2] Create `Core/Steps/CorrespondStep.cs` and add its name to `Core/Steps/ApiDeltaStepNames.cs` — project, propose, confirm, cover, and **compare nothing before confirmation** (WR-104)
- [X] T064 [US2] Add the correspondence to `Core/RunSelection.cs`, project it in `Pres/ApiDeltaCheckpointFactory.cs`, and add its kinds to `Pres/ApiDeltaSelectionKinds.cs` (WR-104)
- [X] T065 [US2] Register the projector, the proposer and the prompt in `Pres/ServiceCollectionExtensions.cs`, and the step in the ordered chain after Compare and before Correct

**Checkpoint**: a WSDL is compared against production, coverage is certain, compatibility is partial, and
the two never mix.

> **Note, 2026-10-22.** `CorrespondStep` is written, registered and resolved from the real graph, and is
> **not yet in the pipeline list** — same reason as `ReadWsdlStep`, and the same appointment: a pairing
> produces nothing until the selection of routes to insert consumes it. Both steps join the list with
> User Story 3. `OperationCorrespondence` carries the name the analyser forced (MA0049 forbids a type named
> for its namespace); `tasks.md` had called the file `Correspondence.cs`.

---

## Phase 5: User Story 3 — Choose which operations become routes, and get production back with them in it (P1)

**Goal**: pick operations, and receive production plus exactly those routes — each derived element saying
where it came from, each gap the projection could not carry left as a marker the tool refuses to fill.

**Independent test**: select two WSDL operations as new routes and confirm the generated YAML is production
unchanged plus those two, that every derived element names its WSDL origin, and that every non-projectable
place holds an unfilled marker rather than a plausible schema.

The selecting, the statement before writing, the untouched-byte guarantee and the proof are 003's and are
**not reimplemented**. These tasks are only what a WSDL candidate makes different.

### Tests for User Story 3

- [X] T066 [P] [US3] Create `Tests/Wsdl/DerivedMarkTests.cs` — every derived element names the WSDL document, location and projection confidence, and no copied element carries such a mark (SC-007)
- [X] T067 [P] [US3] Create `Tests/Wsdl/UnfilledMarkerTests.cs` — a response type using an attribute yields an unfilled marker and never a plausible schema, and the count matches the report (SC-008)
- [X] T068 [P] [US3] Create `Tests/Wsdl/WsdlEndToEndTests.cs` — quickstart scenario 11, from WSDL and production to a generated document, with the production file byte-identical afterwards under every flag (SC-009)

### Implementation for User Story 3

- [X] T069 [US3] Extend `Core/Correction/CorrectionMark.cs` with the WSDL origin and the projection confidence, carried in addition to what 003 already records (WR-300)
- [X] T070 [US3] Teach `Core/Correction/CorrectionBuilder.cs` to mark a derived element as derived — the one place a WSDL candidate differs from an OpenAPI one, where 003 copies the candidate's bytes (WR-300)
- [X] T071 [US3] Emit an unfilled marker where the projection carried nothing, in `Core/Correction/CorrectionBuilder.cs`, refusing to fill it with a guess (WR-301)
- [X] T072 [US3] Declare the marker count at document level, extending the declaration 003 already writes, in `Core/Correction/CorrectionBuilder.cs` (WR-302)
- [X] T073 [US3] Show the coverage state and the inferred-correspondence mark on each selectable item, in `Pres/Prompts/CorrectionPicker.cs` (WR-303)
- [X] T074 [US3] Report a projected path colliding with one production already carries, and never overwrite production's route, in `Core/Correction/CorrectionBuilder.cs` (WR-304)
- [X] T075 [US3] State the derivation for every proposed route in the report, in `Core/Reporting/MarkdownReportWriter.cs` (WR-305)
- [X] T076 [US3] Teach `Core/Correction/CorrectionVerifier.cs` that an unfilled marker is expected in a WSDL-derived document and must not be read as a missing change (WR-301, 003 FR-514)

**Checkpoint**: a WSDL operation becomes a route in production, marked, proven, and never written over the
original. **The feature is usable here.**

## Notes de la phase 5, consignées en la faisant

> **Trois défauts que seule la fin de la chaîne pouvait montrer.** Ils vivaient tous dans du code déjà
> écrit et déjà vert : ils n'apparaissent que là où le document engendré doit être **valide**, et non
> seulement relisible.
>
> 1. `ChoiceRule` posait un `$comment` portant le nom du type attendu. Le mot-clé n'existe pas en
>    OpenAPI 3.0 : la revalidation de la production corrigée refusait le document — et la branche ne
>    disait rien du type qu'elle attendait. Les branches reçoivent maintenant les règles de particule de
>    la table, par où elles obtiennent renvoi, format et collection comme partout ailleurs.
> 2. Les particules d'un `xsd:choice` étaient projetées **deux fois** : dans les branches, et au niveau du
>    type, où elles sortaient en `required: [a, b, c]`. Un choix annoncé comme « les trois à la fois »,
>    avec la confiance d'une règle portée. Le projecteur ne les applique plus au niveau d'un type de
>    contenu « choix ». [contracts/projection.md](contracts/projection.md) dit maintenant ce qu'une
>    branche contient — dans la colonne des remarques, et non dans la deuxième colonne : celle-là est
>    recopiée mot pour mot dans `ProjectionRule.Target`, et `ProjectionTableTests` le vérifie. La ligne
>    elle-même n'a pas changé.
> 3. `MarkdownReportWriter.Row` découpait les douze premiers caractères de l'empreinte. Un document
>    projeté n'en a pas — il n'est le contenu d'aucun fichier — et le rapport tombait sur une chaîne
>    vide. Il dit maintenant qu'il n'y en a pas, ce qui est la seule réponse honnête.
>
> **Un manque de 003, relevé parce que WR-300 le rendait visible.** Le commentaire de `Mark` affirmait
> qu'un champ scalaire voyait sa trace figurer dans la déclaration du document ; rien ne l'y écrivait. Un
> champ scalaire ne peut pas porter de membre `x-`, si bien qu'un champ *dérivé* n'aurait eu de trace
> nulle part — ce que WR-300 interdit nommément. La déclaration porte donc `champsDerives`, et la
> clarification est consignée dans [spec.md](spec.md).
>
> **Deux règles candidates pour [CONCEPTION.md](../../CONCEPTION.md) à la phase 8 (T100).** Une trace
> d'origine ne dit jamais « non portée » : un élément écrit a été porté, exactement ou à peu près, et ce
> qui n'a pas été porté est une absence, dite par un marqueur. Et un document projeté doit être valide,
> pas seulement relisible : c'est la seule épreuve qui ait trouvé les deux premiers défauts ci-dessus.
>
> **Un point de laideur laissé tel quel, et dit.** Une méthode insérée sous un chemin que la production
> porte déjà arrive après le commentaire qui introduisait la clé *suivante* : le commentaire paraît alors
> décrire la route neuve. Aucune conséquence sur le sens — un commentaire YAML n'en a pas — mais la
> lisibilité du diff en souffre. Reculer sur les lignes de commentaire relève de `SplicingEmitter`, donc
> de 003 et de ses garanties d'octet intact ; ce n'est pas une correction à glisser dans cette tranche.

---

## Phase 6: User Story 4 — The same WSDL, written differently, gives the same result (P2)

**Goal**: a WSDL delivered with other prefixes, other attribute order, or its schemas split across other
files produces the same coverage, the same findings and the same generated document.

**Independent test**: compare production against three deliveries of one contract and confirm identical
coverage, identical findings and, under the same selection, byte-identical generated documents.

### Tests for User Story 4

- [X] T077 [P] [US4] Create `Tests/Corpus/Wsdl/catalogue-inline.wsdl` — the same contract with its schemas inlined
- [X] T078 [P] [US4] Create `Tests/Corpus/Wsdl/catalogue-prefixes.wsdl` — the same contract with other namespace prefixes, other attribute order and other element form
- [X] T079 [US4] Create `Tests/Projection/ProjectionStabilityTests.cs` — the three deliveries produce identical trees, gaps and figures, and byte-identical generated documents under the same selection (SC-001)

### Implementation for User Story 4

- [X] T080 [US4] Resolve prefixes to namespace URIs before anything is projected, in `Infra/Wsdl/WsdlReader.cs`, so the prefix affects no result (WR-100)
- [X] T081 [US4] Make the projection insensitive to writing and decomposition — attribute order, element form, whitespace, comments, CDATA, top-level declaration order, distribution across documents — in `Core/Projection/Projector.cs` (WR-102)
- [X] T082 [US4] Carry particle order inside a sequence and ignore it inside `xsd:all` and a choice, in `Core/Projection/Projector.cs` (WR-103)
- [X] T083 [US4] Order gaps and figures deterministically in `Core/Projection/ProjectionResult.cs`, so two runs cannot differ by enumeration order (SC-001)

**Checkpoint**: a second delivery of one contract proposes nothing new. Without this the tool would propose
changes nobody made, and a draft that did that once would never be trusted again.

## Notes de la phase 6, consignées en la faisant

> **Un contrat sur deux était lu à moitié, en silence.** Un jeu de schémas reconnaît un doublon à l'URI
> d'où le schéma vient, et deux `xs:schema` écrits dans le même `wsdl:types` en partagent une : le second
> était écarté sans qu'`Add` échoue, sans exception et sans diagnostic. La livraison en ligne perdait donc
> ses cinq types et ses neuf éléments de message, et le contrat se déclarait lu fidèlement. Un fragment
> d'URI ne distingue pas les deux — il est retiré ; deux instances analysées à part, si. `SchemaCompiler`
> analyse maintenant chaque schéma en ligne avec `XmlSchema.Read` avant de l'ajouter.
>
> Trouvé parce que la phase 6 est la première à lire un contrat dont les schémas sont écrits dans le WSDL.
> Aucun essai antérieur ne pouvait le voir : le corpus ordinaire importe ses schémas, et un contrat lu à
> moitié qui ne s'en plaint pas ne se distingue d'un contrat lu en entier que si l'on a l'autre moitié
> sous la main pour comparer. C'est tout l'objet de cette tranche.
>
> **Un second piège du même appel, écarté en chemin.** `XmlSchemaSet.Add(espace, lecteur)` applique
> l'espace de noms donné à *tout* ce que l'appel charge, imports compris : un schéma en ligne qui en
> importe un autre se faisait refuser au motif que l'espace de noms de l'importé n'était pas celui de
> l'importateur. Le chemin des fichiers passait déjà `null` ; celui des schémas en ligne le passe
> désormais aussi.
>
> **Ce que « document engendré identique » peut vouloir dire, et ce qu'il ne peut pas.** Une trace
> d'origine nomme la livraison réellement lue, avec sa ligne et sa colonne. Trois livraisons sont trois
> fichiers : exiger la même trace pour les trois serait exiger qu'elle soit fausse pour deux d'entre
> elles. SC-001 se vérifie donc hors de ce qui nomme la livraison, comme SC-014 se vérifie hors du bloc
> de métadonnées — et un essai de plus établit que chaque livraison nomme bien ses propres documents,
> pour que la neutralisation ne devienne pas une permission.
>
> **Une règle candidate de plus pour [CONCEPTION.md](../../CONCEPTION.md) à la phase 8 (T100).** Ce qui
> sort de la projection est trié, et le tri n'est pas cosmétique : les attributs d'un type compilé
> arrivent dans l'ordre d'une table de hachage, et un rapport dont les lignes changent de place d'une
> exécution à l'autre fait apparaître des différences à chaque relance de l'intégration continue.

---

## Phase 7: User Story 5 — Make the reading and the projection the team's own (P3)

**Goal**: the import policy, the URL policy, the limits and the projection's configurable choices are read
and validated before any document is read, and the tool works with none of them set.

**Independent test**: run the same comparison with and without a configuration that raises one limit and
allows one host; then supply an unknown key and confirm the run does not start.

### Tests for User Story 5

- [X] T084 [P] [US5] Create `Tests/Configuration/WsdlRulesetTests.cs` — an unknown key names its file, line and column and the run does not start; no configuration at all still produces usable results; remote resolution with an empty host list is refused rather than read as allowing everything

### Implementation for User Story 5

- [X] T085 [US5] Add the WSDL sections to `Core/Configuration/Ruleset.cs` — limits, import policy, URL policy, and the verb table of the route derivation
- [X] T086 [US5] Read them through the existing tree-based reader in `Infra/Files/RulesetSections.cs`, so every diagnostic carries a line and a column and an unknown key is **named** (WR-704)
- [X] T087 [US5] Refuse remote resolution enabled with an empty host allow-list, in `Infra/Files/RulesetSections.cs` (WR-007)
- [X] T088 [US5] Read and validate the configuration before any document is read, in `Core/Steps/SelectStep.cs` (WR-704)

**Checkpoint**: the defaults are the team's rather than the tool's, and a bad configuration stops the run
before it has read anything.

## Notes de la phase 7, consignées en la faisant

> **Un repli silencieux retiré au passage.** Le répartiteur des sections lisait *toute* section inconnue
> comme si c'était « extract » : la branche par défaut y menait, et « extract » n'avait pas de cas à
> elle. Le contrôle des clés de haut niveau refusait déjà une clé inconnue en la nommant, si bien que le
> repli n'était atteint par rien — mais il n'attendait qu'une clé ajoutée à la liste sans l'être au
> répartiteur. « extract » a maintenant son cas, et la branche par défaut ne lit plus rien.
>
> **T088 était déjà tenue, à un fil près.** `SelectStep` lit et valide le jeu de règles avant toute
> lecture de document, et refuse de démarrer s'il n'est pas applicable. Ce qui manquait était en aval :
> `ReadWsdlStep` et `CorrespondStep` employaient les valeurs livrées au lieu de celles qu'on venait de
> lire. Une configuration validée que personne ne consulte est pire qu'absente — elle donne à l'équipe
> la preuve que l'outil l'a lue.
>
> **Une table de verbes écrite remplace celle qui est livrée, elle ne s'y ajoute pas.** Une équipe qui
> écrit trois verbes veut ses trois verbes ; les ajouter aux trente-six d'origine lui ferait produire
> des itinéraires qu'elle n'a pas demandés, et elle ne saurait pas d'où ils viennent.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T089 [P] Add the correspondence, coverage and comparability sections to `Core/Reporting/MarkdownReportWriter.cs`, in a fixed order, present even when empty (WR-602)
- [ ] T090 [P] Carry the same three sections into `Core/Reporting/JsonReportWriter.cs`, so the machine-readable report says what the readable one says (WR-600)
- [ ] T091 [P] State the proportion compared as a figure and not as prose, in `Core/Reporting/MarkdownReportWriter.cs` (WR-603, SC-005)
- [ ] T092 [P] Report every import left unresolved by policy and every projection that could not be established, in the limitations section of `Core/Reporting/MarkdownReportWriter.cs` (WR-604)
- [ ] T093 Register the contract, the retrieved origin and the projection gap with `RedactionRegistry` in `Infra/ServiceCollectionExtensions.cs`, so no retrieved value can reach a log sink (WR-605)
- [ ] T094 [P] Create `Tests/Wsdl/WsdlSecretsTests.cs` — across every file a run writes, no credential, token or secret-shaped value appears, including a contract whose `soap:address` carries one (SC-015)
- [ ] T095 [P] Create `Tests/Wsdl/WsdlResumeTests.cs` — a run interrupted after confirmation resumes without asking again, retrieves nothing, and reaches identical artifacts outside the metadata block (SC-014)
- [ ] T096 [P] Create `Tests/Wsdl/WsdlPerformanceTests.cs` — 200 operations and 5,000 types across 20 documents under thirty seconds, **measured per phase**: 003's performance test found a stack overflow rather than a slowdown, and only the per-phase curve showed where (SC-002)
- [ ] T097 [P] Create `Tests/Wsdl/WsdlLimitTests.cs` — every configured limit, when reached, reported with the limit named (SC-013)
- [ ] T098 Create `Tests/Wsdl/WsdlOfflineTests.cs` — no outbound call when the inputs are files, established by observation and not by inspecting configuration (SC-012)
- [ ] T099 [P] Update `README.md` — apidelta accepts a WSDL candidate, and what the new report sections say
- [ ] T100 [P] Update `CONCEPTION.md` with the rules this feature adds, if any survive the implementation — a rule earns its place by having been broken once
- [ ] T101 Run the full gate on `DevToolbox.slnx`: `dotnet build` with zero warnings, `dotnet test` green, then clone the pushed branch into an empty directory and run both there — **a fresh clone is the only thing that catches it**, as version 1.2.0 nearly proved

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)** — T001 is done, T002 is independent, and T003 to T014 are the corpus: they
  block every test that reads it.
- **Phase 2 (Foundational)** — depends on Phase 1. **Blocks every story.**
- **Phase 3 (US1)** — depends on Phase 2. Nothing else depends on the URL half, T038 to T045.
- **Phase 4 (US2)** — depends on US1: there is nothing to project until a contract can be read.
- **Phase 5 (US3)** — depends on US2: there is nothing to select until coverage exists.
- **Phase 6 (US4)** — depends on US2 for the projection and on US3 for the generated document.
- **Phase 7 (US5)** — depends on US1 and US2, so that the settings have subjects.
- **Phase 8 (Polish)** — each task depends on the story whose output it reports.

### User Story Dependencies

Unlike a typical feature these stories are **a chain, not a fan**. US1 reads, US2 projects and compares,
US3 writes. Each is independently testable, and none is independently useful: a projection with nothing to
project, or a selection with nothing to select, is not a smaller product.

US4 and US5 are genuinely independent of each other and can be built in either order once US3 lands.

### Within Each User Story

Tests before implementation. And in Phase 3 the corpus comes before the tests, which come before the
reader — the plan's one procedural rule, with its reason recorded there.

### Parallel Opportunities

- **T003 to T014** — the whole corpus, fourteen files, no shared state
- **T015 to T027** — the foundational shapes, one file each, bounded only where T018, T021 and
  T022 depend on their parts
- **T028 to T031** — the four US1 test files
- **T046 to T050** — the five US2 test files
- **T051, T052, T059** — independent shapes within US2
- **T054, T055, T056** — the three rule families, one folder each
- **T077, T078** — the two alternative deliveries
- **T089 to T092, T094 to T097, T099, T100** — the polish set, nearly all independent

## Parallel Example: Phase 1

The corpus, written at once, before anything reads it.

```text
T003  catalogue.wsdl + catalogue-types.xsd + commun.xsd
T004  production.yaml
T005  correspondance.yaml
T007  hostile/doctype.wsdl
T008  hostile/billion-laughs.wsdl
T009  hostile/chain-400.wsdl
T010  hostile/cycle.wsdl
T011  hostile/traversal.wsdl
T012  hostile/same-name/
T013  enormous-enum.xsd
T014  attributes.wsdl
```

## Implementation Strategy

### MVP: User Story 1, files only

T001 to T037. The tool reads a WSDL graph from disk and refuses what it cannot read, by name. It compares
nothing yet — and that is a defensible stopping point, because a reader that lies is the one failure
everything downstream inherits.

### Incremental delivery

1. **US1, files** (T001–T037) — read it, or refuse honestly
2. **US1, URL** (T038–T045) — additive; the box's first outbound call, with its own hostile set
3. **US2** (T046–T065) — coverage certain, compatibility partial, nothing compared before confirmation
4. **US3** (T066–T076) — production back, with the chosen routes marked and proven. **Usable here.**
5. **US4** (T077–T083) — a second delivery of one contract proposes nothing new
6. **US5** (T084–T088) — the defaults become the team's
7. **Polish** (T089–T101) — the reports, the secrets, the resume, the scale, the fresh clone

### Parallel Team Strategy

Two people can split after Phase 2. One takes the reading half — US1 including the URL and its guard. The
other takes the projection table, T053 to T056, which needs only the foundational shapes and the corpus.
They meet at T058, where the projector assembles what both produced.
