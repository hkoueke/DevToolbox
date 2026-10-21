# Phase 0 — Research: prqueue

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Date**: 2026-08-25

Every decision below is recorded as *decision · rationale · alternatives rejected*. Items marked **VERIFY**
depend on the developer's own server and cannot be settled from documentation alone; each names the check
that settles it, and every one of them has a defined behaviour if the answer is "no".

---

## R1 — There is no collection-wide pull request query. The sweep is ours.

**Decision**: Enumerate the projects, then issue one pull request query **per project**, concurrently with
bounded parallelism. Merge the results into a single ordered queue.

**Rationale**: Microsoft's own cross-project guidance is explicit that no user interface feature and no
single API answers this across projects, and prescribes exactly this loop. The per-project endpoint
`GET {collection}/{project}/_apis/git/pullrequests` already spans **every repository in that project**, so
the loop is over projects, not repositories — an order of magnitude fewer calls than the naive advice.

This is also the finding that justifies the feature. The gap the tool fills is not a convenience; it is a
capability the platform does not offer at all on-premises.

**Alternatives rejected**:

- *Per repository* (`.../repositories/{id}/pullrequests`) — same data, one call per repository instead of
  one per project. Strictly worse.
- *Ask the developer to pick a project first* — removes the only thing the web interface cannot do.
- *Wait for a collection-level endpoint* — the cloud "My Work" view is not backed by one that on-premises
  servers expose.

---

## R2 — This endpoint pages by `$top`/`$skip`, not by continuation token

**Decision**: Add a skip/top paging helper to the **shared** `AzureDevOpsApiReader` alongside the existing
continuation-token one. Follow pages until a short page is returned.

**Rationale**: `AzureDevOpsApiReader.GetAllPagesAsync` follows the `x-ms-continuationtoken` header, which
the pull request endpoint does not use. A second tool needing a second paging idiom is the evidence that
paging is a shared infrastructure concern rather than a tool concern. The addition is purely additive — a
new method, no change to the existing one, whose behaviour stays pinned by the existing tests.

**Alternatives rejected**:

- *Paging helper inside the tool's Infrastructure* — the third tool that meets a `$skip` endpoint would copy
  it again, and the retry, cancellation, and failure-translation behaviour would then exist twice.
- *One unbounded request with a very large `$top`* — silently truncates. `GetAllPagesAsync` was written
  precisely so a caller never receives a quietly short list, and that discipline must not be abandoned here.

---

## R3 — Identity comes from the connection, not from the operating system

**Decision**: Resolve the developer's identity from the server's connection data, taking the authenticated
user's id. Announce it before any read. If it cannot be resolved, stop (FR-003).

**Rationale**: The reviewer filter is a server identity id, not a Windows account name. Only the server can
map the negotiated session to that id, and asking the operating system would produce a name the pull request
API cannot match. Reading it also proves the session actually authenticated, which makes the announcement
meaningful rather than decorative.

**VERIFY**: that `GET {collection}/_apis/connectionData` responds on this server version and carries
`authenticatedUser.id`. **If it does not**: the tool stops with an explanation, exactly as FR-003 requires —
there is no fallback that could be trusted, and guessing an identity would produce a confident wrong queue.

**Alternatives rejected**:

- *`Environment.UserName` / `WindowsIdentity`* — yields a domain account, not the collection identity id.
- *Ask the developer to type their identity* — an invitation to typos, and it would let one developer read
  another's queue by accident.

---

## R4 — Group reviewers: filter on the client, not on the server

**Decision**: Per project, read the **active pull requests** and match `reviewers[]` on the client against
the developer's identity and the groups they belong to. Mark each entry with which of the two routes put it
in the queue. Do **not** rely on `searchCriteria.reviewerId` as the source of truth.

**Rationale**: Two reasons, and the second is the decisive one.

1. FR-006 requires group-awaited entries to be *marked as such*. A server-side filter returns a matching set
   with no indication of why each row matched, so the distinction the requirement demands cannot be
   recovered from it.
2. Whether the server expands group membership when filtering by `reviewerId` is undocumented. Building on
   an unverified expansion is precisely the "short, confident, wrong list" the spec was written to avoid.

The payload supports client-side matching directly: reviewer entries carry `isContainer` to mark group
identities, `vote` (10 approved · 5 approved with suggestions · 0 no vote · −5 waiting for author · −10
rejected), `hasDeclined`, `isRequired`, and `votedFor`, which lists the groups through which a person's vote
was rolled up. "Awaited" is therefore *my reviewer entry exists, `vote == 0`, and `hasDeclined` is false* —
directly readable, no inference.

**Cost accepted**: reading all active pull requests per project rather than a filtered subset. Active pull
requests are a small population by nature; a project with a hundred open at once is unusual, and the bound
in R10 keeps the fan-out controlled.

**VERIFY**: whether membership resolution is available to a non-administrator on this server. **If it is
not**: FR-006a takes over — groups whose membership cannot be established are named as uninspected scope and
the queue is never called complete. The tool still works; it is honest about being narrower.

**Alternatives rejected**:

- *`searchCriteria.reviewerId` alone* — cheapest by far, and the one to revisit if validation V4 shows the
  server expands groups. It cannot satisfy the marking requirement on its own.
