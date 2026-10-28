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
  copies optimisées et les documents engendrés sont écrits depuis l'arbre de syntaxe, qui est fidèle au sens *et* à
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

## Un document projeté doit être valide, et pas seulement relisible

La projection produit un document OpenAPI que rien ne réémet tant qu'il n'entre pas en production. Tant
qu'on se contente de le relire, ses défauts restent invisibles : un `$comment` posé par la règle du choix
se relisait parfaitement, et la revalidation de la production consolidée le refusait — OpenAPI 3.0 ne
connaît pas ce mot-clé. Le défaut n'était visible qu'au bout de la chaîne, là où le document est *inséré
puis revalidé*.

C'est pourquoi l'épreuve de bout en bout vaut d'exister en plus des épreuves d'unité : elle ne vérifie pas
ce qu'elles vérifient déjà, elle prend les défauts qui n'existent qu'au bout.

## Le fichier que l'outil écrit ne porte que de l'OpenAPI

Un fichier consolidé portait trois sortes de notes de l'outil : un bloc `x-apidelta-correction` à la
racine — fichier source, empreinte, statut, nombre d'endroits non remplis, champs dérivés — une trace
`x-apidelta-origin` sur chaque élément dérivé, et un `x-apidelta-unfilled` partout où une projection
n'avait rien porté.

Elles sont retirées. Ce fichier n'est pas un rapport : il est destiné à **remplacer** le contrat en
service, dans un dépôt, après relecture. Les notes y survivaient à la publication, et la plus trompeuse
annonçait `statut: correction proposée, non publiée` dans un document sur le point de l'être.

Ce qu'elles disaient se lit dans le rapport posé à côté, qui en devient le seul porteur — d'où les
épreuves qui comparent son compte d'endroits non remplis à celui du plan, et qui vérifient que chaque
élément dérivé s'y retrouve. **Le registre que la construction rend reste tenu** : c'est lui qui alimente
le rapport, et c'est lui que le vérificateur consulte pour savoir quels emplacements avaient le droit de
changer.

Corollaire, qui est la raison de tenir cette règle ici : le retrait du bloc de racine a découvert que la
vérification ne regardait que dans un sens. Elle parcourait l'arbre de la production pour établir que
chaque valeur s'y retrouvait inchangée — donc elle voyait ce qui disparaît et ce qui change, **jamais ce
qui apparaît**. Le seul ajout de l'outil étant son propre bloc, nommé dans la liste des emplacements
permis, le trou ne pouvait pas se voir. Un membre ajouté à la racine d'une production consolidée passait
la vérification sans un mot.

## Une trace d'origine ne dit jamais « non portée »

Un élément *écrit* a forcément été porté, exactement ou à peu près. Ce qui n'a pas été porté n'est pas un
élément : c'est une absence, et une absence se dit comme un endroit non rempli, jamais comme une origine.
Annoncer « non portée » sur un élément présent dirait au lecteur le contraire de ce qu'il a sous les yeux.

La règle a survécu au déplacement des traces hors du document : `DerivedOrigin.Confidence` ne vaut jamais
`NotCarried` pour un élément inscrit au registre, et c'est là que l'épreuve la tient désormais. Sans
cela, un élément dérivé d'une traduction serait indiscernable d'un élément recopié des octets d'un
candidat — et c'est dans le rapport, maintenant, que cette distinction se lit.

## Ce qui sort de la projection est trié, et ce tri n'est pas cosmétique

Les attributs d'un type compilé arrivent dans l'ordre d'une table de hachage, qui n'est l'ordre de
personne. Les opérations sortent dans l'ordre où le contrat les déclare, qui est une façon d'écrire. Les
schémas d'un jeu compilé sortent dans l'ordre où les documents y ont été versés, donc dans l'ordre du
découpage en fichiers.

Trois ordres qu'une seconde livraison du même service peut changer sans rien changer au contrat. Non
triés, ils feraient apparaître des différences à chaque relance de l'intégration continue, et l'outil
annoncerait des changements que personne n'a faits — ce qu'un brouillon ne peut se permettre qu'une fois.

## Deux schémas dans un même `wsdl:types` ne partagent pas une URI

`XmlSchemaSet` reconnaît un doublon à l'URI d'où le schéma vient. Deux `xs:schema` écrits dans le même
document en partagent une : le second est écarté **sans exception, sans diagnostic et sans que `Add`
échoue**. Un contrat dont les schémas sont écrits dans le WSDL perdait ainsi la moitié de ses types tout
en se déclarant lu fidèlement.

Un fragment d'URI ne les distingue pas — il est retiré. `SchemaCompiler` analyse donc chaque schéma en
ligne avec `XmlSchema.Read` avant de l'ajouter, ce qui donne deux instances distinctes. Au passage :
`Add(espace, lecteur)` applique l'espace de noms donné à *tout* ce que l'appel charge, imports compris, et
refuse alors l'import d'un autre espace de noms. Les deux chemins passent `null`.

## Un budget déclaré doit mordre, et un essai doit le prouver

Six budgets de ce dépôt ont été déclarés, documentés et réglables sans être appliqués : la profondeur
d'imbrication et les valeurs d'énumération d'un XSD, le délai de récupération, le confinement vérifié par
préfixe de chaîne, `wsdl:import` ni suivi ni refusé, et `limits.maxReferences`. Tous passaient la
génération, et quatre passaient aussi une revue.

Un budget non appliqué est pire qu'un budget absent : une équipe qui écrit `maxReferences: 500` obtient la
preuve d'une protection qu'elle n'a pas. L'absence se voit ; la protection imaginaire se croit.

Deux obligations, donc, pour tout seuil nouveau :

- **Il est appliqué à l'endroit où le travail se fait**, et non vérifié après coup. Un budget contrôlé à
  la fin a déjà laissé faire tout ce qu'il existe pour borner.
- **Un essai le franchit et constate le refus.** L'essai doit avoir été vu rougir une fois, garde-fou
  retiré. Sans cela il n'atteste rien : celui de `maxReferences` plaçait son assertion sous une condition
  que rien ne rendait jamais vraie, et il est resté vert pendant toute la vie du défaut.

Corollaire sur la forme des essais : **une assertion sous condition n'affirme rien** quand la condition
peut ne jamais se produire. `if (quelque chose) { Should()… }` sans `else` est vert le jour où « quelque
chose » cesse d'arriver — c'est-à-dire le jour où la régression survient. Si les deux cas sont légitimes,
les deux branches affirment ; si l'un ne doit pas se produire, c'est lui qu'il faut affirmer.
