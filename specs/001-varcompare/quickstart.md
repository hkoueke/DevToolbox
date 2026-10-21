# Quickstart & validation: varcompare (read-only)

How to build, run, and prove this feature works. Written for a developer on a domain-joined Windows machine
with access to the on-premises Azure DevOps Server.

---

## Prerequisites

| Need | Check |
|------|-------|
| .NET 10 SDK | `dotnet --version` → `10.*` |
| On the corporate network or VPN | the configured server host resolves and responds |
| Windows account with read access to the collection | you can open the Library page in a browser |
| A project with ≥2 variable groups | portal → Pipelines → Library |

**You will never be asked for a username, password, or token.** The tool uses your Windows session
(integrated authentication). If the server rejects it, the tool says so and stops — it does not prompt.

**No disposable project is needed.** This tool is read-only; it cannot alter anything.

Configure the target in `appsettings.json`:

```jsonc
{
  "AzureDevOpsServer": {
    "BaseUrl": "https://devops.entreprise.local",
    "Collection": "DefaultCollection",
    "ApiVersion": "7.1"
  }
}
```

Sanity-check connectivity with your own session before running the tool:

```powershell
Invoke-RestMethod -UseDefaultCredentials `
  "https://devops.entreprise.local/DefaultCollection/_apis/projects?api-version=7.1" |
  Select-Object -ExpandProperty value | Select-Object name
```

---

## Build and run

```bash
dotnet restore
dotnet build            # must be clean: warnings are errors
dotnet test
dotnet run --project src/DevToolbox.Console
```

Then: **Azure DevOps** tab → **varcompare**.

Logs: `%LOCALAPPDATA%\DevToolbox\logs\` · Run state: `%LOCALAPPDATA%\DevToolbox\runs\` ·
Settings: `%APPDATA%\DevToolbox\settings.json`

---

## Validation scenarios

### V1 — Compare two groups (US1 · SC-001, SC-002)

**Setup**: two groups with deliberate drift — one variable in both, one missing from the second, one present
but empty.

1. Launch inside a folder cloned from a repo on the configured server → collection and project are
   **detected and shown for confirmation** (FR-002a).
2. Select both groups → comparison renders.

**Expect**: union of variable names, one row each; the shared variable `✔ set` in both; the missing one
`✘ missing` in the second; the empty one `○ empty`. Total row count stated, plus the "as at" read time.
Under 60 seconds, ≤5 interactions.

**Also run outside any repo** → offered the projects you can access in the configured collection (FR-002b).

### V2 — Read the states correctly (US2 · SC-003, SC-004, SC-012)

**Setup**: one ordinary group with a secret and a read-only variable; one **key-vault-backed** group.

**Expect**: key-vault column labelled, entries marked `[kv]`; secret shows `🔒 secrète`, never a value;
read-only marked `[ro]`; legend present. Every word on screen is French (FR-012a).

Then:
- Open a variable's detail view → values appear **only here** (FR-015b).
- Before opening anything, no value is anywhere on screen (SC-012).
- Toggle **differences only** → identical rows hidden, hidden count stated (FR-014).
- Re-run with colour disabled (`NO_COLOR=1` or a monochrome profile) → every state still identifiable (SC-003).
- Inspect the log file and the run file → zero secret values (SC-004).

### V3 — Many groups (US2 · SC-011)

Select more groups than fit the terminal width.

**Expect**: a warning, then the **stacked layout**; every variable and every group still present; manual
toggle back to side-by-side works; nothing silently dropped.

### V4 — Read-only guarantee (FR-018 · SC-006, SC-007)

- Walk every screen and every prompt: there is **no** edit, add, set-value, or apply action anywhere
  (FR-018c), and **no** username, password, or token prompt (SC-007).
- Where a variable is missing, the tool points at the Library page instead of offering to fix it.
- Inspect the request log for a full session: **100% `GET`** (SC-006). The architecture test enforces this at
  build time, but confirm it once against a real run.
- After a full session, re-check the groups in the portal — `modifiedOn` is unchanged on every one of them.

### V5 — Failure and recovery (US3 · SC-008)

1. Start a comparison of 3+ groups, then break connectivity mid-run (disconnect the VPN, or point at an
   unreachable host).
2. **Expect** visible retries first (FR-036), then the four choices in order.
3. Choose **Continue** → already-retrieved groups are **not** fetched again (SC-008).
4. Choose **Restart** → starts clean.
5. Choose **Abort** → states which groups were read and which were not.
6. Kill the process mid-run, relaunch → the interrupted run is offered; resuming **re-reads** the groups
   (FR-039b); the run file contains **no values** (FR-039a).
7. Backdate a run file >7 days and relaunch → it is swept (FR-039c).

### V6 — Session and target behaviour (FR-028, FR-032)

- Normal launch → the **server and collection** are announced before any call (FR-028c).
- Disconnect from the corporate network → `ServerUnreachable`, reported distinctly from a permission failure.
- Run as an account the server does not accept → "the server rejected your Windows credentials", naming the
  server and collection, then back to the menu. **No credential prompt** (FR-032).
- Point `BaseUrl` at an `http://` URL → refused at startup unless the explicit insecure opt-in is set
  (FR-031a).
- Answer the first-run prompt with the short host name `azure`, or `azure/` → accepted and saved as
  `https://azure/` (FR-031a).

### V7 — Permissions (FR-029)

Use an account that can read some groups but not others. **Expect**: readable groups still compared, the
unreadable columns marked, and a plain permission message — not an empty result and not a crash.

---

## Definition of done for this feature

- [ ] `dotnet build` clean with warnings-as-errors and Meziantou / AsyncFixer / Sonar active
- [ ] `dotnet test` green, including retry-exhaustion, circuit-breaker-open, resume, and restart tests
- [ ] Architecture test passes: no inward dependency violation, and **no HTTP verb other than `GET`** in the
      gateway (Principle I, FR-018b)
- [ ] V1–V7 executed against the real server
- [ ] Logs and run files inspected: zero values, zero credential material (SC-004)
- [ ] STRIDE notes in `plan.md` reviewed, each threat mitigated or explicitly accepted
