# Contract — the console experience

**Feature**: [../spec.md](../spec.md) · **Plan**: [../plan.md](../plan.md) · **Date**: 2026-10-16

Spectre.Console renders everything (Principle V). `System.Console` is banned solution-wide by
`BannedSymbols.txt` and fails the build at the call site. Every screen offers **Back**, the root offers
**Exit**, `Ctrl+C` cancels cooperatively, and nothing here blocks the UI thread on a synchronous wait.

---

## 1. Where it lives

A new tab, **OpenAPI**, which costs nothing: `ShellLayout.DistinctTabs` derives the tab strip from the
registered tools, so declaring `Tab => "OpenAPI"` is the whole change.

```csharp
public sealed class ApiDeltaTool : ITool
{
    public string Id => "apidelta";
    public string DisplayName => "apidelta";
    public string Tab => "OpenAPI";
    public string Description => "Compare une description OpenAPI candidate à la production, et audite la production.";
    public bool RequiresServer => false;      // the first tool that talks to nothing
}
```

`RequiresServer => false` is what lets the toolbox start on a machine that has never been pointed at an
Azure DevOps server (R11). The header shows the server when one is configured and says nothing about it
otherwise — it must not name a server this tool never contacts.

---

## 2. The selection screen

In order, each step skippable backwards:

1. **Production description** — a path prompt with validation: the file exists, is readable, is within the
   size limit, and its format is detected and shown (`YAML, OpenAPI 3.0.3`). A path that does not resolve
   is re-prompted, never accepted and failed later.
2. **Mode** — a `SelectionPrompt`: *Comparer*, *Comparer et produire un correctif*, *Auditer*,
   *Optimiser*, *Extraire des opérations choisies*, *Tout*. The chosen mode decides whether step 3 is asked
   at all, and whether the operation picker of §2b appears (FR-701).
3. **Candidate description** — same validation. Asked only for compare, patch and everything.
4. **Output directory** — defaults to the production file's directory; confined, and a directory outside
   it is confirmed (FR-709). Existing artifacts of the same names are listed, and overwriting them is
   confirmed.
5. **Ruleset and suppressions** — optional paths, each defaulting to a conventionally named file beside the
   production description when one exists, and to the shipped defaults otherwise.
6. **Optimization intent** — only when the mode includes optimization: *proposer seulement* or *appliquer
   ce qui est sûr*. Applying is never the default.
7. **Summary and confirmation** — what will be read, what will be written, what will not be touched. The
   sentence that matters is rendered explicitly: *« Le fichier de production n'est pas modifié. »*

Both inputs being the same file is accepted with a warning, because auditing a file against itself is a
reasonable way to see the audit and produces an empty comparison, not an error.

---

## 2b. Choosing operations, in the extract mode

Shown after the production description is read and validated, because the list comes from the document
(FR-902). A four-hundred-operation description is not a list to scroll, so narrowing comes first:

```text
Extraire quelles opérations ?   412 opérations dans production.yaml

  Filtrer par étiquette      ▸ Documents (84) · Dossiers (131) · Pièces (97) · … 
  Filtrer par chemin         ▸ saisir une partie du chemin
  Tout parcourir

┌ Documents ───────────────────────────────────────────────────────────────┐
│ [ ] POST   /documents                  Dépose un document               │
│ [x] GET    /documents/{id}             Lit un document                   │
│ [ ] DELETE /documents/{id}             Supprime un document   obsolète   │
│ [x] POST   /ajouterDocument            Ajoute un document                │
└──────────────────────────────────────────────────────────────────────────┘
  2 opérations choisies · Espace pour cocher · Entrée pour valider · Retour
```

- A `MultiSelectionPrompt` grouped by tag, paged, with the filter applied first. Selection is **per
  operation**, so one method of a path can be taken without its siblings (FR-901).
- Each row shows path, method, summary and whether it is deprecated — enough to choose without opening the
  file. Confirm whether `MultiSelectionPrompt` in Spectre.Console 0.57.2 offers built-in search; the filter
  step above is required regardless, and makes the answer a convenience rather than a dependency.
- Choosing nothing and confirming is refused with an explanation rather than producing an empty extract
  (FR-701).
- Before building, a confirmation states what will be carried along: *« 2 opérations, 37 composants, les
  paramètres de chemin hérités et l'exigence de sécurité globale. Le fichier source n'est pas modifié. »*

After the build, the result screen replaces the comparison summary with the extract's own: the operations
taken, what each inherited, the closure size, the verification verdict, and the file that was written.
A verification that failed names the operation and what differed, and no file exists (FR-910).

---

## 3. While it runs

Ten named steps (R9), rendered through the shared Spectre `Progress`, each with its own line and its own
outcome. The run below is a compare, so `Extract` never appears:

```text
✓ Sélection                      0,1 s
✓ Lecture de la production       2,3 s   418 Ko · sha256 3f9a…
✓ Lecture du candidat            1,9 s   402 Ko · sha256 7c21…
✓ Validation                     0,8 s   2 avertissements
✓ Comparaison                    1,4 s   37 différences
✓ Version                        0,0 s   2.4.1 → 3.0.0 (majeure)
⠋ Audit                                  AUD-104 · 6 200 / 10 000 schémas
  Plan
  Écriture
```

