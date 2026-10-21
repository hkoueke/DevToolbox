# Product Requirements Document

# OpenAPI Semantic Delta, Patch, Audit and Safe Optimization Tool

**Document version:** 3.0  
**Status:** Ready for implementation  
**Target runtime:** .NET 8  
**Primary interface:** Command-line application  
**Primary inputs:** OpenAPI JSON/YAML source and production YAML/JSON target  
**Primary outputs:** Markdown report, YAML patch, JSON report, optional optimized production specification

---

## 1. Executive summary

The product is a developer tool that compares an incoming OpenAPI description with a production OpenAPI description, calculates the semantic changes required to update production, recommends and applies an API version increment, and audits the production description for correctness, consistency, redundancy, maintainability and safe size reduction.

The tool operates on the OpenAPI semantic model, not on raw text. Formatting, property order, indentation and line endings must not generate API differences. A separate audit and optimization engine analyzes the physical and structural quality of the production file.

The tool supports four complementary capabilities:

1. **Semantic diff:** detect added, removed and modified OpenAPI elements.
2. **Patch generation:** produce a reviewable YAML fragment containing the changes and their dependencies.
3. **Production audit:** find invalid, suspicious, inconsistent, redundant, unused or badly formatted content.
4. **Safe optimization:** propose or apply behavior-preserving changes that reduce duplication and file size.

By default, the tool never overwrites the production file. Audit findings and optimization proposals are reported separately from functional API changes.

---

## 2. Problem statement

API specifications grow over time and often contain:

- repeated header parameters across operations;
- repeated response definitions;
- duplicate or structurally equivalent schemas;
- components that are no longer referenced;
- obsolete paths that are documented only in descriptions rather than with `deprecated: true`;
- inconsistent response-code quoting, casing, naming, descriptions and formats;
- unresolved or invalid references;
- request bodies that are accidentally optional;
- string dates without an appropriate format;
- duplicated metadata and gateway configuration;
- syntactically valid YAML that is semantically invalid OpenAPI;
- semantically equivalent content represented in different ways;
- large generated files that are difficult to review and maintain.

A naïve text diff cannot reliably distinguish formatting noise from a contract change. A naïve deduplication routine can also break the API by extracting elements that are not fully equivalent or by changing `$ref`, nullability, requiredness, serialization or gateway-specific behavior.

The product must therefore separate **contract comparison**, **quality auditing**, and **safe optimization**, while producing enough evidence for a developer to review every proposed change.

---

## 3. Goals

### 3.1 Primary goals

- Support OpenAPI 3.0.x and 3.1.x input in JSON, YAML and YML.
- Detect additions, removals and modifications across the complete supported OpenAPI model.
- Classify changes as breaking, non-breaking or documentation-only.
- recommend and apply a semantic version increment to generated output.
- Resolve transitive dependencies and avoid dangling `$ref` values.
- Generate a human-readable Markdown report.
- Generate a machine-readable JSON report.
- Generate a reviewable YAML patch.
- Audit the production description for validity, quality, redundancy and maintainability.
- Estimate and report potential size savings.
- Generate an optional optimized production description using only explicitly safe transformations.
- Preserve all vendor extensions, including `x-sg-*`, unless an enabled rule explicitly targets one.

### 3.2 Secondary goals

- Provide deterministic output suitable for source control.
- Provide rule identifiers, severities and remediation guidance.
- Allow rule suppression with an auditable justification.
- Allow project-specific linting and organization-specific conventions.
- Support CI/CD execution with meaningful exit codes.
- Produce evidence that an optimization does not alter the API contract.

---

## 4. Non-goals

The first release will not:

- deploy an API or publish it to an API gateway;
- call a production endpoint;
- prove that a running implementation conforms to the OpenAPI description;
- automatically resolve genuine business conflicts;
- overwrite production files by default;
- assume two similar components are interchangeable when their observable semantics differ;
- remove deprecated endpoints merely because they are deprecated;
- change operation behavior solely to reduce file size;
- delete unknown vendor extensions;
- guarantee preservation of YAML comments, anchors or original key order when a document is parsed and fully re-serialized.

