# Contract — Application ports owned by apidelta

**Feature**: [../spec.md](../spec.md) · **Plan**: [../plan.md](../plan.md) · **Date**: 2026-10-16

Ports the tool's `Core` owns, plus the three changes to shared code that this feature requires. Every port
returns `Result` or `Result<T>` — an expected failure is a value already fit to display, never an exception
crossing a layer. Every asynchronous member takes a `CancellationToken` and propagates it unchanged.
Library assemblies use `ConfigureAwait(false)` throughout.

**None of these members can reach the network.** There is no HTTP client in this tool's dependency graph,
which is the offline guarantee made structural rather than disciplinary.

---

## Shared changes

All three are prerequisites, named in the plan's Complexity Tracking. The first is purely additive; the
second forces a mechanical edit in `tools/VarCompare/`; the third is a defect fix.

### `ITool` — one additive member

```csharp
// src/DevToolbox.Application/Abstractions/ITool.cs
public interface ITool
{
    // … unchanged members …

    /// <summary>Indique si l'outil a besoin que le serveur soit configuré pour fonctionner.</summary>
    bool RequiresServer => true;
}
```

A default interface member, so no existing tool is edited and no registration changes. apidelta returns
`false`; the shell resolves and validates the server configuration only when a tool that needs one is
chosen (R11).

### `PersistedRun` — selection generalized

```csharp
// src/DevToolbox.Application/Abstractions/PersistedRun.cs
public sealed record PersistedRun(
    Guid RunId,
    string ToolId,
    IReadOnlyList<PersistedSelectionEntry> Selection,
    IReadOnlyList<string> CompletedSteps,
    DateTimeOffset StartedAt,
    DateTimeOffset LastUpdatedAt);

// src/DevToolbox.Application/Abstractions/PersistedSelectionEntry.cs — new
public sealed record PersistedSelectionEntry(string Label, string Value);
```

`PersistedGroupSelection` is replaced. The runtime shape guard in `FileRunCheckpointStore` keeps its job
over a **narrower** allow-list — `Guid`, `string`, `DateTimeOffset`, `IReadOnlyList<string>`,
`IReadOnlyList<PersistedSelectionEntry>`; `int` leaves it. The invariant is unchanged and its wording holds
as written: no member of this type, even indirectly, can reach a value.

`ResumePrompt.Ask` then displays whatever entries a tool chose to persist, which is what lets one prompt
serve varcompare, prqueue and apidelta.

apidelta's entries, in display order:

| Label | Value |
|---|---|
| `Mode` | `Compare`, `Patch`, `Audit`, `Optimize`, `Everything` |
| `Production` | path, then `sha256:` prefix and the first 12 hex characters |
| `Candidat` | path and hash, absent for audit and optimize |
| `Sortie` | the output directory |
| `Règles` | the ruleset path, when one was supplied |

### `AppPaths.CombineConfined` — confinement fixed

```csharp
// src/DevToolbox.Infrastructure/Platform/AppPaths.cs
public static string CombineConfined(string root, string relativePath);
```

Same signature, correct behaviour: comparison against the root *with a trailing separator*, or via
`Path.GetRelativePath` rejecting a result that starts with `..` or is rooted. Today's check accepts
`C:\out\..\outside\x` under the root `C:\out` (R12). apidelta is the first caller to pass a
developer-chosen root, so the fix comes first and carries its own test.

---

## Ports owned by `ApiDelta.Core`

### Reading

```csharp
public interface ISpecReader
{
    Task<Result<SpecDocument>> ReadAsync(
        string path,
        DocumentRole role,
        LoadLimits limits,
        CancellationToken cancellationToken);
}
```

Detects the format from content (FR-001), builds the syntax tree, and reports **every** duplicate mapping
key with both positions before anything binds them (FR-007). A failure returns diagnostics and no document:
`SpecDocument` cannot be constructed from a document that was not read faithfully (FR-009).

Implemented in `ApiDelta.Infrastructure/Yaml/` and `/Json/`. YamlDotNet and `Utf8JsonReader` appear nowhere
else.

```csharp
public interface ISpecValidator
{
    Result<ValidationReport> Validate(SpecDocument document, ReferencePolicy policy);
}

public interface IReferenceGraph
{
    Result<ReferenceGraphResult> Resolve(SpecDocument document, ReferencePolicy policy);
}
```

Both are implemented over `Microsoft.OpenApi` in `ApiDelta.Infrastructure/Semantic/`, which receives the
tree projected to JSON (R3) and maps the library's pointer-located diagnostics back onto our nodes, so
every structural error reaches the screen with a line and a column (R7).

### Canonical form and equivalence

```csharp
public interface ICanonicalizer
{
    CanonicalForm Canonicalize(SpecNode node, EquivalenceProfile profile);
}

public interface IFingerprinter
{
    Fingerprint Compute(SpecNode node, EquivalenceProfile profile);   // cached per (node, profile)
}

public interface IContractEquivalenceVerifier
{
    Result<VerificationResult> Verify(SpecDocument before, ReadOnlyMemory<byte> afterBytes);
}
```

`Verify` reparses the bytes it is given, revalidates, resolves references and compares under the
`Contract` profile (OPT-011, R14). It takes bytes rather than a tree on purpose: verifying the edited tree
would skip serialization, which is where a quoting mistake would appear.

### Comparison, versioning, dependencies

