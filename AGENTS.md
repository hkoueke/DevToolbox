# Consignes de contribution — DevToolbox

## Règle impérative : aucun micro-développement

Avant **chaque intervention** sur ce dépôt, lire ce fichier et appliquer cette règle. L'utilisateur a explicitement refusé les « microdevs » à plusieurs reprises. Ne pas revenir à des modifications atomisées présentées comme des livraisons.

### Mode de travail obligatoire

1. **Livrer des lots fonctionnels complets**, cohérents et intégrés, et non une succession de petites corrections, tests ou commits isolés.
2. **Préparer le périmètre de bout en bout avant de coder** : entrée utilisateur, lecture et validation, traitement métier, génération du résultat, intégration à l'interface, tests et documentation utile.
3. **Regrouper les modifications liées** dans un lot substantiel. Une correction de CI fait partie du lot en cours ; elle ne constitue pas une nouvelle livraison à annoncer.
4. **Valider globalement** : tests unitaires et d'intégration, cas limites, sécurité, non-régression et CI sur le dernier commit. Ne pas annoncer un lot comme achevé si la CI est inconnue ou en échec.
5. **Communiquer seulement des résultats consolidés** : fonctionnalité réellement utilisable, tests vérifiés, limites restantes et liens vers PR/CI. Éviter les comptes rendus répétitifs pour une poignée de lignes.
6. **Ne jamais fusionner une pull request sans autorisation explicite de l'utilisateur.**

### Critère de réussite du chantier WSDL → OpenAPI / Sparky

Le parcours visé est : sélectionner/importer un WSDL, résoudre de manière sécurisée ses imports WSDL/XSD et références qualifiées, identifier les services, bindings et véritables endpoints SOAP, projeter fidèlement opérations, messages, types XML et faults, produire un YAML **OpenAPI 3.0.3 valide**, l'exposer dans DevToolbox et vérifier son exploitabilité pour **Sparky API Manager**.

Ne pas inventer des routes REST ou des schémas que le WSDL ne garantit pas. Ne pas exposer d'identifiants, autorités privées, paramètres sensibles ou secrets présents dans les URLs. Si une construction SOAP ne peut pas être représentée fidèlement en OpenAPI, la signaler explicitement au lieu de prétendre que la conversion est complète.

**Contrôle avant toute réponse « Continue » :** ai-je réalisé ou fait progresser un ensemble fonctionnel significatif et validé, plutôt qu'un microdev ? Si non, poursuivre le travail et ne pas présenter une micro-modification comme un résultat.

Ce fichier formalise une exigence explicite du propriétaire du projet et doit être relu au début de chaque session de développement sur DevToolbox.
