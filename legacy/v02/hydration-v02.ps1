#requires -Version 5.1
[CmdletBinding()]
param([ValidateSet('Check','CreateVM','Status')][string]$Action='Check')
$ErrorActionPreference='Stop'
$root=Join-Path $env:LOCALAPPDATA 'AISecureAcademy'
$stateFile=Join-Path $root 'hydration-v02.json'
$vmName='AISecure-Linux-PrivEsc-Dev'
$vmFolder=Join-Path $root 'vms'
$vbox=Join-Path $env:ProgramFiles 'Oracle\VirtualBox\VBoxManage.exe'
function Invoke-VBox([string[]]$Arguments) {
  & $vbox @Arguments
  if($LASTEXITCODE -ne 0){throw "VBoxManage failed (exit $LASTEXITCODE): $($Arguments -join ' ')"}
}
function Read-State {
  if(Test-Path -LiteralPath $stateFile){return (Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json)}
  return $null
}
function Write-State($data){
  New-Item -ItemType Directory -Path $root -Force | Out-Null
  $tmp="$stateFile.tmp"
  $data | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $tmp -Encoding UTF8
  Move-Item -LiteralPath $tmp -Destination $stateFile -Force
}
function Check {
  if(-not (Test-Path -LiteralPath $vbox)){throw 'VirtualBox is not found. Run the v0.1 bootstrap first.'}
  $os=Get-CimInstance Win32_OperatingSystem
  $cpu=Get-CimInstance Win32_Processor | Select-Object -First 1
  $drive=Get-PSDrive -Name ($env:SystemDrive.TrimEnd(':'))
  $ram=[math]::Round($os.TotalVisibleMemorySize/1MB,1)
  $free=[math]::Round($drive.Free/1GB,1)
  [pscustomobject]@{
    Windows=$os.Caption; VirtualBox=(& $vbox --version)
    MemoryGB=$ram; FreeDiskGB=$free
    VirtualizationFirmwareEnabled=[bool]$cpu.VirtualizationFirmwareEnabled
    Ready=($ram -ge 8 -and $free -ge 35 -and $cpu.VirtualizationFirmwareEnabled)
  }
}
switch($Action){
 'Check' {Check | Format-List}
 'Status' {
   $s=Read-State
   if($null -eq $s){Write-Host 'No v0.2 VM record found.'}
   else {$s | Format-List; Write-Host 'This stage does not install Ubuntu or create vulnerable configurations.'}
 }
 'CreateVM' {
   $p=Check
   $p | Format-List
   if(-not $p.Ready){throw 'Minimum requirements not met.'}
   if(Read-State){throw 'Existing v0.2 state found. Refusing to overwrite or duplicate the VM.'}
   $existing=& $vbox list vms
   if($LASTEXITCODE -ne 0){throw 'Could not list existing VMs.'}
   if($existing -match ('"'+[regex]::Escape($vmName)+'"')){throw 'VM name already exists; refusing to touch it.'}
   Write-Host 'Will create an EMPTY Ubuntu VM (4 GB RAM, 2 CPUs, 30 GB disk, isolated internal network).'
   Write-Host 'No ISO will be downloaded, and no operating system will be installed in this stage.'
   if((Read-Host 'Proceed? (yes/no)') -ne 'yes'){Write-Host 'Cancelled.';break}
   New-Item -ItemType Directory -Path $vmFolder -Force | Out-Null
   $state=[ordered]@{schemaVersion=2;labId='linux-privesc';vmName=$vmName;vmCreatedByKit=$false;stage='starting';createdAt=(Get-Date).ToString('o');vmDisk=''}
   Write-State $state
   try {
     Invoke-VBox @('createvm','--name',$vmName,'--ostype','Ubuntu_64','--basefolder',$vmFolder,'--register')
     $state.vmCreatedByKit=$true;$state.stage='registered';Write-State $state
     Invoke-VBox @('modifyvm',$vmName,'--memory','4096','--cpus','2','--nic1','intnet','--intnet1','aisecurelab','--cableconnected1','on','--audio-enabled','off','--usb-ohci','off')
     Invoke-VBox @('storagectl',$vmName,'--name','SATA','--add','sata','--controller','IntelAhci')
     $disk=Join-Path (Join-Path $vmFolder $vmName) 'lab.vdi'
     $state.vmDisk=$disk;$state.stage='disk-pending';Write-State $state
     Invoke-VBox @('createmedium','disk','--filename',$disk,'--size','30720','--format','VDI')
     Invoke-VBox @('storageattach',$vmName,'--storagectl','SATA','--port','0','--device','0','--type','hdd','--medium',$disk)
     $state.stage='vm-scaffold-ready';Write-State $state
     Write-Host 'VM scaffold created successfully. No Ubuntu installation yet.'
   } catch {
     $state.stage='incomplete-needs-review';Write-State $state
     Write-Warning 'Partial VM may exist. No automatic deletion performed. Inspect state before retrying.'
     throw
   }
 }
}
