#requires -Version 5.1
<#
AI Secure Academy Hydration Kit
Consolidated v0.1-v0.3: host checks, VirtualBox bootstrap, isolated VM, Debian ISO preparation.
No guest OS installation or intentionally vulnerable settings in this release.
#>
[CmdletBinding()]
param(
 [ValidateSet('Check','Install','CreateVM','PrepareMedia','Status','Version')]
 [string]$Action='Check'
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$Version='0.3.5-preview'
$Root=Join-Path $env:LOCALAPPDATA 'AISecureAcademy'
$Manifest=Join-Path $Root 'hydration-state.json'
$VMManifest=Join-Path $Root 'hydration-v02.json'
$MediaManifest=Join-Path $Root 'debian-media.json'
$VMName='AISecure-Linux-PrivEsc-Dev'
$VBox=Join-Path $env:ProgramFiles 'Oracle\VirtualBox\VBoxManage.exe'
$IsoName='debian-13.7.0-amd64-netinst.iso'
$IsoHash='a7ef94ac2fb9a7fec454552abd629b7cc9d5155c886165a45649f5ce6167e355'
$IsoBase='https://cdimage.debian.org/debian-cd/current/amd64/iso-cd/'
function Show-Banner {
 if($Action -eq 'Version'){return}
 $now=Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
 Write-Host ''
 Write-Host '  // AI SECURE' -ForegroundColor Cyan
 Write-Host '     A C A D E M Y' -ForegroundColor DarkCyan
 Write-Host ''
 Write-Host '  HYDRATION KIT' -ForegroundColor Magenta
 Write-Host '  CYBER LAB PROVISIONING' -ForegroundColor Gray
 Write-Host '  -------------------------------------------------------------' -ForegroundColor DarkGray
 Show-Line 'LAB' 'Linux Privilege Escalation'
 Show-Line 'LAB ID' 'linux-privesc'
 Show-Line 'VIRTUAL MACHINE' $VMName
 Show-Line 'DATE / TIME' $now
 Show-Line 'RELEASE' $Version
 Show-Line 'OPERATION' $Action.ToUpperInvariant()
 Write-Host ''
 if(-not [Console]::IsOutputRedirected){
  foreach($frame in @('[>.........]','[==>.......]','[====>.....]','[======>...]','[=========>]')){
   Write-Host (([char]13)+'  BOOT SEQUENCE  '+$frame) -NoNewline -ForegroundColor Cyan
   Start-Sleep -Milliseconds 75
  }
  Write-Host (([char]13)+'  BOOT SEQUENCE  [ONLINE]      ') -ForegroundColor Green
 }
 Write-Host '  -----------------------------------------------------------------' -ForegroundColor DarkGray
}
function Show-Line([string]$Label,[string]$Value,[bool]$Good=$true){
 $color=if($Good){'Green'}else{'Yellow'}
 Write-Host ('  {0,-25}' -f $Label) -NoNewline -ForegroundColor DarkGray
 Write-Host ('  '+$Value) -ForegroundColor $color
}
function Step([int]$Index,[int]$Total,[string]$Message){
 $percent=[int](($Index-1)*100/$Total)
 Write-Progress -Id 1 -Activity "AI Secure Hydration Kit - $Action" -Status "Step $Index/$Total : $Message" -PercentComplete $percent
 Write-Host ''
 Write-Host ("  [{0:00}/{1:00}]  {2}" -f $Index,$Total,$Message.ToUpperInvariant()) -ForegroundColor Cyan
}
function Complete-Steps {
 Write-Progress -Id 1 -Activity 'AI Secure Hydration Kit' -Completed
 if($Action -eq 'Version'){return}
 Write-Host ''
 Write-Host '  -------------------------------------------------------------' -ForegroundColor DarkGray
 if($script:SessionFailed){Write-Host '  [!] OPERATION FAILED - REVIEW ERROR ABOVE' -ForegroundColor Red}
 elseif($script:SessionCancelled){Write-Host '  [-] OPERATION CANCELLED / NO FURTHER CHANGES' -ForegroundColor Yellow}
 else{Write-Host '  [+] OPERATION FINISHED - AI SECURE ACADEMY' -ForegroundColor Green}
 Write-Host ''
}
function Confirm([string]$Question){$ok=((Read-Host "$Question (yes/no)") -eq 'yes');if(-not $ok){$script:SessionCancelled=$true};return $ok}
function Save-Json([string]$Path,$Data){
 New-Item -ItemType Directory -Path (Split-Path $Path) -Force | Out-Null
 $tmp="$Path.tmp"
 $Data | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $tmp -Encoding UTF8
 Move-Item -LiteralPath $tmp -Destination $Path -Force
}
function Read-Json([string]$Path){
 if(Test-Path -LiteralPath $Path){return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)}
 return $null
}
function Run-VBox([string[]]$ArgsList){
 & $VBox @ArgsList
 if($LASTEXITCODE -ne 0){throw "VirtualBox command failed (exit $LASTEXITCODE)."}
}
function Check-Host {
 $os=Get-CimInstance Win32_OperatingSystem
 $cpu=Get-CimInstance Win32_Processor | Select-Object -First 1
 $drive=Get-PSDrive -Name ($env:SystemDrive.TrimEnd(':'))
 $ram=[math]::Round($os.TotalVisibleMemorySize/1MB,1)
 $free=[math]::Round($drive.Free/1GB,1)
 $ready=($ram -ge 8 -and $free -ge 35 -and [bool]$cpu.VirtualizationFirmwareEnabled)
 [pscustomobject]@{
  Windows=$os.Caption; RAM_GB=$ram; FreeDisk_GB=$free
  FirmwareVirtualization=[bool]$cpu.VirtualizationFirmwareEnabled
  VirtualBoxInstalled=(Test-Path -LiteralPath $VBox)
  MeetsVMRequirements=$ready
 }
}
function Assert-VM {
 $s=Read-Json $VMManifest
 if($null -eq $s -or -not $s.vmCreatedByKit -or $s.stage -ne 'vm-scaffold-ready'){
  throw 'No owned, ready VM scaffold recorded. Run -Action CreateVM first.'
 }
 if(-not (Test-Path -LiteralPath $VBox)){throw 'VirtualBox not found.'}
 $info=& $VBox showvminfo $s.vmName --machinereadable
 if($LASTEXITCODE -ne 0){throw 'VM is missing from VirtualBox.'}
 if(($info -join "`n") -notmatch 'VMState="poweroff"'){throw 'Power off the VM first.'}
 return $s
}
$script:SessionFailed=$false
$script:SessionCancelled=$false
try{
 Show-Banner
 switch($Action){
  'Version' {Write-Output $Version}
  'Check' {
   Step 1 3 'Checking Windows hardware'
   $p=Check-Host
   Show-Line 'Operating system' $p.Windows
   Show-Line 'Memory' ("$($p.RAM_GB) GB")
   Show-Line 'Free disk' ("$($p.FreeDisk_GB) GB")
   Show-Line 'Virtualization' $(if($p.FirmwareVirtualization){'ENABLED'}else{'DISABLED'}) $p.FirmwareVirtualization
   Step 2 3 'Checking existing Academy state'
   foreach($file in @($Manifest,$VMManifest,$MediaManifest)){
    $exists=Test-Path -LiteralPath $file
    Show-Line (Split-Path $file -Leaf) $(if($exists){'FOUND'}else{'NOT FOUND'}) $exists
   }
   Step 3 3 'Checking VirtualBox and lab readiness'
   if(Test-Path $VBox){$vb=(& $VBox --version);Show-Line 'VirtualBox version' $vb}else{Show-Line 'VirtualBox' 'NOT INSTALLED' $false}
   if(-not $p.MeetsVMRequirements){Write-Warning 'VM minimum requirements not met.'}
   $vmState=Read-Json $VMManifest
   $mediaState=Read-Json $MediaManifest
   Show-Line 'VM scaffold' $(if($vmState -and $vmState.stage -eq 'vm-scaffold-ready'){'READY'}else{'NOT READY'}) ([bool]($vmState -and $vmState.stage -eq 'vm-scaffold-ready'))
   Show-Line 'Debian media' $(if($mediaState -and $mediaState.status -eq 'iso-attached-not-installed'){'ATTACHED'}else{'NOT RECORDED'}) ([bool]($mediaState -and $mediaState.status -eq 'iso-attached-not-installed'))
   Show-Line 'Guest OS' 'NOT VERIFIED / INSTALL PENDING' $false
   Write-Host 'Diagnostics complete; no changes made.' -ForegroundColor Green
  }
  'Status' {
   Step 1 3 'Reading host installation state'
   if(Test-Path $Manifest){Get-Content $Manifest -Raw}else{Write-Host 'No host state.'}
   Step 2 3 'Reading VM state'
   if(Test-Path $VMManifest){Get-Content $VMManifest -Raw}else{Write-Host 'No VM state.'}
   Step 3 3 'Reading Debian media state'
   if(Test-Path $MediaManifest){Get-Content $MediaManifest -Raw}else{Write-Host 'No media state.'}
  }
  'Install' {
   Step 1 3 'Checking system requirements'
   $p=Check-Host;$p | Format-List
   if(-not $p.MeetsVMRequirements){throw 'Minimum VM requirements not met.'}
   Step 2 3 'Checking VirtualBox'
   if(Test-Path $VBox){Write-Host 'VirtualBox already installed; leaving it unchanged.';break}
   if(-not (Get-Command winget.exe -ErrorAction SilentlyContinue)){throw 'winget not available.'}
   if(-not (Confirm 'Install Oracle VirtualBox with winget?')){Write-Host 'Cancelled.';break}
   Step 3 3 'Installing VirtualBox (may request elevation)'
   & winget install --id Oracle.VirtualBox --exact --source winget --accept-source-agreements --accept-package-agreements
   if($LASTEXITCODE -ne 0){throw 'VirtualBox installation failed.'}
   if(-not (Test-Path $Manifest)){
    Save-Json $Manifest ([ordered]@{schemaVersion=1;labId='linux-privesc';virtualBoxInstalledByKit=$true;vmCreatedByKit=$false;timestamp=(Get-Date).ToString('o')})
   }
   Write-Host 'VirtualBox installed. Reopen PowerShell before CreateVM.' -ForegroundColor Green
  }
  'CreateVM' {
   Step 1 5 'Checking prerequisites and existing VM'
   $p=Check-Host
   if(-not $p.MeetsVMRequirements -or -not (Test-Path $VBox)){throw 'Host or VirtualBox prerequisites missing.'}
   if(Test-Path $VMManifest){throw 'Existing VM manifest found. Refusing duplicate creation.'}
   $existing=& $VBox list vms
   if($LASTEXITCODE -ne 0){throw 'Cannot list VirtualBox VMs.'}
   if(($existing -join "`n") -match ('"'+[regex]::Escape($VMName)+'"')){throw 'VM name already exists; refusing to overwrite.'}
   if(-not (Confirm 'Create an EMPTY isolated 4GB RAM / 2 CPU / 30GB Debian VM?')){break}
   $folder=Join-Path $Root 'vms'
   $disk=Join-Path (Join-Path $folder $VMName) 'lab.vdi'
   $s=[ordered]@{schemaVersion=2;labId='linux-privesc';vmName=$VMName;vmCreatedByKit=$false;stage='starting';createdAt=(Get-Date).ToString('o');vmDisk=$disk}
   Save-Json $VMManifest $s
   try{
    Step 2 5 'Registering the virtual machine'
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    Run-VBox -ArgsList @('createvm','--name',$VMName,'--ostype','Debian_64','--basefolder',$folder,'--register')
    $s.vmCreatedByKit=$true;$s.stage='registered';Save-Json $VMManifest $s
    Step 3 5 'Configuring isolated networking and hardware'
    Run-VBox -ArgsList @('modifyvm',$VMName,'--memory','4096','--cpus','2','--nic1','intnet','--intnet1','aisecurelab','--cableconnected1','on','--audio-enabled','off')
    Step 4 5 'Creating virtual disk'
    Run-VBox -ArgsList @('storagectl',$VMName,'--name','SATA','--add','sata','--controller','IntelAhci')
    Run-VBox -ArgsList @('createmedium','disk','--filename',$disk,'--size','30720','--format','VDI')
    Step 5 5 'Attaching virtual disk and recording state'
    Run-VBox -ArgsList @('storageattach',$VMName,'--storagectl','SATA','--port','0','--device','0','--type','hdd','--medium',$disk)
    $s.stage='vm-scaffold-ready';Save-Json $VMManifest $s
    Write-Host 'Empty, isolated Debian VM created. OS not installed.' -ForegroundColor Green
   }catch{
    $s.stage='incomplete-needs-review';Save-Json $VMManifest $s
    throw
   }
  }
  'PrepareMedia' {
   Step 1 5 'Checking owned VM and existing media state'
   $vm=Assert-VM
   if(Test-Path $MediaManifest){throw 'Media already recorded. Inspect Status before retrying.'}
   $dir=Join-Path $Root 'images'
   $iso=Join-Path $dir $IsoName
   Step 2 5 'Checking pinned Debian 13.7.0 image'
   if(-not (Test-Path -LiteralPath $iso)){
    if(-not (Confirm "Download $IsoName (~756 MB) from Debian?")){break}
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    $part="$iso.partial"
    Step 3 5 'Downloading ISO (curl displays live progress)'
    & curl.exe -fL --retry 5 --retry-delay 3 -C - -o $part ($IsoBase+$IsoName)
    if($LASTEXITCODE -ne 0){throw "Download failed; partial file retained at $part"}
    Move-Item -LiteralPath $part -Destination $iso
   }else{
    Step 3 5 'Reusing previously downloaded ISO'
   }
   Step 4 5 'Validating pinned SHA-256 checksum'
   $actual=(Get-FileHash -LiteralPath $iso -Algorithm SHA256).Hash.ToLowerInvariant()
   if($actual -ne $IsoHash){throw 'Debian ISO SHA-256 mismatch. Refusing to attach.'}
   Write-Host 'SHA-256 verified.' -ForegroundColor Green
   Step 5 5 'Attaching ISO to powered-off VM'
   if(-not (Confirm 'Attach verified Debian ISO to VM?')){break}
   $info=& $VBox showvminfo $vm.vmName --machinereadable
   if($LASTEXITCODE -ne 0){throw 'Cannot inspect VM.'}
   if(($info -join "`n") -match 'storagecontrollername\d+="HydrationIDE"'){
    throw 'HydrationIDE already exists. Inspect VM manually before continuing.'
   }
   Run-VBox -ArgsList @('storagectl',$vm.vmName,'--name','HydrationIDE','--add','ide','--controller','PIIX4')
   Run-VBox -ArgsList @('storageattach',$vm.vmName,'--storagectl','HydrationIDE','--port','0','--device','0','--type','dvddrive','--medium',$iso)
   Save-Json $MediaManifest ([ordered]@{schemaVersion=3;labId='linux-privesc';vmName=$vm.vmName;isoFile=$iso;isoUrl=($IsoBase+$IsoName);sha256=$actual;status='iso-attached-not-installed';preparedAt=(Get-Date).ToString('o')})
   Write-Host 'Debian ISO attached. Guest OS is NOT installed.' -ForegroundColor Green
  }
 }
}catch{$script:SessionFailed=$true;Write-Host ('  ERROR: '+$_.Exception.Message) -ForegroundColor Red;throw}finally{Complete-Steps}
