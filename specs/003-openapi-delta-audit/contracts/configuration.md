# Contract — ruleset and suppressions

**Feature**: [../spec.md](../spec.md) · **Plan**: [../plan.md](../plan.md) · **Date**: 2026-10-16

Two optional input files. **The tool works with neither** (FR-656): the shipped defaults are a working
configuration, and a team adopts a ruleset when it disagrees with one, not in order to start.

Both are read before the run begins. Invalid configuration is reported with the file and the offending
entry, and the run does not start — there is no silent fallback to defaults, because a ruleset that is
quietly ignored changes what the audit reports without saying so (FR-655).

---

## 1. The ruleset

```yaml
# apidelta.ruleset.yaml
rules:
  AUD-005:
    severity: error
  AUD-101:
    severity: warning
    minimumOccurrences: 3
  AUD-204:
    enabled: false
  OPT-006:
    enabled: false          # inline schema extraction: off by default, and this keeps it off
  OPT-009:
    enabled: true           # deprecation flag: changes published metadata, so opt-in

protectedExtensions:
  - x-sg-

deprecationPhrases:
  - Endpoint obsolète
  - Deprecated endpoint

standardHeaders:
  - X-serviceEmetteur
  - X-correlationId

standardResponses: [ "400", "401", "403", "404", "500" ]

namingConventions:
  paths: kebab-case
  components: PascalCase
  operationIds: camelCase
  properties: camelCase

environmentPlaceholders:
  pattern: '\$\((?<name>[A-Za-z0-9_.-]+)\)'
  known: [ catalog.host, catalog.name ]
  unknown: warning          # or: error

canonicalFormatting:
  indent: 2
  lineWidth: 120
  lineEndings: lf           # lf | crlf | preserve
  quoteResponseCodes: true
  finalNewline: true
  keyOrder: canonical       # canonical | preserve

references:
  allowLocal: false
  allowRemote: false
  remoteHosts: []
  root: .                   # local references may not resolve outside this directory

limits:
  maxBytes: 33554432        # 32 MiB
  maxDepth: 200
  maxReferences: 20000
  maxAliasExpansions: 10000
  maxFindingsPerRule: 500
  maxNearDuplicateBucket: 200

versioning:
  enumValueAdded: major     # major | minor | patch
  successResponseAdded: minor
  errorResponseAdded: patch
  fallbackBaseline: null    # a baseline is never invented unless this is set explicitly

output:
  maxGrowthPercent: 0       # an optimized document larger than the original is refused

suppressions:
  requireJustification: true
```

### Rules of reading it

- **An unknown rule identifier is an error**, naming the identifier. A typo that silently disables nothing
  is how a team comes to believe a rule is off when it is on — and the reverse.
- **An unknown top-level key is an error.** The alternative is a configuration file whose misspelled
  sections do nothing for a year.
- `severity` may be `error`, `warning`, `information` or `opportunity`. It never changes a safety class:
  severity is how much a team cares, safety is what the tool can prove, and only the second decides whether
  anything may be applied (FR-412, FR-413).
- `enabled: false` on a rule means it produces no finding **and** appears in the report as disabled
  (FR-608). Disabling is a visible choice.
- `protectedExtensions` only ever adds protection. No ruleset can make an unknown vendor extension
  eligible for automatic removal or relocation (FR-806, OPT-010).
- `references.allowRemote: true` requires a non-empty `remoteHosts`; remote resolution with an empty
  allow-list is invalid configuration, not an open door (FR-006).
- `limits` may be lowered freely and raised with the understanding that they are the denial-of-service
  budget (FR-010). A limit that is reached is reported with the limit named, and the rules that could not
  finish are reported as not evaluated (FR-418).
- `versioning.fallbackBaseline` is the only way an unparseable production version is replaced rather than
  reported (FR-300).

---

## 2. Suppressions

```yaml
# apidelta.suppressions.yaml
suppressions:
  - ruleId: AUD-007
    location: /paths/~1ajouterDocument/post/requestBody
    justification: Le portail historique envoie le corps sans en-tête de longueur ; corrigé en 2027T1.
    owner: equipe-documents
    expires: 2027-03-31

  - ruleId: AUD-204
    location: /paths/*
    justification: Les chemins historiques sont figés par contrat avec le partenaire.
    owner: architecture
```

| Field | Required | Notes |
|---|---|---|
| `ruleId` | yes | must be a known identifier |
| `location` | yes | an exact JSON Pointer, or a glob over one — `*` matches one segment, `**` any number |
| `justification` | when `requireJustification` is true, which is the default | rejected as invalid configuration when missing (FR-652) |
| `owner` | yes | who to ask; a suppression without an owner is an orphan |
| `expires` | no | ISO date; an expired suppression is **not honoured** and is reported (FR-653) |

### Rules of applying them

- A suppressed finding is **listed** with its justification and owner, in its own report section, never
  dropped (FR-654). A suppression hides a finding from the failure threshold, not from the reader.
- An expired suppression reports the finding *and* reports the expiry. Both, every run.
- A suppression that matched nothing is reported as unmatched: it is usually a location that was fixed or
  renamed, and leaving it in place silently is how a suppression file becomes archaeology.
- Suppression never applies to a `Diagnostic`. A document that could not be read faithfully cannot be
  suppressed into readability (FR-009).

---

## 3. Where the defaults come from

The shipped defaults are the values above, with three deliberate choices:

- **`references.allowLocal: false`, `allowRemote: false`** — the secure default, and the deterministic one.
- **`OPT-006` and `OPT-009` disabled** — the two transformations that change generated client type names
  and published metadata respectively.
- **La section `extract` est retirée** — le 2026-10-27, avec FR-900 à FR-913. Une clé de configuration
  pour un mode qui n'existe plus serait refusée par le contrôle des clés inconnues, ce qui est le bon
  comportement et une mauvaise surprise : un jeu de règles d'équipe qui la portait doit être nettoyé, et
  le refus le nommera avec sa ligne.
- **The organization-specific values are present but replaceable** — the `x-sg-` prefix, the French
  deprecation phrase, the `$(catalog.host)` placeholder pattern. They come from the source product
  requirements document's own corpus, so the tool is useful on that corpus out of the box and not tied to
  it afterwards (spec Assumptions).

A ruleset is resolved as: shipped defaults → ruleset file → nothing else. There is no environment-variable
or command-line layer in this release, because there is no non-interactive invocation in this release
(FR-708a).
