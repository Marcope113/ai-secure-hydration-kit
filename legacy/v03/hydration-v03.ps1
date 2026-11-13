#requires -Version 5.1
[CmdletBinding()]
param([ValidateSet('Check','PrepareMedia','Status')][string]$Action='Check')
$ErrorActionPreference='Stop'
$root=Join-Path $env:LOCALAPPDATA 'AISecureAcademy'
$stateFile=Join-Path $root 'hydration-v02.json'
$mediaFile=Join-Path $root 'ubuntu-media.json'
$isoDir=Join-Path $root 'images'
$vbox=Join-Path $env:ProgramFiles 'Oracle\VirtualBox\VBoxManage.exe'
$base='https://releases.ubuntu.com/24.04/'
function VBox([string[]]$argsList){
  & $vbox @argsList
  if($LASTEXITCODE -ne 0){throw "VBoxManage failed: $($argsList -join ' ')"}
}
function Save-Json($path,$data){
  $tmp="$path.tmp"
  $data | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $tmp -Encoding UTF8
  Move-Item -LiteralPath $tmp -Destination $path -Force
}
function Get-VMState {
  if(-not (Test-Path $stateFile)){throw 'v0.2 manifest missing. Run v0.2 CreateVM first.'}
  $state=Get-Content $stateFile -Raw | ConvertFrom-Json
  if($state.stage -ne 'vm-scaffold-ready' -or -not $state.vmCreatedByKit){throw 'VM is not in expected ready state.'}
  if(-not (Test-Path $vbox)){throw 'VirtualBox executable missing.'}
  $info=& $vbox showvminfo $state.vmName --machinereadable
  if($LASTEXITCODE -ne 0){throw 'The recorded VM cannot be found.'}
  if(($info -join "`n") -notmatch 'VMState="poweroff"'){throw 'VM must be powered off.'}
  return $state
}
switch($Action){
 'Check' {
   $s=Get-VMState
   Write-Host "VM: $($s.vmName) (powered off; ready for ISO)"
   Write-Host 'This step downloads an official Ubuntu Server 24.04 ISO and attaches it; it does not install Ubuntu.'
 }
 'Status' {
   if(Test-Path $mediaFile){Get-Content $mediaFile -Raw}
   else {Write-Host 'No ISO preparation recorded.'}
 }
 'PrepareMedia' {
   $s=Get-VMState
   if(Test-Path $mediaFile){throw 'Media manifest already exists. Inspect -Action Status before retrying.'}
   Write-Host 'Fetching official Ubuntu 24.04 checksum index (HTTPS)...'
   $response=Invoke-WebRequest -UseBasicParsing -Uri ($base+'SHA256SUMS')
   $index=if($response.Content -is [byte[]]){[System.Text.Encoding]::UTF8.GetString($response.Content)}else{[string]$response.Content}
   Write-Host ('Checksum index characters: '+$index.Length)
   $entries=@()
   foreach($line in ($index -split "`n")){
     if($line -match '^([a-fA-F0-9]{64})\s+\*?(?:\./)?(ubuntu-24\.04(?:\.\d+)?-live-server-amd64\.iso)\s*$'){
       $entries += [pscustomobject]@{Hash=$Matches[1].ToUpperInvariant();File=$Matches[2]}
     }
   }
   if($entries.Count -eq 0){
     Write-Warning ('Checksum index sample: '+$index.Substring(0,[math]::Min(200,$index.Length)))
     throw 'No matching Ubuntu Server ISO in official checksum index. No changes were made.'
   }
   $entry=$entries | Sort-Object File -Descending | Select-Object -First 1
   $url=$base+$entry.File
   $iso=Join-Path $isoDir $entry.File
   Write-Host "Selected: $($entry.File)"
   Write-Host 'This may download several GB. The VM will remain powered off.'
   if((Read-Host 'Download and attach official ISO? (yes/no)') -ne 'yes'){Write-Host 'Cancelled.';break}
   New-Item -ItemType Directory -Path $isoDir -Force | Out-Null
   if(-not (Test-Path $iso)){
     $part="$iso.partial"
     if(Test-Path $part){Remove-Item -LiteralPath $part -Force}
     try {
       Start-BitsTransfer -Source $url -Destination $part -ErrorAction Stop
     } catch {
       Write-Warning 'BITS failed; trying standard HTTPS download.'
       Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $part
     }
     Move-Item -LiteralPath $part -Destination $iso -Force
   }
   $actual=(Get-FileHash -LiteralPath $iso -Algorithm SHA256).Hash.ToUpperInvariant()
   if($actual -ne $entry.Hash){throw "SHA-256 mismatch. ISO was NOT attached. Delete suspect ISO manually: $iso"}
   Write-Host 'SHA-256 verified against official Ubuntu checksum listing.'
   $info=& $vbox showvminfo $s.vmName --machinereadable
   if($LASTEXITCODE -ne 0){throw 'Cannot inspect VM storage.'}
   if(($info -join "`n") -notmatch 'storagecontrollername\d+="HydrationIDE"'){
     VBox -argsList @('storagectl',$s.vmName,'--name','HydrationIDE','--add','ide','--controller','PIIX4')
   } else {
     throw 'An IDE controller already exists. Refusing to alter unknown controller configuration.'
   }
   VBox -argsList @('storageattach',$s.vmName,'--storagectl','HydrationIDE','--port','0','--device','0','--type','dvddrive','--medium',$iso)
   Save-Json $mediaFile ([ordered]@{schemaVersion=3;labId=$s.labId;vmName=$s.vmName;isoFile=$iso;isoUrl=$url;sha256=$actual;status='iso-attached-not-installed';preparedAt=(Get-Date).ToString('o')})
   Write-Host 'ISO attached. Ubuntu is NOT installed yet. Do not start the VM until unattended installation is configured.'
 }
}
