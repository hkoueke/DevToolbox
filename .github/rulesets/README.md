# Règles de branches

Les protections de branche vivent sur le serveur, pas dans le dépôt : rien ici ne s'applique tout seul.
Ces deux fichiers sont la forme versionnée de ce qui doit être configuré, pour qu'un réglage modifié à la
main se remarque.

## Appliquer

**Settings → Rules → Rulesets → New ruleset → Import a ruleset**, puis choisir `master.json`, et
recommencer avec `develop.json`.

En ligne de commande, si `gh` est installé et authentifié :

```bash
gh api repos/{owner}/{repo}/rulesets --method POST --input .github/rulesets/master.json
gh api repos/{owner}/{repo}/rulesets --method POST --input .github/rulesets/develop.json
```

Les deux règles supposent que la branche existe déjà côté serveur et que le workflow `ci-cd` y a tourné au
moins une fois, sans quoi le contrôle requis `build` n'apparaît pas dans la liste proposée par l'interface.

## Ce que `master.json` impose

| Règle | Effet |
|-------|-------|
| `pull_request` | Aucune poussée directe. Passer par une demande de tirage est le seul chemin. |
| `required_status_checks` → `build` | Le job `build` du workflow `ci-cd` doit être vert, et la branche à jour. |
| `non_fast_forward` | Pas de réécriture d'historique : un `push --force` est refusé. |
| `deletion` | La branche ne peut pas être supprimée. |

`required_approving_review_count` est à **0** volontairement : l'exigence demandée est la demande de
tirage, pas l'approbation, et GitHub interdit d'approuver sa propre demande. À un développeur, exiger une
approbation vous enfermerait dehors. Dès qu'une deuxième personne travaille sur le dépôt, passez ce
compteur à 1.

Il n'y a délibérément **pas** de règle d'historique linéaire : le report de `develop` sur `master` se fait
par une fusion, et les deux branches doivent continuer à partager leur historique.

## Ce que `develop.json` impose

Le travail quotidien reste fluide : on pousse sur `develop` sans cérémonie. Seules la suppression de la
branche et la réécriture de son historique sont bloquées.
