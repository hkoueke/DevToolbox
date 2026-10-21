# Contract — Console experience for prqueue

**Feature**: [../spec.md](../spec.md) · **Date**: 2026-08-25

Spectre.Console renders everything. `System.Console` is banned outside the two existing guarded uses. All
text is French (FR-019); identifiers, logs, and these artifacts stay English.

---

## Where it lives

Tab **Azure DevOps**, beside varcompare. `Id = "prqueue"`, `DisplayName = "prqueue"`,
`Description = "les demandes de tirage qui attendent votre relecture (lecture seule)"`.

Discovered through `ITool`, registered by `AddPrQueue()`. No existing tool's menu, file, or registration is
edited.

---

## The announcement, before anything is read

FR-002 requires the identity on screen before the first read. It is a panel, not a line, because a developer
must not be able to skim past whose queue they are about to act on.

```
╭─ Cible ──────────────────────────────────────────────╮
│ serveur      azure.entreprise.local                  │
│ collection   DefaultCollection                       │
│ vous êtes    Hervé Koueke Kouemeni                   │
╰──────────────────────────────────────────────────────╯
```

If the identity cannot be resolved, this panel is **not** shown with a blank field. The tool explains and
stops (FR-003):

```
Impossible d'établir quelle identité votre session représente sur ce serveur.
Sans elle, une file vide ne voudrait rien dire : elle pourrait aussi bien signifier
« rien ne vous attend » que « le filtre n'a pas fonctionné ». Rien n'est affiché.
```

---

## The sweep

`Progress`, one line, cancellable. It names the project being read so a slow one is visible rather than
mysterious.

```
Lecture des projets  ━━━━━━━━━━━━━━━━━━━━━━╸           14/23   Facturation
```

`Ctrl+C` cancels cooperatively and leaves the described state: which projects were read, which were not.

---

## The queue

Ordered by age, oldest first (FR-008). The column is headed **âge**, not *attente*, because that is what it
measures (FR-008a).

```
File de relecture — Hervé Koueke Kouemeni · au 2026-08-25 08:14:02Z

  âge     projet         dépôt              n°    titre                        vers        attendu
  ──────────────────────────────────────────────────────────────────────────────────────────────────
  12 j    Facturation    facturation-api    412   Corrige l'arrondi TVA        main        ⚑ requis
   6 j    Portail        portail-web        1180  Ajoute le mode sombre        develop     ⧉ équipe
   2 j    Facturation    facturation-api    431   Passe le client en typé      main        personnel

  3 demandes attendent votre relecture · 23 projets lus sur 23 · âge compté depuis l'ouverture
```

**Symbols carry meaning without colour** (FR-020). Colour is reinforcement only; every state is also a word
or a mark, and the ASCII fallback is used when the terminal has no Unicode.

| Mark | Unicode | ASCII | Meaning |
|------|---------|-------|---------|
| required | `⚑ requis` | `! requis` | A required reviewer under branch policy |
| group | `⧉ équipe` | `# équipe` | Awaited through a group, not personally (FR-006) |
| personal | `personnel` | `personnel` | Named directly |
| blocked | `✖ conflits` | `x conflits` | `BlockingReason` other than `None` |
| undetermined | `? indéterminé` | `? indéterminé` | Blocking state could not be established |

**Too narrow a terminal** falls back to a stacked layout — one entry per panel — rather than truncating
(FR-023). No entry is ever dropped. This reuses varcompare's measured-layout approach: measure what will be
written, prefer columns while they genuinely fit.

---

## The two sentences that matter most

Everything else in this contract is presentation. These two are correctness.

**Nothing waiting, everything read** (FR-014):

```
Rien n'attend votre relecture. 23 projets lus sur 23, aucun groupe non résolu.
```

**Nothing waiting, something unread** (FR-013, FR-015) — never the sentence above:

```
Rien n'attend votre relecture dans ce qui a pu être lu.
File incomplète : 21 projets lus sur 23.
  non lus   Paie (droits insuffisants) · Archives (serveur indisponible)
  groupes   « Équipe Facturation » — appartenance non vérifiable
```

A developer must never have to wonder which of the two they are looking at. Both sentences are asserted by
tests over `TestConsole` output, in both the complete and the partial case.

---

## The scope prompt

Offered when the collection is large enough for the sweep to be worth narrowing (FR-011). Pre-selected from
what was remembered; the choice is stored per collection and survives restarts.

```
Quels projets balayer ?  (espace pour cocher, Entrée pour valider —
cochez « <-- Retour », en fin de liste, pour revenir en arrière)
```

Uses the shared `NavigationLabels`: the back entry carries the `<-- ` prefix no project name can imitate,
sits at the end of the list, and the list wraps so one Up keystroke reaches it (FR-021).

---

## The detail view

Opened on explicit choice, and only then does a description reach the screen (User Story 3, R6).

```
╭─ 412 · Corrige l'arrondi TVA ────────────────────────────────────────╮
│ dépôt        facturation-api        auteur    Claire Meunier         │
│ de           fix/tva-arrondi        vers      main                   │
│ ouvert       il y a 12 jours        fusion    conflits               │
├──────────────────────────────────────────────────────────────────────┤
│ Corrige l'arrondi de la TVA sur les lignes de facture …              │
├──────────────────────────────────────────────────────────────────────┤
│ relecteurs   Hervé Koueke Kouemeni    ○ pas de vote   requis         │
│              Équipe Facturation       ○ pas de vote   groupe         │
│              Claire Meunier           ✔ approuvé                     │
╰──────────────────────────────────────────────────────────────────────╯

Cet outil est en lecture seule. Relisez dans Azure DevOps :
Repos > Pull requests > facturation-api !412
```

The closing line is required (FR-017). The tool states where the review happens and offers **no** means of
voting, commenting, or completing — the temptation this feature is most exposed to, refused deliberately
rather than by omission.

---

## Recovery

On failure, the shared recovery prompt offers the four constitutional choices — retry the failed step,
continue from it, restart, abort. A run interrupted mid-sweep is resumable on the next launch: the selected
project scope survives, and the pull requests are re-read, never restored from a checkpoint.

Aborting prints what was established:

```
╭─ L'exécution s'est arrêtée avant terme ──────────────────╮
│ issue      annulée                                       │
│ lus        14 projets sur 23                             │
│ non lus    Paie · Archives · Portail · …                  │
│ modifié    rien — cet outil ne fait jamais que lire      │
╰──────────────────────────────────────────────────────────╯
```
