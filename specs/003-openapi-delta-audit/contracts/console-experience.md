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

1. **Mode** — a `SelectionPrompt` of exactly two entries, each naming what the person came to do rather
   than what the tool does inside (FR-700):

   - *Consolider ma production avec un autre fichier* — « vous donnez votre fichier de production et un
     fichier compagnon ; vous recevez un fichier de production consolidé, écrit à côté. »
   - *Analyser un fichier en vue d'un nettoyage* — « vous donnez un fichier ; vous recevez ce qu'il a
     accumulé et ce qu'un nettoyage lui gagnerait. »

   The mode is asked **first**, because it decides what else is asked. Asking for files before knowing what
   for is how the old six-entry menu came to ask for a companion file it would then ignore.
2. **Production file** — a path prompt with validation: the file exists, is readable, is within the size
   limit, and its format is detected and shown (`YAML, OpenAPI 3.0.3`). A path that does not resolve is
   re-prompted, never accepted and failed later.
3. **Companion file** — same validation, and asked only when consolidating (FR-701). It may be an OpenAPI
   description or a WSDL contract; which one it is is detected from its content, never from its extension.
4. **Output directory** — defaults to the production file's directory; confined, and a directory outside
   it is confirmed (FR-709). Existing artifacts of the same names are listed, and overwriting them is
   confirmed.
5. **Ruleset and suppressions** — optional paths, each defaulting to a conventionally named file beside the
   production description when one exists, and to the shipped defaults otherwise.
6. **Cleanup intent** — only when analysing: *chiffrer seulement* or *appliquer ce qui est sûr*. Applying
   is never the default, and the figures come first (FR-701a).
7. **Summary and confirmation** — what will be read, what will be written, what will not be touched. The
   sentence that matters is rendered explicitly: *« Votre fichier de production n'est pas modifié. »*

Both files being the same file is accepted with a warning, because consolidating a file with itself
produces an empty comparison, not an error.

---

## 3. While it runs

Named steps (R9), rendered through the shared Spectre `Progress`, each with its own line and its own
outcome. The run below is an analysis, so the steps that need a companion file are shown as skipped with
their reason — never as a tick, and never hidden:

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
    Ce qui était fait  Consolider une production
    Production         C:\work\production.yaml   sha256:3f9a7c21b0d4
    Compagnon          C:\work\new-api.json      sha256:7c21f0a4e8b2
    Sortie             C:\work\artifacts
    Étapes             Choix des fichiers, Lecture de la production, Lecture du compagnon
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
