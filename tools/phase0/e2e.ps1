# End-to-end test: runs INSIDE Windows Sandbox (started by start-e2e.ps1).
# Installs Spike from the host's dist folder, downloads AnyDesk inside the Sandbox and
# starts it. Spike should warn about the download, then bark and show the red alarm.
# Logs and evidence are copied to C:\phase0\out\e2e (a folder on the host).

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'
$out = 'C:\phase0\out\e2e'
New-Item -ItemType Directory -Force $out | Out-Null
Start-Transcript "$out\transcript.txt" | Out-Null

function Step($text) { Write-Host "$(Get-Date -Format HH:mm:ss)  $text" }

Step 'install Spike'
$p = Start-Process 'C:\spike\Spike.exe' -ArgumentList '--install-quiet', 'Sanne', '"06-12 34 56 78"', 'Nico', 'nl' -Wait -PassThru
Step "install exit code $($p.ExitCode)"
Start-Sleep -Seconds 15
Get-Service Spike | Format-List Name, Status, StartType | Out-String | Write-Host
Get-Process Spike -ErrorAction SilentlyContinue | Select-Object Id, SessionId, Path | Format-Table | Out-String | Write-Host

Step 'download AnyDesk into Downloads (Spike should warn)'
$dl = Join-Path $env:USERPROFILE 'Downloads\AnyDesk.exe'
Invoke-WebRequest -Uri 'https://download.anydesk.com/AnyDesk.exe' -OutFile $dl -UserAgent 'Mozilla/5.0' -UseBasicParsing
Start-Sleep -Seconds 12

Step 'start AnyDesk renamed to factuur.exe (Spike should bark and freeze it)'
$renamed = Join-Path $env:USERPROFILE 'Downloads\factuur.exe'
Copy-Item $dl $renamed
Start-Process $renamed
Start-Sleep -Seconds 20

Step 'state after the alarm'
Get-Process -Name factuur -ErrorAction SilentlyContinue | ForEach-Object {
    $threads = $_.Threads | Group-Object WaitReason | ForEach-Object { "$($_.Name)=$($_.Count)" }
    Write-Host "factuur.exe pid $($_.Id): $($threads -join ', ')"
}
Copy-Item 'C:\ProgramData\Spike\spike.log' $out -ErrorAction SilentlyContinue
Copy-Item 'C:\ProgramData\Spike\Bewijs' (Join-Path $out 'Bewijs') -Recurse -Force -ErrorAction SilentlyContinue
'ready' | Set-Content "$out\ready.txt"
Step 'ready: the alarm is on screen in the Sandbox'
Stop-Transcript | Out-Null
