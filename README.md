# DevToolbox

Une boîte à outils console pour le travail quotidien d'une équipe. Elle embarque aujourd'hui deux outils :
l'un lit Azure DevOps, l'autre lit deux fichiers sur votre disque et n'émet aucune requête.

## varcompare

Compare côte à côte les variables de deux groupes de variables Azure DevOps ou plus, afin que les écarts de
configuration entre environnements sautent aux yeux au lieu d'exiger d'ouvrir chaque groupe dans le
navigateur et de comparer à la main.

**L'outil est strictement en lecture seule.** Il ne peut rien créer, modifier, renommer ni supprimer. Non par
convention, mais par construction : aucun verbe HTTP modifiant l'état n'existe dans le code, et la
compilation échoue si l'on en introduit un. Là où une différence apparaît, l'outil indique où aller la
corriger soi-même.

## apidelta

Deux choses, et deux seulement :

- **Consolider ma production avec un autre fichier.** Vous donnez votre fichier de production et un
  fichier compagnon — une description OpenAPI (Swagger) ou un contrat de service WSDL. L'outil dit ce qui
  casse pour les applications qui appellent, vous laisse choisir ce que vous reprenez, et **vous rend
  votre fichier de production avec ces éléments déjà dedans**, écrit à côté.
- **Analyser un fichier en vue d'un nettoyage.** Vous donnez un fichier. L'outil l'examine — erreurs,
  oublis, répétitions — et chiffre ce qu'un nettoyage lui gagnerait. Il n'applique rien de lui-même : il
  chiffre d'abord, et vous décidez ensuite.

Ce qu'il promet, et ce qu'il refuse :

- **Il ne modifie jamais le fichier de production**, sauf demande explicite *et* confirmation nommant le
  fichier — la seule action de la boîte qui ne se défait pas en supprimant un rapport. La production
  corrigée, elle, est écrite à côté : aucun drapeau ne la fait écrire par-dessus l'original.
- **Hors des endroits que vous avez choisis, la production consolidée est la production, octet pour octet.**
  Commentaires, indentation, ordre des clés y survivent, parce que l'outil recopie les octets d'origine au
  lieu de réécrire le fichier. Le diff que vous relisez est la sélection que vous avez faite, et rien
  d'autre n'est arrivé à côté.
- **Il dit ce qu'il va insérer avant de l'insérer**, élément par élément, avec l'effet et la confiance que
  la comparaison a établis. Un changement cassant est annoncé comme tel : vous pouvez le vouloir, pas
  l'emporter sans le savoir.
- **Il prouve son propre travail avant de publier.** Une production consolidée n'existe sur le disque que si
  l'outil l'a relue, revalidée, a résolu chaque référence, et a établi que ce que vous n'avez pas choisi
  est identique à l'octet et que ce que vous avez choisi ne diffère que comme demandé. Faute de quoi rien
  n'est écrit, et le rapport dit pourquoi.

### Parcours guidé d’apidelta

Dans **OpenAPI → apidelta**, choisissez votre objectif :

- **Comparer deux fichiers et choisir les changements à reprendre** : indiquez le fichier OpenAPI de référence, puis le nouveau fichier OpenAPI ou WSDL. Vérifiez les correspondances proposées si le nouveau fichier est un WSDL, puis choisissez les changements à inclure. La confirmation crée un fichier fusionné distinct de l’original.
- **Vérifier un fichier et préparer son nettoyage** : commencez par le rapport seul, ou demandez une copie nettoyée. Le remplacement de l’original reste une option explicite, avec sauvegarde proposée et confirmation finale nommant le fichier.

Les réglages personnalisés sont facultatifs. Si un fichier de configuration est trouvé à côté de votre fichier, l’outil le nomme et vous demande s’il faut l’utiliser. Les choix de placement des champs donnent des exemples d’adresses et de paramètres HTTP. Les bilans indiquent les fichiers réellement écrits ; **Continuer** passe au bilan suivant, puis **Entrée** ramène au menu à la fin. Les rapports conservent les identifiants techniques nécessaires à une analyse détaillée.