Comment-preserving source editing may be added later through a concrete-syntax-tree YAML engine. Until then, the optimized output is a newly serialized artifact and the original file remains untouched.

---

## 5. Product modes

The developer selects one or more modes.

### 5.1 `diff`

Compares the candidate/source specification with production and reports semantic changes.

### 5.2 `patch`

Generates a YAML patch containing required additions, modifications, removals when enabled, transitive dependencies and the incremented `info.version`.

### 5.3 `audit`

Audits one production specification without requiring a source specification.

### 5.4 `optimize`

Generates an optimized copy of production. Safe rules are applied automatically only when explicitly requested. Review-required transformations remain proposals.

### 5.5 `all`

Runs diff, patch, audit and optimization analysis. It does not apply optimization unless `--apply-safe-optimizations` is supplied.

---

## 6. Safety model

Every finding and proposed transformation must be assigned a safety class.

### 6.1 SAFE

The tool has sufficient evidence that the transformation preserves the OpenAPI contract and gateway extensions. It may be applied when `--apply-safe-optimizations` is enabled.

Examples:

- canonical indentation and line endings;
- deterministic ordering where order has no OpenAPI meaning;
- removing an unreachable component after complete internal and external reference analysis;
- replacing several exactly equivalent inline parameters with one reusable parameter and `$ref` references;
- replacing exactly equivalent inline responses with one component;
- adding `deprecated: true` when the operation description contains a configured, unambiguous deprecation marker, only if this metadata change is allowed by policy.

### 6.2 REVIEW_REQUIRED

The transformation is likely useful but may affect generated clients, documentation, governance policy or gateway behavior. It must never be applied automatically without a dedicated opt-in flag.

Examples:

- merging differently named but structurally equivalent schemas;
- renaming components;
- converting inline schemas to shared schemas when code generators may derive type names differently;
- changing a string into `format: date-time` based only on its name or description;
- changing `requestBody.required` from omitted to `true`;
- changing response status codes;
- consolidating vendor-extension objects.

### 6.3 UNSAFE

The tool reports the opportunity but does not generate an automatic transformation.

Examples:

- deleting an endpoint;
- removing enum values;
- adding required properties;
- hardening security;
- merging schemas that differ in validation, nullability, defaults, examples with contractual relevance, discriminator behavior or additional-properties behavior;
- removing gateway-specific extensions whose semantics are unknown.

---

## 7. Inputs

### 7.1 Diff and patch inputs

- `--source <path>`: candidate OpenAPI JSON/YAML.
- `--target <path>`: production OpenAPI JSON/YAML.

### 7.2 Audit input

- `--target <path>`: production OpenAPI JSON/YAML.

### 7.3 Optional configuration

- organization ruleset;
- suppression file;
- output directory;
- output mode;
- strictness level;
- semantic-version policy;
- deprecation phrases;
- protected vendor-extension prefixes;
- external `$ref` resolution policy;
- maximum allowed output size or growth;
- canonical formatting options.

---

## 8. Outputs

Default artifact names:

- `openapi-delta-report.md`
- `openapi-delta-patch.yaml`
- `openapi-delta.json`
- `openapi-audit-report.md`
- `openapi-optimization-plan.yaml`
- `openapi-production.optimized.yaml`, only when optimization application is requested

The tool must never replace the target file unless both `--in-place` and `--force` are supplied. Even then, it must create a backup unless `--no-backup` is also supplied.

---

## 9. Functional requirements: loading and validation

### FR-001 Format support

The tool shall load `.json`, `.yaml` and `.yml` files and detect format from content if the extension is misleading.

### FR-002 OpenAPI versions

The tool shall support OpenAPI 3.0.x and 3.1.x. It shall reject unsupported versions with a clear diagnostic.

### FR-003 Syntax validation

The tool shall report YAML/JSON parsing errors with file, line and column when available.

### FR-004 Structural validation

The tool shall validate required OpenAPI fields, allowed shapes, path templates, response keys, component names and reference structure.

### FR-005 Reference validation

The tool shall detect unresolved, invalid, cyclic and disallowed external references. Cycles valid for schemas must not be treated as errors.

### FR-006 External references

