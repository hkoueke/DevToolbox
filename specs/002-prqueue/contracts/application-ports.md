# Contract — Application ports owned by prqueue

**Feature**: [../spec.md](../spec.md) · **Date**: 2026-08-25

Ports the tool's `Core` owns, plus the two additions to the shared layers. Every one returns
`Result<T>` — failures are values already fit to display, never exceptions crossing a layer. Every
asynchronous member takes a `CancellationToken` and propagates it unchanged.

**None of these members can express a change.** That is the read-only guarantee made structural: there is no
method to call, so no discipline to keep.

---

## Shared additions

Both live in the shared layers because more than one tool now needs them (R2, R8). Both are additive; no
existing signature changes, and no file under `tools/VarCompare/` is edited.

### `IProjectCatalog` — `DevToolbox.Application/Abstractions/`

```csharp
public interface IProjectCatalog
{
    Task<Result<IReadOnlyList<ProjectIdentifier>>> ListAsync(CancellationToken cancellationToken);
}
```

Enumerates the projects the developer can see in the configured collection. Implemented in
`DevToolbox.Infrastructure/AzureDevOps/ProjectCatalog.cs` over the existing reader.

> varcompare keeps its own `IVariableGroupGateway.ListProjectsAsync`. Two paths coexist deliberately —
> see the plan's Complexity Tracking. Migrating varcompare is a separate, safe change and is not a
> precondition for this feature.

### `AzureDevOpsApiReader` — new member

```csharp
public Task<Result<IReadOnlyList<T>>> GetAllPagesBySkipAsync<T>(
    Func<int, string> buildUrl,   // called with the running skip count
    int pageSize,
    CancellationToken cancellationToken);
```

Follows `$top`/`$skip` pages until a short page returns, accumulating every item. A page that fails fails the
whole read, so a caller never receives a silently truncated list — the same contract the continuation-token
method already keeps. The existing method is untouched and its tests still pin it.

---

## Ports owned by `PrQueue.Core`

### `IIdentityResolver`

```csharp
public interface IIdentityResolver
{
    Task<Result<DeveloperIdentity>> ResolveAsync(CancellationToken cancellationToken);
}
```

Resolves whose queue this is (FR-002). A failure here stops the run (FR-003) — the caller has no branch that
continues without an identity, which is what makes "no list without an identity" structural rather than a
convention.

### `IPullRequestGateway`

```csharp
public interface IPullRequestGateway
{
    Task<Result<IReadOnlyList<PullRequestRecord>>> ListActiveAsync(
        ProjectIdentifier project,
        CancellationToken cancellationToken);

    Task<Result<EntryDetail>> GetDetailAsync(
        QueueEntry entry,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<GroupMembership>>> ResolveMembershipsAsync(
        DeveloperIdentity identity,
        CancellationToken cancellationToken);
}
```

Reads only. `ListActiveAsync` returns one project's active pull requests with their reviewers, already
mapped off the wire. `GetDetailAsync` is the on-demand second read (R6) and is never called during a sweep.

`ResolveMembershipsAsync` is permitted to fail *without* failing the run: its failure becomes
`InspectionScope.UnresolvedGroups` (FR-006a). This is the one port whose failure is not fatal, and the
distinction is deliberate — a narrower queue that says so beats no queue.

### `IProjectScopeChooser`

```csharp
public interface IProjectScopeChooser
{
    Task<IReadOnlyList<ProjectIdentifier>> ChooseAsync(
        IReadOnlyList<ProjectIdentifier> available,
        IReadOnlyList<Guid> remembered,
        CancellationToken cancellationToken);
}
```

Offers the narrowing in FR-011, pre-selecting what was remembered. Returning everything is the normal
answer; narrowing exists to bound cost on a large collection, never to become the usual way of working.

### `IQueuePresenter`

```csharp
public interface IQueuePresenter
{
    void Announce(DeveloperIdentity identity);
    void Render(PullRequestQueue queue);
    void RenderDetail(EntryDetail detail);
    void RenderStale(PullRequestQueue queue, TimeSpan threshold);
}
```

`Announce` is separate and called first, because FR-002 requires the identity on screen *before* any read —
a presenter that could only render a finished queue would make that impossible to honour.

`Render` takes the whole `PullRequestQueue`, never a bare list of entries, so `InspectionScope` cannot be
dropped on the way to the screen. A renderer that accepted `IReadOnlyList<QueueEntry>` would allow exactly
the omission FR-014 forbids.

---

## Steps

Five, in order, each idempotent because each only reads (R9). Checkpointed after each success.

| # | Step | Reads | Writes to context | Fails when |
|---|------|-------|-------------------|-----------|
| 1 | `ResolveIdentity` | connection data | `DeveloperIdentity` | Identity unresolvable — terminal, FR-003 |
| 2 | `ListProjects` | project list | `ProjectScope.Available` | The collection cannot be listed at all |
| 3 | `SelectProjects` | — (prompts) | `ProjectScope.Selected` | Never; an empty selection means back |
| 4 | `ReadPullRequests` | pull requests, memberships | records + `InspectionScope` | Only if **every** project failed; otherwise partial with named gaps |
| 5 | `BuildQueue` | — | `PullRequestQueue` | Never; it is pure computation over what step 4 gathered |

Step 4 is the long one and the one that fails, which is why it is its own step: a retry resumes there rather
than re-resolving an identity that was already established.

**Checkpoint contents**: selected project ids and step progress. Nothing else. `PersistedProjectScope`
carries no member that can reach a pull request, and the store refuses a record whose shape has grown.
