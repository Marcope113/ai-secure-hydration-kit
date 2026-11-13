#requires -Version 5.1
[CmdletBinding()]
param([ValidateSet('Check','Version')][string]$Action='Check')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$version='0.1.0-preview'
if($Action -eq 'Version'){Write-Output "AI Secure Hydration Kit $version";return}
Write-Host "AI Secure Hydration Kit $version" -ForegroundColor Cyan
Write-Host ('OS: '+[Environment]::OSVersion.VersionString)
Write-Host ('PowerShell: '+$PSVersionTable.PSVersion)
Write-Host ('Git available: '+[bool](Get-Command git -ErrorAction SilentlyContinue))
$vbox=Join-Path $env:ProgramFiles 'Oracle\VirtualBox\VBoxManage.exe'
if(Test-Path -LiteralPath $vbox){Write-Host "VirtualBox: $vbox";& $vbox --version}else{Write-Host 'VirtualBox: not found'}
Write-Host ('Academy state directory: '+(Join-Path $env:LOCALAPPDATA 'AISecureAcademy'))
Write-Host 'Diagnostic complete; no system changes made.' -ForegroundColor Green
