# Phase 0: starts Windows Sandbox with this folder mapped in and runs measure.ps1 inside it.
# Results appear in tools\phase0\out on the host. Everything inside the Sandbox is gone
# when the Sandbox window is closed.

$here = $PSScriptRoot
$outDir = Join-Path $here 'out'
New-Item -ItemType Directory -Force $outDir | Out-Null

$wsb = @"
<Configuration>
  <Networking>Enable</Networking>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$here</HostFolder>
      <SandboxFolder>C:\phase0</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell -NoProfile -ExecutionPolicy Bypass -File C:\phase0\measure.ps1</Command>
  </LogonCommand>
</Configuration>
"@
$path = Join-Path $env:TEMP 'spike-phase0.wsb'
Set-Content $path $wsb -Encoding UTF8
Start-Process $path
