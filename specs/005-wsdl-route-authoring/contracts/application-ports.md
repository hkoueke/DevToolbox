# Contract: Application ports

What `Core` declares and `Presentation` or `Infrastructure` implements. One port is added, one existing
port gains a capability, one loses a parameter.

---

## `IRouteAuthoringPrompt` (new)

Declared in `ApiDelta.Core.Abstractions`, implemented by `ApiDelta.Presentation.Prompts`.

```csharp
public interface IRouteAuthoringPrompt
{
    Task<AuthoringRecord> AuthorAsync(
        IReadOnlyList<AuthoringSubject> subjects,
        IReadOnlyList<string> productionPaths,
        IReadOnlyList<OfferedHeader> headers,
        CancellationToken cancellationToken);
}
```

`AuthoringSubject` carries, per kept operation: its name, the fields the contract declares for its input
(name, declared type, composite or not), the derived suggestion and what that suggestion is worth.

**Obligations of an implementation**

1. It asks nothing about an operation absent from `subjects` (RA-003), and asks nothing at all when
   `subjects` is empty.
2. It returns a record whose every `AuthoredRoute` satisfies the invariants of the data model. Validation
   belongs to `Core` and the prompt calls it — it does not reimplement it, and it does not return a value
   it was told was invalid.
3. It pre-fills the path and verb with the suggestion and shows what the suggestion is worth, never
   presenting a fallback as a derivation (RA-002).
4. It asks for the insertion point from `productionPaths`, in that list's order (RA-009).
5. It offers `headers` as given, in that order, and offers nothing when the list is empty (RA-023).
6. It asks requiredness for every placement but `Path` (RA-012, RA-014).
7. It never asks for a description (RA-018).
8. Cancellation returns what was settled so far; it does not discard the sitting.

**Why a new port rather than extending `ICorrespondencePrompt`**: the two answer different questions —
"which existing route does this operation correspond to" and "what should this new route be". Principle II
forbids an interface that accretes unrelated operations, and the step that still pairs covered operations
must not be coupled to the step that authors new ones.

---

## `ISplicingEmitter` (changed)

The port is unchanged; `Splice` gains one member.

```csharp
public sealed record Splice(
    SpliceKind Kind,
    SpecPointer Target,
    string? Key = null,
    SpecNode? Value = null,
    string? AfterKey = null);
```

**Obligations**

1. `Kind is Add` with `AfterKey` set inserts the new member **after the named sibling's content**, at the
   sibling's indentation.
2. `AfterKey` naming no existing member is a refusal, not an append. Silently appending would put the
   route somewhere the developer did not choose, which is the defect this feature exists to remove.
3. `AfterKey` null keeps today's behaviour exactly: insertion after the mapping's last entry.
4. Bytes outside the insertion are unchanged, as today. The existing round-trip and span tests hold this.

---

## `ICorrespondenceProposer` (changed)

```csharp
// before
OperationCorrespondence Propose(
    IReadOnlyList<string> operations,
    IReadOnlyList<string> routes,
    IReadOnlyDictionary<string, string>? declared = null);

// after
OperationCorrespondence Propose(
    IReadOnlyList<string> operations,
    IReadOnlyList<string> routes);
```

The `declared` parameter is removed with WR-105 (RA-026). Its only caller read a context key that nothing
ever wrote, so the parameter was always null and the `Declared` branch unreachable.

---

## `AuthorStep` (new step in the ordered chain)

| Property | Value |
|---|---|
| Position | After `Correspond`, before `Validate` |
| Runs in | `Consolidate` mode only |
| Reads from context | the contract, the projection, production, the kept operations |
| Writes to context | the `AuthoringRecord` |
| Checkpoint | writes one entry per authored route and per placement, as strings |

**Obligations**

1. Skipped entirely when the companion is not a service contract, or when the developer kept nothing.
2. A resumed run whose checkpoint carries a confirmed record uses it and asks only about what is missing
   (RA-019).
3. Collisions are established here, before anything is written, and reported with both sides (RA-006).
4. Failure names the operation and the rule that refused, never a position in a list.

---

## What no port gains

- **No port reads or writes a file of authoring decisions.** RA-024 and RA-025 forbid it, and the plan
  records why: the declared correspondence file was this idea, and it became a capability with no reader.
- **No port derives requiredness, path placement or headers from the contract.** The specification
  establishes that the information is not there.
