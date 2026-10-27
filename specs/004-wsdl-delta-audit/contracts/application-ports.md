# Contract: application ports

Four new ports and one typed client. Every signature returns `Result<T>` or a value — no exceptions escape
to the top level, as Principle III requires.

Nothing here replaces a port 003 already has. `ISpecReader`, `IDocumentComparer`, `ICorrectionBuilder`,
`ICorrectionVerifier` and `ISplicingEmitter` are consumed unchanged.

## `IWsdlReader` — WR-001 … WR-009

```csharp
public interface IWsdlReader
{
    Task<WsdlReadResult> ReadAsync(
        string path, WsdlLimits limits, ImportPolicy policy, CancellationToken cancellationToken);

    WsdlReadResult ReadBytes(
        ReadOnlyMemory<byte> content, string name, WsdlLimits limits, ImportPolicy policy);
}
```

`WsdlReadResult` carries `WsdlContract?` and `IReadOnlyList<Diagnostic>` — the same shape as
`SpecReadResult`, and for the same reason: a contract exists only if it was read faithfully, so no later
step can receive a half-read one.

`ReadBytes` has no I/O and therefore no await. It exists so a test can read a hostile document without
touching the filesystem, and so the reader can read a materialised document from memory when it has just
written it.

**Forbidden by contract.** No implementation may process a DTD, resolve an external entity, or open a
path outside `policy.ConfinementRoot`. These are not options the policy can grant.

## `IWsdlProjector` — WR-110 … WR-115, WR-305

```csharp
public interface IWsdlProjector
{
    Result<ProjectionResult> Project(WsdlContract contract, ProjectionSettings settings);
}
```

Pure. No clock, no filesystem, no console, no network. Given the same contract and settings it returns the
same tree, the same gaps and the same figures — which is what SC-001 rests on.

**Forbidden by contract.** The returned `SpecDocument` must have `Format = SpecFormat.Projected` and must
carry no byte extents. An implementation that fabricated extents would let someone splice against them.

## `ICorrespondenceProposer` — WR-105, WR-108

```csharp
public interface ICorrespondenceProposer
{
    Correspondence Propose(
        IReadOnlyList<WsdlOperation> operations,
        IReadOnlyList<SelectableOperation> routes,
        Correspondence? declared);
}
```

Returns an **unconfirmed** correspondence. `declared` takes precedence where it speaks (WR-105): a team
that wrote a mapping file has already decided, and the proposer does not second-guess it.

Every proposed entry carries its `Basis` — which tokens matched. A proposal a developer cannot interrogate
is a proposal they cannot correct quickly, and correcting quickly is the whole point of the screen.

## `ICorrespondencePrompt` — WR-104, WR-303

```csharp
public interface ICorrespondencePrompt
{
    Correspondence Confirm(Correspondence proposed, CoverageReport preview);
}
```

Returns a confirmed correspondence, or one with `IsConfirmed = false` if the developer declines — in which
case the run succeeds having compared nothing, because declining is an answer.

`preview` lets the screen say what the correspondence would mean for coverage before it is accepted. A
developer confirming blind would be confirming the one thing the report later presents as certain.

## `IContractFetcher` — WR-010 … WR-019

```csharp
public interface IContractFetcher
{
    Task<Result<WsdlContract>> FetchAsync(
        Uri entry, string outputDirectory, ImportPolicy policy, CancellationToken cancellationToken);
}
```

Retrieves, materialises, and returns a contract whose documents are files on disk. The only outbound call
in this feature.

**Implemented over a typed `IHttpClientFactory` client** with the standard resilience handler — timeout,
bounded retries on transient status, circuit breaker — as Principle VII requires. `new HttpClient()` is
banned by `BannedSymbols.txt`, and `HttpMethod.Post/Put/Patch/Delete` are banned everywhere, which this
port has no reason to touch: retrieving a contract is a GET.

**Forbidden by contract.** No implementation may acquire or send a credential (WR-017), follow a
`soap:address` (WR-010), or retrieve without the scheme and address checks of WR-014 and WR-015.
