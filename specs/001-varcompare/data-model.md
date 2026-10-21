# Phase 1 Data Model: varcompare (read-only)

**Feature**: `specs/001-varcompare/` | **Date**: 2026-08-20 | **Revision**: 2

Types live in `DevToolbox.Tools.VarCompare.Core` (pure — no I/O, no HTTP, no Spectre) unless marked
otherwise. All immutable, collections exposed read-only, `sealed` by default.

**Revision 2**: write-side types (`WriteRequest`, `WritePlan`, `WriteOutcome`) and `GroupFingerprint` are
removed — the tool is read-only and there is no longer anything to conflict with. `Organization` is replaced
by `ServerTarget` because the target is an on-premises Azure DevOps Server with a collection segment.

---

## 1. Where we are pointed

### `ServerTarget` (value object, from validated options — lives in **`DevToolbox.Domain`**, shared with tool #2)

| Field | Type | Rules |
|-------|------|-------|
| `BaseUrl` | `Uri` | Absolute; **HTTPS required** unless `AllowInsecureHttp` is explicitly set (FR-031a) |
| `Collection` | `string` | Required. 1–64 chars, no path separators, no `..`, no control characters |
| `ApiVersion` | `string` | Default `7.1` |

`ToCollectionSegment()` is the only sanctioned way to place the collection into a URL — escaped, validated.

### `ProjectIdentifier` (value object — lives in **`DevToolbox.Domain`**, shared with tool #2)

| Field | Type | Rules |
|-------|------|-------|
| `Id` | `Guid?` | Present once resolved from the projects endpoint |
| `Name` | `string` | Required. 1–64 chars, no path separators, no `..`, no control characters |

`ToRouteSegment()` returns `Id` when known, otherwise `Uri.EscapeDataString(Name)` (FR-031c).

### `RepositoryOrigin` (produced by Infrastructure, FR-002a)

| Field | Type | Notes |
|-------|------|-------|
| `Collection` | `string` | Derived relative to the configured base URL, so a virtual directory such as `/tfs` is not mistaken for the collection |
| `Project` | `ProjectIdentifier` | Name only at this stage |
| `RepositoryName` | `string` | Display only |
| `RemoteName` | `string` | e.g. `origin` — shown when confirming |
| `SourceUrl` | `string` | The raw remote, shown so the developer can sanity-check it |

**Rule**: accepted only when the remote's host matches the configured server host; otherwise `None` and the
tool asks (FR-002d).

---

## 2. Variable groups

### `VariableGroupSummary`

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `int` | |
| `Name` | `string` | |
| `Description` | `string?` | |
| `Origin` | `VariableGroupOrigin` | `Ordinary` \| `KeyVaultBacked` |
| `IsShared` | `bool` | |

### `VariableGroupOrigin` (enum)

`Ordinary` (wire `type == "Vsts"`) | `KeyVaultBacked` (wire `type == "AzureKeyVault"`).

Drives the key-vault-sourced qualifier on every cell of that group's column (FR-009).

### `VariableGroupSnapshot`

| Field | Type | Notes |
|-------|------|-------|
| `Summary` | `VariableGroupSummary` | |
| `Variables` | `IReadOnlyDictionary<string, VariableEntry>` | Key comparer **`OrdinalIgnoreCase`** (R7) |
| `ModifiedOn` | `DateTimeOffset?` | Informational — displayed, guards nothing |
| `RetrievedAt` | `DateTimeOffset` | From the injected clock; shown as "as at …" |

**Lifetime**: in-memory only. Never serialized to the recovery file (FR-039a).

### `VariableEntry`

| Field | Type | Notes |
|-------|------|-------|
| `Name` | `string` | Original casing as stored in *this* group |
| `Value` | `string?` | `null` when secret or key-vault-backed |
| `IsSecret` | `bool` | |
| `IsReadOnly` | `bool` | |

Derived: `IsEmpty => Value is not null && Value.Length == 0`.

---

## 3. The comparison

### `VariableComparison` (aggregate root)

| Field | Type | Notes |
|-------|------|-------|
| `Groups` | `IReadOnlyList<VariableGroupSummary>` | Column order = selection order |
| `Rows` | `IReadOnlyList<ComparisonRow>` | Sorted by canonical name, ordinal-ignore-case |
| `Summary` | `ComparisonSummary` | |
| `RetrievedAt` | `DateTimeOffset` | Earliest snapshot time — the "as at" the UI states |

**Construction rules**: rows are the union of names across all groups matched case-insensitively (FR-006,
FR-007); exactly one `CellState` per (row, group) — a total function, no gaps (FR-008); fewer than two groups
is rejected (FR-004).

### `ComparisonRow`

| Field | Type | Notes |
|-------|------|-------|
| `CanonicalName` | `string` | Uppercase-invariant matching key |
| `DisplayName` | `string` | Casing from the first group that has it |
| `Cells` | `IReadOnlyList<ComparisonCell>` | Same order and length as `Groups` |
| `HasCasingDivergence` | `bool` | Groups spell the name differently |
| `ReadableValuesDiffer` | `bool` | Present everywhere, readable values not identical (FR-011) |
| `IsPresentEverywhere` | `bool` | |

