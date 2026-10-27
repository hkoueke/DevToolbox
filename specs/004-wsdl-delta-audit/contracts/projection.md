# Contract: the projection

The table of WR-110, as the code must implement it. One rule per row, and a test that the rule set covers
this table exactly — the shape 003 already uses for its rule catalogue.

`ProjectionConfidence` has three members, and the third is the one that makes the feature honest:

- **`Carried`** — the correspondence holds. A finding over it carries the comparer's own confidence.
- **`Approximate`** — the correspondence nearly holds. A finding over it says so and carries *uncertain*
  confidence (WR-112). Never presented as an equivalence.
- **`NotCarried`** — there is no counterpart. Produces a `ProjectionGap`, grouped by reason (WR-111),
  never a per-occurrence finding.

| WSDL / XSD | OpenAPI / JSON Schema | Confidence | Note |
|------------|-----------------------|------------|------|
| Element in a `sequence` | Object property | `Carried` | order kept, see the last row |
| `minOccurs="0"` | absent from `required` | `Carried` | |
| `maxOccurs` greater than one | `type: array` with bounds | `Carried` | `unbounded` leaves `maxItems` absent |
| `maxLength`, `minLength`, `pattern` | the same keywords | `Carried` | |
| `enumeration` | `enum` | `Carried` | |
| `minInclusive`, `maxInclusive`, `minExclusive`, `maxExclusive` | `minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum` | `Carried` | |
| `totalDigits`, `fractionDigits` | — | `NotCarried` | no JSON Schema counterpart; reason `NumericPrecision` |
| `xsd:string`, `boolean`, `int`, `long`, `decimal`, `double`, `dateTime`, `date`, `base64Binary` | `type` with `format` | `Carried` | the `format` mapping is a convention, stated in the report, not an equivalence |
| `xsd:duration`, `gYear`, `gMonthDay`, and the other gregorian types | `type: string` | `Approximate` | reason `NoTypedCounterpart`; the shape survives, the meaning does not |
| `nillable="true"` | `nullable`, or `null` in a type union | `Carried` | which one depends on production's declared version |
| `xsd:choice` | `oneOf` | `Approximate` | one branch per particle; reason `ExclusiveByParticle`; XSD choice is exclusive by particle, `oneOf` by validation. Each branch holds that one particle, projected by the particle rows above — so the branch states the type it expects and requires that particle alone. The particles of a choice are **not** projected a second time at the type level: that declared every branch required at once |
| derivation by extension | `allOf` | `Approximate` | reason `BaseRelationship`; `allOf` does not express the base/derived relationship |
| derivation by restriction | — | `NotCarried` | reason `Restriction`; JSON Schema cannot narrow a referenced schema |
| `xsd:any`, `processContents` | `additionalProperties` | `Approximate` | reason `OpenContent`; `strict` vs `lax` vs `skip` collapses to true/false |
| `xsd:all` | object properties, order ignored | `Carried` | JSON object members are unordered, so `all` maps exactly |
| SOAP fault | status code and response body | `Approximate` | reason `DerivedStatusCode`; the status code is derived, not declared |
| SOAP header | a header parameter | `Approximate` | reason `SoapRole`; mandatory/optional survives, the SOAP role does not |
| `xsd:attribute` | — | `NotCarried` | reason `Attribute`; the single largest source of gaps in practice |
| substitution group, abstract head | — | `NotCarried` | reason `SubstitutionGroup` |
| mixed content | — | `NotCarried` | reason `MixedContent` |
| namespace of a declaration | — | `NotCarried` | reason `Namespace`; JSON has no namespaces |
| binding `style`, `use`, transport | — | `NotCarried` | reason `Binding` |
| particle order inside a `sequence` | — | `NotCarried` | reason `ParticleOrder`; carried into the shape, not into the contract |
| `assert`, `alternative`, `openContent` (XSD 1.1) | — | `NotCarried` | reason `Xsd11`; the type is reported as not projectable rather than projected approximately |

## Route derivation — WR-305

Deterministic, and stated in the report for every operation.

| Operation name shape | Derived route | Note |
|----------------------|---------------|------|
| verb + subject, verb in the table | method from the verb, path from the plural subject | `deposerDocument` → `POST /documents` |
| verb + subject, verb absent from the table | `POST` on the subject | reported as a partial derivation |
| no recognisable verb | `POST` on the operation name | reported as a **fallback**, not a derivation |

The verb table is configurable. The fallback is not: an RPC-shaped service has no REST shape to find, and
inventing one would be the tool deciding something it cannot know.

## What the projection must never do

- Produce a `SpecDocument` with byte extents. R-03, and the splicer would eventually be pointed at them.
- Report `Approximate` as `Carried`. WR-112; a reader cannot recover doubt from a confident finding.
- Emit one finding per non-carried occurrence. WR-111; four hundred identical findings teach one thing at
  four hundred lines' cost.
- Project a type whose meaning depends on an XSD 1.1 construct. The honest failure is to decline it.

## Clarification, 2026-10-22

**Every non-carried row now names a reason.** The table as first written named a reason on some rows and
left it to the note on others — `xsd:choice`, derivation by extension, `xsd:any`, SOAP fault and SOAP
header. Implementing it showed why that does not hold: the reason is what groups the gaps, so a row without
one produces a gap that cannot be grouped, which means a line per occurrence — exactly what WR-111 exists
to prevent. Five reasons were added: `ExclusiveByParticle`, `BaseRelationship`, `OpenContent`,
`DerivedStatusCode` and `SoapRole`. Each states what the note already said.

**`assert`, `alternative` and `openContent` are refused at read time, not at projection time.** The .NET
schema compiler reads XSD 1.0 and rejects them with "element is not supported in this context", which does
not tell the developer that their schema is XSD 1.1. That complaint is now reissued under `WSDL-XSD11`,
naming the construct and saying the type would have been declined rather than approximated. The projection
rule remains, carries the same message, and matches nothing reachable today — it becomes reachable, without
being rewritten, the day an XSD 1.1 compiler is used.
