# Contract: Console experience (read-only)

The UI contract for the DevToolbox shell and the varcompare tool. Spectre.Console is the only renderer
(Principle V). This is what acceptance tests assert against `TestConsole` output.

---

## 1. Shell

```
┌─ DevToolbox ────────────────────────────────────────────────────────────────┐
│  [ Azure DevOps ]   Git   Utilities        devops.entreprise.local ▸ Default │  ← tab strip + target
├─────────────────────────────────────────────────────────────────────────────┤
│  ▸ varcompare — compare Azure DevOps variable groups (read-only)            │
│    …                                                                        │
│    ← Back        ⏻ Exit                                                     │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Invariants**
- Exactly one active tab; tabs come from registered `ITool.Tab` values, never a hardcoded list.
- The root menu always offers **Exit**; every sub-menu always offers **Back** (FR-001).
- The **server and collection** are visible before any read happens (FR-028c). No identity or credential is
  shown, because none is held — authentication is the ambient Windows session.
- `Ctrl+C` cancels cooperatively: token trips → in-flight work cancels → checkpoint flushes → sinks dispose →
  documented non-zero exit code. It is never the only way out.
- Non-interactive profile ⇒ one explanatory line, then a clean exit. Never hang on a prompt.

---

## 2. varcompare step flow

| # | Step | Screen | Requirements |
|---|------|--------|--------------|
| 1 | `IdentifyProject` | detected collection/project shown for confirmation, or project list | FR-002a/b/c/d |
| 2 | `ListGroups` | selectable list: name, ordinary/key-vault marker, filter box | FR-003, FR-005 |
| 3 | `SelectGroups` | multi-select, ≥2 enforced with an explanation, selection preserved on error | FR-004 |
| 4 | `RetrieveGroup:{id}` | `Progress` bar per group; retries visible as "retrying (2/3)…" | FR-036 |
| 5 | `BuildComparison` | the comparison + legend + "as at" read time | FR-006–FR-017 |

There is no write step. The tool ends at the comparison, with a refresh option.

---

## 3. Cell rendering

Colour is reinforcement; the symbol and text carry the meaning (FR-012, SC-003).

| State | Symbol | Text | Colour |
|-------|--------|------|--------|
| `PresentWithValue` | `✔` | `set` | green |
| `PresentEmpty` | `○` | `empty` | yellow |
| `PresentSecret` | `🔒` / `#` ascii | `secret` | blue |
| `Absent` | `✘` | `missing` | red |
| `Undetermined` | `?` | `unknown` | grey |

Qualifiers append a marker: key-vault-sourced `[kv]` (magenta), read-only `[ro]` (dim). A row where
`ReadableValuesDiffer` is marked `≠` in its header. Casing divergence is marked `~` with a footnote.

**The legend is always rendered** beneath the comparison (FR-013). Unicode symbols degrade to the ASCII
column when the profile lacks Unicode.

**No cell ever contains a value** (FR-015a, SC-012).

---

## 4. Layout switching

- `Auto` (default): measure `AnsiConsole.Profile.Width` against required column widths; if the side-by-side
  grid does not fit, **warn first**, then render stacked (FR-016b).
- Manual toggle available at any group count (FR-016d).
- Stacked form carries identical states, symbols, legend, and differences-only filtering (FR-016c).
- Neither layout may drop a group or a row; if everything cannot be shown, say so rather than render a
  partial result (FR-016e).

---

## 5. Detail view

Opened only by an explicit action on a chosen variable (FR-015b). Shows, per group: state, `NameAsStored`,
and the readable value. Secret and key-vault-backed entries show `value exists — not retrievable`, never a
placeholder that could be mistaken for content (FR-015c). Long or multi-line values are shortened with an
explicit indicator (FR-017).

---

## 6. Read-only affordances

The tool offers no way to change anything (FR-018c). Where it would be useful, it tells the developer **where
to go**:

```
Api__Timeout is missing from 'app-prod'.
This tool is read-only — add it in Azure DevOps:
  Pipelines ▸ Library ▸ app-prod
```

**Forbidden anywhere in the UI**: an "edit", "add", "set value", or "apply" action; a value entry prompt; a
username, password, or token prompt. Asserted by a test that enumerates every reachable prompt (SC-007).

---

## 7. Failure and recovery

Any step failure renders the four choices, always in this order (FR-034):

```
Step 'RetrieveGroup:app-uat' failed: the server is not responding.
  1) Retry this step
  2) Continue from this step
  3) Restart varcompare from the beginning
  4) Abort
```

- *Continue* must not re-fetch groups already retrieved in this session (FR-035, SC-008).
- *Abort* states which groups had been read and which had not (FR-040).
- On next launch an interrupted run is offered for resume or discard, is re-read rather than restored from
  remembered contents, and expired runs (>7 days) are gone (FR-039a–d).

Authentication failure is distinct and terminal for the run:

```
The server rejected your Windows credentials.
  server      devops.entreprise.local
  collection  DefaultCollection
Check that you are on the corporate network and that your account has access.
```

No credential prompt follows. Ever (FR-032).

---

## 8. What must never appear on screen

Secret values · key-vault-backed values · any credential material · stack traces · raw response bodies · file
paths outside the working root. Asserted by tests over `TestConsole` output and the captured log (SC-004).