### Le fichier compagnon peut aussi être un WSDL

`apidelta` accepte un contrat de service WSDL 1.1 comme fichier compagnon, en face d'une production OpenAPI.
Il suit les imports — schémas voisins compris — dans un périmètre qu'il annonce, puis **projette** le
contrat dans la forme qu'il sait déjà comparer. Le document d'entrée peut être un chemin ou une adresse ;
dans le second cas l'outil écrit d'abord sur le disque tout ce qu'il a obtenu, et ne lit que ces fichiers.

Trois choses à savoir avant de s'en servir :

- **Rien n'est comparé avant que vous ayez validé l'appariement.** Aucune convention ne fait le pont entre
  une opération WSDL et une route REST : l'outil propose une paire par opération, dit sur quoi il s'est
  fondé, et attend. Renoncer est une réponse — la séance réussit, n'ayant rien comparé. L'appariement
  validé est mémorisé : une séance reprise ne vous le redemande pas.
- **Ce que la traduction ne porte pas est recensé, jamais deviné.** Un attribut XSD, un contenu mixte, un
  groupe de substitution n'ont pas de contrepartie en OpenAPI. Le rapport nomme chacun de ces endroits —
  quoi, où, pourquoi — en donne le nombre, et dit de quel document du contrat, de quelle ligne et avec
  quelle fidélité chaque élément dérivé provient. Le fichier produit, lui, ne porte que de l'OpenAPI : il
  est destiné à remplacer votre contrat, pas à garder les notes de l'outil. Un schéma plausible serait la
  seule sortie dont vous ne pourriez pas récupérer le fait que personne ne sait.
- **Le rapport chiffre ce qui était comparable.** Couverture du contrat par la production, part portée
  exactement, et ce qui n'a pas de contrepartie groupé par motif. « La projection s'est bien passée » ne
  veut rien dire ; « 62 % des constructions portées » se recoupe et se discute.

La lecture, les budgets et la table de verbes qui dérive les itinéraires se règlent sous la section
`wsdl` du jeu de règles. Une clé inconnue y est refusée en nommant son fichier, sa ligne et sa colonne :
l'outil ne retombe jamais en silence sur ses propres valeurs.

Onglet **OpenAPI** → **apidelta**. Aucun serveur, aucun identifiant, aucun réseau — sauf si vous donnez
vous-même l'adresse d'un contrat : les appels sortants concernent alors ce contrat et ses imports autorisés.

## Prérequis

| Besoin | Pour quoi | Vérification |
|--------|-----------|--------------|
| SDK .NET 10 | toute la boîte | `dotnet --version` → `10.*` |
| Windows, poste joint au domaine | les outils Azure DevOps | l'outil s'authentifie avec votre session Windows |
| Réseau d'entreprise ou VPN | les outils Azure DevOps | l'hôte du serveur se résout et répond |
| Accès en lecture à la collection | les outils Azure DevOps | vous pouvez ouvrir la page Library dans un navigateur |

Les trois dernières lignes sont les prérequis de **varcompare**, non ceux de la boîte. `apidelta` lit un ou deux
fichiers sur votre disque : il fonctionne sur un poste qui n'a jamais vu de serveur Azure DevOps, et son
onglet est atteignable sans qu'aucune adresse de serveur ait été saisie.

**Aucun mot de passe, jeton ni nom d'utilisateur ne vous sera jamais demandé.** L'authentification est
l'authentification Windows intégrée, avec les identifiants du processus en cours. Il n'existe aucune invite
de saisie d'identifiants dans l'outil, ni aucun type capable d'en porter un. Si le serveur rejette votre
session, l'outil le dit et s'arrête.

## Compiler et exécuter

