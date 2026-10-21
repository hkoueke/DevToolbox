# Specification Quality Checklist: A WSDL as a candidate for apidelta

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-21
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

### Validation — 2026-10-21, after the narrowing

All sixteen items pass. 5 user stories, 56 functional requirements, 16 success criteria, 4 clarifications
logged. No duplicate identifiers; every internal reference resolves; all thirteen requirements cited from
003 exist there; every scenario list is sequential.

**The specification is now less than half the size it was**, and that is the main result of the session: 11
stories to 5, 95 requirements to 56, 1183 lines to 611. Nothing was lost that had a subject. What went was
everything that treated the WSDL as a contract the team owns — comparing two WSDL versions, versioning by
target namespace, a nine-transformation cleanup catalogue, twenty-five examination rules, and extraction of
an operation into a standalone WSDL. Each had been specified in good faith and each described work nobody
had asked for. The XSD semantics they carried survive where they are actually needed: in the projection,
WR-100 to WR-115.

**What the feature is.** 003 already compares a candidate against the production description and writes
production plus the developer's selection as one complete corrected document. This feature lets the
candidate be a WSDL. That framing is what made the narrowing possible, and it is why the spec can decline to
restate the selection, the statement before writing, the untouched-byte guarantee, the marking and the
verification — they are 003 FR-501 to FR-518, cited rather than copied, because two copies of a contract
diverge.

**What it genuinely adds, and the two places the work lives.**

*Reading.* A WSDL is a graph of files, not a file, and it may arrive as a URL. WR-005 opens local import
resolution by default — diverging deliberately from 003's closed reference policy, because a WSDL whose
types sit in a sibling schema is the ordinary input rather than the exception — and WR-006 pays for that
with an obligation never to be silent about the perimeter. WR-011 materialises a URL input to disk before
reading it, which is what lets a new kind of input obey rules already in force instead of needing
exceptions; WR-012 and WR-013 close the two defects materialising introduces, a name collision between two
retrieved documents and a relative import that must still mean what it meant at its origin.

*Projecting.* WR-110 publishes the projection as a table: what maps, what maps only approximately, and what
has no counterpart at all — `xsd:attribute`, substitution groups, namespaces, binding style, particle order.
Publishing it is what makes "not comparable" a falsifiable claim instead of an excuse. WR-107 keeps coverage
and compatibility apart so the certain half is not diluted by the uncertain one; WR-111 groups the
non-comparable by reason rather than enumerating it; WR-603 states the proportion actually compared as a
figure, so a clean report cannot be mistaken for a silent one.

**The one rule most likely to be argued with later**, and the reason it is written down: WR-301 forbids
filling what the projection cannot carry and demands an explicit unfilled marker in its place. A plausible
schema would be more pleasant to receive and is the one output from which a reader cannot recover the fact
that nobody knows. WR-302 makes a document carrying such markers say so and count them, so it is
recognisable as unfinished from itself alone rather than only from the report.

**A correspondence, which no convention supplies.** Nothing in either language says that `POST /documents`
answers `deposerDocument`. WR-104 to WR-106: the tool proposes, the developer confirms, nothing is compared
before, each entry records its origin, and the report states that every compatibility finding is only as
reliable as the correspondence above it. A wrong correspondence does not produce a wrong finding here and
there — it produces a confident report about the wrong pair, which is the worst failure available to this
feature.

**Known gap, recorded rather than hidden.** This specification depends on 003 FR-500 to FR-518, which 003's
code does not yet implement — `tasks.md` there closed at 209 of 209 behind a fragment builder. The agreed
order is to implement those first, and no release is tagged until then.

Ready for `/speckit-plan`.