External references shall be disabled by default for deterministic and secure execution. They may be enabled for local files with `--allow-local-refs`. Network references require a separate `--allow-remote-refs` option and configurable allowlist.

### FR-007 Duplicate YAML keys

The YAML reader shall detect duplicate mapping keys before object-model deserialization. Duplicate keys are errors because many parsers silently keep only one value.

### FR-008 Encoded text audit

The tool shall detect suspicious HTML entities such as `&gt;` or `&lt;` in fields where decoded text is expected and report them without silently changing them.

---

## 10. Functional requirements: semantic diff

### FR-100 Document-level comparison

Compare `openapi`, `info`, `servers`, `paths`, `components`, `security`, `tags`, `externalDocs` and all specification extensions.

### FR-101 Info comparison

Compare title, description, terms of service, contact, license, version and extensions. `info.version` is treated specially by the version engine and shall not mask other changes.

### FR-102 Path and operation comparison

Detect added, removed and modified paths and all HTTP operations: GET, PUT, POST, DELETE, OPTIONS, HEAD, PATCH and TRACE.

### FR-103 Operation fields

Compare tags, summary, description, operationId, parameters, requestBody, responses, callbacks, deprecated, security, servers and extensions.

### FR-104 Parameter comparison

Compare identity using `(name, in)`, then compare description, required, deprecated, allowEmptyValue, style, explode, allowReserved, schema, example, examples, content and extensions.

### FR-105 Request-body comparison

Compare requiredness, description, media types, schemas, encoding, examples and extensions.

### FR-106 Response comparison

Compare response codes, default response, descriptions, headers, links, media types, schemas, examples and extensions.

### FR-107 Schema comparison

Compare types, formats, titles, descriptions, properties, required sets, enums, const, defaults, examples, nullable semantics, readOnly, writeOnly, deprecated, discriminator, XML metadata, external docs, numeric constraints, string constraints, array constraints, object constraints, composition and conditional keywords supported by the document version.

### FR-108 Components comparison

Compare schemas, parameters, headers, request bodies, responses, security schemes, examples, links, callbacks and path items where supported by the OpenAPI version.

### FR-109 Security comparison

Compare global and operation-level security requirements, all schemes and OAuth flows/scopes. Scope-list ordering shall not create false differences.

### FR-110 Vendor extensions

Compare every `x-*` extension as structured JSON values. Unknown extensions shall be preserved and treated as potentially behavior-affecting.

### FR-111 Semantic normalization

Ignore differences that have no defined semantic effect, including indentation, comments, line endings, mapping order and order of set-like collections. Preserve order where order is semantically meaningful, including security requirement alternatives and composition arrays when tooling behavior may depend on order.

### FR-112 Change classification

Every detected change shall include:

- stable rule/change identifier;
- JSON Pointer location;
- human-readable path;
- old value summary;
- new value summary;
- classification: breaking, non-breaking, documentation-only or unknown;
- confidence;
- rationale;
- recommended action.

---

## 11. Dependency resolution

### FR-200 Transitive closure

For every operation included in a patch, recursively collect referenced parameters, schemas, request bodies, responses, headers, examples, links, callbacks, security schemes and nested references.

### FR-201 Cycle handling

Use a visited set keyed by canonical reference URI plus fragment to prevent infinite recursion.

### FR-202 Dangling-reference prevention

Generated patch and optimized output must contain no new unresolved internal `$ref` values.

### FR-203 External-reference preservation

If an allowed external reference remains external, preserve it exactly and include it in the dependency report.

### FR-204 Conflict detection

If source and target contain the same component name with different semantics, the tool shall report a conflict. It shall not silently overwrite the production component.

---

## 12. Semantic version engine

The production `info.version` is the version baseline.

### FR-300 Version parsing

Support semantic versions in `MAJOR.MINOR.PATCH` form and optionally pre-release/build metadata. Invalid versions are reported. A configurable fallback policy may be used only when explicitly enabled.

### FR-301 Patch increment

Recommend PATCH for documentation-only and behavior-neutral metadata changes.

Examples:

- corrected descriptions;
- changed examples that do not redefine constraints;
- contact information changes;
- non-behavioral tag or external-documentation changes.