```bash
dotnet restore
dotnet build            # doit être propre : les avertissements sont des erreurs
dotnet test
dotnet run --project src/DevToolbox.Console
```

Au premier démarrage, l'outil demande quel serveur Azure DevOps lire, en proposant par défaut la valeur
présente dans `appsettings.json`. Le nom court suffit — `azure`, ou `azure/` : le préfixe `https://` est
ajouté pour vous, et un schéma explicite est repris tel quel. Votre réponse est enregistrée et réutilisée :
vous n'avez pas à modifier le fichier livré.

Ensuite : onglet **Azure DevOps** → **varcompare**.

## Où sont rangées les données

| Quoi | Où |
|------|-----|
| Réglages, dont votre adresse de serveur | `%APPDATA%\DevToolbox\settings.json` |
| Exécutions interrompues, péremption à 7 jours | `%LOCALAPPDATA%\DevToolbox\runs\` |
| Journaux, conservation 7 jours | `%LOCALAPPDATA%\DevToolbox\logs\` |

Aucune valeur de variable n'est jamais écrite dans l'un d'eux. Le fichier d'exécution ne retient que votre
collection, votre projet, les groupes choisis et l'avancement des étapes : assez pour reprendre une
sélection, jamais assez pour reconstituer une comparaison. Reprendre relit les groupes, si bien que vous
n'agissez jamais sur une image périmée.

## Reprendre ou recommencer une session

Ces choix concernent les sélections enregistrées dans DevToolbox, pas vos fichiers, vos rapports ni les données du serveur.

| À l’ouverture de l’outil | Effet |
|---|---|
| **Reprendre les choix enregistrés** | Réutilise les choix affichés et relit les données. apidelta vérifie aussi que les fichiers n’ont pas changé. |
| **Nouvelle session — oublier les anciens choix** | Supprime immédiatement la session enregistrée et repose les questions de sélection. |
| **Nouvelle session — garder les anciens choix provisoirement** | Repose les questions ; l’ancienne session reste disponible si vous annulez avant tout nouvel enregistrement. Elle est remplacée dès le premier enregistrement automatique des nouveaux choix. |

Une session entièrement réussie est retirée de la liste de reprise. Une reprise apidelta devenue invalide est oubliée avec une explication, puis les fichiers sont redemandés. Un seul enregistrement est gardé par outil ; une nouvelle sélection ne réinitialise pas les réglages du serveur.

| Après une erreur | Effet |
|---|---|
| **Réessayer cette étape** | Rejoue seulement l’étape en erreur, avec les choix et les étapes déjà terminées. |
| **Passer cette étape** | Ne la rejoue pas ; poursuit avec ce qui est disponible. Le bilan signale les résultats potentiellement incomplets et la session reste reprenable. |
| **Tout recommencer** | Efface le point de reprise concerné et l’état de travail en mémoire, puis revient à la première question. Les fichiers et rapports déjà écrits ne sont pas effacés ni annulés. |
| **Arrêter ici** | Arrête le traitement et conserve les derniers choix enregistrés, s’il y en a, pour une prochaine ouverture. |

Les enregistrements sont remplacés par écriture atomique : une mise à jour annulée conserve l’enregistrement précédent. Si l’outil ne parvient pas à oublier une session, il l’annonce ; une remise à zéro ne se poursuit pas comme si l’oubli avait réussi.

## Lire la comparaison

Chaque cellule affiche un seul état. La couleur ne fait que renforcer : le symbole et le mot portent le sens,
de sorte que l'affichage reste lisible sans couleur.

| Symbole | Signification |
|---------|---------------|
| `✔ set` | présente, avec une valeur |
| `○ empty` | présente, valeur vide |
| `🔒 secret` | présente et secrète : Azure DevOps ne retourne pas la valeur |
| `✘ missing` | absente de ce groupe |
| `? unknown` | état indéterminable |

Qualificatifs : `[kv]` provient d'un coffre de clés, `[ro]` en lecture seule dans ce groupe. Une ligne
marquée `≠` est présente partout mais ses valeurs lisibles diffèrent. Le signe `~` indique que les groupes
écrivent le nom avec une casse différente (Azure DevOps les traite comme une seule variable).

**Les valeurs ne sont jamais affichées dans la comparaison.** Ouvrez la vue de détail d'une variable pour les
voir, étant entendu que les secrets et les entrées adossées à un coffre restent illisibles même là, puisque
la plateforme ne les retourne pas.

Depuis la comparaison, vous pouvez parcourir page à page un résultat long, filtrer sur les seules
différences, basculer entre la disposition côte à côte et la disposition empilée, ouvrir une variable, ou
rafraîchir pour relire les groupes.

## Organisation du dépôt

```text
src/
  DevToolbox.Domain/           noyau partagé, aucune référence de paquet
  DevToolbox.Application/      ports et cas d'usage
  DevToolbox.Infrastructure/   client Azure DevOps, stockage, journalisation
  DevToolbox.Presentation/     coquille Spectre.Console
  DevToolbox.Console/          racine de composition
