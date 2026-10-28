# Contract: Console experience

What the developer sees. Five questions per kept operation, each one short, each one naming its subject.
The repository's standing rules apply: a prompt names the result rather than the tool's work, a label names
what distinguishes it from its neighbours, and the screen is cleared on entering the tool.

---

## 1. The route

```
┌ documents 3 sur 21 ─ AjouterDocuments ────────────────────────────────┐
│ Suggestion : POST /documents          (repli — aucun verbe reconnu)   │
└───────────────────────────────────────────────────────────────────────┘

Quel chemin ? (entrée pour garder la suggestion)
> /Dossier/AjouterDocuments

Quel verbe ?
> POST
```

- The suggestion is pre-filled and editable. Enter keeps it.
- Its worth is shown in the developer's words — *dérivée*, *partielle*, *repli* — never as a bare route
  that looks like a fact (RA-002).
- A refused path says which rule it broke and where:
  `« Dossier/Ajouter Documents » : un chemin commence par « / », et ne contient pas d'espace.`

## 2. Where it goes

```
Où placer cette route dans votre production ?
> après /Dossiers/
  après /Dossier/CreerDossierDocument
  après /Dossier/AjouterDocuments
  …
  à la fin
```

In the document's own order (RA-009), so the developer picks a neighbour they can see. "À la fin" is the
last entry, not the first, because a route usually joins a family.

## 3. The fields

```
Où vont les champs de « AjouterDocuments » ?
  espace pour changer, entrée pour valider

> request        AjouterDocumentCommand   composite   → corps
  identifiant    string                               → corps
  date           string                               → corps
```

- Pre-set from the verb (RA-011). `POST` proposes the body; `GET` proposes the query.
- A composite under `GET` or `DELETE` arrives pre-set to *nulle part* and marked as needing a decision, so
  accepting the defaults cannot silently drop it.
- A refusal names the field and its type:
  `« request » est de type AjouterDocumentCommand, qui n'est pas un scalaire : il ne peut pas voyager dans
  la requête. Placez-le dans le corps, ou changez le verbe.`

## 4. What is required

```
« identifiant » est-il obligatoire ? (o/N)
```

- Asked once per placed field, except in the path (RA-014).
- The default is no, and the question says why it is asked:
  `Le contrat ne le dit pas — tout y est optionnel et nillable.`

## 5. The headers

```
Votre production porte ces en-têtes sur toutes ses routes. Les attacher ?
  espace pour décocher, entrée pour valider

> [x] X-idCorrelation      (20 routes sur 20)
  [x] X-serviceEmetteur    (20 routes sur 20)
  [x] X-serviceInitial     (20 routes sur 20)
  [x] X-utilisateur        (20 routes sur 20)
  [x] X-MARCHE             (20 routes sur 20)
```

- Checked by default when every production route carries them, unchecked otherwise.
- Nothing is shown when production declares none (RA-023).

---

## Before writing

```
3 routes composées :

  POST /Dossier/AjouterDocuments     après /Dossiers/        5 en-têtes, corps, 0 écarté
  GET  /Dossier/{identifiant}        après la précédente     5 en-têtes, 3 en requête, 1 écarté
  POST /Document/PasserEnRecu        à la fin                5 en-têtes, corps, 0 écarté

1 champ écarté : « categorieDossier » de GetDocument.
3 descriptions restent à écrire : chaque route composée porte « À COMPLÉTER ».

Votre fichier de production ne sera pas modifié : un fichier consolidé distinct sera écrit à côté
des rapports.

Écrire la production consolidée de ces éléments ? (o/N)
```

Dropped fields and unwritten descriptions are stated **before** the accord, not discovered afterwards in
the file — the same rule the unfilled markers of feature 004 already follow.

---

## In the report

One section, one row per authored route, and one sub-list of placements. A table per route would make a
21-operation report unreadable; placements belong under their route rather than in a flat table of eighty
rows.

```markdown
## Routes composées

| Route | Opération | Où | Suggestion | Descriptions |
|---|---|---|---|---|
| `POST /Dossier/AjouterDocuments` | `AjouterDocuments` | après `/Dossiers/` | repli, remplacée | à écrire |

### `POST /Dossier/AjouterDocuments`

| Champ | Type déclaré | Destination | Obligatoire |
|---|---|---|---|
| `request` | `AjouterDocumentCommand` | corps | oui |

En-têtes : `X-idCorrelation`, `X-serviceEmetteur`, `X-serviceInitial`, `X-utilisateur`, `X-MARCHE`.
```

- *Suggestion* says what the tool proposed and whether the developer kept it, which is what lets a reader
  tell a decision from a guess (RA-028).
- Dropped fields appear in their route's table with destination *écartée*, and are also totalled in one
  line, so neither reading misses them.
- The count of placeholders is stated (RA-020).

---

## What the developer is never asked

- For a description. It is left as `À COMPLÉTER` (RA-018).
- For requiredness of a path parameter. OpenAPI settles it (RA-014).
- For a pairing of an operation production already covers. That question belongs to the pairing step,
  which this feature leaves alone.