### FR-302 Minor increment

Recommend MINOR for backward-compatible API additions.

Examples:

- new path or operation;
- optional request property;
- optional parameter;
- new response that does not replace an existing success contract;
- new schema used only by new functionality;
- widened accepted request constraints when demonstrably compatible.

### FR-303 Major increment

Recommend MAJOR for breaking or restrictive changes.

Examples:

- removed or renamed path/operation;
- newly required parameter, request body or property;
- removed response or media type;
- narrowed constraints;
- removed enum value;
- incompatible type/format change;
- stronger security requirement;
- changed parameter location;
- changed success-response schema incompatibly.

### FR-304 Configurable policy

Enum additions and response additions may affect generated clients. Their classification shall be configurable. The default report must state the selected policy.

### FR-305 Highest-impact rule

The recommended increment is determined by the highest-impact detected change: MAJOR overrides MINOR, which overrides PATCH.

### FR-306 No-change handling

If no semantic change exists, retain the current version and report `NONE`.

### FR-307 Output version

The patch and transformed output shall contain the recommended version unless `--version` explicitly overrides it. An override lower than the recommendation requires `--force-version` and must be prominently reported.

---

## 13. Production audit engine

The audit engine must distinguish **errors**, **warnings**, **information**, and **optimization opportunities**.

### 13.1 Validity and correctness audit

#### AUD-001 Invalid OpenAPI structure

Report violations of the selected OpenAPI version.

#### AUD-002 Unresolved references

Report every unresolved reference and all locations that use it.

#### AUD-003 Unused components

Report components unreachable from paths, webhooks where supported, security requirements and referenced components. Classify removal as SAFE only after complete dependency analysis and absent configured keep rules.

#### AUD-004 Missing required path parameters

Every `{placeholder}` in a path must have a corresponding required path parameter, and every declared path parameter must occur in the template.

#### AUD-005 Duplicate operationId

Report missing or duplicate operation IDs based on configured policy.

#### AUD-006 Invalid or inconsistent response definitions

Report operations with no responses, missing descriptions, invalid response keys, duplicate semantic status codes or inconsistent standard error responses.

#### AUD-007 Request-body requiredness

Report body-bearing operations where `requestBody.required` is omitted. This is REVIEW_REQUIRED, not an automatic correction.

#### AUD-008 Nullable/version compatibility

Report constructs that use OpenAPI 3.0 `nullable` semantics in 3.1 documents or 3.1 JSON Schema union/null constructs in 3.0 documents.

#### AUD-009 Inconsistent type and format

Report invalid format/type combinations and suspicious date, date-time, UUID, URI, email and binary declarations. Heuristic suggestions are REVIEW_REQUIRED.

#### AUD-010 Empty or misleading fields

Report empty `externalDocs.url`, empty descriptions where policy requires content, empty scope descriptions, empty tags, empty servers and placeholders unresolved in environments where they are not allowed.

### 13.2 Redundancy audit

#### AUD-100 Repeated parameters

Find identical inline parameters repeated across operations or paths. Group candidates by semantic fingerprint and report repetition count, locations and potential byte savings.

#### AUD-101 Repeated header sets

Detect identical groups of headers repeated across operations. Recommend reusable `components.parameters` references or path-level parameters where scope and override semantics remain equivalent.

#### AUD-102 Repeated responses

Detect identical reusable response candidates, including standard 400, 401, 403, 404 and 500 responses.

#### AUD-103 Repeated request bodies

Detect identical inline request bodies and media-type definitions.

#### AUD-104 Repeated schemas

Detect exact semantic duplicates separately from near-duplicates.

- Exact duplicates may be SAFE to reference if generator naming impact is excluded by policy.
- Differently named duplicate components are REVIEW_REQUIRED.
- Near-duplicates are suggestions only.

#### AUD-105 Repeated examples and headers

Detect exact duplicates suitable for `components.examples` or `components.headers`.

#### AUD-106 Duplicate enums

Detect identical enum definitions. Do not merge automatically when names convey different domain meaning.

#### AUD-107 Equivalent source/target aliases

