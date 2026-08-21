# DevToolbox

Une boîte à outils console pour le travail quotidien avec Azure DevOps Server. Elle embarque aujourd'hui un
seul outil.

## varcompare

Compare côte à côte les variables de deux groupes de variables Azure DevOps ou plus, afin que les écarts de
configuration entre environnements sautent aux yeux au lieu d'exiger d'ouvrir chaque groupe dans le
navigateur et de comparer à la main.

**L'outil est strictement en lecture seule.** Il ne peut rien créer, modifier, renommer ni supprimer. Non par
convention, mais par construction : aucun verbe HTTP modifiant l'état n'existe dans le code, et la
compilation échoue si l'on en introduit un. Là où une différence apparaît, l'outil indique où aller la
corriger soi-même.

## Prérequis

| Besoin | Vérification |
|--------|--------------|
| SDK .NET 10 | `dotnet --version` → `10.*` |
| Windows, poste joint au domaine | l'outil s'authentifie avec votre session Windows |
| Réseau d'entreprise ou VPN | l'hôte du serveur se résout et répond |
| Accès en lecture à la collection | vous pouvez ouvrir la page Library dans un navigateur |

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
présente dans `appsettings.json`. Votre réponse est enregistrée et réutilisée : vous n'avez pas à modifier le
fichier livré.

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
tools/VarCompare/              l'outil : Core / Infrastructure / Presentation
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

`master` est protégée par un ruleset : poussée directe refusée, demande de tirage obligatoire, job `ci`
vert exigé, et ni suppression ni réécriture d'historique. Les fichiers correspondants sont dans
[`.github/rulesets/`](.github/rulesets/) avec la marche à suivre pour les appliquer — une protection de
branche est un réglage de serveur, elle ne s'active pas toute seule en arrivant dans le dépôt.

## Publier une version

L'intégration continue compile, teste et vérifie la publication mono-fichier à chaque poussée sur `master`
ou `develop`, et à chaque demande de tirage vers l'une des deux.

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

## Contribuer

Les règles du dépôt ne sont pas décoratives : les avertissements sont des erreurs, quatre analyseurs sont
actifs, et une liste d'API interdites transforme plusieurs règles en échecs de compilation plutôt qu'en
remarques de relecture.
