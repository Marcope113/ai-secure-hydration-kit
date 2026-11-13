#requires -Version 5.1
[CmdletBinding()]
param([ValidateSet('Check','Install','Status')][string]$Action='Check')
$ErrorActionPreference='Stop'
$labRoot=Join-Path $env:LOCALAPPDATA 'AISecureAcademy'
$manifestPath=Join-Path $labRoot 'hydration-state.json'
function Test-Prerequisites {
  $os=Get-CimInstance Win32_OperatingSystem
  $cpu=Get-CimInstance Win32_Processor | Select-Object -First 1
  $disk=Get-PSDrive -Name ($env:SystemDrive.TrimEnd(':'))
  $ramGB=[math]::Round($os.TotalVisibleMemorySize/1MB,1)
  $freeGB=[math]::Round($disk.Free/1GB,1)
  $vbox=Get-Command VBoxManage.exe -ErrorAction SilentlyContinue
  if(-not $vbox) {
    $candidate=Join-Path $env:ProgramFiles 'Oracle\VirtualBox\VBoxManage.exe'
    if(Test-Path $candidate){$vbox=$candidate}
  }
  [pscustomobject]@{
    WindowsVersion=$os.Caption
    Architecture=$os.OSArchitecture
    MemoryGB=$ramGB
    FreeDiskGB=$freeGB
    VirtualizationFirmwareEnabled=$cpu.VirtualizationFirmwareEnabled
    VirtualBoxInstalled=[bool]$vbox
    MinimumMemoryMet=($ramGB -ge 8)
    MinimumDiskMet=($freeGB -ge 25)
  }
}
function Save-State($state) {
  New-Item -ItemType Directory -Path $labRoot -Force | Out-Null
  $state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
}
switch($Action){
  'Check' {
    $p=Test-Prerequisites
    $p | Format-List
    if(-not $p.MinimumMemoryMet -or -not $p.MinimumDiskMet -or -not $p.VirtualizationFirmwareEnabled){
      Write-Warning 'Requirements not met. Do not proceed until corrected.'
    }
  }
  'Status' {
    if(Test-Path $manifestPath){Get-Content $manifestPath -Raw}
    else {Write-Host 'No Hydration Kit installation recorded.'}
  }
  'Install' {
    $p=Test-Prerequisites
    $p | Format-List
    if(-not $p.MinimumMemoryMet -or -not $p.MinimumDiskMet -or -not $p.VirtualizationFirmwareEnabled){
      throw 'Minimum prerequisites not met. Run -Action Check.'
    }
    if(-not $p.VirtualBoxInstalled){
      if(-not (Get-Command winget.exe -ErrorAction SilentlyContinue)){
        throw 'winget is unavailable. Install App Installer or VirtualBox manually, then retry.'
      }
      Write-Host 'VirtualBox is missing. Installation requires your confirmation and may request elevation.'
      $answer=Read-Host 'Install Oracle VirtualBox via winget? (yes/no)'
      if($answer -ne 'yes'){Write-Host 'Cancelled without installing VirtualBox.';break}
      & winget install --id Oracle.VirtualBox --exact --source winget --accept-source-agreements --accept-package-agreements
      if($LASTEXITCODE -ne 0){throw 'VirtualBox installation failed.'}
      Save-State ([ordered]@{schemaVersion=1;labId='linux-privesc';virtualBoxInstalledByKit=$true;vmCreatedByKit=$false;timestamp=(Get-Date).ToString('o')})
      Write-Host 'VirtualBox installed. Reopen PowerShell if needed and run -Action Check.'
    } else {
      Write-Host 'VirtualBox already exists; it will not be removed by this kit.'
      if(-not (Test-Path $manifestPath)){
        Save-State ([ordered]@{schemaVersion=1;labId='linux-privesc';virtualBoxInstalledByKit=$false;vmCreatedByKit=$false;timestamp=(Get-Date).ToString('o')})
      }
    }
    Write-Host 'Bootstrap complete. VM image download/import is NOT implemented in this prototype.'
  }
}
