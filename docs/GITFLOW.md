# GitFlow DevToolbox

Les branches permanentes sont `master` (production) et `develop` (intégration). La CI requise conserve le nom `build`. Aucune fusion sans accord explicite du propriétaire.

## Feature
Créer `feature/nom` depuis `develop`, publier et ouvrir une PR vers `develop`. Attendre revue et CI verte.

## Release
Créer `release/vX.Y.Z` depuis `develop`. Stabiliser sur cette branche et ouvrir `release/vX.Y.Z → master`. Après accord et fusion, relever le nouveau HEAD de `master` puis créer le tag annoté `vX.Y.Z` sur ce commit exact. Le workflow vérifie le tag avant livraison Windows et SHA-256. Vérifier le job `release` et ses artefacts.

Après publication, ouvrir une PR `master → develop` pour réintégrer toute correction de stabilisation. En cas de divergence, résoudre les conflits dans une branche de synchronisation et ouvrir une PR vers `develop` ; ne jamais forcer les protections.

## Hotfix
Créer `hotfix/nom` depuis `master`, tester, puis ouvrir une PR vers `master`. Après accord et fusion, créer un nouveau tag patch sur le HEAD de `master`, vérifier la publication et reporter les changements dans `develop` ainsi que toute release active concernée.

## Rollback et incidents
Ne jamais déplacer un tag publié. Suspendre une livraison défaillante, documenter les artefacts concernés et préparer un nouveau hotfix. Préférer une PR de revert à un push forcé.

## Contrôles
Les PR vers `master` n'acceptent que `release/*` et `hotfix/*`. Les PR Dependabot `dependabot/github_actions/*` et `dependabot/nuget/*` sont autorisées vers `develop` uniquement ; les autres familles de bots ne bénéficient d'aucune exception. Les PR vers `develop` acceptent `feature/*`, `release/*`, `hotfix/*` et `master`. Les pushes `release/*` et `hotfix/*` sont testés. Les tags suivent SemVer et doivent pointer exactement vers HEAD de `master`. Les règles sont testées dans la CI. La configuration des protections GitHub reste du ressort d'un administrateur.

## Audit automatique de la synchronisation
Le job GitHub Actions `gitflow-sync` s'exécute chaque lundi et lors d'un lancement manuel du workflow. Il vérifie, sur l'historique Git réel, que `origin/master` est un ancêtre de `origin/develop`. Un échec indique une release ou un hotfix non réintégré : ouvrir une PR `master → develop`, résoudre les conflits si nécessaire, puis relancer l'audit. Cet audit est séparé du job `build` pour permettre la période normale entre publication et backmerge ; il ne fusionne jamais automatiquement. Les tests Git créent un dépôt temporaire et vérifient un tag valide, le refus d'un tag hors master, l'absence de backmerge et sa résolution.
