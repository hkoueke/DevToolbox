# Contract: Application ports (read-only)

The abstractions `DevToolbox.Tools.VarCompare.Core` defines and owns. Infrastructure and Presentation
implement them; the dependency arrow points inward (Principle I). Every asynchronous member takes a
`CancellationToken` — no exceptions.

Signatures are the contract. `Result<T>` is a small outcome type (success or typed failure); these ports do
not throw for expected conditions such as permission denial or an unreachable server.

---

## Data access — retrieval only

```csharp
public interface IVariableGroupGateway
{
    Task<Result<IReadOnlyList<ProjectIdentifier>>> ListProjectsAsync(
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<VariableGroupSummary>>> ListGroupsAsync(
        ProjectIdentifier project, CancellationToken cancellationToken);

    Task<Result<VariableGroupSnapshot>> GetGroupAsync(
        ProjectIdentifier project, int groupId, CancellationToken cancellationToken);
}
```

The collection and server come from validated options, not from parameters — there is one configured target
per run.

**Guarantees required of any implementation**
- **Retrieval only.** No member creates, updates, or deletes anything. The interface exposes no way to
  express a change, which is how FR-018 is enforced structurally rather than by discipline.
- Pagination is followed to exhaustion; a partial list is a failure, never a truncated success (FR-016).
- No implementation detail (status code, URL, JSON) appears in the returned failures.

**Asserted by test**: an architecture test fails the build if any HTTP method other than `GET` appears in the
gateway implementation (SC-006).

---

## Target announcement

```csharp
public interface ITargetAnnouncer
{
    /// Called once per run, before any read (FR-028c).
    void AnnounceTarget(TargetAnnouncement target);
}
```

There is **no credential port, no token port, and no sign-in port** — authentication is ambient Windows
integrated auth handled by the HTTP handler. This absence is deliberate: with no type able to carry a
credential, SC-007 (zero credential prompts reachable) holds by construction.

**Forbidden by contract**: no implementation of any port in this file may prompt for a username, a password,
or a token.

---

## Where the developer is

```csharp
public interface IWorkingFolderInspector
{
    /// Walks up for .git, parses .git/config, accepts a remote only when its host matches the
    /// configured server. Returns None when absent, ambiguous, or foreign — never a guess (FR-002d).
    Result<RepositoryOrigin?> DetectOrigin(string startDirectory);
}
```

---

## Recovery

```csharp
public interface IRunCheckpointStore
{
    Task SaveAsync(PersistedRun run, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersistedRun>> ListResumableAsync(string toolId, CancellationToken cancellationToken);
    Task DiscardAsync(Guid runId, CancellationToken cancellationToken);
    Task SweepExpiredAsync(TimeSpan maxAge, CancellationToken cancellationToken);
}

public interface IStepRunner
{
    /// Executes named steps in order, checkpointing after each success.
    /// On failure, asks IRecoveryPrompt and acts on the choice (FR-033–FR-035).
    Task<ToolRun> RunAsync(ToolRun run, IReadOnlyList<IToolStep> steps, CancellationToken cancellationToken);
}

public interface IToolStep
{
    string Name { get; }
    bool IsIdempotent { get; }
    Task<Result> ExecuteAsync(ToolRunContext context, CancellationToken cancellationToken);
}
```

`IToolStep` carries `IsIdempotent` on the shared abstraction, because Principle VIII requires a step that
cannot be safely repeated to declare itself non-resumable and tool #2 will have such steps. Every varcompare
step returns `true` — they are all reads, so *continue* is always safe here.

**Guarantee**: `SaveAsync` must reject — at runtime and in a test — any `PersistedRun` reachable from a
variable value (FR-039a).

---

## Presentation (implemented in the Spectre layer)

```csharp
public interface IComparisonPresenter
{
    void Render(VariableComparison comparison, ComparisonViewOptions options);
    void RenderLegend();
    void RenderDetail(VariableDetail detail);          // only on explicit request (FR-015b)
}

public interface IRecoveryPrompt
{
    Task<RecoveryChoice> AskAsync(StepState failed, CancellationToken cancellationToken);
    // RecoveryChoice: RetryStep | ContinueFromStep | RestartAll | Abort
}
```

`ComparisonViewOptions`: `Layout` (`SideBySide` | `Stacked` | `Auto`), `DifferencesOnly` (bool).

**Guarantees asserted by test**: `Render` never emits a variable value in either layout (FR-015a, SC-012);
every state is distinguishable with colour disabled (FR-012, SC-003); no prompt anywhere in the presentation
layer collects a credential or offers to change a value (FR-018c, SC-007).

---

## Shell contract (shared, enables the second tool)

```csharp
public interface ITool
{
    string Id { get; }                   // "varcompare"
    string DisplayName { get; }
    string Tab { get; }                  // e.g. "Azure DevOps"
    string Description { get; }
    Task<ToolRun> RunAsync(CancellationToken cancellationToken);
}
```

Each tool ships a `ServiceCollectionExtensions.AddXxx(...)` — the idiom already used throughout
`GovernanceAuditor`. The shell builds tabs and menus by enumerating registered `ITool`s, so adding
GovernanceAuditor as tool #2 requires **no edit to varcompare** (Principle II, research R15).

**Shared, not per-tool**: the Azure DevOps server options, the typed `HttpClient`, the integrated-auth
handler, and the resilience pipeline live in `DevToolbox.Infrastructure.AzureDevOps` so tool #2 inherits them
rather than duplicating them.
