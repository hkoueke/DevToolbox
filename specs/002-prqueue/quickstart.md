# Phase 1 — Quickstart & validation: prqueue

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Date**: 2026-08-25

How to build, run, and prove the feature works. V1 and V4 also settle the open environment questions from
[research.md](./research.md) — run them **first**, before implementation gets far, because V1's answer
decides whether the feature can ship at all.

## Prerequisites

- Domain-joined Windows 11, on the corporate network or VPN
- .NET 10 SDK
- An account with read access to at least two projects in the collection
- At least one pull request awaiting your review, in a project you can read

No token, no password, no configuration beyond the server address the toolbox already asks for on first run.

## Build and run

```bash
dotnet build DevToolbox.slnx        # warnings are errors
dotnet test DevToolbox.slnx
dotnet run --project src/DevToolbox.Console
```

Then: tab **Azure DevOps** → **prqueue**.

---

## V1 — Identity resolves, and is announced first  *(settles V-A)*

**Run this before writing the gateway.** If the answer is no, FR-003 says the tool stops, and there is no
design that recovers — the feature does not ship.

1. Start the tool.
2. Confirm the target panel appears **before** any progress bar, and names you.
3. Confirm the name matches the account the server knows you by.

**Passes when**: the identity is on screen before the first read, and it is right.

**If it fails**: capture what `_apis/connectionData` returned. The route is not in the published REST
reference and its availability on this server version is exactly what this step establishes. Stop and report
before building further — every later step depends on it.

---

## V2 — The sweep completes, and the cost is known  *(settles V-D)*

1. Run with every project selected.
2. Note the project count and the elapsed time from the progress display.

**Passes when**: the queue appears within 30 seconds for up to 50 projects (SC-004), with progress visible
throughout — never an apparently hung screen.

**If it is slower**: FR-011's remembered subset is the mitigation, not a redesign. Record the real numbers
here; they are what the performance criteria should have been calibrated against.

---

## V3 — Entries from several projects, in one list, oldest first

1. Ensure you are a reviewer on pull requests in **two different projects**.
2. Run the tool without narrowing scope.

**Passes when**: both appear in one list, no project was ever chosen to see them, and the list is ordered by
age descending. Cross-check one entry against the web interface: repository, number, title, author, target
branch.

This is SC-001 and the reason the feature exists.

---

## V4 — Group-awaited entries, and what the server will tell us  *(settles V-B and V-C)*

The most informative scenario. Arrange a pull request whose reviewer is a **group you belong to**, and where
you are *not* named individually.

1. Confirm it appears in the queue, marked `⧉ équipe` rather than `personnel` (FR-006).
2. Separately, query the same project two ways and compare the result sets:
   - all active pull requests, filtered on the client (what the tool does)
   - the same request with `searchCriteria.reviewerId={your id}`

**Passes when**: the group-awaited entry appears, and its route is marked.

**What the comparison tells you**:

- *Both sets contain it* → the server expands groups. Record it: the server-side filter becomes a legitimate
  optimisation for a later revision (V-C). It does not change this design, which still needs client-side
  matching to know *why* each row matched.
- *Only the client-side set contains it* → the server does not expand groups, and R4's decision was
  load-bearing rather than merely cautious.

**If memberships cannot be resolved at all** (V-B): confirm the tool degrades as FR-006a requires — the
group is named under `groupes` in the scope line, and the queue is **not** reported as complete. That
degradation is a passing outcome, not a failure.

---

## V5 — The two sentences are distinguishable

The one that matters most. Both halves must be exercised.

1. **Complete and empty**: run where nothing awaits you and every project reads. Expect
   *"Rien n'attend votre relecture. N projets lus sur N…"*.
2. **Partial and empty**: repeat where at least one project returns `403` — a project you cannot read.
   Expect *"Rien n'attend votre relecture dans ce qui a pu être lu. File incomplète…"* with the project
   named.

**Passes when**: the two are unmistakably different on screen, and the second names what was missed.

**Fails when**: a project that could not be read is silently absent. That turns "nothing is waiting" into a
falsehood and is the single most damaging defect this feature can carry.

---

## V6 — Already answered means gone

For each case, confirm the pull request is **absent** from the queue:

| Case | Why it must not appear |
|------|------------------------|
| You approved it | FR-007 — you answered |
| You voted "waiting for author" | FR-007 — you answered |
| You **declined** the review | FR-007a — a decline is an answer, not silence |
| It is a **draft** | FR-009 — its author has not asked for review |
| It is completed or abandoned | FR-010 |
| **You authored it** | FR-012 — even if you are also a reviewer |

Then confirm the queue states it shows reviews awaited from you and not your own pull requests (FR-012a).

---

## V7 — Detail on demand, and no way to act

1. Open an entry.
2. Confirm the full description appears — longer than the 400 characters the list endpoint returns, if the
   pull request has one.
3. Confirm every reviewer and vote matches the web interface.
4. Confirm the closing line names where the review happens.

**Passes when**: there is **no** option anywhere to vote, comment, complete, or abandon (FR-017, FR-024).
Read every menu on the path, not just the detail view.

---

## V8 — Cancellation and resume leave a described state

1. Start a sweep over many projects and press `Ctrl+C` mid-way.
2. Confirm the abort summary names how many projects were read and which were not, and states that nothing
   was modified.
3. Relaunch. Confirm the interrupted run is offered, that the selected project scope is restored, and that
   the pull requests are **re-read** rather than restored from the checkpoint.

**Passes when**: resuming re-reads. A queue restored from a checkpoint would show yesterday's reviews as
today's, which is the same class of lie as a silently skipped project.

---

## Automated coverage

What the suite must assert, so these scenarios are not the only guard:

| Area | Assertion |
|------|-----------|
| Architecture | No file under `tools/VarCompare/` is referenced by prqueue; the new gateway uses no verb but `GET` |
| Infrastructure | `$skip` paging accumulates every page and fails whole on a bad page; `403` becomes a named uninspected project; identity failure is terminal |
| Core | Awaited = reviewer present, `vote == 0`, not declined; declined, voted, draft, authored and completed all excluded; ordering by age descending; `IsComplete` false whenever anything is uninspected |
| Presentation | No list renders without an identity; the complete-empty and partial-empty sentences differ; states legible with colour disabled; the back entry is reachable and marked |
| Logging | `RecordingLogger` sees no title, description, or reviewer name across a full run |
