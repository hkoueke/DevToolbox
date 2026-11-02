param(
 [string]$MasterRef='refs/remotes/origin/master',
 [string]$DevelopRef='refs/remotes/origin/develop'
)
$ErrorActionPreference='Stop'
git rev-parse --verify "$MasterRef^{commit}" *> $null
if ($LASTEXITCODE -ne 0) { throw "master introuvable: $MasterRef" }
git rev-parse --verify "$DevelopRef^{commit}" *> $null
if ($LASTEXITCODE -ne 0) { throw "develop introuvable: $DevelopRef" }
git merge-base --is-ancestor $MasterRef $DevelopRef
if ($LASTEXITCODE -ne 0) {
 if ($LASTEXITCODE -eq 1) { throw 'Synchronisation manquante: master contient des commits absents de develop. Ouvrir une PR master -> develop.' }
 throw 'Impossible de verifier la synchronisation GitFlow'
}
Write-Host 'Synchronisation master -> develop OK'
