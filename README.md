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

## Contribuer

Les règles du dépôt ne sont pas décoratives : les avertissements sont des erreurs, quatre analyseurs sont
actifs, et une liste d'API interdites transforme plusieurs règles en échecs de compilation plutôt qu'en
remarques de relecture.
