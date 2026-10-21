# Contract — Azure DevOps REST surface consumed by prqueue

**Feature**: [../spec.md](../spec.md) · **Date**: 2026-08-25

Four reads. **Every one is a `GET`.** No other verb is expressible: `HttpMethod.Post/Put/Patch/Delete` are
banned solution-wide by `BannedSymbols.txt` and fail the build at the call site.

Routes are built relative to the configured base address, with the collection taken as a relative segment so
an installation behind a virtual directory such as `/tfs` works unchanged. Every segment is validated and
`Uri.EscapeDataString`-escaped before it reaches a URL.

---

## 1. Who am I

```http
GET {collection}/_apis/connectionData?api-version={v}
```

**Consumed**: `authenticatedUser.id` (Guid), `authenticatedUser.displayName`.

**Used for**: FR-002, FR-003. Resolved once per run, before any other read.

**On failure**: the run stops with an explanation. There is no fallback — a queue filtered by a guessed
identity is worse than no queue (R3).

> **VERIFY (V-A)**: this route is not part of the published REST reference and its availability on a given
> Azure DevOps Server version must be confirmed against the developer's own server. Quickstart V1 settles it.

---

## 2. Which projects exist

```http
GET {collection}/_apis/projects?api-version={v}&$top=100
```

Paged by the `x-ms-continuationtoken` response header. Served by the shared `IProjectCatalog` (R8), which
uses the existing `AzureDevOpsApiReader.GetAllPagesAsync`.

**Consumed**: `id`, `name`.

**Used for**: FR-004, FR-011.

---

## 3. The pull requests of one project

```http
GET {collection}/{project}/_apis/git/pullrequests?api-version={v}&$top={n}&$skip={m}
```

The workhorse. **Spans every repository in the project**, which is why the sweep iterates projects rather
than repositories (R1).

**Search criteria**: none sent. `searchCriteria.status` defaults to `active`, which is exactly what FR-010
requires, and `searchCriteria.reviewerId` is deliberately **not** used — see R4: a filtered result cannot say
*why* each row matched, and FR-006 requires that distinction.

**Paged by `$top`/`$skip`, not by continuation token.** This is why the shared reader gains a second paging
helper (R2). Follow pages until a page shorter than `$top` comes back.

**Consumed per pull request**: `pullRequestId`, `title`, `createdBy.displayName`, `creationDate`,
`targetRefName`, `repository.name`, `repository.project`, `isDraft`, `mergeStatus`, and each
`reviewers[]` entry's `id`, `isContainer`, `vote`, `hasDeclined`, `isRequired`.

**Not consumed**: `_links`, `url`, `remoteUrl`, `artifactId`, `commits`, `lastMerge*`, `completionOptions`,
`mergeOptions`, `workItemRefs`, `imageUrl`. What is not carried cannot leak.

**Note**: this endpoint truncates `description` to 400 characters. The queue does not display descriptions,
so this only matters to read 4.

**On failure**: the project becomes an `UninspectedProject` carrying its `FailureReason`, and the queue is
no longer complete (FR-013). A `403` is a permission answer, not an absence of pull requests.

---

## 4. One pull request, on demand

```http
GET {collection}/{project}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}?api-version={v}
```

Issued **only** when a developer opens an entry (User Story 3). Never during the sweep.

**Consumed**: `description` (full), `sourceRefName`, and the complete `reviewers[]` list with votes.

**Used for**: FR-016. One read on explicit demand, mirroring varcompare's detail view — the fuller, more
revealing thing happens only when asked for (R6).

---

## 5. Group membership

**VERIFY (V-B)**: the route by which a non-administrator can enumerate their own group memberships on this
server version is unconfirmed. Quickstart V4 settles both the route and the permission.

**If unavailable**: FR-006a governs. Groups whose membership cannot be established are named in
`InspectionScope.UnresolvedGroups`, the queue is never reported as complete, and the tool still works —
narrower, and honest about it. This is why no route is pinned here: the design does not depend on one.

---

## Failure translation

Unchanged from the shared reader, which already maps transport and status outcomes onto the domain's
`FailureReason` before anything above Infrastructure sees them. Nothing in the tool sees a URL, a status
code, or a JSON shape.

| Condition | Becomes | Surfaces as |
|-----------|---------|-------------|
| `401` | `Unauthorized` | The session was refused; the run stops |
| `403` | `Forbidden` | That project is uninspected, and named |
| `404` | `NotFound` | That project is uninspected, and named |
| Timeout, breaker open, retries exhausted | `Unavailable` | That project is uninspected, and named |
| Cancellation | `Cancelled` | Described state: what was swept, what was not |

The rule throughout: **a project that could not be read is named, never skipped.** A skipped project turns
"nothing is waiting" into a falsehood, which is the single failure mode this feature is most exposed to.