tools/VarCompare/              un outil : Core / Infrastructure / Presentation
tools/ApiDelta/                l'autre, même découpage, aucune requête réseau
tests/DevToolbox.Tests/        un seul projet de tests, un dossier par domaine
```

Les dépendances ne pointent que vers l'intérieur, et un test d'architecture fait échouer la compilation dès
que ce n'est plus vrai.

## Branches

| Branche | Rôle |
|---------|------|
| `develop` | Là où le développement se fait. C'est la branche par défaut du travail quotidien. |
| `master` | L'état publié. On n'y pousse jamais directement : elle ne reçoit que des demandes de tirage. |

Une fonctionnalité part de `develop` et y revient :

```bash
git switch develop
git switch -c feature/mon-sujet
# … travail, commits …
git push -u origin feature/mon-sujet   # puis demande de tirage vers develop
```

`master` est protégée par un ruleset : poussée directe refusée, demande de tirage obligatoire, job
`build` de la chaîne `ci-cd` vert exigé, et ni suppression ni réécriture d'historique. Les fichiers
correspondants sont dans [`.github/rulesets/`](.github/rulesets/) avec la marche à suivre pour les
appliquer — une protection de branche est un réglage de serveur, elle ne s'active pas toute seule en
arrivant dans le dépôt.

## Publier une version

Intégration et livraison tiennent dans un seul workflow, [`ci-cd`](.github/workflows/ci-cd.yml), pour que
tout se lise sur une seule exécution : le job `build` compile, teste et vérifie la publication
mono-fichier à chaque poussée sur `master` ou `develop` et à chaque demande de tirage vers l'une des deux ;
le job `release` ne s'ajoute que lorsque le déclencheur est une balise.

Une version se coupe en reportant `develop` sur `master`, puis en balisant :

```bash
# Demande de tirage develop → master, relue et fusionnée sur GitHub, puis :
git switch master
git pull
git tag -a v1.0.0 -m "v1.0.0"
git push origin v1.0.0
```

La chaîne de publication refuse une balise qui ne serait pas contenue dans `master` : baliser une branche
de travail échoue avant qu'aucun binaire ne soit produit.

La chaîne recompile, rejoue les tests, puis publie un exécutable **Windows x64 autonome et mono-fichier**,
empaqueté avec son `appsettings.json` dans `DevToolbox-<version>-win-x64.zip`, accompagné de son empreinte
SHA-256, et attaché à une release GitHub. Le runtime .NET n'a pas à être installé sur le poste cible. Une
balise portant un suffixe (`v1.0.0-rc.1`) produit une préversion.

Poser la balise depuis l'interface GitHub — **Releases → Draft a new release** — fonctionne aussi, mais la
release existe alors avant que la chaîne ne tourne. Le job `release` le reconnaît : il téléverse les
fichiers sur la release déjà là plutôt que d'échouer, et ne remplace sa description que si elle est vide.

## Contribuer

Les règles du dépôt ne sont pas décoratives : les avertissements sont des erreurs, quatre analyseurs sont
actifs, et une liste d'API interdites transforme plusieurs règles en échecs de compilation plutôt qu'en
remarques de relecture.

Celles que le compilateur ne peut pas tenir sont écrites dans **[CONCEPTION.md](CONCEPTION.md)** : dix
règles, chacune avec sa raison, sur les points de reprise, l'arbre de syntaxe et l'écriture d'un document.
Lisez-les avant d'y toucher — ce sont les endroits où rien ne vous arrêtera, et où une erreur produit un
fichier valide qui décrit autre chose.


## Export WSDL vers OpenAPI (expérimental)

Onglet **OpenAPI** → **WSDL vers OpenAPI**. Indiquez un WSDL local, une URL HTTPS ou le `manifest.json` d'une collecte sauvegardée, puis un **nouveau** fichier YAML, le titre et la version du service. Le fichier est publié seulement après validation, sans écraser une sortie existante. Les endpoints SOAP ne sont jamais appelés.

Depuis un fichier local, la conversion reste hors ligne. Depuis une URL, choisissez aussi un **nouveau dossier de collecte**. L'hôte de l'URL est autorisé pour cette collecte ; les autres hôtes d'import doivent être nommés explicitement. Les adresses IP internes restent interdites tant qu'elles n'ont pas été saisies dans le champ prévu. HTTP sans chiffrement demande une confirmation ; refuser annule la collecte.

La collecte suit les imports WSDL et XSD, y compris les chemins relatifs après redirection et les fichiers de même nom. Elle conserve les sources originales dans `sources/`, une copie autonome dans `offline/`, le YAML validé `openapi.yaml`, un `manifest.json` avec les empreintes SHA-256 et un `compatibility.json` décrivant les validations effectuées. Le manifeste et le rapport ne contiennent pas les URL d'origine. Les WSDL/XSD sources et copies hors ligne peuvent contenir des adresses privées : conservez ce dossier comme vos fichiers d'entrée. Il n'est rendu visible sous son nom final qu'après réussite ; un échec nettoie la collecte temporaire.

Pour refaire l'export **sans réseau**, donnez le `manifest.json` comme source. Les empreintes des sources, copies hors ligne et YAML conservé sont vérifiées. Avec le même titre et la même version, le YAML reproduit doit avoir la même empreinte. Changer ces deux métadonnées produit un nouvel export sans modifier la collecte initiale. Le `compatibility.json` concerne toujours le `openapi.yaml` conservé dans cette collecte.

Ce parcours livre un export SOAP. Les objectifs de comparaison, choix de modifications, édition d'une production et reprise d'une séance restent accessibles dans **apidelta**, avec leur propre parcours. Les scénarios correspondants du quickstart décrivent ces autres objectifs.

Le parcours suit les `wsdl:import` et `xs:import/include` locaux sous le répertoire du WSDL sélectionné. Les préfixes sont résolus dans leur portée XML : service/port → binding → portType → opération → messages → éléments/types XSD. Une référence qualifiée ne se rabat jamais sur un nom local d'un autre namespace. Les déclarations dupliquées, références manquantes et opérations surchargées ambiguës sont refusées. Deux bindings homonymes de namespaces distincts restent distincts ; plusieurs bindings SOAP 1.1/1.2 peuvent exposer le même portType.

Chaque véritable chemin SOAP donne un seul `POST`, sans URL REST inventée. L'autorité de l'adresse n'est pas exportée ; le consommateur devra renseigner son serveur. Identifiants, query string, fragments, schémas non HTTP(S), chemins ambigus et caractères de contrôle sont refusés sans afficher l'URL. En mode fichier local, les imports distants restent refusés. Les traversées hors racine, liens symboliques et jonctions sont refusés ; DTD et entités externes restent interdits. Les budgets de taille, profondeur et nombre de documents s'appliquent à la lecture ; la projection est bornée à 64 types imbriqués et accepte les références récursives nommées.

### XML document/literal pris en charge

Les messages `document/literal` sont projetés à partir de leurs parties référençant des **éléments globaux**, avec le nom XML et le namespace de chaque élément. Les types nommés et anonymes, objets `sequence`/`all` simples, éléments facultatifs, répétitions non enveloppées, types récursifs et restrictions de chaînes (énumération/longueur) sont représentés dans `components.schemas`. Les primitives prises en charge sont `string`, `boolean`, `int`, `long`, `decimal` et `base64Binary`. Les préfixes XML sont normalisés ; leur orthographe d'origine ne fait pas partie de l'identité du contrat.

Les enveloppes et types de média suivent SOAP 1.1 (`text/xml`) ou SOAP 1.2 (`application/soap+xml`). Plusieurs opérations sur un endpoint sont décrites par des variantes `anyOf`, avec leurs actions dans la description standard du POST, **sans extension `x-soap-*`**. Une SOAPAction vide reste distinguée d'une action absente. Les réponses avec sortie utilisent 200 ; les échanges one-way gardent un 202 sans corps. Ces statuts sont des conventions d'export, pas des garanties du WSDL. Les faults déclarés sont décrits sous `default`, avec leur détail métier et leur enveloppe SOAP ; les champs de protocole du Fault restent ouverts et définis par SOAP, sans inventer de statut ou de contrainte métier.

Les SOAPAction sont vérifiées **avant** leur copie dans la description : une action absente reste absente, une action vide reste vide ; les URN et références relatives sans autorité restent inchangées. Toute action portant une autorité (y compris une URL HTTP(S)), une query string, un fragment, un encodage `%`, un caractère de contrôle ou une autre forme non exportable est refusée par `SOAP_ACTION_UNSAFE`, sans afficher sa valeur. Retirer une autorité ou des paramètres changerait l'action de dispatch : le générateur refuse plutôt que de la réécrire.

### Refus explicites et limites

L'export refuse l'ensemble du document lorsqu'une construction **atteinte depuis un port exposé** ne peut pas être portée. La console affiche le motif et un code de diagnostic, notamment :

| Construction | Diagnostic |
|---|---|
| Style RPC / corps encoded ou literal manquant | `SOAP_RPC_UNSUPPORTED` / `SOAP_ENCODED_OR_MISSING_LITERAL_BODY_UNSUPPORTED` |
| Facettes de `base64Binary` (longueur en octets, énumération binaire) | `XSD_BINARY_FACET_UNSUPPORTED` |
| Attributs XML, contenu mixte | `XSD_ATTRIBUTES_UNSUPPORTED` / `XSD_MIXED_CONTENT_UNSUPPORTED` |
| Groupes de substitution, éléments abstraits | `XSD_SUBSTITUTION_GROUP_OR_ABSTRACT_ELEMENT_UNSUPPORTED` |
| Choix, wildcard, cardinalité d'un groupe | `XSD_CHOICE_WILDCARD_OR_GROUP_CARDINALITY_UNSUPPORTED` |
| Nillable, contraintes d'identité, default/fixed | `XSD_NILLABLE_UNSUPPORTED` / `XSD_IDENTITY_DEFAULT_OR_FIXED_UNSUPPORTED` |
| Headers SOAP ou extensions de flux non représentées | `SOAP_HEADERS_OR_NON_BODY_EXTENSIONS_UNSUPPORTED` |
| Notification et solicit-response initiées par le service | `WSDL_NOTIFICATION_OR_SOLICIT_RESPONSE_UNSUPPORTED` |
| Import invalide, manquant ou hors du périmètre autorisé | `WSDL_XSD_GRAPH_UNRESOLVED_OR_INVALID` |
| Base URI XML explicite | `XML_BASE_UNSUPPORTED` ; refusée également pendant la collecte |
| Hôte/IP non autorisés ou redirection refusée | `COLLECTION_PERMISSIONDENIED` / `COLLECTION_REDIRECTREFUSED` |
| Collecte modifiée ou manifeste non confiné | `SNAPSHOT_CHANGED` / `SNAPSHOT_INVALID` |

Les listes/unions XSD, facettes non représentées (dont les regex XSD), primitives non prises en charge et parties document/literal désignant directement un type sont également refusées. Aucun schéma approximatif ne les remplace. Pour une particule locale `xs:element ref`, le nom, le type et les contraintes sont lus sur la déclaration globale résolue ; les bornes `minOccurs`/`maxOccurs` restent celles de la particule locale. Les contraintes `fixed`, `default` et d'identité de l'élément référencé sont donc refusées elles aussi. Les facettes de longueur ne sont traduites que pour un véritable type chaîne XSD : une longueur binaire en octets n'est jamais présentée comme une longueur de texte Base64. Un `base64Binary` sans facette reste pris en charge.

OpenAPI décrit la structure XML et les cardinalités ; il ne remplace pas la validation XSD, notamment pour l'ordre des éléments et les formes lexicales XML. Le YAML généré porte cette limite dans sa description. Il est relu avec YamlDotNet puis soumis à **Microsoft.OpenApi**, résolution des références et règles de validation comprises, avant toute écriture définitive.

### Collecte réseau : périmètre et budgets

HTTPS est utilisé par défaut, sans identifiant ni cookie. Les requêtes d'accès au contrat et chaque redirection passent par le contrôle des hôtes et des IP, avec épinglage de l'adresse de connexion pour empêcher une seconde résolution DNS vers une adresse privée. Les URL avec identifiants, fragments ou paramètres arbitraires sont refusées avant requête. Les formes de métadonnées `?wsdl`, `?singleWsdl`, `?wsdl=wsdl0` et `?xsd=xsd0` (suffixe numérique) sont acceptées. Un contrat nécessitant d'autres paramètres ou une authentification doit être téléchargé séparément, puis utilisé comme fichier local.

L'export interactif utilise les plafonds suivants : 16 Mio par document/réponse, 64 Mio cumulés, 256 documents, 64 niveaux d'imports, 256 niveaux XML, 5 redirections par récupération et 45 secondes pour la collecte réseau complète. Les API permettent de réduire les budgets ; les plafonds de rejeu hors ligne restent applicables. La découverte des imports borne aussi sa file de travail. Aucun contournement de certificat TLS n'est installé. Les proxys qui empêchent de garantir l'épinglage de l'adresse sont refusés ; utilisez alors une collecte locale obtenue séparément.

### Constat de validation pour Sparky API Manager

Les sorties utilisent OpenAPI 3.0.3 standard. Le [scénario d'acceptation reproductible](specs/004-wsdl-delta-audit/acceptance.md) fournit un YAML de référence, son empreinte et les commandes de vérification. Il couvre les imports, SOAP 1.1/1.2, un endpoint partagé, une opération à sens unique et des faults. Chaque collecte produit aussi un constat `compatibility.json` lié à l'empreinte du YAML effectivement validé par Microsoft.OpenApi.

**Aucun import réel dans Sparky n'est attesté** : aucune cible ni session Sparky n'est configurée ici. Le constat démontre la validité du document OpenAPI, pas son importabilité dans une version déterminée de Sparky. L'instance cible doit encore recevoir le YAML, définir l'autorité du service et vérifier le rendu des variantes XML `anyOf` et des faults. Les refus du tableau ci-dessus restent applicables : l'outil n'est pas un convertisseur universel de WSDL. La PR reste en draft pour revue de ce périmètre.
