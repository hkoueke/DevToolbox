param(
 [string]$EventName=$env:EVENT_NAME,
 [string]$RefName=$env:REF_NAME,
 [string]$RefType=$env:REF_TYPE,
 [string]$HeadRef=$env:HEAD_REF,
 [string]$BaseRef=$env:BASE_REF,
 [switch]$SkipGit
)
$ErrorActionPreference='Stop'
function Require([bool]$Ok,[string]$Why) { if (-not $Ok) { throw $Why } }
function ValidBranch([string]$Name) {
 Require ($Name -cmatch '^(master|develop|(feature|release|hotfix)/[A-Za-z0-9][A-Za-z0-9._/-]*)$') "Branche invalide: $Name"
 Require ($Name -notmatch '(\.\.|//|/\.|\.lock$|/$)') "Nom de branche dangereux: $Name"
}
function ValidVersion([string]$Name) {
 $num='(?:0|[1-9][0-9]*)'
 $id='(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)'
 Require ($Name -cmatch "^v$num\.$num\.$num(?:-$id(?:\.$id)*)?$") "Tag SemVer invalide: $Name"
}
if ($EventName -eq 'pull_request') {
 if ($HeadRef -notmatch '^dependabot/(github_actions|nuget)/[A-Za-z0-9][A-Za-z0-9._/-]*$') { ValidBranch $HeadRef }
 Require ($BaseRef -in @('master','develop')) "Cible PR interdite: $BaseRef"
 if ($BaseRef -eq 'master') {
   Require ($HeadRef -match '^(release/v[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?|hotfix/[A-Za-z0-9][A-Za-z0-9._/-]*)$') 'master accepte release/* ou hotfix/*'
 } else {
   Require ($HeadRef -eq 'master' -or $HeadRef -match '^(feature|release|hotfix)/' -or $HeadRef -match '^dependabot/(github_actions|nuget)/[A-Za-z0-9][A-Za-z0-9._/-]*$') 'develop accepte feature/*, release/*, hotfix/*, master et Dependabot'
 }
} elseif ($RefType -eq 'tag') {
 ValidVersion $RefName
 if (-not $SkipGit) {
   $tagCommit=(git rev-parse "refs/tags/$RefName^{commit}").Trim()
   Require ($LASTEXITCODE -eq 0) 'Tag introuvable'
   $master=(git rev-parse 'refs/remotes/origin/master^{commit}').Trim()
   Require ($LASTEXITCODE -eq 0) 'master introuvable'
   Require ($tagCommit -eq $master) 'Le tag doit cibler exactement HEAD de master'
 }
} elseif ($EventName -eq 'push' -and $RefType -eq 'branch') { ValidBranch $RefName }
Write-Host "GitFlow valide: $EventName $RefName"
) { ValidBranch $HeadRef }
 Require ($BaseRef -in @('master','develop')) "Cible PR interdite: $BaseRef"
 if ($BaseRef -eq 'master') {
   Require ($HeadRef -match '^(release/v[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?|hotfix/[A-Za-z0-9][A-Za-z0-9._/-]*)$') 'master accepte release/* ou hotfix/*'
 } else {
   Require ($HeadRef -eq 'master' -or $HeadRef -match '^(feature|release|hotfix)/') 'develop accepte feature/*, release/*, hotfix/* ou master'
 }
} elseif ($RefType -eq 'tag') {
 ValidVersion $RefName
 if (-not $SkipGit) {
   $tagCommit=(git rev-parse "refs/tags/$RefName^{commit}").Trim()
   Require ($LASTEXITCODE -eq 0) 'Tag introuvable'
   $master=(git rev-parse 'refs/remotes/origin/master^{commit}').Trim()
   Require ($LASTEXITCODE -eq 0) 'master introuvable'
   Require ($tagCommit -eq $master) 'Le tag doit cibler exactement HEAD de master'
 }
} elseif ($EventName -eq 'push' -and $RefType -eq 'branch') { ValidBranch $RefName }
Write-Host "GitFlow valide: $EventName $RefName"
) 'develop accepte feature/*, release/*, hotfix/* ou master'
 }
} elseif ($RefType -eq 'tag') {
 ValidVersion $RefName
 if (-not $SkipGit) {
   $tagCommit=(git rev-parse "refs/tags/$RefName^{commit}").Trim()
   Require ($LASTEXITCODE -eq 0) 'Tag introuvable'
   $master=(git rev-parse 'refs/remotes/origin/master^{commit}').Trim()
   Require ($LASTEXITCODE -eq 0) 'master introuvable'
   Require ($tagCommit -eq $master) 'Le tag doit cibler exactement HEAD de master'
 }
} elseif ($EventName -eq 'push' -and $RefType -eq 'branch') { ValidBranch $RefName }
Write-Host "GitFlow valide: $EventName $RefName"
) { ValidBranch $HeadRef }
 Require ($BaseRef -in @('master','develop')) "Cible PR interdite: $BaseRef"
 if ($BaseRef -eq 'master') {
   Require ($HeadRef -match '^(release/v[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?|hotfix/[A-Za-z0-9][A-Za-z0-9._/-]*)$') 'master accepte release/* ou hotfix/*'
 } else {
   Require ($HeadRef -eq 'master' -or $HeadRef -match '^(feature|release|hotfix)/') 'develop accepte feature/*, release/*, hotfix/* ou master'
 }
} elseif ($RefType -eq 'tag') {
 ValidVersion $RefName
 if (-not $SkipGit) {
   $tagCommit=(git rev-parse "refs/tags/$RefName^{commit}").Trim()
   Require ($LASTEXITCODE -eq 0) 'Tag introuvable'
   $master=(git rev-parse 'refs/remotes/origin/master^{commit}').Trim()
   Require ($LASTEXITCODE -eq 0) 'master introuvable'
   Require ($tagCommit -eq $master) 'Le tag doit cibler exactement HEAD de master'
 }
} elseif ($EventName -eq 'push' -and $RefType -eq 'branch') { ValidBranch $RefName }
Write-Host "GitFlow valide: $EventName $RefName"
