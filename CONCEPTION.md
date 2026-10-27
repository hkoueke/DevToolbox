# Règles de conception faciles à casser par inadvertance

Ce que le compilateur et les analyseurs ne peuvent pas tenir. Chaque règle est ici parce qu'elle a été
cassée au moins une fois, ou parce qu'elle tombe au premier changement qui paraît anodin — et chacune dit
*pourquoi*, car une règle dont la raison est perdue finit par être contournée de bonne foi.

Les règles rendues mécaniques — symboles bannis, avertissements traités comme des erreurs, un seul type
public par fichier — vivent dans `Directory.Build.props`, `BannedSymbols.txt` et `.editorconfig`. Elles
n'ont pas besoin d'être écrites ici : la génération échoue toute seule.


- **Une `ComparisonCell` ne porte aucune valeur, et ne doit pas se mettre à en porter.** Cette absence est ce
  qui rend structurelle, et non conventionnelle, la promesse qu'aucune valeur n'apparaît dans la comparaison.
  Les valeurs vivent dans les instantanés et n'atteignent l'écran que par la vue de détail.
- **`PersistedRun` ne doit jamais gagner un membre pouvant atteindre une valeur de variable.** Le magasin de
  points de reprise vérifie la forme du type à l'exécution et refuse d'écrire s'il a été élargi.
- **Lorsqu'un groupe n'a pas pu être lu, ses cellules sont `Undetermined`, jamais `Absent`.** Annoncer
  « absente » serait affirmer ce que l'on ne peut pas établir. `ComparisonBuilder.DeriveState` teste l'état
  dégradé *avant* l'absence, et cet ordre compte.
- **Reprendre relit.** Seule la sélection survit à un redémarrage. Ne jamais restaurer le contenu d'un groupe
  depuis un point de reprise.
- **Le temps, les chemins et le système de fichiers passent par des abstractions injectées** (`IClock`,
  `AppPaths`), afin que les tests puissent diriger l'application vers un dossier temporaire.
  `Environment.GetFolderPath` ignore `LOCALAPPDATA`, ce qui explique qu'`AppPaths` prenne des racines
  explicites.
- **Rien de ce que nous publions n'est jamais sérialisé depuis le modèle sémantique.** Les correctifs, les
  copies optimisées et les extraits sont écrits depuis l'arbre de syntaxe, qui est fidèle au sens *et* à
  l'écriture. Microsoft.OpenApi sert à valider, jamais à réémettre : son modèle ne porte pas ce qu'il ne
  comprend pas, si bien qu'un aller-retour par lui ferait disparaître en silence toute extension de
  fournisseur — exactement la donnée que l'outil promet de ne pas toucher.
- **Une classe de sûreté n'est pas une gravité.** La gravité dit combien l'équipe s'en soucie et se
  configure ; la sûreté dit ce que l'outil a prouvé et ne se configure pas. Seule la seconde autorise une
  application automatique. `Finding.AutofixAvailable` est calculé, non renseigné, pour que personne ne
  puisse affirmer l'inverse par distraction.
- **Indéterminé n'est pas absent, et ce n'est pas qu'une règle de varcompare.** Une règle d'audit qui n'a
  pas pu s'exécuter rend `RuleState.NotEvaluated`, `AuditResult.IsComplete` devient faux, et le premier
  écran comme la section 19 du rapport le disent. Un examen sans constat n'est une bonne nouvelle que si
  toutes les règles ont tourné.
- **Une `ByteSpan` se compte en octets, et l'analyseur YAML, lui, compte en caractères.** `ByteOffsets`
  traduit les deux, et retirer cette traduction décale toute étendue située après la première lettre
  accentuée. Le piège est que l'épreuve de va-et-vient de `VerbatimEmitter` ne peut pas le voir : elle
  concatène les étendues dans l'ordre du document, si bien que les intervalles entre elles absorbent
  n'importe quel décalage uniforme. C'est
  `SpecReaderTests.Every_scalar_span_designates_that_scalar_in_the_bytes` qui tient cette garantie, en
  prenant chaque étendue isolément. En dépendent la mesure des tailles — donc les économies annoncées par
  l'optimiseur — et toute réécriture partielle.
- **L'étendue d'une collection de bloc dépasse son contenu.** Le repère de fin qu'un analyseur YAML donne à
  une association de bloc est posé là où le jeton *suivant* commence : l'étendue de `paths` finit après la
  ligne `components:`. `SplicingEmitter.ContentEnd` recule donc sur les blancs pour retrouver le dernier
  caractère écrit. Insérer au repère brut place la route neuve sous `components` — un document valide, qui
  décrit autre chose.

## Le seul appel sortant d'apidelta vit dans un seul espace de noms

`Infrastructure/Http` est le seul endroit d'où l'outil atteint le réseau. Le cœur et la présentation n'ont
aucune dépendance sur `System.Net.Http`, et `LayeringTests` le vérifie assemblage par assemblage.

Ce n'est pas du rangement. Les contrôles qui rendent une récupération acceptable — le modèle d'adresse,
l'adresse résolue, la confirmation d'un service interne, le budget de réponse, le refus d'une réponse qui
n'est pas du XML — sont tous posés sur ce chemin. Un second chemin, même bien intentionné, les
contournerait tous sans qu'aucun essai ne s'en aperçoive.

La règle a été resserrée, et non levée, quand la récupération par adresse est apparue : elle disait
« aucun appel sortant » du temps où apidelta ne lisait que des fichiers.

## Récupérer ne change pas le sens des imports

Un document récupéré est écrit tel quel, octet pour octet, et son `schemaLocation` relatif se résout
contre l'**adresse** d'où ce document vient — jamais contre le dossier où il a été écrit.
`RetrievalMap` traduit ensuite cette adresse en chemin local.

Les deux autres manières de s'en sortir ont été écartées pour une raison qu'il faut garder en tête avant
de les reproposer. Réécrire les `schemaLocation` dans les fichiers écrits rendrait l'empreinte mensongère :
elle ne désignerait plus ce que le serveur a servi, et c'est tout ce qu'une empreinte sert à dire.
Reproduire l'arborescence des adresses sur le disque fonctionnerait, mais ferait dépendre les chemins de la
forme des URL d'un serveur, encodages compris.
