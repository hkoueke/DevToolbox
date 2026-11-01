# WSDL par URL : constat d'acceptation et limites Sparky

Ce constat couvre l'export **WSDL 1.1 document/literal → OpenAPI 3.0.3**, depuis une URL avec collecte
des imports, puis rejeu hors ligne. La comparaison/édition d'une production et la reprise des séances
apidelta relèvent de leurs scénarios distincts du [quickstart](quickstart.md).

## Preuve reproductible disponible

Le corpus [url/service.wsdl](../../tests/DevToolbox.Tests/Tools/ApiDelta/Corpus/Wsdl/url/service.wsdl)
contient deux ports SOAP 1.1/1.2 sur le même endpoint, un appel avec sortie et fault, une opération
one-way, deux imports WSDL, des XSD homonymes dans des dossiers différents et un include XSD.

Le [YAML de référence](../../tests/DevToolbox.Tests/Tools/ApiDelta/Corpus/Wsdl/url/expected.openapi.yaml)
est produit par l'exporteur réel. SHA-256 des octets UTF-8 sans BOM, fins de ligne LF :

```text
58d00c377ef5f6f02d86e305fd14f50d2d04d7a8507672dcddfcda8ce38d9772
```

```bash
dotnet test DevToolbox.slnx -c Release --filter 'FullyQualifiedName~WsdlUrlExportTests'
dotnet test DevToolbox.slnx -c Release --filter 'FullyQualifiedName~PinnedConnectionTests.The_registered_exporter'
sha256sum tests/DevToolbox.Tests/Tools/ApiDelta/Corpus/Wsdl/url/expected.openapi.yaml
```

Le premier ensemble contrôle les hôtes et IP, redirections, schémas d'URL, requêtes non authentifiées,
taille annoncée/absente, budgets cumulés, profondeur, annulation, erreurs et nettoyage atomique. Il
compare l'export au YAML de référence et vérifie son rejeu sans requête. Le deuxième utilise le client
HTTP enregistré par la DI et un vrai serveur local : six documents collectés, épinglage de connexion,
même YAML, puis zéro connexion supplémentaire pendant le rejeu. Aucun serveur externe n'est appelé.

Pour vérifier l'empreinte sous PowerShell :

```powershell
(Get-FileHash tests/DevToolbox.Tests/Tools/ApiDelta/Corpus/Wsdl/url/expected.openapi.yaml -Algorithm SHA256).Hash.ToLowerInvariant()
```

## Ce que le constat établit

- Le YAML émis est reparsé et validé avec Microsoft.OpenApi et ses règles sémantiques par défaut.
- Le chemin SOAP réel est conservé, sans autorité privée ni route REST inventée.
- Les deux types MIME SOAP et les enveloppes XML sont présents. Les réponses 200, 202 sans corps et
  les faults sont représentées sans garantir de statut HTTP métier absent du WSDL.
- Les constructions non prises en charge sont refusées explicitement ; aucune fidélité universelle
  XSD n'est annoncée. La validation XSD, notamment l'ordre XML et les formes lexicales, reste nécessaire.
- Les originaux sont conservés sans réécriture. Seules les copies autonomes réécrivent leurs imports
  vers les documents de la collecte. Le manifeste ne publie pas les URL ; il porte les empreintes des
  deux jeux de fichiers et du YAML. Une modification ou un lien sortant invalide le rejeu.

Dans chaque collecte, `compatibility.json` nomme le validateur et lie ses contrôles à `YamlSha256`.
`SparkyImport` vaut explicitement **Not tested: no real target configured**. Le rapport n'est pas une
attestation d'import Sparky et ne remplace pas la vérification indépendante du YAML fourni.

## Acceptation restant à exécuter sur la cible Sparky

Aucune URL d'instance, version de Sparky, session ou autorisation d'import n'est disponible dans cet
environnement. Aucun import n'a été tenté ni revendiqué. Sur l'instance cible autorisée :

1. Importer le YAML de référence et conserver le résultat, la version de Sparky et l'empreinte du fichier.
2. Vérifier la présence du POST `/soap`, des types MIME XML et des enveloppes SOAP 1.1/1.2.
3. Vérifier le rendu des variantes `anyOf`, des réponses 200/202 et du Fault déclaré.
4. Renseigner explicitement le serveur cible : l'export omet l'autorité pour ne pas publier d'adresse privée.

Une incompatibilité de rendu ou d'import dans cette version de Sparky doit être consignée comme telle.
La validité OpenAPI seule ne permet pas de conclure cette acceptation. La PR reste en draft ; aucune
fusion n'est autorisée par ce constat.
