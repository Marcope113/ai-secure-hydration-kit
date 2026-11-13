#requires -Version 5.1
<#
Windows developer bootstrap for AI Secure Hydration Kit Desktop.
Installs .NET 8 SDK only with consent; then launches Avalonia source project.
Production students will receive a self-contained published binary instead.
#>
[CmdletBinding()]
param([switch]$CheckOnly)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'desktop\AISecure.Hydration.Desktop\AISecure.Hydration.Desktop.csproj'
function Resolve-Dotnet {
 $cmd=Get-Command dotnet.exe -ErrorAction SilentlyContinue
 if($cmd){return $cmd.Source}
 $path=Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
 if(Test-Path -LiteralPath $path){return $path}
 return $null
}
function Get-Sdk8([string]$dotnet) {
 if(-not $dotnet){return $false}
 $sdks=& $dotnet --list-sdks
 if($LASTEXITCODE -ne 0){throw 'Unable to query installed .NET SDKs.'}
 return [bool](@($sdks | Where-Object { $_ -match '^8\.' }).Count)
}
if(-not [Environment]::OSVersion.Platform.ToString().Equals('Win32NT')){throw 'Windows only.'}
Write-Host '// AI SECURE ACADEMY' -ForegroundColor Cyan
Write-Host 'HYDRATION KIT: Windows Desktop Bootstrap' -ForegroundColor Magenta
Write-Host ('DATE: '+(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Write-Host '[1/4] Checking .NET 8 SDK' -ForegroundColor Cyan
$dotnet=Resolve-Dotnet
$ready=Get-Sdk8 $dotnet
if($ready){Write-Host 'NET 8 SDK already available.' -ForegroundColor Green}
elseif($CheckOnly){Write-Warning '.NET 8 SDK missing. No changes made.';return}
else{
 Write-Host 'Developer preview requires .NET 8 SDK. The student release will not.' -ForegroundColor Yellow
 if((Read-Host 'Install Microsoft .NET 8 SDK using winget? (yes/no)') -ne 'yes'){
  Write-Host 'Cancelled without installing dependencies.';return
 }
 if(-not (Get-Command winget.exe -ErrorAction SilentlyContinue)){throw 'winget is missing; cannot install SDK automatically.'}
 Write-Host '[2/4] Installing Microsoft .NET 8 SDK (may request elevation)' -ForegroundColor Cyan
 & winget.exe install --id Microsoft.DotNet.SDK.8 --exact --source winget --accept-source-agreements --accept-package-agreements
 if($LASTEXITCODE -ne 0){throw "winget installation failed (exit $LASTEXITCODE)."}
 $dotnet=Resolve-Dotnet
 if(-not (Get-Sdk8 $dotnet)){throw 'SDK installation not detected. Restart terminal and retry.'}
}
if($CheckOnly){Write-Host 'Check complete; no changes made.';return}
if(-not (Test-Path -LiteralPath $project)){throw "Desktop project not found: $project"}
Write-Host '[3/4] Restoring Avalonia dependencies' -ForegroundColor Cyan
& $dotnet restore $project
if($LASTEXITCODE -ne 0){throw 'Dependency restore failed.'}
Write-Host '[4/4] Starting Hydration Kit Desktop' -ForegroundColor Cyan
& $dotnet run --no-restore --project $project
if($LASTEXITCODE -ne 0){throw "Desktop app exited with error $LASTEXITCODE."}