### `ComparisonCell`

| Field | Type | Notes |
|-------|------|-------|
| `State` | `CellState` | |
| `IsKeyVaultSourced` | `bool` | True iff the group is `KeyVaultBacked` |
| `IsReadOnly` | `bool` | |
| `NameAsStored` | `string?` | `null` when absent |

**The cell deliberately carries no value.** Values are read from the snapshot only when the detail view is
opened — which is what makes FR-015a and SC-012 structurally true rather than a UI convention.

### `CellState` (enum)

| Member | Condition |
|--------|-----------|
| `Absent` | name not in the dictionary |
| `PresentWithValue` | `Value` non-null and non-empty |
| `PresentEmpty` | `Value == ""` |
| `PresentSecret` | `IsSecret == true` (the API returns `value: null`) |
| `Undetermined` | group retrieval degraded — unreachable vault, or partially readable |

### `ComparisonSummary`

`TotalVariables`, `PresentEverywhere`, `MissingSomewhere`, `DifferingValues`, `SecretCount`,
`KeyVaultSourcedCount`, `ReadOnlyCount`. Backs the stated total (FR-016) and the hidden-row count (FR-014).

### `VariableDetail` (on demand, FR-015b)

`DisplayName` plus `PerGroup`: `IReadOnlyList<{ GroupName, State, Value (null when unreadable),
NameAsStored, IsReadOnly, IsKeyVaultSourced }>`.

Built from in-memory snapshots. Never logged, never persisted (FR-015d).

---

## 4. Run and recovery

### `ToolRun`

`RunId` (`Guid`, correlation id for every log line — FR-041), `ToolId` (`"varcompare"`), `Steps`,
`StartedAt`, `EndedAt?`, `Outcome` (`Success` | `Failed` | `Cancelled` | `Abandoned`).

### `StepState`

`Name` — one of `IdentifyProject`, `ListGroups`, `SelectGroups`, `RetrieveGroup:{id}`, `BuildComparison` —
plus `Status` (`Pending` | `Running` | `Completed` | `Failed` | `Skipped`), `StartedAt`, `EndedAt`,
`FailureReason` (`string?`, message only — never an exception dump to the user).

**Transitions**: `Pending → Running → (Completed | Failed)`. From `Failed`, the four recovery choices
(FR-034) map to: *retry* → `Running` again · *continue* → `Running` from this step, earlier `Completed` steps
untouched · *restart* → all steps `Pending`, snapshots discarded · *abort* → `Outcome = Abandoned`.

Every step in this tool is idempotent — a consequence of being read-only — so *continue* is always safe.

### `PersistedRun` (Application — defined beside `IRunCheckpointStore`; the only thing that survives the process)

| Field | Type |
|-------|------|
| `RunId` | `Guid` |
| `ToolId` | `string` |
| `Collection` | `string` |
| `Project` | `string` |
| `SelectedGroups` | `IReadOnlyList<{ Id: int, Name: string }>` |
| `CompletedSteps` | `IReadOnlyList<string>` |
| `StartedAt` / `LastUpdatedAt` | `DateTimeOffset` |

**Invariant, asserted by a test that inspects the serialized JSON**: no field may ever hold a variable value
(FR-039a, SC-008). `VariableGroupSnapshot` is deliberately unreachable from this type. Files older than 7 days
are swept at startup (FR-039c).

---

## 5. Session

There is no credential type, no token type, and no `ResolvedSession`. Authentication is ambient: the HTTP
handler presents the process's Windows credentials (R2). The only session-shaped thing the app holds is what
it displays for confirmation:

### `TargetAnnouncement`

`ServerHost` (`string`), `Collection` (`string`), `AuthenticationMode` (constant: *integrated Windows
authentication*). Rendered before any read (FR-028c).

**By construction, there is nothing here to leak** — no token, no password, no username entry. SC-007 (zero
credential prompts reachable) is satisfied by the absence of any type that could carry one.

---

## 6. Mapping to the wire format

Infrastructure-only DTOs, `System.Text.Json` with explicit options, no polymorphism (Principle III).

| Wire (`VariableGroup`) | Domain |
|------------------------|--------|
| `id`, `name`, `description` | `VariableGroupSummary` |
| `type` = `"Vsts"` / `"AzureKeyVault"` | `Origin` |
| `isShared` | `IsShared` |
| `modifiedOn` | `ModifiedOn` |
| `variables[name] = { value, isSecret, isReadOnly }` | `VariableEntry` |
| `variableGroupProjectReferences` | **not mapped** — only needed when updating a group, which this tool never does |

List responses are `{ count, value: [...] }`, paged via the `x-ms-continuationtoken` response header and
followed to exhaustion (R4). Unknown wire fields are ignored.
