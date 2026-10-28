# Data Model: Authoring a route instead of deriving one

Records and pure functions, all in `DevToolbox.Tools.ApiDelta.Core.Authoring` unless stated. Every type
here is reachable in a unit test with no console and no filesystem, which Principle II requires of
`Core`.

---

## `ParameterLocation` (enum)

Where a field of an operation's input travels.

| Member | Meaning |
|---|---|
| `Path` | A path parameter. Always required (RA-014). |
| `Query` | A query parameter. |
| `Body` | Part of the request body, carrying the reference the projection derived. |
| `Header` | A header parameter named by the developer. |
| `Nowhere` | Deliberately dropped. Recorded and reported, never silently omitted (RA-017). |

`Nowhere` is a member rather than an absence so that a dropped field is a decision in the record, not a
gap in it. A field missing from the record would be indistinguishable from a field nobody was asked about.

---

## `FieldPlacement` (record)

One field of one operation's input, and what the developer decided about it.

| Member | Type | Rule |
|---|---|---|
| `Field` | `string` | The field's name as the contract declares it. Never empty. |
| `Declared` | `string` | Its type as the contract declares it, for a refusal that can name it. |
| `IsComposite` | `bool` | True when the type is not a scalar. Decided by the projection, not re-derived here. |
| `Location` | `ParameterLocation` | Where it goes. |
| `Required` | `bool` | What the developer answered (RA-012). Forced true for `Path` (RA-014). |
| `Name` | `string?` | The parameter name when it differs from the field — a header, typically. Null otherwise. |

**Invariants**

1. `Location is Path` implies `Required` — enforced in the constructor, not left to the caller.
2. `IsComposite` and `Location is Path or Query or Header` is refused (RA-013). The refusal carries
   `Field` and `Declared` so the message can name both.
3. `Location is Nowhere` implies `Required` is false. A dropped field cannot be required.

---

## `AuthoredRoute` (record)

What the developer settled for one operation.

| Member | Type | Rule |
|---|---|---|
| `Operation` | `string` | The contract's operation name. The key of the record. |
| `Path` | `string` | A validated OpenAPI path template (`RouteSyntax`). |
| `Method` | `HttpOperationMethod` | The existing enum, reused rather than restated. |
| `Placements` | `IReadOnlyList<FieldPlacement>` | Every field of the input, placed or dropped. Never empty when the operation declares fields. |
| `Headers` | `IReadOnlyList<string>` | The component names of accepted reusable headers (RA-022). |
| `Insertion` | `InsertionPoint` | Where in production the route goes. |
| `Suggestion` | `RouteDerivationKind` | What the pre-filled suggestion was worth — derived, partial or fallback (RA-002). |
| `TookSuggestion` | `bool` | Whether the developer accepted it unchanged. Feeds RA-028. |

**Invariants**

1. Every `{placeholder}` in `Path` is matched by a `FieldPlacement` whose `Location is Path` and whose
   effective name equals the placeholder (RA-015).
2. No two placements share an effective name within the same location.
3. `Headers` names components that production declares; an unknown name is a defect, not a new parameter.

---

## `InsertionPoint` (record)

Where an authored route joins the production document (RA-008).

| Member | Type | Rule |
|---|---|---|
| `AfterPath` | `string?` | The existing path the new one follows. Null means the end of `paths`. |

Expressed as "after which existing path" rather than as an index, because an index would be invalidated by
the route authored before it in the same run, and because a developer chooses a neighbour they can read,
not a position they must count (RA-009).

---

## `AuthoringRecord` (record)

Every authored route of one run.

| Member | Type | Rule |
|---|---|---|
| `Routes` | `IReadOnlyList<AuthoredRoute>` | In the order the developer authored them. |

**Behaviour**

- `Dropped()` — every `FieldPlacement` whose `Location is Nowhere`, with its operation, for the report.
- `Placeholders()` — one entry per authored route, for the count RA-020 requires.
- `Collisions(production)` — pairs of authored routes sharing a path *and* a verb, and authored routes
  colliding with a production route, for RA-006. Returned rather than thrown: the caller names both sides.
- `Empty` — the record of a run where nothing was authored, so the step has a value to carry when the
  developer keeps nothing (RA-003).

---

## `RouteSyntax` (static)

Validates what the developer typed, before it is used for anything (RA-004, RA-005, and the STRIDE finding
of the plan).

| Function | Returns |
|---|---|
| `Path(string typed)` | Success, or a refusal naming what is wrong |
| `Method(string typed)` | The `HttpOperationMethod`, or a refusal listing what OpenAPI defines |
| `Placeholders(string path)` | The placeholder names, in order, for the RA-015 check |

**Rules a path must satisfy**

1. Starts with `/`.
2. No whitespace anywhere.
3. Braces balanced, never nested, never empty.
4. No duplicate placeholder name.
5. No empty segment (`//`).
6. No segment that would change meaning once escaped as a JSON Pointer segment — this is the rule that
   came out of the STRIDE pass, and it exists because `SpecPointer` escapes segments and a path carrying
   `~` could otherwise build a pointer designating a different node.

Each failure names the rule it broke and the offending position. None repairs the input.

---

## `PlacementDefaults` (static)

The per-verb proposal (RA-011), and nothing else.

| Function | Returns |
|---|---|
| `For(method, fields)` | One `FieldPlacement` per field, pre-set |

`GET` and `DELETE` pre-set scalars to `Query`; `POST`, `PUT` and `PATCH` pre-set everything to `Body`. A
composite under `GET` or `DELETE` is pre-set to `Nowhere` **and flagged as needing a decision**, so that
accepting the defaults wholesale cannot silently drop it.

---

## `PlacementRules` (static)

The refusals, separated from the defaults because a refusal and a proposal are not the same thing.

| Function | Returns |
|---|---|
| `Check(placement)` | A refusal, or nothing — invariants 2 and 3 of `FieldPlacement` |
| `Check(route)` | Refusals for the placeholder/field mismatch and duplicate names |

---

## `OfferedHeader` (record) and `ReusableHeaders` (static)

| Member | Type | Meaning |
|---|---|---|
| `Component` | `string` | The component name under `#/components/parameters`. |
| `Name` | `string` | The header's wire name, for display. |
| `Routes` | `int` | How many production operations reference it. |

`ReusableHeaders.In(production)` returns them, most-referenced first, keeping only those referenced at
least once (RA-021, RA-023). A production declaring none yields an empty list, and the prompt then asks
nothing.

---

## Changes to existing types

| Type | Change | Why |
|---|---|---|
| `Splice` | Gains `AfterKey` (`string?`) | The splicer must add a member after a named sibling, not only at the end (RA-008) |
| `CorrectionRequest` | Gains `AuthoringRecord` | The builder assembles authored routes from placements instead of copying the projected operation |
| `RunSelection` | Gains `Authoring` | So the record survives a resume (RA-019) |
| `ApiDeltaSelectionKinds` | Two new kinds | One per authored route, one per placement, both strings |
| `CorrespondenceEntry`, `CorrespondenceOrigin`, `ICorrespondenceProposer`, `ApiDeltaContextKeys` | Lose the `Declared` members and the `declared` parameter | WR-105 withdrawn (RA-026) |

## What is deliberately not modelled

- **No description type.** The placeholder is a constant string, not a record. Modelling it would invite a
  second placeholder.
- **No requiredness inference.** There is nothing to infer from, as the specification establishes.
- **No response placement.** Responses keep what the projection derived; out of scope.
- **No authoring file format.** RA-025 forbids the file, so there is no format to model.
