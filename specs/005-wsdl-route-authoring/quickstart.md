# Quickstart: validating authored routes

How to prove this feature works, end to end and in parts. Shapes and rules are in
[data-model.md](./data-model.md) and [contracts/](./contracts/); this file is only about running and
seeing.

## Prerequisites

```bash
dotnet build DevToolbox.slnx    # warnings are errors: one warning fails the build
dotnet test DevToolbox.slnx
```

Both must be clean before and after. The suite stands at 862 tests at the time this plan was written.

## The pair to validate against

Any WSDL plus OpenAPI pair whose path conventions a naming rule cannot reproduce. The pair this feature
was specified against has these properties, which make it the useful shape:

- 21 operations, of which 19 have no counterpart in production.
- Production paths carry a `/Dossier` or `/Document` prefix and keep the operation name — so every derived
  route misses.
- Five header parameters declared as reusable components, referenced by all 20 production routes, absent
  from the WSDL.
- Every contract element `minOccurs="0" nillable="true"`, so requiredness is nowhere.

Test corpora live in the test assembly; a developer's own files are never added to the repository.

## Validating the parts

Each of these is a unit test with no console and no filesystem, which is what putting authoring in `Core`
buys.

| What to prove | Where | What a failure means |
|---|---|---|
| A malformed path is refused, naming its rule | `Authoring/RouteSyntaxTests` | A typed value could reach pointer construction |
| A composite field is refused for path/query/header, naming field and type | `Authoring/PlacementRulesTests` | The tool would invent structure |
| A path placeholder with no field placed in it is named before writing | `Authoring/PlacementRulesTests` | An invalid document could be written |
| `GET` pre-sets the query, `POST` the body, a composite under `GET` goes nowhere and is flagged | `Authoring/PlacementDefaultsTests` | Accepting defaults could silently drop a field |
| The five headers of a real production are found, counted and ordered | `Authoring/ReusableHeadersTests` | Headers would be invented or missed |
| A dropped field survives into `Dropped()`, and placeholders into `Placeholders()` | `Authoring/AuthoringRecordTests` | A field would disappear unnamed |

## Validating the whole

```bash
dotnet test DevToolbox.slnx --filter "FullyQualifiedName~AuthoredRouteEndToEnd"
```

The end-to-end test drives the chain with a `TestConsole` whose keys are pushed, and establishes:

1. Three operations authored onto paths no naming rule produces, written under exactly those paths and
   verbs.
2. Each one placed where it was asked to be placed, with every pre-existing path still in its original
   position — this is the one that catches a splicer regression.
3. Each one carrying `À COMPLÉTER` as its description, and the report stating the count.
4. A field placed nowhere, named in the report and absent from the document.
5. The existing verification passing: nothing outside the selection moved.
6. The written document reparsing and revalidating as the OpenAPI version it declares.

## Validating by hand

The application refuses a non-interactive session by design, so the manual pass needs a real terminal:

```bash
dotnet run --project src/DevToolbox.Console
```

Choose *Fusionner deux fichiers en un seul*, give a production description and a `.wsdl`, keep two or three
operations, and check against [contracts/console-experience.md](./contracts/console-experience.md) that:

- the suggestion is pre-filled and its worth is named;
- the insertion choices appear in the document's own order;
- a composite field refused for the query names the field and its type;
- the summary before the accord states dropped fields and unwritten descriptions;
- the written file carries `À COMPLÉTER` and no `x-apidelta` member at all.

## Proving the guards bite

A guard that has not been seen to fail proves nothing — the rule this repository adopted after finding six
budgets that never bit. For each guard this feature adds, remove it and watch the test go red, then put it
back:

| Guard | Remove this | Expect |
|---|---|---|
| Path validation | the `RouteSyntax.Path` call in the prompt | the malformed-path test fails |
| Composite refusal | the `IsComposite` branch of `PlacementRules` | the composite test fails |
| Placeholder/field match | the `Check(route)` call | the placeholder test fails |
| Chosen insertion point | `AfterKey` in `SplicingEmitter.Adding` | the end-to-end ordering assertion fails |
| Description placeholder | the placeholder constant's use | the placeholder-count test fails |

If removing a guard leaves every test green, the guard is not yet tested, whatever the coverage says.

## After the feature

- `CONCEPTION.md` gains the rules this feature establishes, if any survive implementation unchanged.
- `specs/004-wsdl-delta-audit/spec.md` records the withdrawal of WR-105 as a dated clarification, since a
  published requirement is being retired.
- The report's new section is reflected in `README.md`'s description of what apidelta writes.
