# Contract: console experience

Two screens and two report sections. Everything else the developer sees is 003's, unchanged — which is the
point: a developer who knows the OpenAPI reports must read a WSDL comparison with nothing new explained
(SC-016).

## The correspondence screen — WR-104, WR-106, WR-303

Shown after both documents are read and projected, **before anything is compared**.

```text
Le WSDL porte 23 opérations. La production porte 31 routes.

  ✓ déclarée    deposerDocument          → POST /documents
  ~ proposée    lireDocument             → GET /documents/{id}        (jetons : lire, document)
  ~ proposée    listerDossiers           → GET /dossiers              (jetons : lister, dossier)
  ? ambiguë     majDocument              → PUT /documents/{id}  ou  PATCH /documents/{id}
  ∅ absente     archiverDocument         → rien en production

  [espace] corriger une ligne   [entrée] confirmer   [échap] renoncer
```

Four marks, and each one is a different kind of news:

- **`✓ déclarée`** came from the mapping file. The tool does not second-guess it (WR-105).
- **`~ proposée`** is the tool's guess, with the tokens that justified it. A developer who disagrees can
  see *why* the tool thought so, which is what makes correcting fast.
- **`? ambiguë`** is the proposer declining between near-ties. It must be resolved or left absent; it is
  never silently resolved, because `Ambiguous` exists precisely so uncertainty is not hidden (WR-108).
- **`∅ absente`** is a coverage gap in the making. Nothing is wrong yet — something is missing.

**Renoncer is an answer.** The run succeeds having compared nothing, and the report says the
correspondence was not confirmed. A tool that treated declining as failure would push a developer to
confirm something they had not read.

## The draft selection screen — 003, extended by WR-303

003's screen, with one column added: the coverage state, beside the impact and confidence it already
shows. And one mark: an item resting on an **inferred** correspondence says so where it is shown.

```text
  ∅ absente     POST /documents        — route absente de la production   [correspondance proposée]
  ✓ couverte    GET /documents/{id}    — 2 différences                    [correspondance déclarée]
```

A developer selecting under an inferred correspondence is choosing on a weaker footing, and must be told
**at the moment of choosing** — not afterwards in a report they may not open.

## Report section: correspondance — WR-106

Entry by entry, with each entry's origin, followed by one sentence that the report must carry verbatim in
substance: *chaque constat de compatibilité ne vaut que ce que vaut la correspondance qui le porte.*

A wrong correspondence does not produce a wrong finding here and there. It produces a confident report
about the wrong pair of operations, which is the worst failure this feature can have — so the report says
so rather than letting a reader assume otherwise.

## Report section: couverture — WR-107, WR-108, WR-109

One row per WSDL operation: its state, and for an absent one, what production would need to carry.

**Never merged with the breaking changes.** A coverage gap is not a regression: nothing is broken,
something is missing, and the two are acted on by different people on different days.

## Report section: comparabilité — WR-111, WR-603

The proportion, as a figure, and the grouped non-carried entries.

```text
Comparé : 23 opérations sur 23 correspondues, 1 842 champs projetés sur 2 310 (79,7 %).

| Non porté                        | Nombre | Pourquoi |
|----------------------------------|--------|----------|
| attributs XSD                    | 412    | aucun équivalent en OpenAPI |
| groupes de substitution          | 34     | aucun équivalent en OpenAPI |
| ordre des particules             | 22     | les membres d'un objet JSON ne sont pas ordonnés |
```

The figure is the point. A reader must be able to tell a clean report from a silent one, and only a number
does that — which is why WR-603 says "as a figure and not as prose".

## What the console must never do

- Compare before the correspondence is confirmed. WR-104.
- Resolve an ambiguity by picking. WR-108.
- Show a coverage gap among the breaking changes. WR-109.
- Reproduce a `soap:address` value, or a URL's userinfo or query. WR-019, WR-605.
- Use `System.Console`. The two guarded usages in `Presentation/Shell` remain the only ones.