Detect multiple components that differ only in descriptions, titles, examples or names. Report the precise differing fields and classify as REVIEW_REQUIRED.

### 13.3 Consistency and formatting audit

#### AUD-200 Canonical YAML formatting

Check configurable indentation, line width, final newline, trailing whitespace, quoting policy and line endings.

#### AUD-201 Deterministic ordering

Recommend a stable key order for top-level fields, paths, HTTP methods, response codes and components. Sorting must never alter semantically ordered arrays.

#### AUD-202 Response-code representation

Report inconsistent quoting of response keys. Canonical output shall quote status-code strings consistently.

#### AUD-203 Header naming consistency

HTTP headers are case-insensitive, but inconsistent casing harms maintenance. Report variants such as `X-MARCHE` and `X-Marche`; never merge them without confirming semantic equivalence.

#### AUD-204 Naming conventions

Audit path style, component names, operation IDs, tags and property naming against configuration.

#### AUD-205 Deprecation metadata

Report descriptions containing configured phrases such as `Endpoint obsolète` when `deprecated: true` is absent.

#### AUD-206 Date/time header formatting

Report likely date/time strings without formats, but do not modify automatically.

#### AUD-207 Description quality

Report HTML-escaped Markdown markers, suspicious encoding, duplicated descriptions, obvious placeholders and configured terminology issues.

#### AUD-208 Required array consistency

Report required properties that do not exist, impossible required/nullability combinations and arrays lacking `items`.

#### AUD-209 `additionalProperties` consistency

Report inconsistent object openness and cases where generated contracts may differ unexpectedly. No automatic change.

#### AUD-210 Extension formatting

Audit known `x-sg-*` structures against a configurable schema. Unknown extensions remain opaque and preserved.

### 13.4 Maintainability and governance audit

#### AUD-300 Missing reusable standards

Report operations that do not use configured standard headers, security, error responses or tags.

#### AUD-301 Inconsistent required headers

Report the same header being required in some comparable operations and optional in others.

#### AUD-302 Inconsistent security

Report operations that unexpectedly override or disable global security, including `security: []`.

#### AUD-303 Server and environment placeholders

Validate configured placeholders such as `$(catalog.host)` and environment-specific properties. Unknown placeholders are errors or warnings based on policy.

#### AUD-304 Vendor configuration consistency

Validate gateway configuration, catalog names, property references and target URL placeholders without altering them.

#### AUD-305 Dead metadata

Report unused tags, unreferenced security schemes and components that have no effect until referenced.

---

## 14. Semantic fingerprinting and duplicate detection

### FR-400 Canonical representation

For duplicate detection, transform each candidate object into a canonical JSON representation.

Canonicalization shall:

- remove source-location metadata;
- normalize mapping order;
- normalize set-like arrays;
- preserve semantically ordered arrays;
- resolve local references only when the selected comparison mode requires structural equivalence;
- retain all validation keywords and vendor extensions by default;
- optionally exclude documentation-only fields for near-duplicate analysis, never for SAFE equivalence.

### FR-401 Hashing

Create a stable cryptographic hash of the canonical representation for grouping exact duplicates.

### FR-402 Equivalence profiles

Provide profiles:

- `strict`: includes all fields and extensions;
- `contract`: excludes clearly non-contractual documentation fields but includes constraints, examples according to policy, and extensions;
- `shape`: compares structural shape for suggestion purposes only.

Only `strict` equivalence qualifies for automatic extraction by default.

### FR-403 Savings estimate

For every deduplication candidate, report:

- current occurrences;
- current estimated serialized size;
- estimated component plus references size;
- estimated absolute and percentage savings;
- break-even occurrence count;
- safety class.

---

## 15. Safe optimization engine

### OPT-001 Canonical serialization

Generate deterministic YAML with configurable indentation, quoting, line endings and key ordering.

### OPT-002 Extract exact repeated parameters

Create reusable parameter components only when every semantic field and extension is strictly equivalent. Preserve parameter override behavior and insertion scope.

### OPT-003 Extract exact repeated responses

Create reusable response components when strict equivalence is proven.

### OPT-004 Extract exact repeated request bodies

Create reusable request-body components when strict equivalence is proven.

