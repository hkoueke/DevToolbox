# Contract — the artifacts apidelta produces

**Feature**: [../spec.md](../spec.md) · **Plan**: [../plan.md](../plan.md) · **Date**: 2026-10-16

What lands on disk, under what name, in what order, and what about it is guaranteed stable. These files are
the product: the terminal is an index over them (R17), and a consumer — a reviewer, a pipeline, a later
version of this tool — reads them.

Everything here is written by `IArtifactStore` into the developer-chosen output directory, confined, with a
temporary name that is moved into place only once the bytes are complete.

---

## 1. Default names

| Kind | Default name | Written when |
|---|---|---|
| `DeltaReport` | `openapi-delta-report.md` | a companion file was supplied |
| `ConsolidatedDocument` | `openapi-production.consolidated.yaml` | mode is consolidate *and* verification passed |
| `DeltaJson` | `openapi-delta.json` | always |
| `AuditReport` | `openapi-audit-report.md` | mode is analyse |
| `OptimizationPlan` | `openapi-optimization-plan.yaml` | mode is analyse |
| `OptimizedDocument` | `openapi-production.optimized.yaml` | application was requested *and* verification passed |
| `Manifest` | `openapi-optimization-manifest.yaml` | an optimization was applied |

An existing file of the same name is overwritten only after confirmation; the production file itself is
never one of these names unless an in-place write was explicitly requested and confirmed (FR-550).

Two rows were corrected on 2026-10-27 rather than only added to. `Patch` / `openapi-delta-patch.yaml` had
outlived the clarification of 2026-10-21 that replaced the fragment with the complete consolidated
description: this contract went on naming an artifact the code had stopped writing, which is the drift a
contract exists to prevent. `Extract` / `openapi-extract.yaml` is withdrawn with FR-900 to FR-913.

---

## 2. Determinism

**Identical inputs and identical configuration produce byte-identical artifacts**, across runs and across
machines (FR-604, SC-004), with exactly one exception: a delimited run-metadata block.

In Markdown:

```markdown
<!-- apidelta:run-metadata
runId: 6f1c…
startedAt: 2026-10-16T09:14:22Z
durationMs: 8431
candidate: C:\work\new-api.json
production: C:\work\production.yaml
-->
```

In JSON, the single `runMetadata` object. Golden-file tests normalize that block and nothing else, which is
what keeps the guarantee testable rather than aspirational.

Ordering is fixed everywhere a collection is written: differences by JSON Pointer, then by change
identifier; findings by rule identifier, then by pointer; redundancy groups by estimated saving descending,
then by fingerprint; manifest entries by rule, then by origin pointer. Where a tie remains, the fingerprint
breaks it. No collection is ever written in hash-table order.

---

## 3. The human-readable delta report

Sections in this exact order (FR-600), each present even when empty, because a missing section is
indistinguishable from an overlooked one:

1. Executive summary — counts by impact and severity, the recommended version, the headline saving
2. Input document metadata and hashes
3. Validation status
4. Recommended version change — with the contributing changes and the resolved policy
5. Breaking changes
6. Added operations
7. Removed operations
8. Modified operations
9. Component changes
10. Security changes
11. Vendor-extension changes
12. Production audit findings
13. Redundancy groups
14. Safe optimization proposals
15. Review-required proposals
16. Estimated file-size reduction
17. Suppressed findings — with justification and owner
18. Generated artifacts — each with its path, size and hash
19. Limitations and assumptions — including every rule that could not be evaluated, every reference left
    unresolved by policy, and the standing note that regenerated output does not preserve comments,
    anchors or original key order (FR-556, FR-606)

The audit report carries sections 2, 3, 12, 13, 14, 15, 16, 17, 18 and 19 and nothing else: audit findings
never arrive mixed into contract changes (FR-411).

### A finding, as rendered

```text
AUD-101  Repeated header parameter                             warning · SAFE
X-serviceEmetteur appears inline in 14 operations with an identical strict fingerprint.
  /paths/~1documents/post/parameters/0   POST /documents → parameters[0]
  … 13 more locations
Recommendation: extract to components.parameters.XServiceEmetteur.
Estimated saving: 2.1 KB (break-even at 2 occurrences). Autofix available.
```

---

## 4. The machine-readable report

One object, with a stable field layout (FR-601). Field names are `camelCase`; unknown fields may be added
in later versions, and no field present here is ever renamed or repurposed.

