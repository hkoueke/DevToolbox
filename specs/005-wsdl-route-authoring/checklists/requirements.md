# Specification Quality Checklist: Authoring a route instead of deriving one

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-10-28
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

Both clarifications were answered on 2026-10-28 and the markers removed.

- **No declaration file.** Authoring decisions live in the checkpoint and nowhere else (RA-024, RA-025).
  A completed sitting is not carried to the next run, which is why the earlier draft's "second delivery
  under 2 minutes" criterion was withdrawn rather than left unmet.
- **WR-105 is withdrawn** (RA-026), and its remains removed: a context key read and never written, a
  proposer parameter, a corpus example, a report marker. Leaving an unfed capability in place after
  deciding against it would repeat a defect `CONCEPTION.md` already names twice.

Three requirements came from the answer rather than the original draft, and each is covered by an
acceptance scenario:

- The developer chooses **where in the production document** the route is placed (RA-008, RA-009).
- Requiredness is **asked** for every placement but the path (RA-012, RA-014).
- The description is left as a **conspicuous placeholder**, never composed (RA-018 to RA-020).

Named facts the specification relies on, all established against a real pair before drafting:

- 19 of 19 derived routes missed their real counterpart.
- The five governance headers of the production description appear zero times in the service contract.
- Every element of the contract is `minOccurs="0" nillable="true"`, so requiredness is not stated.

One assumption goes beyond what was asked and is flagged for review at planning: the request named three
destinations for a field — route, header, body — and the specification offers **four**, splitting the
route into path and query, because OpenAPI distinguishes them and a document conflating them would not be
valid. "Nowhere" is kept as a fifth answer so a field can be dropped on purpose rather than in silence.