```csharp
public interface IDocumentComparer
{
    Result<IReadOnlyList<Difference>> Compare(
        SpecDocument candidate,
        SpecDocument production,
        CancellationToken cancellationToken);
}

public interface IChangeClassifier
{
    ChangeAssessment Classify(Difference difference, VersioningPolicy policy);
}

public interface IVersionPolicy
{
    VersionRecommendation Recommend(
        IReadOnlyList<Difference> differences,
        SpecDocument production,
        VersioningPolicy policy,
        VersionOverride? requested);
}

public interface IDependencyResolver
{
    Result<DependencyClosure> Close(
        IReadOnlyList<SpecPointer> roots,
        SpecDocument candidate,
        SpecDocument production,
        CancellationToken cancellationToken);
}
```

The comparer walks the tree guided by `IMetamodel`; it does not reflect over a library model (R6, R7).

```csharp
public interface IMetamodel
{
    MetamodelEntry Resolve(SpecPointer pointer, OpenApiVersion version);
}
```

**An unmatched pointer returns the safe default**, never `null`: unordered comparison, vendor role,
`Unknown` impact, ineligible for automatic transformation. A missing row makes the tool less decisive, not
wrong.

### Rules

```csharp
public interface IAuditRule
{
    string RuleId { get; }
    FindingSeverity DefaultSeverity { get; }
    NodeKinds Visits { get; }

    RuleOutcome Evaluate(AuditContext context, CancellationToken cancellationToken);
}

public interface IOptimizationRule
{
    string RuleId { get; }
    SafetyClass Safety { get; }
    bool EnabledByDefault { get; }

    Result<IReadOnlyList<Transformation>> Propose(OptimizationContext context, CancellationToken cancellationToken);
    Result<SpecNode> Apply(Transformation transformation, SpecNode root);
}
```

One sealed class per identifier, registered explicitly rather than discovered by assembly scanning (R16),
so `CompositionRootTests` resolving the real graph catches a rule that was written but never wired.

`Evaluate` returns a `RuleOutcome` rather than a list of findings, because a rule that could not run must
be able to say so (FR-418). `Visits` lets the audit make one pass over the tree for all rules instead of
one pass per rule (R15).

### Extraction

```csharp
public interface IOperationCatalog
{
    Result<IReadOnlyList<SelectableOperation>> List(SpecDocument document);
}

public interface IExtractBuilder
{
    Result<ExtractResult> Build(
        SpecDocument source,
        ExtractRequest request,
        CancellationToken cancellationToken);
}
```

`IOperationCatalog` walks the paths once and returns path, method, summary, tags and pointer — no schemas,
no examples — so listing four hundred operations for the picker costs four hundred short strings.

`IExtractBuilder` reuses `IDependencyResolver` with the selected operations as roots: **a patch closes over
the changed set, an extract closes over the chosen set, and it is the same engine.** What the builder adds
on top of the closure is the inheritance that a naïve trim loses — path-level parameters, servers in force,
the global security requirement and the schemes it names (FR-905) — and the carried tag definitions
(FR-906).

Verification reuses the equivalence verifier, with one addition specific to this mode:

```csharp
public interface IExtractVerifier
{
    Result<ExtractVerification> Verify(
        SpecDocument source,
        ExtractRequest request,
        ReadOnlyMemory<byte> extractBytes);
}
```

It reparses the emitted bytes, revalidates, resolves, asserts that nothing unselected is present, and
compares each selected operation in **effective** form — after inheritance on both sides — against the
source. Comparing the written operation instead would pass an extract that had silently dropped a
path-level parameter, which is the one failure this mode must be unable to produce (FR-910).

### Configuration

```csharp
public interface IRulesetStore
{
    Task<Result<Ruleset>> LoadAsync(string? path, CancellationToken cancellationToken);
}

public interface ISuppressionStore
{
    Task<Result<IReadOnlyList<Suppression>>> LoadAsync(string? path, CancellationToken cancellationToken);
}
```

A `null` path yields the shipped defaults (FR-656). Invalid configuration is a `Result` failure that names
the file and the offending entry, returned **before the run starts** — never a silent fallback to defaults
(FR-655).

### Output

```csharp
public interface ISpecEmitter
{
    Result<ReadOnlyMemory<byte>> Emit(SpecNode root, CanonicalFormatting formatting);
}

public interface IPatchWriter
{
    Result<ReadOnlyMemory<byte>> Write(PatchRequest request);
}

public interface IReportWriter
{
    Result<ReadOnlyMemory<byte>> Write(ApiDeltaRun run, ReportFormat format);
}

public interface IArtifactStore
{
    Task<Result<Artifact>> PublishAsync(
        ArtifactKind kind,
        ReadOnlyMemory<byte> content,
        RunSelection selection,
        CancellationToken cancellationToken);
}
```

Every writer produces bytes; only `IArtifactStore` touches disk, and it writes to a temporary name in the
output directory and moves the file into place, so a retried `Write` step cannot leave a half-written
artifact that looks finished. It is also where confinement and the out-of-directory confirmation apply
(FR-709), and the only place an in-place overwrite can happen — behind the explicit request, the
confirmation, the backup and the unchanged-since-read check (FR-550).

### Prompting

```csharp
public interface IApiDeltaPrompt
{
    Result<RunSelection> AskForSelection(RunSelection? resumed, CancellationToken cancellationToken);

    Result<ExtractRequest> AskForOperations(
        IReadOnlyList<SelectableOperation> operations,
        IReadOnlyList<OperationSelector> resumed,
        CancellationToken cancellationToken);

    bool ConfirmOverwrite(string path, string backupPath);
    bool ConfirmWriteOutside(string directory);
}
```

Implemented in `Presentation` over Spectre. `Core` depends on the port, never on a renderer — which is also
what lets every engine test run without a terminal.