A skipped step is shown as skipped with its reason, never as a tick (data model, §11). Long-running rules
report progress within the audit step, because an audit that appears frozen for twenty seconds is
indistinguishable from one that has hung.

Cancellation is honoured at every step boundary and inside the audit's tree walk. A cancelled run writes
nothing and says what it did not finish.

---

## 4. The result screen

Summary first (R17), because a terminal cannot usefully hold four thousand findings:

```text
┌ apidelta ─────────────────────────────────────────────────────────────┐
│ Production  production.yaml      418 Ko   OpenAPI 3.0.3              │
│ Candidat    new-api.json         402 Ko   OpenAPI 3.0.3              │
├───────────────────────────────────────────────────────────────────────┤
│ Version recommandée   2.4.1 → 3.0.0   majeure                        │
│   ↳ 3 changements cassants, 12 compatibles, 22 documentaires         │
├───────────────────────────────────────────────────────────────────────┤
│ Audit   4 erreurs · 61 avertissements · 23 informations              │
│   SAFE 48 · REVIEW_REQUIRED 31 · signalé seulement 9                 │
│   1 règle non évaluée : AUD-104 (comparaison tronquée)               │
├───────────────────────────────────────────────────────────────────────┤
│ Réduction estimée     18,0 Ko sur 418 Ko (4,3 %)                     │
│ Issue                 succès · clean · 8,4 s                         │
└───────────────────────────────────────────────────────────────────────┘
```

Then a menu: breaking changes, all differences, findings by rule, redundancy groups, safe proposals,
review-required proposals, suppressed findings, generated artifacts — each a paged view, each with Back.
The artifacts view lists every file with its path and size, so the developer can leave knowing where the
answer is.

**The rule that could not be evaluated is on the summary screen, not buried.** A tool that reports
sixty-one warnings and quietly skipped a rule has told the developer something false about completeness.

---

## 5. Confirmations

Three, and all three are explicit `ConfirmationPrompt`s with the default on the safe answer:

| When | What is asked |
|---|---|
| Writing outside the chosen output directory | the directory, and that it is outside |
| Overwriting an existing artifact | the file names |
| Writing over the production description in place | the file, the backup that will be created, and that this is the one action that is not reversible by deleting an artifact |

The in-place confirmation also refuses when the file changed on disk since it was read (FR-550) — the
answer to a stale prompt is not a licence to overwrite newer content.

---

## 6. Failure and recovery

Through the shared `IRecoveryPrompt`, on any step failure (FR-703):

```text
L'étape « Validation » a échoué : référence non résolue #/components/schemas/Dossier (ligne 2 184).

  Réessayer l'étape
  Continuer à partir de cette étape
  Reprendre l'exécution depuis le début
  Abandonner
```

Abandoning states what exists on disk. Because `Write` is the only step that touches the world, retrying
any other step is free, and retrying `Write` is safe because artifacts are moved into place whole.

An interrupted run is offered on the next launch through the shared `ResumePrompt`, which displays the
labelled selection entries:

```text
Reprendre une exécution interrompue ?
  apidelta · il y a 14 minutes
    Mode        Tout
    Production  C:\work\production.yaml   sha256:3f9a7c21b0d4
    Candidat    C:\work\new-api.json      sha256:7c21f0a4e8b2
    Sortie      C:\work\artifacts
    Étapes      Sélection, Lecture de la production, Lecture du candidat
```

**Resuming re-reads.** Both files are read again from disk; nothing is restored from the checkpoint. If
either content hash no longer matches, the resume is refused with the reason, and a fresh run is offered
(FR-705) — a resumed comparison of a file that has since changed would be a confident answer about a
document that no longer exists.

---

## 7. Degradation

Terminal capability is queried through the existing `ConsoleCapabilities`, never assumed:

- **No colour** — severity and safety are carried by words, not only by colour. `SAFE` and
  `REVIEW_REQUIRED` are printed, not implied by green and yellow.
- **No Unicode** — the tick, the spinner and the box characters fall back to ASCII.
- **Not interactive** — the shell already refuses to start and returns its documented
  `ExitCodes.NotInteractive`. apidelta adds nothing here: this release has no non-interactive path
  (FR-708a), and the run's `outcome.code` is displayed and written to the machine-readable report rather
  than becoming a process exit code.
- **Narrow terminal** — the summary panel wraps; tables drop their least important column before they
  truncate a pointer, because a truncated location is useless.

---

## 8. Language

Interface text is French, matching the rest of the toolbox. Rule identifiers, severities and safety classes
are not translated: `AUD-101`, `warning`, `SAFE` and `REVIEW_REQUIRED` appear in the reports, in the
ruleset and on screen with the same spelling, because they are the output contract (FR-419) and a reader
moving between the screen and the JSON must see the same words.
