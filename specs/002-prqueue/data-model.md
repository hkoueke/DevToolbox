# Phase 1 — Data Model: prqueue

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Date**: 2026-08-25

Types live in `DevToolbox.Tools.PrQueue.Core`. They are `sealed record`s, immutable, and free of any
reference to Spectre, `HttpClient`, or JSON. Wire shapes live in `…PrQueue.Infrastructure/Wire/` and never
escape that assembly.

---

## Design rules this model is built to enforce

Two of these mirror varcompare's structural guarantees. They are the reason the shapes look the way they do,
and changing them quietly is how the guarantees get lost.

1. **A `QueueEntry` carries no pull request content beyond its title.** No description, no diff, no comment.
   The description reaches the screen only through `EntryDetail`, built on demand when a developer opens an
   entry. The absence is what makes "content never reaches the log or the checkpoint" structural rather than
   a matter of caller discipline.
2. **`PersistedProjectScope` must never gain a member that can reach a pull request.** The checkpoint store
   inspects the shape of what it is asked to write and refuses a record that has grown. Selected project ids
   and step progress are the whole of it.
3. **An entry that could not be established is `Undetermined`, never absent.** `InspectionScope` is not
   decoration: an answer without it cannot be trusted, and every renderer takes both together.

---

## Core entities

### `DeveloperIdentity`

Who the queue belongs to. Resolved from the connection (R3), announced before any read.

| Member | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | The collection identity id. This, not a name, is what reviewer entries are matched against |
| `DisplayName` | `string` | For the announcement only |

The identity is never persisted and never logged by name — only its presence or absence is logged.

### `ProjectScope`

Which projects a run swept, and which the developer chose to sweep.

| Member | Type | Notes |
|--------|------|-------|
| `Available` | `IReadOnlyList<ProjectIdentifier>` | Everything the collection offers, from `IProjectCatalog` |
| `Selected` | `IReadOnlyList<ProjectIdentifier>` | What this run reads. Equals `Available` unless narrowed under FR-011 |

`ProjectIdentifier` is the existing `DevToolbox.Domain.AzureDevOps` type — reused, not redefined.

### `QueueEntry`

One pull request awaiting the developer. The unit of the list.

| Member | Type | Notes |
|--------|------|-------|
| `Project` | `ProjectIdentifier` | Which project it came from |
| `RepositoryName` | `string` | Shown; the developer needs it to know where they are going |
| `PullRequestId` | `int` | The number a developer recognises |
| `Title` | `string` | The only content-bearing member, and deliberately the only one |
| `AuthorDisplayName` | `string` | Who opened it |
| `TargetBranch` | `string` | Where it is going, short form (`refs/heads/` stripped) |
| `CreatedAt` | `DateTimeOffset` | The basis of `Age` |
| `AwaitedRoute` | `AwaitedRoute` | Personally, or through which group (FR-006) |
| `IsRequired` | `bool` | A required reviewer, per branch policy |
| `Blocking` | `BlockingReason` | For triage — see below |

`Age` is derived, never stored: `clock.UtcNow - CreatedAt`, via the injected `IClock` so tests can drive it.
Named `Age`, not `Wait`, because that is what it measures (FR-008a).

### `AwaitedRoute`

Why this entry is in the queue. A discriminated shape rather than a boolean, because the group case carries
a name the developer needs.

| Case | Carries | Meaning |
|------|---------|---------|
| `Personally` | — | The developer is named as a reviewer |
| `ThroughGroup` | `GroupName` | A group they belong to is the reviewer (FR-006) |

Rendered distinctly. "Your team is awaited" and "you are awaited" are different facts, and a developer
triages them differently.

### `ReviewVote`

The server's vote scale, named. Values are the wire values so the mapping is a cast, not a table.

| Value | Wire | Meaning |
|-------|-----:|---------|
| `Approved` | 10 | |
| `ApprovedWithSuggestions` | 5 | |
| `NoVote` | 0 | **The only value that makes an entry awaited** |
| `WaitingForAuthor` | −5 | |
| `Rejected` | −10 | |

**Awaited** is precisely: a reviewer entry for this developer (or a group they belong to) exists, its vote is
`NoVote`, and `hasDeclined` is false (FR-007, FR-007a). Anything else is an answer already given.

### `BlockingReason`

What stands between this pull request and completion, for triage only (R7).

| Value | Derived from | Why a reviewer cares |
|-------|--------------|----------------------|
| `None` | `mergeStatus = succeeded` | Reviewable now |
| `Conflicts` | `mergeStatus = conflicts` | Not worth reviewing until rebased |
| `RejectedByPolicy` | `mergeStatus = rejectedByPolicy` | Blocked regardless of this review |
| `AwaitingAuthor` | any reviewer vote of −5 or −10 | The author owes a change |
| `Undetermined` | `mergeStatus` absent, `notSet`, or unrecognised | Say so; never guess `None` |

`Undetermined` exists for the same reason varcompare's does: an unknown must not be reported as a clean
state.

### `EntryDetail`

Built on demand when an entry is opened (User Story 3, R6). **Not** part of `QueueEntry`, so no path renders
a description unless the developer asked for one.

