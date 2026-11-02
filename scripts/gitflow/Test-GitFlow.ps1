$ErrorActionPreference='Stop'
$script=Join-Path $PSScriptRoot 'Validate-GitFlow.ps1'
$cases=@(
 @{n='feature develop';e='pull_request';h='feature/x';b='develop';ok=$true},
 @{n='release master';e='pull_request';h='release/v2.3.0';b='master';ok=$true},
 @{n='hotfix master';e='pull_request';h='hotfix/urgent';b='master';ok=$true},
 @{n='hotfix develop';e='pull_request';h='hotfix/urgent';b='develop';ok=$true},
 @{n='dependabot actions';e='pull_request';h='dependabot/github_actions/actions/checkout-8';b='develop';ok=$true},
 @{n='dependabot nuget';e='pull_request';h='dependabot/nuget/nuget-packages';b='develop';ok=$true},
 @{n='dependabot master refused';e='pull_request';h='dependabot/nuget/update';b='master';ok=$false},
 @{n='backmerge';e='pull_request';h='master';b='develop';ok=$true},
 @{n='feature master';e='pull_request';h='feature/x';b='master';ok=$false},
 @{n='develop master';e='pull_request';h='develop';b='master';ok=$false},
 @{n='wrong target';e='pull_request';h='feature/x';b='release/v2.3.0';ok=$false},
 @{n='bad source';e='pull_request';h='fix/x';b='develop';ok=$false},
 @{n='tag stable';e='push';r='v2.3.0';t='tag';ok=$true},
 @{n='tag prerelease';e='push';r='v2.3.0-rc.1';t='tag';ok=$true},
 @{n='leading zero';e='push';r='v02.3.0';t='tag';ok=$false},
 @{n='bad tag';e='push';r='v2.3';t='tag';ok=$false},
 @{n='numeric prerelease zero';e='push';r='v2.3.0-rc.01';t='tag';ok=$false},
 @{n='release push';e='push';r='release/v2.3.0';t='branch';ok=$true},
 @{n='hotfix push';e='push';r='hotfix/urgent';t='branch';ok=$true}
)
$fail=@()
foreach($c in $cases) {
 $pass=$true
 try { & $script -EventName $c.e -HeadRef $c.h -BaseRef $c.b -RefName $c.r -RefType $c.t -SkipGit *> $null } catch { $pass=$false }
 if($pass -ne $c.ok) { $fail+= $c.n }
}
if($fail.Count) { throw "Tests echoues: $($fail -join ', ')" }
Write-Host "$($cases.Count) scenarios GitFlow OK"