```json
{
  "schemaVersion": 1,
  "runMetadata": { "runId": "…", "startedAt": "…", "durationMs": 8431, "toolVersion": "…" },
  "inputs": {
    "production": { "path": "…", "format": "yaml", "openApiVersion": "3.0.3", "bytes": 418233, "sha256": "…" },
    "candidate":  { "path": "…", "format": "json", "openApiVersion": "3.0.3", "bytes": 402110, "sha256": "…" }
  },
  "diagnostics": [
    { "code": "LOAD-DUPLICATE-KEY", "severity": "error", "message": "…",
      "jsonPointer": "/paths/~1documents/post", "line": 412, "column": 7,
      "related": [ { "line": 388, "column": 7 } ] }
  ],
  "differences": [
    { "changeId": "PARAMETER-REQUIRED-ADDED", "jsonPointer": "…", "displayPath": "…",
      "kind": "modified", "before": { "text": "required: false", "truncated": false },
      "after": { "text": "required: true", "truncated": false },
      "classification": "breaking", "confidence": "certain",
      "rationale": "…", "recommendedAction": "…" }
  ],
  "version": {
    "baseline": "2.4.1", "increment": "major", "recommended": "3.0.0",
    "contributors": [ "…" ],
    "resolvedPolicy": { "enumValueAdded": "major", "successResponseAdded": "minor" },
    "override": null
  },
  "ruleOutcomes": [ { "ruleId": "AUD-003", "state": "evaluated", "findingCount": 4 },
                    { "ruleId": "AUD-104", "state": "truncated", "reason": "…", "findingCount": 12 } ],
  "findings": [
    { "ruleId": "AUD-101", "severity": "warning", "safety": "SAFE",
      "message": "Header parameter is repeated in 14 operations.",
      "jsonPointer": "/paths/~1documents/post/parameters/0", "displayPath": "…",
      "locations": [ "…" ], "suggestedComponentName": "XServiceEmetteur",
      "estimatedBytesSaved": 2180, "autofixAvailable": true,
      "recommendedAction": "…", "documentationUrl": null,
      "evidence": { "occurrences": 14, "fingerprint": "sha256:…", "profile": "strict" } }
  ],
  "redundancyGroups": [
    { "fingerprint": "sha256:…", "kind": "parameter", "tier": "exact", "occurrences": 14,
      "currentBytes": 2380, "projectedBytes": 200, "absoluteSaving": 2180,
      "percentSaving": 91.6, "breakEvenOccurrences": 2, "safety": "SAFE" }
  ],
  "optimization": {
    "transformations": [ { "ruleId": "OPT-002", "safety": "SAFE", "state": "applied",
                           "origins": [ "…" ], "destination": "/components/parameters/XServiceEmetteur",
                           "evidence": "sha256:…", "reversal": "…" } ],
    "totalEstimatedSaving": 18442,
    "growthCheck": { "projectedBytes": 399791, "limit": null, "exceeded": false },
    "verification": { "reparsed": true, "revalidated": true,
                      "referencesResolved": true, "contractEquivalent": true, "differences": [] }
  },
  "suppressions": [ { "ruleId": "AUD-005", "location": "…", "state": "expired",
                      "justification": "…", "owner": "…", "expires": "2026-06-30" } ],
  "artifacts": [ { "kind": "deltaReport", "path": "…", "bytes": 21188, "sha256": "…" } ],
  "sizes": { "productionBefore": 418233, "productionAfter": 399791, "savedBytes": 18442 },
  "outcome": { "state": "success", "code": "clean", "durationMs": 8431 }
}
```

`outcome.code` is the FR-708 vocabulary — `clean`, `thresholdExceeded`, `breakingChange`, `invalidUsage`,
`loadFailure`, `outputFailure`, `internalError`. It is reported here and on screen; it is **not** this
release's process exit code (FR-708a).

**No secret reaches these files.** Value summaries are bounded and pass through the same redaction path as
logs; a finding about a secret-shaped value in an example or a server URL names the location and the
pattern, never the value (FR-607).

---

## 5. The consolidated production file

The production file itself, with the chosen content already in it (FR-500), written beside the original and
never over it (FR-515):

- the production bytes, untouched everywhere the selection did not name — comments, indentation and key
  order survive, because the writer splices the original bytes instead of re-emitting the file;
- the recommended `info.version`;
- the chosen routes and the chosen single differences, each carrying its origin mark (FR-512);
- every component the above depend on, transitively closed (FR-200);
- removals **only** when they were requested, and nothing from the cleanup unless it was explicitly
  combined in.

**Guaranteed before the file exists**: it reparses, revalidates against its declared version, every
reference resolves, what the selection did not name is byte-identical, and what it did name differs only
as chosen (FR-506, FR-514). Failing any of those, nothing is written and the report says which.

This section replaced *The patch* on 2026-10-27, three sections after the requirement it describes had
already changed. The fragment it documented stopped being written on 2026-10-21.

---

## 6. The optimization plan and the manifest

The plan lists every transformation as a proposal, grouped by safety class, each with its rule, origins,
destination, evidence fingerprint, estimated saving and break-even count. It changes nothing and names no
file it has written.

The manifest is written only when an optimization was applied, alongside the optimized document:

```yaml
runId: 6f1c…
input:  { path: production.yaml, sha256: …, bytes: 418233 }
output: { path: openapi-production.optimized.yaml, sha256: …, bytes: 399791 }
reversible: true
entries:
  - ruleId: OPT-002
    safety: SAFE
    evidence: sha256:…
    origins: [ "/paths/~1documents/post/parameters/0", "…" ]
    destination: /components/parameters/XServiceEmetteur
    reversal: inline the component at each origin and delete it
```

**Guaranteed**: the optimized document exists on disk only if its `VerificationResult` had all four flags
true (OPT-011, FR-552, SC-008). The manifest is written in the same step, so an optimized file without a
manifest is not a state this tool can produce.

---

## 7. L'extrait, retiré

Retiré le 2026-10-27 avec FR-900 à FR-913 : ni `openapi-extract.yaml`, ni son rapport propre, ni la
section d'extrait des deux rapports ne sont plus écrits. Le journal de clarifications de
[spec.md](../spec.md) dit pourquoi.