### OPT-005 Extract exact repeated headers and examples

Create reusable components when supported and strictly equivalent.

### OPT-006 Extract inline schemas

Disabled by default. It requires explicit opt-in because client generators may derive different type names after extraction.

### OPT-007 Remove unused components

Remove only components proven unreachable and not matched by keep patterns. External consumers that reference fragments of this file must be considered via an optional external-reference index. Without that index, removal is REVIEW_REQUIRED.

### OPT-008 Consolidate path-level parameters

Move identical parameters shared by every operation of a path to the Path Item only when override behavior and generated result remain equivalent.

### OPT-009 Add explicit deprecation flag

May be applied only under a configured deprecation phrase policy. This remains metadata-changing and is disabled by default.

### OPT-010 Preserve gateway behavior

No optimization may remove, merge or relocate `x-*` data unless a vendor-specific rule proves equivalence.

### OPT-011 Post-transformation verification

After optimization, the tool shall reparse the output, validate it, resolve references and compare the optimized document against the pre-optimization document using the contract-equivalence profile.

If equivalence cannot be established, output generation fails and no optimized file is published.

### OPT-012 Rollback artifact

When optimization is applied, generate an optimization manifest listing every transformation, original location, destination component, fingerprint and reversibility information.

---

## 16. Patch semantics

A plain OpenAPI fragment is the required V1 patch format because it is easy to review and manually merge. The product may additionally emit an OpenAPI Overlay-compatible document when `--patch-format overlay` is selected.

Patch content shall include:

- updated `info.version`;
- added paths and operations;
- complete modified objects where a field-level fragment would be ambiguous;
- removals only when `--include-removals` is specified;
- all required new dependencies;
- no unrelated audit optimizations unless `--include-safe-optimizations` is specified.

Functional delta and cleanup changes must remain separate by default to keep pull requests reviewable.

---

## 17. Reporting requirements

### 17.1 Markdown report structure

1. Executive summary
2. Input document metadata and hashes
3. Validation status
4. Recommended version change
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
17. Suppressed findings
18. Generated artifacts
19. Limitations and assumptions

### 17.2 Machine-readable report

The JSON report shall include stable schemas for:

- run metadata;
- diagnostics;
- differences;
- classifications;
- version recommendation;
- audit findings;
- optimization proposals;
- artifact paths;
- before/after hashes and sizes.

### 17.3 Finding format

Each finding shall contain:

```json
{
  "ruleId": "AUD-101",
  "severity": "warning",
  "safety": "SAFE",
  "message": "Header parameter is repeated in 14 operations.",
  "jsonPointer": "/paths/~1documents/post/parameters/0",
  "locations": [],
  "suggestedComponentName": "XCorrelationId",
  "estimatedBytesSaved": 2180,
  "autofixAvailable": true,
  "documentationUrl": null
}
```

---

## 18. Command-line interface

### Compare and generate report plus patch

```bash
openapi-tool diff \
  --source new-api.json \
  --target production.yaml \
  --output ./artifacts
```

### Audit production only

```bash
openapi-tool audit \
  --target production.yaml \
  --output ./artifacts
```

### Generate optimization plan without changing production

```bash
openapi-tool optimize \
  --target production.yaml \
  --plan-only \
  --output ./artifacts
```

### Apply only safe optimizations to a new file

```bash
openapi-tool optimize \
  --target production.yaml \
  --apply-safe-optimizations \
  --output-file openapi-production.optimized.yaml
```

### Run everything

```bash
openapi-tool all \
  --source new-api.json \
  --target production.yaml \
  --mode both \
  --apply-safe-optimizations \
  --output ./artifacts
```

### Suggested options

```text
--source <file>
--target <file>
--output <directory>
--mode report|patch|json|both|all
--patch-format fragment|overlay
--ruleset <file>
--suppressions <file>
--strict
--fail-on error|warning|breaking|none
--allow-local-refs
--allow-remote-refs
--remote-ref-host <host>
--include-removals
--include-safe-optimizations
--apply-safe-optimizations
--allow-review-required <rule-id,...>
--version <semver>
--force-version
--canonical-format
--in-place
--force
--no-backup
--verbose
```