- *Resolve nothing, direct assignments only* — rejected in the spec's clarification session.

---

## R5 — The wait cannot be read, so it is renamed rather than faked

**Decision**: Order by the pull request's age since creation, and label the column as age. Recorded in the
spec as FR-008a; the original assumption is marked superseded.

**Rationale**: The payload carries `creationDate` and nothing that says when a reviewer was added or when a
pull request came back to someone. Reconstructing the real moment means reading each entry's iterations and
threads — one call per entry, turning a fixed number of reads into an unbounded one, to refine an ordering
that only needs to be approximately right. A column labelled honestly for what it measures costs nothing and
misleads nobody.

**Known imprecision, accepted and stated**: a pull request reopened after changes looks older than it has
been awaited.

**Alternatives rejected**:

- *Read iterations per entry* — the cost lands squarely on the 30-second target in SC-004 and buys ordering
  accuracy, not correctness.
- *Label it "waiting" anyway* — measuring one thing and naming it another is the failure this codebase's
  `Undetermined`/`Absent` discipline exists to prevent.

---

## R6 — Description is truncated by the list endpoint

**Decision**: Show the truncated description in the list; when an entry is opened (User Story 3), issue one
further read for that pull request to get the full description and merge detail.

**Rationale**: The by-project endpoint documents that description is truncated to 400 characters. One read
on explicit demand is the same shape as varcompare's detail view: the expensive, fuller thing happens only
when the developer asks for it.

---

## R7 — Blocking reasons come free with the payload

**Decision**: Derive "what is blocking this" from `mergeStatus` (`conflicts`, `rejectedByPolicy`, `queued`,
`succeeded`, `failure`) and from the reviewer votes already read. Defer full policy evaluation.

**Rationale**: This covers the cases a reviewer actually acts on — a conflicted pull request is not worth
reviewing yet, a rejected one is waiting on its author — without a second endpoint per entry. Naming *which*
policy is unsatisfied is a refinement, and it belongs with the deferred authored-pull-request section
(FR-012) where it is the whole point rather than a detail.

---

## R8 — Project enumeration is shared, and varcompare is not touched

**Decision**: Introduce `IProjectCatalog` in the shared Application layer with an implementation in the
shared Infrastructure. `prqueue` uses it. `varcompare` keeps `IVariableGroupGateway.ListProjectsAsync`
unchanged.

**Rationale**: Both tools must enumerate projects, but the constitution requires that adding a tool touch no
existing tool code. Moving varcompare's method into the shared layer would edit varcompare; duplicating the
route and DTO inside prqueue would put the same read in two places with two failure translations.

**Cost accepted and recorded in Complexity Tracking**: two ways to list projects coexist until someone
migrates varcompare deliberately. That migration is a separate, safe change, not a precondition for this
feature.

---

## R9 — Steps, so the run is resumable

**Decision**: Five named steps — `ResolveIdentity`, `ListProjects`, `SelectProjects`, `ReadPullRequests`,
`BuildQueue` — checkpointed after each success, mirroring varcompare's shape.

**Rationale**: Principle VIII requires named, individually recoverable steps. `ReadPullRequests` is the long
one and the one that fails, so it must be the one a retry resumes at. Every step is idempotent: they all
read.

**Checkpoint contents**: the selected project subset and step progress. Nothing else — no pull request
titles, no identities, no descriptions. The checkpoint store already refuses to write a record whose shape
has grown, and that guard covers this feature unchanged.

---

## R10 — Bounded parallelism, reusing the pattern that exists

**Decision**: `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` from a validated `PrQueueOptions`,
default 5 — the same construct and the same default as `RetrieveGroupsStep`.

**Rationale**: The constitution forbids unbounded concurrency. The fan-out here is over projects rather than
groups, but the shape, the cancellation behaviour, and the progress reporting are identical, so the tool
should look identical too. A reviewer who has read one should recognise the other.

---

## R11 — The remembered project subset lives in settings

**Decision**: Persist the subset chosen under FR-011 in the existing per-user settings file, keyed by
collection.

**Rationale**: The settings store already exists and is the right home for a preference. It is not run
state, so it does not belong in the checkpoint store, whose records expire after seven days — a preference
that evaporated weekly would be worse than none. Nothing stored is sensitive: project names the developer
can already read.

---

## Open questions carried into implementation

| # | Question | Settles it | If the answer is no |
|---|----------|-----------|---------------------|
| V-A | Does `_apis/connectionData` expose `authenticatedUser.id` on this server? | One request, quickstart V1 | The tool stops as FR-003 requires; the feature cannot ship without it |
| V-B | Can a non-administrator resolve their own group memberships? | Quickstart V4 | FR-006a: groups are named as uninspected, queue never called complete |
| V-C | Does `searchCriteria.reviewerId` expand groups server-side? | Quickstart V4 compares both result sets | Nothing changes — R4 already does not depend on it. A "yes" merely unlocks a future optimisation |
| V-D | How many projects and how many active pull requests does this collection actually hold? | Quickstart V2 reports counts and elapsed time | If SC-004 cannot be met, FR-011's remembered subset is the mitigation, not a redesign |