| Member | Type | Notes |
|--------|------|-------|
| `Entry` | `QueueEntry` | What was opened |
| `Description` | `string` | Full text, from the per-entry read |
| `SourceBranch` | `string` | Short form |
| `Reviewers` | `IReadOnlyList<ReviewerVote>` | Everyone, with their vote and whether required |
| `WebLocation` | `string` | Where the review is performed (FR-017) — displayed, never opened by the tool |

### `ReviewerVote`

| Member | Type |
|--------|------|
| `DisplayName` | `string` |
| `Vote` | `ReviewVote` |
| `IsRequired` | `bool` |
| `IsGroup` | `bool` |
| `HasDeclined` | `bool` |

### `InspectionScope`

What the run actually managed to look at. Travels with every result and is rendered with it — an answer
without it cannot be trusted (FR-013, FR-014, FR-015).

| Member | Type | Notes |
|--------|------|-------|
| `Inspected` | `IReadOnlyList<ProjectIdentifier>` | Read successfully |
| `Uninspected` | `IReadOnlyList<UninspectedProject>` | Named, with why |
| `UnresolvedGroups` | `IReadOnlyList<string>` | Memberships that could not be established (FR-006a) |
| `IsComplete` | `bool` | Derived: `Uninspected.Count == 0 && UnresolvedGroups.Count == 0` |

`IsComplete` is computed, never assigned. A caller cannot claim completeness it did not earn.

### `UninspectedProject`

| Member | Type | Notes |
|--------|------|-------|
| `Project` | `ProjectIdentifier` | |
| `Reason` | `FailureReason` | The existing domain enum — permission, unavailable, cancelled |

### `PullRequestQueue`

The whole answer. What `BuildQueue` produces and the renderer consumes.

| Member | Type | Notes |
|--------|------|-------|
| `Identity` | `DeveloperIdentity` | Whose queue |
| `Entries` | `IReadOnlyList<QueueEntry>` | Ordered by `Age` descending (FR-008) |
| `Scope` | `InspectionScope` | Always present |
| `ReadAt` | `DateTimeOffset` | What moment this reflects |

`Entries` being empty and `Scope.IsComplete` being false is the case the whole design exists to keep
distinguishable from an empty complete queue.

---

## Persisted shape

### `PersistedProjectScope`

Written to the checkpoint after `SelectProjects`. **This type must never gain a member that can reach a pull
request** — the store verifies its shape at runtime and refuses to write if it has been widened.

| Member | Type |
|--------|------|
| `ProjectIds` | `IReadOnlyList<Guid>` |

The remembered subset under FR-011 is stored separately in settings (R11), keyed by collection, because it
is a preference and must outlive the checkpoint's seven-day expiry.

---

## Wire mapping

DTOs in `…Infrastructure/Wire/`, mapped once at the boundary. Nothing below is visible outside that
assembly.

| Wire field | Maps to | Notes |
|------------|---------|-------|
| `pullRequestId` | `QueueEntry.PullRequestId` | |
| `title` | `QueueEntry.Title` | |
| `createdBy.displayName` | `QueueEntry.AuthorDisplayName` | |
| `creationDate` | `QueueEntry.CreatedAt` | Basis of `Age` (R5) |
| `targetRefName` | `QueueEntry.TargetBranch` | `refs/heads/` stripped for display |
| `repository.name` | `QueueEntry.RepositoryName` | |
| `repository.project` | `QueueEntry.Project` | Present on this endpoint, so no second read |
| `isDraft` | *filter* | Draft entries are excluded (FR-009), not mapped |
| `status` | *filter* | Only `active` is read; the endpoint defaults to it (FR-010) |
| `mergeStatus` | `BlockingReason` | Per the table above |
| `reviewers[].id` | *match* | Against `DeveloperIdentity.Id` and group ids |
| `reviewers[].isContainer` | `AwaitedRoute` | Marks a group identity |
| `reviewers[].vote` | `ReviewVote` | Cast; wire values are the enum values |
| `reviewers[].hasDeclined` | *filter* | A decline removes the entry (FR-007a) |
| `reviewers[].isRequired` | `QueueEntry.IsRequired` | |
| `reviewers[].votedFor` | — | Not mapped. Useful evidence during quickstart V4, not a queue concern |
| `description` | `EntryDetail.Description` | Truncated to 400 chars by the list endpoint; the full text comes from the per-entry read (R6) |

**Never mapped, deliberately**: `_links`, `url`, `remoteUrl`, `artifactId`, `commits`, `lastMerge*`,
`completionOptions`, `mergeOptions`, `workItemRefs`, `imageUrl`. Anything not needed is not carried, so it
cannot leak.

---

## Redaction

`QueueEntry`, `EntryDetail`, `ReviewerVote`, and `PullRequestQueue` are registered with
`RedactionRegistry` at startup. The filter works by type, so the sink refuses them regardless of what a
future call site does — a caller cannot log a title by accident, because the type itself is refused.

Titles, descriptions, and reviewer names never reach a log or a checkpoint. What is logged per project is
its identifier, the entry count, and the outcome.