---

## 19. Configuration and suppression

Rules shall be configurable by ID.

```yaml
rules:
  AUD-005:
    severity: error
  AUD-101:
    severity: warning
    minimumOccurrences: 3
  OPT-006:
    enabled: false

protectedExtensions:
  - x-sg-

versioning:
  enumValueAdded: major
  successResponseAdded: minor

suppressions:
  requireJustification: true
```

A suppression must contain rule ID, exact or glob location, justification, owner and optional expiration date. Expired suppressions shall be reported.

---

## 20. Exit codes

- `0`: completed; no finding at or above configured failure threshold.
- `1`: validation or audit threshold exceeded.
- `2`: breaking change detected when configured to fail.
- `3`: invalid command-line usage or missing file.
- `4`: parsing or reference-resolution failure.
- `5`: output generation or post-transformation verification failure.
- `6`: internal unexpected error.

---

## 21. Architecture

Suggested solution structure:

```text
OpenApiTool.Cli
OpenApiTool.Application
OpenApiTool.Domain
OpenApiTool.Parsing
OpenApiTool.Diff
OpenApiTool.Versioning
OpenApiTool.Audit
OpenApiTool.Optimization
OpenApiTool.Serialization
OpenApiTool.Reporting
OpenApiTool.Tests.Unit
OpenApiTool.Tests.Integration
OpenApiTool.Tests.Golden
```

Key abstractions:

```text
IOpenApiLoader
IOpenApiValidator
ISemanticNormalizer
IDocumentComparer
IChangeClassifier
IDependencyResolver
IVersionPolicy
IAuditRule
IOptimizationRule
IContractEquivalenceVerifier
IPatchWriter
IReportWriter
```

Implementation should use an OpenAPI object-model library for parsing and semantic access, plus a YAML parser capable of pre-deserialization duplicate-key detection. A lint adapter may integrate Spectral-compatible rules, but the core application must not require Node.js to perform its built-in checks.

---

## 22. Performance and reliability

- Support at least 1,000 operations and 10,000 schemas.
- Complete a typical enterprise document audit in under 30 seconds on a developer workstation.
- Avoid recursive call-stack overflow by using iterative graph traversal where practical.
- Cache canonical fingerprints.
- Parse each input only once per run.
- Produce deterministic results for identical inputs and configuration.
- Cancel cleanly on Ctrl+C.
- Never emit partial optimized output as a successful artifact.

---

## 23. Security requirements

- Disable remote reference fetching by default.
- When enabled, protect against SSRF with host allowlists, scheme restrictions, timeouts and response-size limits.
- Reject file references outside configured roots unless explicitly allowed.
- Do not log access tokens, credentials or sensitive examples.
- Provide optional secret-pattern auditing for examples and server URLs.
- Treat input as untrusted and enforce maximum document size, nesting depth and reference count.
- Do not execute vendor extensions or embedded scripts.

---

## 24. Testing strategy

### 24.1 Unit tests

Cover:

- version classification;
- canonicalization;
- strict and contract fingerprints;
- change classification;
- dependency traversal and cycles;
- each audit rule;
- each optimization rule;
- suppression matching;
- byte-savings calculations.

### 24.2 Integration tests

Cover:

- JSON source versus YAML target;
- YAML versus YAML;
- OpenAPI 3.0 versus 3.0;
- OpenAPI 3.1 versus 3.1;
- local external references;
- invalid references;
- vendor extensions;
- large generated descriptions;
- optimization followed by equivalence verification.

### 24.3 Golden-file tests

Maintain representative specifications and approved Markdown, JSON, patch and optimized YAML outputs. Normalize environment-specific paths before comparison.

### 24.4 Mutation and negative tests

Intentionally vary requiredness, nullability, enum values, security and schema constraints to ensure unsafe changes are never classified as safe.

### 24.5 Property-based tests

Verify canonicalization idempotence and that YAML/JSON round trips preserve the semantic model.

---

## 25. Acceptance criteria

The product is accepted when all the following are true:

1. It parses valid OpenAPI 3.0.x and 3.1.x JSON/YAML inputs.
2. It reports syntax, structural and reference errors with actionable locations.
3. It detects additions, removals and modifications across paths, operations, components, security, metadata and vendor extensions.
4. It generates a patch with no unresolved internal references.
5. It recommends a version increment from the target version and explains every contributing reason.
6. It audits duplicate parameters, responses, request bodies, schemas, headers, examples and unused components.
7. It reports formatting and consistency problems separately from contract changes.
8. Every optimization has a safety class and evidence.
9. Safe optimization output reparses and passes contract-equivalence verification.
10. The original production file is unchanged by default.
11. The report includes before/after sizes and estimated or actual savings.
12. Unknown vendor extensions are retained.
13. Circular schema references do not crash the tool.
14. Duplicate YAML keys are detected before normal parsing loses them.
15. Output is deterministic for identical inputs and configuration.
16. Functional changes and cleanup optimizations are separate unless explicitly combined.

---

## 26. Example audit result for the provided production style

The audit engine should be capable of producing findings such as:

```text
AUD-101 Repeated header parameter
X-serviceEmetteur appears inline in 14 operations with an identical strict fingerprint.
Safety: SAFE
Recommendation: Extract to components.parameters.XServiceEmetteur.
Estimated saving: 2.1 KB.
```

```text
AUD-205 Missing deprecation metadata
POST /initialiserDossiersPiecesEtDocuments contains "Endpoint obsolète" but deprecated is absent.
Safety: REVIEW_REQUIRED
Recommendation: Add deprecated: true after owner review.
```

```text
AUD-007 Request-body requiredness unspecified
POST /ajouterDocument defines a request body without required: true.
Safety: REVIEW_REQUIRED
Reason: OpenAPI treats the body as optional when required is omitted.
```

```text
AUD-202 Inconsistent response-key quoting
Both 200 and '200' styles are present.
Safety: SAFE
Recommendation: Apply canonical quoting during serialization.
```

```text
AUD-010 Empty external documentation URL
externalDocs.url is empty.
Safety: REVIEW_REQUIRED
Recommendation: Populate a valid URL or remove externalDocs.
```

These are findings, not assumptions. The implementation must calculate occurrences and verify semantic equivalence before proposing an autofix.

---

## 27. Delivery plan

### Milestone 1: Foundation

- CLI
- parsing
- syntax/structural validation
- reference graph
- normalized model
- JSON and Markdown diagnostics

### Milestone 2: Full semantic diff and versioning

- document comparison
- change classification
- semantic version policy
- dependency-complete patch

### Milestone 3: Audit engine

- validity rules
- consistency rules
- redundancy fingerprints
- unused-component analysis
- size estimates

### Milestone 4: Safe optimizer

- reusable parameters/responses/request bodies/headers/examples
- canonical formatting
- unused-component handling
- contract-equivalence verification
- optimization manifest

### Milestone 5: Enterprise hardening

- large-file performance
- external references
- vendor rules
- CI samples
- security limits
- complete golden test suite

The milestones organize delivery, but the release described by this PRD is complete only when all acceptance criteria are met.

---

## 28. Design decisions and rationale

- Reusable OpenAPI components and `$ref` references are the primary standards-based mechanism for reducing repeated definitions and improving maintainability.
- Audit output is separate from the functional patch because cleanup mixed with endpoint changes creates noisy, risky reviews.
- Exact semantic fingerprints are required before automatic deduplication.
- Schema deduplication is more conservative than parameter or response deduplication because code generators often use component names as generated type names.
- OpenAPI Overlay output is optional because a fragment is simpler for manual review, while overlays provide a standardized representation of update/remove actions.
- A valid OpenAPI document can still be inconsistent or poorly documented, so schema validation alone is insufficient. The product includes configurable lint rules.
- Full-file reserialization cannot reliably preserve YAML comments and anchors with a semantic object model, so optimized output is a new artifact and never silently replaces the original.

---

## 29. Reference material

- OpenAPI Specification: https://spec.openapis.org/oas/latest.html
- OpenAPI reusable components: https://learn.openapis.org/specification/components.html
- OpenAPI Overlay Specification: https://spec.openapis.org/overlay/latest.html
- Spectral: https://github.com/stoplightio/spectral
