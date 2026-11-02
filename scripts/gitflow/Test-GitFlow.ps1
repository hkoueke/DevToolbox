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

# Integration tests use an isolated real Git repository to exercise ancestry and tag provenance.
$original=(Get-Location).Path
$tmp=Join-Path ([IO.Path]::GetTempPath()) ("devtoolbox-gitflow-"+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
 Set-Location $tmp
 git init -q
 if ($LASTEXITCODE -ne 0) { throw 'git init failed' }
 git config user.email gitflow-tests@example.invalid
 git config user.name GitFlowTests
 'initial' | Set-Content fixture.txt
 git add fixture.txt
 git commit -qm initial
 $initial=(git rev-parse HEAD).Trim()
 git branch -f master $initial
 git branch -f develop $initial
 git update-ref refs/remotes/origin/master $initial
 git update-ref refs/remotes/origin/develop $initial
 git tag -a v2.3.0 -m v2.3.0
 & $script -EventName push -RefType tag -RefName v2.3.0 *> $null
 & (Join-Path $PSScriptRoot 'Check-GitFlow-Sync.ps1') *> $null
 git checkout -q -b feature/off-master
 'off-master' | Add-Content fixture.txt
 git commit -qam off-master
 git tag -a v2.3.1 -m v2.3.1
 $rejected=$false
 try { & $script -EventName push -RefType tag -RefName v2.3.1 *> $null } catch { $rejected=$true }
 if (-not $rejected) { throw 'Off-master tag was accepted' }
 git checkout -q master
 'production' | Add-Content fixture.txt
 git commit -qam production
 $production=(git rev-parse HEAD).Trim()
 git update-ref refs/remotes/origin/master $production
 $rejected=$false
 try { & (Join-Path $PSScriptRoot 'Check-GitFlow-Sync.ps1') *> $null } catch { $rejected=$true }
 if (-not $rejected) { throw 'Missing backmerge was accepted' }
 git update-ref refs/remotes/origin/develop $production
 & (Join-Path $PSScriptRoot 'Check-GitFlow-Sync.ps1') *> $null
 Write-Host 'Git integration: valid tag, off-master rejection, backmerge detection and recovery OK'
} finally {
 Set-Location $original
 Remove-Item -LiteralPath $tmp -Recurse -Force
}
